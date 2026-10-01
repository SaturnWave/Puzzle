using System;
using System.Collections.Generic;

namespace Puzzle.Bot
{
    /// <summary>
    /// How human the greedy bot plays. <see cref="NoticePercent"/> models visual search on a big board (each
    /// legal move is seen with this chance); <see cref="Temperature"/> models imperfect judgment (softmax over
    /// move scores; 0 always plays the best-scoring move).
    /// </summary>
    public sealed class SkillProfile
    {
        private readonly int _noticePercent = 100;
        private readonly double _temperature;

        public static SkillProfile Novice { get; } = new SkillProfile { Name = "novice", NoticePercent = 50, Temperature = 12 };

        public static SkillProfile Casual { get; } = new SkillProfile { Name = "casual", NoticePercent = 70, Temperature = 6 };

        /// <summary>The calibration profile: move limits are set against this player.</summary>
        public static SkillProfile Average { get; } = new SkillProfile { Name = "average", NoticePercent = 85, Temperature = 3 };

        public static SkillProfile Expert { get; } = new SkillProfile { Name = "expert", NoticePercent = 100, Temperature = 0 };

        public static IReadOnlyList<SkillProfile> All { get; } = new[] { Novice, Casual, Average, Expert };

        public string Name { get; init; } = "custom";

        public int NoticePercent
        {
            get => _noticePercent;
            init => _noticePercent = value >= 1 && value <= 100
                ? value
                : throw new ArgumentOutOfRangeException(nameof(NoticePercent), value, "Notice chance must be 1..100.");
        }

        public double Temperature
        {
            get => _temperature;
            init => _temperature = value >= 0 && !double.IsNaN(value)
                ? value
                : throw new ArgumentOutOfRangeException(nameof(Temperature), value, "Temperature must be zero or positive.");
        }

        public static bool TryFind(string name, out SkillProfile profile)
        {
            foreach (SkillProfile candidate in All)
            {
                if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    profile = candidate;
                    return true;
                }
            }

            profile = Average;
            return false;
        }
    }
}
