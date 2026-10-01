using System.Diagnostics;
using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;
using Puzzle.Core.Randomness;
using Puzzle.Tooling;
using Puzzle.Web.Sessions;

namespace Puzzle.Web.Api;

/// <summary>HTTP endpoints of the test lab.</summary>
internal static class PuzzleApi
{
    private const int SimulationMoveCap = 150;
    private const int MaxSimulationRuns = 2000;
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

        uint seed = request.Seed ?? (uint)Random.Shared.Next();
        try
        {
            GameSession session = sessions.Create(level, seed, assist);
            return Results.Ok(ToSession(session));
        }
        catch (LevelInitializationException ex)
        {
            return Results.UnprocessableEntity(ex.Message);
        }
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

        int runs = Math.Clamp(request.Runs ?? 300, 1, MaxSimulationRuns);
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

    private static SessionDto ToSession(GameSession session) =>
        new SessionDto(session.Id, session.Seed, session.Assist, StateMapper.ToSummary(session.Level), StateMapper.ToState(session.Game));
}
