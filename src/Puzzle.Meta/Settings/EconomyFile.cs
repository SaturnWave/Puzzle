using System;

namespace Puzzle.Meta.Settings
{
    // Serialization contract of the packed economy settings (Economy.json): the game's sections of
    // economy/default.json as tools/unity/sync-engine.js writes them, with a list of named entries wherever the
    // settings file has an object keyed by a name, because Unity's reader knows no such object. Field names are
    // camelCase on purpose: Unity's JsonUtility maps public fields verbatim, and System.Text.Json reads them with
    // IncludeFields (the level files and the museum's catalogue do the same).
    // Every value here is raw and unvalidated; EconomyFileMapper turns it into EconomyRules or says what is wrong.
    // Unity's reader fills a field the document leaves out with 0 or an empty list and says nothing: the pack script
    // refuses a settings file that leaves a field out, and the format version below is 0 for a document that is
    // no pack at all.
#pragma warning disable SA1307, IDE1006, CA1051 // public camelCase fields are the wire format

    [Serializable]
    public sealed class EconomyFile
    {
        public const int CurrentFormatVersion = 3;

        public int formatVersion;
        public LivesFile lives = new LivesFile();
        public WinCoinsFile coins = new WinCoinsFile();
        public RewardsFile rewards = new RewardsFile();
        public ExtraMovesOfferFile extraMovesOffer = new ExtraMovesOfferFile();
        public PricesFile prices = new PricesFile();
        public StartFile start = new StartFile();
        public BoosterUnlocksFile boosterUnlocks = new BoosterUnlocksFile();
        public StartBoostersFile startBoosters = new StartBoostersFile();
        public HintsFile hints = new HintsFile();
        public ShopFile shop = new ShopFile();
        public BuildFile build = new BuildFile();
    }

    [Serializable]
    public sealed class LivesFile
    {
        public int max;
        public int minutesPerLife;
    }

    [Serializable]
    public sealed class WinCoinsFile
    {
        public long perMoveLeftInFinale;
        public long perLevelWon;
    }

    [Serializable]
    public sealed class RewardsFile
    {
        public RewardFile paintingCompleted = new RewardFile();
        public DailyRewardFile daily = new DailyRewardFile();
    }

    [Serializable]
    public sealed class RewardFile
    {
        public long coins;
        public int boostersEachTime;

        /// <summary>Record names of boosters, in turn.</summary>
        public string[] boosterRotation = Array.Empty<string>();
    }

    [Serializable]
    public sealed class DailyRewardFile
    {
        public long coins;
        public int everyDays;
        public int boostersEachTime;

        /// <summary>Record names of boosters, in turn.</summary>
        public string[] boosterRotation = Array.Empty<string>();
    }

    [Serializable]
    public sealed class ExtraMovesOfferFile
    {
        public int freeAdsPerDay;
        public long priceCoins;
    }

    [Serializable]
    public sealed class PricesFile
    {
        /// <summary>One entry for each of the seven boosters.</summary>
        public BoosterPriceFile[] boosters = Array.Empty<BoosterPriceFile>();
    }

    [Serializable]
    public sealed class BoosterPriceFile
    {
        /// <summary>The booster's record name.</summary>
        public string booster = string.Empty;
        public long coins;
    }

    [Serializable]
    public sealed class BoosterCountFile
    {
        /// <summary>The booster's record name.</summary>
        public string booster = string.Empty;
        public int count;
    }

    [Serializable]
    public sealed class StartFile
    {
        public long coins;

        /// <summary>One entry for each of the seven boosters; a count may be 0.</summary>
        public BoosterCountFile[] boosters = Array.Empty<BoosterCountFile>();
    }

    /// <summary>When the boosters used during play open, and what each brings then; in the pack since format version 2.</summary>
    [Serializable]
    public sealed class BoosterUnlocksFile
    {
        /// <summary>One entry for each of the four boosters used during play.</summary>
        public BoosterLevelFile[] levels = Array.Empty<BoosterLevelFile>();

        /// <summary>One entry for each of the four boosters used during play; a count may be 0.</summary>
        public BoosterCountFile[] gift = Array.Empty<BoosterCountFile>();
    }

    [Serializable]
    public sealed class BoosterLevelFile
    {
        /// <summary>The booster's record name.</summary>
        public string booster = string.Empty;
        public int level;
    }

    [Serializable]
    public sealed class StartBoostersFile
    {
        public int perAttempt;
    }

    /// <summary>When a hint shows itself, how many an attempt shows, and whose move it is; in the pack since format version 3.</summary>
    [Serializable]
    public sealed class HintsFile
    {
        public int idleSeconds;
        public int perAttempt;

        /// <summary>The name of a player model of the bot family: "casual", "skill-60".</summary>
        public string bot = string.Empty;
    }

    [Serializable]
    public sealed class ShopFile
    {
        public CoinPackFile[] coinPacks = Array.Empty<CoinPackFile>();
        public BoosterSetFile[] boosterSets = Array.Empty<BoosterSetFile>();
        public AdForCoinsFile adForCoins = new AdForCoinsFile();
    }

    [Serializable]
    public sealed class CoinPackFile
    {
        public string name = string.Empty;
        public long coins;
    }

    [Serializable]
    public sealed class BoosterSetFile
    {
        public string name = string.Empty;
        public long priceCoins;

        /// <summary>The boosters the set gives, each kind once and with a count of at least 1.</summary>
        public BoosterCountFile[] boosters = Array.Empty<BoosterCountFile>();
    }

    [Serializable]
    public sealed class AdForCoinsFile
    {
        public long coins;
        public int perDay;
    }

    [Serializable]
    public sealed class BuildFile
    {
        /// <summary>"simulated": the only way this build shows an ad.</summary>
        public string ads = string.Empty;

        /// <summary>"notAvailable": this build sells nothing for money.</summary>
        public string realMoneyPurchases = string.Empty;
    }
}
