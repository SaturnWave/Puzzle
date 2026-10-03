using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Tooling;
using Puzzle.Web.Sessions;

namespace Puzzle.Web.Api;

/// <summary>HTTP endpoints of the test lab.</summary>
internal static class PuzzleApi
{
    private const int SimulationMoveCap = 150;
    private const int MaxSimulationRuns = 2000;

    /// <summary>A search bot thinks 25 to 30 times longer per move than a greedy one; this keeps a lab request to seconds.</summary>
    private const int MaxSearchBotSimulationRuns = 100;
    private const int MaxBeamWidth = 32;

    public static void MapPuzzleApi(this WebApplication app)
    {
        RouteGroupBuilder api = app.MapGroup("/api");
        api.MapGet("/levels", ListLevels);
        api.MapGet("/bots", () => BotPolicies.Names);
        api.MapPost("/sessions", CreateSession);
        api.MapGet("/sessions/{id}", GetSession);
        api.MapPost("/sessions/{id}/moves", PlayMove);
        api.MapPost("/sessions/{id}/bot", PlayBotMove);
        api.MapGet("/sessions/{id}/hint", GetHint);
        api.MapPost("/sessions/{id}/solve", Solve);
        api.MapPost("/simulate", Simulate);
    }

    private static IResult ListLevels(LevelLibrary library) =>
        Results.Ok(library.Levels.Select(StateMapper.ToSummary));

    private static IResult CreateSession(NewSessionRequest request, LevelLibrary library, GameSessionStore sessions)
    {
        if (!library.TryGet(request.LevelKey, out LibraryLevel? level))
        {
            return Results.NotFound($"Unknown level '{request.LevelKey}'.");
        }

        int assist = request.Assist ?? 0;
        if (assist < 0 || assist > 100)
        {
            return Results.BadRequest("Assist must be 0..100.");
        }

        if (!TryApplyStartBoard(level.Level, request.StartBoard, out AttemptPolicy? policy))
        {
            return Results.BadRequest("StartBoard must be level, varied or fixed.");
        }

        if (request.PlaySeed > uint.MaxValue || request.Attempt < 0)
        {
            return Results.BadRequest("PlaySeed must fit in 32 bits and Attempt must not be negative.");
        }

        (AttemptSeeds seeds, AttemptOrigin origin) = request switch
        {
            { BoardSeed: ulong board, PlaySeed: ulong play } => (AttemptSeeds.WithFixedBoard(board, play), new AttemptOrigin(null, false, policy.Board)),
            { PlaySeed: ulong play } => (policy.SeedsForPlaySeed((uint)play), new AttemptOrigin(null, false, policy.Board)),
            _ => (policy.SeedsFor(request.Attempt ?? 0, (uint)Random.Shared.Next()), new AttemptOrigin(request.Attempt ?? 0, policy.SeedPool.Count > 0, policy.Board)),
        };

        try
        {
            GameSession session = sessions.Create(level, seeds, origin, assist);
            return Results.Ok(ToSession(session));
        }
        catch (LevelInitializationException ex)
        {
            return Results.UnprocessableEntity(ex.Message);
        }
    }

    /// <summary>
    /// Lets the lab try a level with the other start-board rule. The level's calibrated pool is kept, but it was
    /// measured under the level's own rule, so equal difficulty is only guaranteed for "level".
    /// </summary>
    private static bool TryApplyStartBoard(LevelDefinition level, string? mode, [NotNullWhen(true)] out AttemptPolicy? policy)
    {
        AttemptPolicy own = level.Attempts;
        uint fixedSeed = own.Board is StartBoard.Fixed configured ? configured.Seed : (uint)Rng.Derive((ulong)level.Id.Value, 7);
        policy = (mode ?? "level") switch
        {
            "level" => own,
            "varied" => new AttemptPolicy { Board = new StartBoard.Varied(), SeedPool = own.SeedPool },
            "fixed" => new AttemptPolicy { Board = new StartBoard.Fixed(fixedSeed), SeedPool = own.SeedPool },
            _ => null,
        };
        return policy is not null;
    }

    private static IResult GetSession(string id, GameSessionStore sessions)
    {
        if (!sessions.TryGet(id, out GameSession? session))
        {
            return Results.NotFound();
        }

        lock (session.Gate)
        {
            return Results.Ok(ToSession(session));
        }
    }

    private static IResult PlayMove(string id, MoveDto request, GameSessionStore sessions)
    {
        if (!sessions.TryGet(id, out GameSession? session))
        {
            return Results.NotFound();
        }

        Move move;
        try
        {
            move = StateMapper.ToMove(request);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        lock (session.Gate)
        {
            return Play(session, move);
        }
    }

    private static IResult PlayBotMove(string id, BotRequest request, GameSessionStore sessions)
    {
        if (!sessions.TryGet(id, out GameSession? session))
        {
            return Results.NotFound();
        }

        if (!BotPolicies.TryCreate(request.Bot ?? "average", out IBotPolicy policy))
        {
            return Results.BadRequest($"Unknown bot '{request.Bot}'.");
        }

        lock (session.Gate)
        {
            if (session.Game.Status.IsOver())
            {
                return Results.Conflict("The game is over.");
            }

            Move move = policy.ChooseMove(session.Game, session.Game.FindLegalMoves(), session.BotRng);
            return Play(session, move);
        }
    }

    private static IResult GetHint(string id, string? bot, GameSessionStore sessions)
    {
        if (!sessions.TryGet(id, out GameSession? session))
        {
            return Results.NotFound();
        }

        if (!BotPolicies.TryCreate(bot ?? "expert", out IBotPolicy policy))
        {
            return Results.BadRequest($"Unknown bot '{bot}'.");
        }

        lock (session.Gate)
        {
            if (session.Game.Status.IsOver())
            {
                return Results.Conflict("The game is over.");
            }

            Move move = policy.ChooseMove(session.Game, session.Game.FindLegalMoves(), session.BotRng);
            return Results.Ok(StateMapper.ToMove(move));
        }
    }

    /// <summary>
    /// Perfect-information search from the current position within the moves left. The replica is taken
    /// under the lock; the search itself runs without blocking the session.
    /// </summary>
    private static IResult Solve(string id, SolveRequest request, GameSessionStore sessions)
    {
        if (!sessions.TryGet(id, out GameSession? session))
        {
            return Results.NotFound();
        }

        int beam = Math.Clamp(request.Beam ?? 8, 1, MaxBeamWidth);
        Game replica;
        lock (session.Gate)
        {
            replica = session.Game.CreateReplica();
        }

        var clock = Stopwatch.StartNew();
        SolveResult result = new BeamSearchSolver(beam).Solve(replica, replica.MovesLeft);
        return Results.Ok(new SolveDto(
            result.Solved,
            result.MovesNeeded,
            replica.MovesLeft,
            result.Path.Select(StateMapper.ToMove).ToArray(),
            result.PositionsExplored,
            clock.ElapsedMilliseconds));
    }

    private static IResult Simulate(SimulateRequest request, LevelLibrary library)
    {
        if (!library.TryGet(request.LevelKey, out LibraryLevel? level))
        {
            return Results.NotFound($"Unknown level '{request.LevelKey}'.");
        }

        string bot = request.Bot ?? "average";
        if (!BotPolicies.TryCreate(bot, out _))
        {
            return Results.BadRequest($"Unknown bot '{bot}'.");
        }

        bool playerModel = BotPolicies.Names.Contains(bot, StringComparer.OrdinalIgnoreCase);
        int runs = Math.Clamp(request.Runs ?? 300, 1, playerModel ? MaxSimulationRuns : MaxSearchBotSimulationRuns);
        var clock = Stopwatch.StartNew();
        LevelReport report = BatchSimulator.Run(level.Level, () => CreatePolicy(bot), runs, SimulationMoveCap, seed: 1);
        int curveEnd = Math.Min(SimulationMoveCap, Math.Max(level.Level.MoveLimit * 2, (report.MovesNeeded(0.95) ?? SimulationMoveCap) + 2));
        CurvePointDto[] curve = Enumerable.Range(1, curveEnd).Select(m => new CurvePointDto(m, report.WinRateAt(m))).ToArray();
        int limit = level.Level.MoveLimit;
        return Results.Ok(new SimulationDto(
            bot,
            runs,
            limit,
            report.CompletionRate,
            report.WinRateAt(limit),
            report.NearMissRateAt(limit),
            new MovesNeededDto(report.MovesNeeded(0.10), report.MovesNeeded(0.25), report.MovesNeeded(0.50), report.MovesNeeded(0.75), report.MovesNeeded(0.90)),
            curve,
            report.AverageShuffles,
            report.AverageSpecialsCreated,
            report.AverageCascadesPerMove,
            clock.ElapsedMilliseconds));
    }

    private static IBotPolicy CreatePolicy(string name)
    {
        BotPolicies.TryCreate(name, out IBotPolicy policy);
        return policy;
    }

    private static IResult Play(GameSession session, Move move)
    {
        Game game = session.Game;
        if (game.Status.IsOver())
        {
            return Results.Conflict("The game is over.");
        }

        bool accepted = game.TryPlay(move);
        return Results.Ok(new MoveResultDto(
            accepted,
            StateMapper.ToMove(move),
            accepted ? game.LastEvents.Select(StateMapper.ToEvent).ToArray() : Array.Empty<EventDto>(),
            StateMapper.ToState(game),
            accepted ? StateMapper.ToStats(game.LastMove) : null));
    }

    private static SessionDto ToSession(GameSession session) => new SessionDto(
        session.Id,
        session.Origin.Attempt,
        session.Origin.FromPool,
        StateMapper.StartBoardName(session.Origin.StartBoard),
        session.Seeds.Board,
        session.Seeds.Play,
        session.Assist,
        StateMapper.ToSummary(session.Level),
        StateMapper.ToState(session.Game));
}
