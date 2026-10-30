#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Puzzle.Client.Purchases
{
    /// <summary>
    /// The number the game's server gives a verified payment in the player's ledger of purchases (the Cloud Save item
    /// "purchases", docs/research/server-v1.md §3.2 and §3.7): 1 for the player's first payment, one more for each
    /// after it. It names a payment on every device of the player alike, which the store's own id does not do in the
    /// game's record (that keeps only the latest 64 ids, <see cref="PurchasesMade.TransactionsKept"/>).
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public readonly struct LedgerNumber : IEquatable<LedgerNumber>, IComparable<LedgerNumber>
    {
        /// <exception cref="ArgumentOutOfRangeException">The number is below 1: the ledger counts from 1.</exception>
        public LedgerNumber(long value)
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The ledger numbers its payments from 1.");
            }

            Value = value;
        }

        public long Value { get; }

        public int CompareTo(LedgerNumber other) => Value.CompareTo(other.Value);

        public bool Equals(LedgerNumber other) => Value == other.Value;

        public override bool Equals(object? obj) => obj is LedgerNumber other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

        public static bool operator ==(LedgerNumber left, LedgerNumber right) => left.Equals(right);

        public static bool operator !=(LedgerNumber left, LedgerNumber right) => !left.Equals(right);

        public static bool operator <(LedgerNumber left, LedgerNumber right) => left.Value < right.Value;

        public static bool operator >(LedgerNumber left, LedgerNumber right) => left.Value > right.Value;

        public static bool operator <=(LedgerNumber left, LedgerNumber right) => left.Value <= right.Value;

        public static bool operator >=(LedgerNumber left, LedgerNumber right) => left.Value >= right.Value;
    }

    /// <summary>
    /// The ledger numbers of the payments whose contents are in a player's wallet: delivered with their number, or
    /// delivered before the server could verify them and numbered when it did (no coin comes with the number then).
    /// Two devices of one player each deliver a payment the server numbered at most once, and when their progress is
    /// merged a number found on both sides is counted once (Cloud/ProgressMerge). The set is never cut short, unlike
    /// the store's ids: a payment of long ago is never given a second time, however much the player buys.
    /// <para>
    /// It is written as a list of runs, "1-12,14", which stays a few characters long while the numbers come one after
    /// another, as a player's payments do. The set keeps runs too, so a damaged text that names a run of billions
    /// costs no more memory than one of two. A value that never changes; adding gives another set.
    /// </para>
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class LedgerNumbers : IEquatable<LedgerNumbers>
    {
        /// <summary>The runs, ascending, none overlapping or touching another: "1-3,5" is (1,3) and (5,5), never (1,2),(3,3),(5,5).</summary>
        private readonly Run[] _runs;

        private LedgerNumbers(Run[] runs)
        {
            _runs = runs;
        }

        /// <summary>No number: a player whose payments were never verified, or who bought nothing.</summary>
        public static LedgerNumbers None { get; } = new LedgerNumbers(Array.Empty<Run>());

        public bool IsEmpty => _runs.Length == 0;

        /// <summary>How many numbers the set holds.</summary>
        public long Count => _runs.Sum(run => run.Last - run.First + 1);

        /// <summary>The set as the progress file writes it: runs of numbers in ascending order, "1-12,14"; empty for none.</summary>
        public string Text
        {
            get
            {
                var text = new StringBuilder();
                foreach (Run run in _runs)
                {
                    if (text.Length > 0)
                    {
                        text.Append(',');
                    }

                    text.Append(run.First.ToString(CultureInfo.InvariantCulture));
                    if (run.Last > run.First)
                    {
                        text.Append('-').Append(run.Last.ToString(CultureInfo.InvariantCulture));
                    }
                }

                return text.ToString();
            }
        }

        /// <summary>The set of these numbers, each once however often it is named.</summary>
        public static LedgerNumbers Of(IEnumerable<LedgerNumber> numbers) =>
            new LedgerNumbers(Joined((numbers ?? throw new ArgumentNullException(nameof(numbers))).Select(number => new Run(number.Value, number.Value))));

        /// <summary>
        /// The numbers a text written by <see cref="Text"/> names, part by part. A part no game could have written (not
        /// a number from 1, nor a run from a lower number to a higher one) is set aside with a word on why, and the
        /// rest is kept: a number that is kept is what keeps its payment from being given a second time, so a bad
        /// part costs no other.
        /// </summary>
        /// <param name="text">Nothing or an empty text for no number.</param>
        public static LedgerNumbersReading Read(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new LedgerNumbersReading(None, Array.Empty<string>());
            }

            var runs = new List<Run>();
            var setAside = new List<string>();
            foreach (string part in text.Split(','))
            {
                if (TryReadRun(part.Trim(), out Run run))
                {
                    runs.Add(run);
                }
                else
                {
                    setAside.Add($"the ledger numbers' part \"{part}\": not a number from 1 or a run of them");
                }
            }

            return new LedgerNumbersReading(new LedgerNumbers(Joined(runs)), setAside);
        }

        public bool Contains(LedgerNumber number)
        {
            int low = 0;
            int high = _runs.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                Run run = _runs[middle];
                if (number.Value < run.First)
                {
                    high = middle - 1;
                }
                else if (number.Value > run.Last)
                {
                    low = middle + 1;
                }
                else
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Every number of the set, ascending. A set read from a damaged text may name very many: a caller counts before it walks them.</summary>
        public IEnumerable<LedgerNumber> Numbers
        {
            get
            {
                foreach (Run run in _runs)
                {
                    for (long number = run.First; number <= run.Last && number > 0; number++)
                    {
                        yield return new LedgerNumber(number);
                    }
                }
            }
        }

        /// <summary>The numbers of this set that the other holds too.</summary>
        public LedgerNumbers Intersect(LedgerNumbers other)
        {
            if (other is null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            var common = new List<Run>();
            int mine = 0;
            int theirs = 0;
            while (mine < _runs.Length && theirs < other._runs.Length)
            {
                Run a = _runs[mine];
                Run b = other._runs[theirs];
                long first = Math.Max(a.First, b.First);
                long last = Math.Min(a.Last, b.Last);
                if (first <= last)
                {
                    common.Add(new Run(first, last));
                }

                if (a.Last < b.Last)
                {
                    mine++;
                }
                else
                {
                    theirs++;
                }
            }

            return new LedgerNumbers(Joined(common));
        }

        /// <summary>The numbers of this set that the other does not hold.</summary>
        public LedgerNumbers Except(LedgerNumbers other)
        {
            if (other is null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            var left = new List<Run>();
            foreach (Run run in _runs)
            {
                long from = run.First;
                bool isCoveredToItsEnd = false;
                foreach (Run cut in other._runs.Where(cut => cut.Last >= run.First && cut.First <= run.Last))
                {
                    if (cut.First > from)
                    {
                        left.Add(new Run(from, cut.First - 1));
                    }

                    if (cut.Last >= run.Last)
                    {
                        isCoveredToItsEnd = true;
                        break;
                    }

                    from = Math.Max(from, cut.Last + 1);
                }

                if (!isCoveredToItsEnd)
                {
                    left.Add(new Run(from, run.Last));
                }
            }

            return new LedgerNumbers(Joined(left));
        }

        /// <summary>This set and one more number.</summary>
        public LedgerNumbers With(LedgerNumber number) => Contains(number) ? this : new LedgerNumbers(Joined(_runs.Append(new Run(number.Value, number.Value))));

        /// <summary>Every number of this set and of the other.</summary>
        public LedgerNumbers Union(LedgerNumbers other)
        {
            if (other is null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return other.IsEmpty ? this : IsEmpty ? other : new LedgerNumbers(Joined(_runs.Concat(other._runs)));
        }

        public bool Equals(LedgerNumbers? other) => other is not null && _runs.SequenceEqual(other._runs);

        public override bool Equals(object? obj) => Equals(obj as LedgerNumbers);

        public override int GetHashCode() => HashCode.Combine(_runs.Length, _runs.Length == 0 ? 0 : _runs[_runs.Length - 1].Last);

        public override string ToString() => IsEmpty ? "no ledger number" : $"ledger numbers {Text}";

        /// <summary>The runs in ascending order with every two that overlap or touch made one.</summary>
        private static Run[] Joined(IEnumerable<Run> runs)
        {
            var joined = new List<Run>();
            foreach (Run run in runs.OrderBy(run => run.First))
            {
                bool overlapsOrTouchesTheLast = joined.Count > 0 && run.First - 1 <= joined[joined.Count - 1].Last;
                if (overlapsOrTouchesTheLast)
                {
                    Run last = joined[joined.Count - 1];
                    joined[joined.Count - 1] = new Run(last.First, Math.Max(last.Last, run.Last));
                }
                else
                {
                    joined.Add(run);
                }
            }

            return joined.ToArray();
        }

        /// <summary>A part of the text: "14" or "1-12", every number from 1, the run's first not above its last.</summary>
        private static bool TryReadRun(string part, out Run run)
        {
            run = default;
            int dash = part.IndexOf('-');
            string first = dash < 0 ? part : part.Substring(0, dash);
            string last = dash < 0 ? part : part.Substring(dash + 1);
            if (!TryReadNumber(first, out long from) || !TryReadNumber(last, out long to) || to < from)
            {
                return false;
            }

            run = new Run(from, to);
            return true;
        }

        /// <summary>Digits only, no sign and no space, naming a number from 1 that a long holds.</summary>
        private static bool TryReadNumber(string digits, out long number) =>
            long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number >= 1;

        /// <summary>Every number from <see cref="First"/> to <see cref="Last"/>, both included.</summary>
        private readonly struct Run : IEquatable<Run>
        {
            public Run(long first, long last)
            {
                First = first;
                Last = last;
            }

            public long First { get; }

            public long Last { get; }

            public bool Equals(Run other) => First == other.First && Last == other.Last;

            public override bool Equals(object? obj) => obj is Run other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(First, Last);
        }
    }

    /// <summary>What a text of ledger numbers was read as: the numbers it names, and the parts set aside on the way, each with a word on why (none for a text a game wrote).</summary>
    public sealed class LedgerNumbersReading
    {
        public LedgerNumbersReading(LedgerNumbers numbers, IReadOnlyList<string> setAside)
        {
            Numbers = numbers ?? throw new ArgumentNullException(nameof(numbers));
            SetAside = setAside ?? throw new ArgumentNullException(nameof(setAside));
        }

        public LedgerNumbers Numbers { get; }

        public IReadOnlyList<string> SetAside { get; }
    }
}
