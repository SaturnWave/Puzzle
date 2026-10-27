#nullable enable
using System;

namespace Puzzle.Client.Cloud
{
    /// <summary>
    /// What the settings show of the player's account and its cloud save (docs/research/server-v1.md §5.7): where the
    /// account stands, the "Bulut kaydı" switch, when the cloud last answered and whether its last try failed, the player
    /// code (the account's id, which support and the deletion page ask for), and the platform a "Bağlan" links to, if any.
    /// The cloud makes it anew whenever one of these changes, and counts each such change, so whoever shows it can tell
    /// a view from the one before by <see cref="Revision"/>. A value that never changes.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class AccountView
    {
        /// <param name="lastSynced">When the cloud last answered a read or a write of the save; nothing before it ever did.</param>
        /// <param name="lastTryFailed">True when the cloud did not answer the last try: the settings say it is out of reach for now.</param>
        /// <param name="canLinkTo">The platform the settings' "Bağlan" links an account of this phone to; none when there is nothing to link.</param>
        /// <param name="knownPlayer">The account the phone signed in to last, signed in now or not (cloud.json keeps it); nothing before the first sign-in.</param>
        /// <param name="seenAt">When the view was made: the last sync is told as so long before it.</param>
        public AccountView(
            Account account, CloudPlayerId? knownPlayer, bool isSwitchedOn, DateTimeOffset? lastSynced, bool lastTryFailed, SignInPlatform canLinkTo, int revision, DateTimeOffset seenAt)
        {
            Account = account ?? throw new ArgumentNullException(nameof(account));
            KnownPlayer = Account.Player ?? knownPlayer;
            SeenAt = seenAt;
            IsSwitchedOn = isSwitchedOn;
            LastSynced = lastSynced;
            LastTryFailed = lastTryFailed;
            CanLinkTo = canLinkTo;
            Revision = revision;
        }

        /// <summary>A build or a tool with no cloud: the services are off, the switch shows off, nothing was synced.</summary>
        public static AccountView NoCloud { get; } =
            new AccountView(new Account.Off(ServicesOff.NoCloudProject), null, isSwitchedOn: false, null, lastTryFailed: false, SignInPlatform.None, revision: 0, DateTimeOffset.MinValue);

        public Account Account { get; }

        /// <summary>The account signed in, or the one the phone signed in to last (its session kept while the switch is off); nothing before the first sign-in.</summary>
        public CloudPlayerId? KnownPlayer { get; }

        public bool IsSwitchedOn { get; }

        /// <summary>When the cloud last answered a read or a write of the save; nothing before it ever did.</summary>
        public DateTimeOffset? LastSynced { get; }

        public bool LastTryFailed { get; }

        public SignInPlatform CanLinkTo { get; }

        /// <summary>How many times the cloud's view changed before this one: two views of one cloud with the same revision say the same.</summary>
        public int Revision { get; }

        public DateTimeOffset SeenAt { get; }

        /// <summary>True in a build whose cloud can be turned on and off: one with a cloud project (never the Editor's).</summary>
        public bool HasTheSwitch => !(Account is Account.Off { Why: ServicesOff.NoCloudProject });

        /// <summary>
        /// The player code the settings show and support asks for: the account's id in groups of four, so it can be read
        /// out and typed ("S1Cy lpiB 5AS3 …"); nothing before the first sign-in. A phone whose switch is off shows the
        /// account it keeps the session of, which a deletion asked for by mail needs.
        /// </summary>
        public string? PlayerCode => KnownPlayer is CloudPlayerId player ? Grouped(player.Value) : null;

        public override string ToString() => $"{Account}, switch {(IsSwitchedOn ? "on" : "off")}, last synced {LastSynced?.ToString("u") ?? "never"}{(LastTryFailed ? ", last try failed" : string.Empty)}";

        private static string Grouped(string id)
        {
            const int Group = 4;
            var grouped = new System.Text.StringBuilder(id.Length + (id.Length / Group));
            for (int at = 0; at < id.Length; at += Group)
            {
                if (at > 0)
                {
                    grouped.Append(' ');
                }

                grouped.Append(id, at, Math.Min(Group, id.Length - at));
            }

            return grouped.ToString();
        }
    }
}
