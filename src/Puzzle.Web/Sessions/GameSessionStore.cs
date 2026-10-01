using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Tooling;

namespace Puzzle.Web.Sessions;

/// <summary>Where an attempt's seeds came from.</summary>
public sealed record AttemptOrigin(int? Attempt, bool FromPool, StartBoard StartBoard);

/// <summary>One browser game. <see cref="Game"/> is not thread-safe: hold <see cref="Gate"/> while touching it.</summary>
public sealed class GameSession
{
    public GameSession(string id, LibraryLevel level, AttemptSeeds seeds, AttemptOrigin origin, int assist, Game game)
    {
        Id = id;
        Level = level;
        Seeds = seeds;
        Origin = origin;
        Assist = assist;
        Game = game;
        BotRng = new Rng(Rng.Derive(seeds.Play, 99));
    }

    public string Id { get; }

    public LibraryLevel Level { get; }

    public AttemptSeeds Seeds { get; }

    public AttemptOrigin Origin { get; }

    public int Assist { get; }

    public Game Game { get; }

    /// <summary>Random choices of bots and hints in this session (separate from the game's own streams).</summary>
    public Rng BotRng { get; }

    public object Gate { get; } = new();
}

/// <summary>
/// In-memory sessions with a hard size limit and idle expiry, so an open browser tab can never grow the
/// server without bound.
/// </summary>
public sealed class GameSessionStore
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _idleTimeout;

    public GameSessionStore(IMemoryCache cache, TimeSpan idleTimeout)
    {
        _cache = cache;
        _idleTimeout = idleTimeout;
    }

    /// <exception cref="LevelInitializationException">The level cannot produce a starting board.</exception>
    public GameSession Create(LibraryLevel level, AttemptSeeds seeds, AttemptOrigin origin, int assist)
    {
        Game game = Game.Start(level.Level, seeds, new GameOptions { RecordEvents = true, AssistPercent = assist });
        var session = new GameSession(Guid.NewGuid().ToString("N"), level, seeds, origin, assist, game);
        _cache.Set(session.Id, session, new MemoryCacheEntryOptions { Size = 1, SlidingExpiration = _idleTimeout });
        return session;
    }

    public bool TryGet(string id, [NotNullWhen(true)] out GameSession? session) => _cache.TryGetValue(id, out session);
}
