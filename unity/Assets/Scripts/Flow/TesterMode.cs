#nullable enable
using System;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// The tester mode (the owner's issue #10, approved 2026-10-17): a phone of the owner's plays on without stopping,
    /// with coins and lives that have no end (<see cref="WalletHandling.Unlimited"/>); everything else is as the rules
    /// have it. Only a test build has it: one whose marks in the economy's settings say its ads are the stand-in and
    /// nothing is sold for money, so no build that sells or shows real ads can ever be put in it; no setting of its
    /// own decides that. It is a device setting (<see cref="DeviceSetting.TesterMode"/>), turned on from the support
    /// card by seven taps in a row on its version row and a question, and off by its switch on the settings card.
    /// Every attempt played in it is marked in its record, and the comparison with the bots leaves it out.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public static class TesterMode
    {
        /// <summary>The taps in a row on the support card's version row that ask whether to turn the mode on.</summary>
        public const int TapsToAsk = 7;

        /// <summary>Whether a build with these marks has the tester mode at all: only a test build, whose ads are the stand-in and which sells nothing for money.</summary>
        public static bool IsOfferedBy(AdSource ads, RealMoneySales realMoney) =>
            ads == AdSource.Simulated && realMoney == RealMoneySales.NotAvailable;

        /// <summary>
        /// How the client holds the wallet with these settings in a build with these marks: unlimited while the mode is
        /// on in a build that has it; as the rules have it otherwise, a device file that says it is on included.
        /// </summary>
        /// <exception cref="ArgumentNullException">No settings.</exception>
        public static WalletHandling HandlingFor(DeviceSettings settings, AdSource ads, RealMoneySales realMoney) =>
            (settings ?? throw new ArgumentNullException(nameof(settings))).TesterMode && IsOfferedBy(ads, realMoney)
                ? WalletHandling.Unlimited
                : WalletHandling.Normal;
    }
}
