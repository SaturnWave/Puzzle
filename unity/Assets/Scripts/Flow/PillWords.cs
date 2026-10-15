#nullable enable
using System;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the pill over the board's shelf says, and so how its lines are set. A closed set (private constructor):
    /// nothing at all; one line (how to play, which try this is, why a booster's use was refused); a booster that is
    /// armed (what it is and does over what to pick, the second line the larger); a booster's first use (the word on
    /// it, with the honest line under it beside the first booster). No Unity dependency.
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

        /// <summary>The word on a booster's first use, and under it, beside the first booster's, the honest line; nothing under it otherwise.</summary>
        public sealed class Guide : PillWords
        {
            public Guide(string sentence, string? aside)
            {
                Sentence = sentence ?? throw new ArgumentNullException(nameof(sentence));
                Aside = aside;
            }

            public string Sentence { get; }

            /// <summary>The smaller line under the sentence; nothing for none.</summary>
            public string? Aside { get; }

            public override bool Equals(PillWords? other) => other is Guide guide && guide.Sentence == Sentence && guide.Aside == Aside;

            public override string ToString() => Aside is string aside ? $"{Sentence} / {aside}" : Sentence;
        }
    }
}
