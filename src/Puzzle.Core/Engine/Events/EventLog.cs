using System;
using System.Collections.Generic;

namespace Puzzle.Core.Engine.Events
{
    /// <summary>
    /// Collects events for the current move. When disabled it holds no list at all, and callers check
    /// <see cref="IsEnabled"/> before constructing an event so bot runs stay allocation-free.
    /// </summary>
    internal sealed class EventLog
    {
        private readonly List<GameEvent>? _events;

        public EventLog(bool enabled)
        {
            _events = enabled ? new List<GameEvent>() : null;
        }

        public static EventLog Disabled { get; } = new EventLog(false);

        public bool IsEnabled => _events is not null;

        public void Add(GameEvent gameEvent) => _events?.Add(gameEvent);

        public void Clear() => _events?.Clear();

        public IReadOnlyList<GameEvent> Snapshot() => _events is null ? Array.Empty<GameEvent>() : _events.ToArray();
    }
}
