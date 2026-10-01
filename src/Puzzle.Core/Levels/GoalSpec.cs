using System;
using Puzzle.Core.Model;

namespace Puzzle.Core.Levels
{
    /// <summary>
    /// A level objective. Closed set: the private constructor keeps every variant inside this file, and
    /// <c>GoalSpecTests</c> enumerates the subtypes because C# cannot prove switch exhaustiveness.
    /// </summary>
    public abstract record GoalSpec
    {
        private GoalSpec(int target)
        {
            Target = target > 0
                ? target
                : throw new ArgumentOutOfRangeException(nameof(target), target, "A goal target must be positive.");
        }

        public int Target { get; }

        /// <summary>Destroy <see cref="GoalSpec.Target"/> gems of one color, by any means.</summary>
        public sealed record CollectColor : GoalSpec
        {
            public CollectColor(GemColor color, int target)
                : base(target)
            {
                Color = Enum.IsDefined(typeof(GemColor), color)
                    ? color
                    : throw new ArgumentOutOfRangeException(nameof(color), color, "Unknown gem color.");
            }

            public GemColor Color { get; }
        }

        public sealed record BreakBoxes : GoalSpec
        {
            public BreakBoxes(int target)
                : base(target)
            {
            }
        }

        /// <summary>Clear the last jelly layer of <see cref="GoalSpec.Target"/> cells.</summary>
        public sealed record ClearJelly : GoalSpec
        {
            public ClearJelly(int target)
                : base(target)
            {
            }
        }

        public sealed record BreakChains : GoalSpec
        {
            public BreakChains(int target)
                : base(target)
            {
            }
        }

        public sealed record DropItems : GoalSpec
        {
            public DropItems(int target)
                : base(target)
            {
            }
        }
    }
}
