#nullable enable
using System;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the pill over the board's shelf says, and so how its lines are set. A closed set (private constructor):
    /// nothing at all; one line (how to play, which try this is, why a booster's use was refused); a booster that is
    /// armed (what it is and does over what to pick, the second line the larger); a step of the tutorial, which its
    /// own pill says over the dimmed stage (a booster's first use is one of them since #7). No Unity dependency.
    /// </summary>
    public abstract class PillWords : IEquatable<PillWords>
    {
        private PillWords()
        {
        }

        /// <summary>No pill: nothing is said.</summary>
        public static PillWords None { get; } = new Silent();

        public abstract bool Equals(PillWords? other);

        public override bool Equals(object? obj) => Equals(obj as PillWords);

        public override int GetHashCode() => (ToString() ?? string.Empty).GetHashCode();

        /// <summary>No pill.</summary>
        public sealed class Silent : PillWords
        {
            public override bool Equals(PillWords? other) => other is Silent;

            public override string ToString() => string.Empty;
        }

        /// <summary>One line: how to play, which try this is, why a use was refused.</summary>
        public sealed class Line : PillWords
        {
            public Line(string words)
            {
                Words = words ?? throw new ArgumentNullException(nameof(words));
            }

            public string Words { get; }

            public override bool Equals(PillWords? other) => other is Line line && line.Words == Words;

            public override string ToString() => Words;
        }

        /// <summary>A booster is armed: what it is and does, and under it what the finger is to pick.</summary>
        public sealed class Armed : PillWords
        {
            public Armed(string about, string whatToPick)
            {
                About = about ?? throw new ArgumentNullException(nameof(about));
                WhatToPick = whatToPick ?? throw new ArgumentNullException(nameof(whatToPick));
            }

            /// <summary>"Spatula · bir taşı ya da bir katı kaldırır".</summary>
            public string About { get; }

            /// <summary>"Bir taş seç".</summary>
            public string WhatToPick { get; }

            public override bool Equals(PillWords? other) => other is Armed armed && armed.About == About && armed.WhatToPick == WhatToPick;

            public override string ToString() => $"{About} / {WhatToPick}";
        }

        /// <summary>
        /// A step of the tutorial: what to look at or do, why, and under them a small line: for a step that a touch
        /// ends, that a touch goes on; for a booster used by hand, that it is free, or beside the spatula that every
        /// level can be won without boosters; nothing there for a move shown by hand, which only the move ends.
        /// </summary>
        public sealed class Lesson : PillWords
        {
            public Lesson(string says, string why, string? aside)
            {
                Says = says ?? throw new ArgumentNullException(nameof(says));
                Why = why ?? throw new ArgumentNullException(nameof(why));
                Aside = aside;
            }

            /// <summary>"Bu iki taşın yerini değiştir".</summary>
            public string Says { get; }

            /// <summary>"Aynı üç taş yan yana gelince toplanır".</summary>
            public string Why { get; }

            /// <summary>"Devam etmek için dokun", "Bu seferlik bedava"; nothing for a move shown by hand.</summary>
            public string? Aside { get; }

            public override bool Equals(PillWords? other) =>
                other is Lesson lesson && lesson.Says == Says && lesson.Why == Why && lesson.Aside == Aside;

            public override string ToString() => Aside is string aside ? $"{Says} / {Why} / {aside}" : $"{Says} / {Why}";
        }
    }
}
