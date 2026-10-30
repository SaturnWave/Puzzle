using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Core.Recording;
using Puzzle.Tooling;
using Puzzle.Web.Recording;
using Puzzle.Web.Sessions;

namespace Puzzle.Web.Api;

/// <summary>HTTP endpoints of the test lab.</summary>
internal static class PuzzleApi
{
    private const int SimulationMoveCap = 150;
    private const int MaxSimulationRuns = 2000;

    /// <summary>
    /// Runs cap for the lab's one search bot, the default usta (24 simulations per move, about 0.1 thread-seconds per game
    /// in <c>bench</c> on an idle machine): 100 runs cost about 10 thread-seconds, a few seconds of wall time on a
    /// multi-core machine. Larger budgets cost more per game, so <see cref="TryCreateLabBot"/> refuses every "usta-&lt;n&gt;".
    /// </summary>
    private const int MaxSearchBotSimulationRuns = 100;
    private const int MaxBeamWidth = 32;

    /// <summary>Carries the browser's anonymous device id for the attempt records (docs/GAME_DESIGN.md §20).</summary>
    internal const string DeviceHeader = "X-Device-Id";

    public static void MapPuzzleApi(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder api = app.MapGroup("/api");
        api.MapGet("/levels", ListLevels);
        api.MapGet("/bots", () => BotPolicies.NamesByStrength);
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

    private static IResult CreateSession(
        NewSessionRequest request,
        [FromHeader(Name = DeviceHeader)] string? deviceHeader,
        LevelLibrary library,
        GameSessionStore sessions,
        RetryNumbers retries)
    {
        if (!DeviceId.TryCreate(deviceHeader, out DeviceId? device))
        {
            return DeviceRequired();
        }

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

        bool deviceKept = request.TransientDeviceId != true;
        (AttemptSeeds seeds, AttemptOrigin origin) = request switch
        {
            { BoardSeed: ulong board, PlaySeed: ulong play } => (AttemptSeeds.WithFixedBoard(board, play), ChosenSeeds(policy, deviceKept)),
            { PlaySeed: ulong play } => (policy.SeedsForPlaySeed((uint)play), ChosenSeeds(policy, deviceKept)),
            { Attempt: int asked } => Retry(policy, asked, inSequence: false, deviceKept),
            _ => Retry(policy, retries.Take(device, level.Level), inSequence: true, deviceKept),
        };

        GameSession session;
        try
        {
            session = sessions.Create(level, seeds, origin, assist, device);
        }
        catch (LevelInitializationException ex)
        {
            return Results.UnprocessableEntity(ex.Message);
        }

        // Only now that the new attempt exists has the player really left the old one.
        if (request.Replaces is { } left)
        {
            sessions.Leave(left);
        }

        return Results.Ok(ToSession(session));
    }

    /// <summary>The answer to a request without a well-formed <see cref="DeviceHeader"/>.</summary>
    internal static IResult DeviceRequired() => Results.BadRequest(
        $"Send this device's random id in the {DeviceHeader} header ({DeviceId.MinLength}..{DeviceId.MaxLength} letters, digits, '-' or '_').");

    /// <summary>The seeds of retry <paramref name="attempt"/>; <paramref name="inSequence"/> when the server numbered it.</summary>
    private static (AttemptSeeds Seeds, AttemptOrigin Origin) Retry(AttemptPolicy policy, int attempt, bool inSequence, bool deviceKept) =>
        (policy.SeedsFor(attempt, (uint)Random.Shared.Next()), new AttemptOrigin(attempt, policy.SeedPool.Count > 0, policy.Board, inSequence, deviceKept));

    /// <summary>The origin of seeds given directly: no retry number, so outside any sequence.</summary>
    private static AttemptOrigin ChosenSeeds(AttemptPolicy policy, bool deviceKept) =>
        new(Attempt: null, FromPool: false, policy.Board, InSequence: false, deviceKept);

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

    private static IResult PlayMove(string id, MoveDto request, GameSessionStore sessions, AttemptRecorder recorder)
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
            return Play(session, move, MoveSource.Player, recorder);
        }
    }

    private static IResult PlayBotMove(string id, BotRequest request, GameSessionStore sessions, AttemptRecorder recorder)
    {
        if (!sessions.TryGet(id, out GameSession? session))
        {
            return Results.NotFound();
        }

        if (!TryCreateLabBot(request.Bot ?? "average", out IBotPolicy? policy))
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
            return Play(session, move, MoveSource.Bot, recorder);
        }
    }

    private static IResult GetHint(string id, string? bot, GameSessionStore sessions)
    {
        if (!sessions.TryGet(id, out GameSession? session))
        {
            return Results.NotFound();
        }

        if (!TryCreateLabBot(bot ?? "expert", out IBotPolicy? policy))
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
            session.HintShown();
            return Results.Ok(StateMapper.ToMove(move));
        }
    }

    /// <summary>
    /// Perfect-information search from the current position within the moves left. The replica is taken
    /// under the lock; the search itself runs without blocking the session. The path it shows counts as a hint
    /// while the attempt is being played; after its end it is only a look back (<see cref="GameSession.HintShown"/>).
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
            session.HintShown();
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
        if (!TryCreateLabBot(bot, out _))
        {
            return Results.BadRequest($"Unknown bot '{bot}'.");
        }

        int runs = Math.Clamp(request.Runs ?? 300, 1, BotPolicies.IsPlayerModel(bot) ? MaxSimulationRuns : MaxSearchBotSimulationRuns);
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

    /// <summary>
    /// Creates a bot the lab may run: a player model (the random bot, a named skill profile or a rung of the skill
    /// ladder, as <c>/api/bots</c> lists them) or a measurement bot at its default budget. Any other name, an
    /// "usta-&lt;n&gt;" included, is refused like an unknown one: the lab's run caps hold for the default budget only.
    /// </summary>
    private static bool TryCreateLabBot(string name, [NotNullWhen(true)] out IBotPolicy? policy)
    {
        bool offered = BotPolicies.IsPlayerModel(name)
            || BotPolicies.MeasurementNames.Contains(name, StringComparer.OrdinalIgnoreCase);
        policy = offered && BotPolicies.TryCreate(name, out IBotPolicy created) ? created : null;
        return policy is not null;
    }

    private static IBotPolicy CreatePolicy(string name)
    {
        BotPolicies.TryCreate(name, out IBotPolicy policy);
        return policy;
    }

    /// <summary>Plays <paramref name="move"/>, keeps it in the attempt's history and records the attempt if the move decided it. Hold the gate.</summary>
    private static IResult Play(GameSession session, Move move, MoveSource source, AttemptRecorder recorder)
    {
        Game game = session.Game;
        if (game.Status.IsOver())
        {
            return Results.Conflict("The game is over.");
        }

        bool accepted = game.TryPlay(move);
        if (accepted)
        {
            session.MovePlayed(move, source);
            recorder.RecordIfDecided(session);
        }

        return Results.Ok(new MoveResultDto(
            accepted,
            StateMapper.ToMove(move),
            accepted ? game.LastEvents.Select(StateMapper.ToEvent).ToArray() : Array.Empty<EventDto>(),
            StateMapper.ToState(game),
            accepted ? StateMapper.ToStats(game.LastMove) : null,
            RecordingOf(session)));
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
        StateMapper.ToState(session.Game),
        RecordingOf(session));

    private static RecordingDto RecordingOf(GameSession session) => new(
        WireName(session.Standing),
        session.SequenceBreak is { } sequenceBreak ? WireName(sequenceBreak) : null,
        WireName(session.Record));

    private static string WireName<TEnum>(TEnum value)
        where TEnum : struct, Enum => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
}
