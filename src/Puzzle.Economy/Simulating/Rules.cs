using Puzzle.Economy.Settings;
using Puzzle.Economy.Units;

namespace Puzzle.Economy.Simulating;

/// <summary>What a player knows before an attempt: how often they lost this level, what they hold, what they can pay.</summary>
public readonly record struct BeforeAttemptView(int LossesOnLevel, BoosterShelf Owned, Coins Balance);

/// <summary>
/// What a player sees when the moves have run out. It has the progress on the goals and nothing of the game's
/// future: a rule that reads this view cannot decide on whether the extra moves would win.
/// </summary>
/// <param name="GoalsDone">The largest measured share of the goals that is done; 0 below the first mark.</param>
public readonly record struct OutOfMovesView(Percent GoalsDone, int LossesOnLevel, Coins Balance, int FreeAdsLeftToday);

/// <summary>When a player starts an attempt with a booster (the settings' player.startBoosters).</summary>
public sealed class StartBoosterRule(StartBoosterHabit habit, IReadOnlyDictionary<BoosterKind, Coins> prices)
{
    /// <summary>
    /// After enough losses on the level the first owned booster of the preference is used. With none of them owned,
    /// and after the losses that make the player buy, the first of the preference the coins cover is bought; when
    /// they cover none, the player goes without and what was missing for the cheapest is the shortfall. The settings
    /// never let a player buy before they would use one they own, so an owned booster cannot stand in a purchase's way.
    /// </summary>
    public StartBoosterDecision Decide(BeforeAttemptView view)
    {
        BoosterKind? owned = FirstOrAbsent(habit.Preference.Where(view.Owned.Has));
        if (owned is BoosterKind held)
        {
            return habit.UseAfterLossesOnLevel is int use && view.LossesOnLevel >= use
                ? new StartBoosterDecision.UseOwned(held)
                : new StartBoosterDecision.None();
        }

        if (habit.BuyAfterLossesOnLevel is not int buy || view.LossesOnLevel < buy || habit.Preference.Count == 0)
        {
            return new StartBoosterDecision.None();
        }

        Coins balance = view.Balance;
        if (FirstOrAbsent(habit.Preference.Where(booster => balance.Covers(prices[booster]))) is BoosterKind affordable)
        {
            return new StartBoosterDecision.Buy(affordable, prices[affordable]);
        }

        Coins cheapest = habit.Preference.Min(booster => prices[booster]);
        return new StartBoosterDecision.WantedUnpaid(new Shortfall(cheapest, balance.ShortOf(cheapest)));
    }

    private static BoosterKind? FirstOrAbsent(IEnumerable<BoosterKind> boosters)
    {
        foreach (BoosterKind booster in boosters)
        {
            return booster;
        }

        return null;
    }
}

/// <summary>
/// When a player takes the extra-moves offer and what they pay with (the settings' player.extraMoves and
/// extraMovesOffer, and player.purchases.packWhenShortForExtraMoves for a player who buys coins for it).
/// </summary>
/// <param name="packWhenShort">The coin pack bought when the coins fall short; absent for a player who buys none. It pays for the moves by itself.</param>
public sealed class ExtraMovesRule(ExtraMovesHabit habit, Coins price, StoreProduct? packWhenShort = null)
{
    /// <summary>
    /// The offer is wanted when enough of the goals is done. It is paid with an ad while the player watches ads and
    /// a free one is left today, otherwise with coins when the player pays coins and has them; a player whose coins
    /// fall short buys their pack first if their habit says so. A player who wants it and cannot pay goes without.
    /// </summary>
    public ExtraMovesDecision Decide(OutOfMovesView view)
    {
        if (view.GoalsDone < habit.TakeWhenGoalsDone)
        {
            return new ExtraMovesDecision.NotWanted();
        }

        if (habit.WatchesAds && view.FreeAdsLeftToday > 0)
        {
            return new ExtraMovesDecision.TakenForAd();
        }

        if (!habit.PaysCoins)
        {
            return new ExtraMovesDecision.WantedUnpaid(Shortfall: null);
        }

        if (view.Balance.Covers(price))
        {
            return new ExtraMovesDecision.TakenForCoins(price);
        }

        return packWhenShort is StoreProduct pack
            ? new ExtraMovesDecision.TakenForCoins(price, PackBoughtFirst: pack)
            : new ExtraMovesDecision.WantedUnpaid(new Shortfall(price, view.Balance.ShortOf(price)));
    }
}
