namespace Puzzle.Client.Words
{
    /// <summary>
    /// The stable id of every piece of words the game says: the key of each table's entry (words/&lt;code&gt;.json).
    /// A closed set. An id is never renamed once a table is translated: a new meaning gets a new id. The Turkish
    /// table is the source and says, in each entry's note, where the words stand. No Unity dependency.
    /// </summary>
    public enum WordId
    {
        /// <summary>Navigation bar at the foot of the museum's pages: the shop.</summary>
        NavShop,

        /// <summary>Navigation bar: the museum's hall (the home page).</summary>
        NavMuseum,

        /// <summary>Navigation bar: the museum's floor plan.</summary>
        NavMap,

        /// <summary>Navigation bar: the collection of restored paintings.</summary>
        NavCollection,

        /// <summary>Green button that leads into a level (level card, leaving card).</summary>
        Play,

        /// <summary>Green button once every level is won: the last level again.</summary>
        PlayAgain,

        /// <summary>Green button that goes on (after a won level, after a restored room).</summary>
        GoOn,

        /// <summary>Green button after a lost level.</summary>
        TryAgain,

        /// <summary>Button that closes a card or leaves the jigsaw.</summary>
        Done,

        /// <summary>Green button that buys one booster for coins.</summary>
        Buy,

        /// <summary>Dimmed green button when the coins do not cover a price.</summary>
        ButtonCoinsShort,

        /// <summary>Underlined link under a dimmed button: to the shop.</summary>
        ToTheShop,

        /// <summary>Extra moves, on a start booster's disc and on the offer's green button: "+5 moves".</summary>
        PlusMoves,

        /// <summary>Hall's play button and the level card's nameplate: the level it opens.</summary>
        PlayLevel,

        /// <summary>Hall's small button that leads to the jigsaw of the painting in the frame.</summary>
        HallToTheJigsaw,

        /// <summary>Hall's gift frame while the daily gift waits.</summary>
        HallGift,

        /// <summary>Hall's note when every painting chosen so far is restored.</summary>
        HallMorePaintingsToCome,

        /// <summary>Hall's note when the whole museum is restored.</summary>
        HallMuseumRestored,

        /// <summary>Banner over a room whose fifth painting has just become whole.</summary>
        HallRoomRestored,

        /// <summary>Under the banner of a restored room: the next room by name.</summary>
        HallNextRoom,

        /// <summary>Line under the banner: the room's name and its paintings, all five restored.</summary>
        HallRoomAndItsPaintings,

        /// <summary>The map's plaque: its title.</summary>
        MapTitle,

        /// <summary>Small capitals at the museum's entrance on the floor plan.</summary>
        MapEntrance,

        /// <summary>Map's plaque, small capitals: what the museum holds in all.</summary>
        MapSummary,

        /// <summary>Map: the pill of a room whose paintings are not chosen yet.</summary>
        MapRoomToCome,

        /// <summary>Map: how many of a room's paintings are restored.</summary>
        MapRoomPaintingsWhole,

        /// <summary>Map: the label under the pin of the player's room.</summary>
        MapYouAreHere,

        /// <summary>The name of a room whose period is not decided yet.</summary>
        RoomWithoutAName,

        /// <summary>Small capitals: a room among the museum's rooms, "ROOM 1 / 20".</summary>
        RoomOfRooms,

        /// <summary>Hall's room plaque, small capitals: "4 / 5 RESTORED".</summary>
        RoomPaintingsRestored,

        /// <summary>A locked room: the level it opens at. Turkish fills {levelIn} with the number and its suffix ("51'de"); other languages use {level}.</summary>
        RoomOpensAt,

        /// <summary>Second line of the plate under a frame whose painting is not chosen yet (small capitals).</summary>
        PlatePaintingToCome,

        /// <summary>First line of a plate while no painting is chosen for the frame.</summary>
        PaintingByNumber,

        /// <summary>Second line of the plate under a covered frame: the levels that restore it (small capitals).</summary>
        PlateLevels,

        /// <summary>The collection's plaque: its title.</summary>
        CollectionTitle,

        /// <summary>Collection's plaque, small capitals: restored paintings of all.</summary>
        CollectionPaintingsWhole,

        /// <summary>Pill on a painting being restored: its pieces in place of all.</summary>
        CollectionPiecesInPlace,

        /// <summary>Line under a painting looked at by itself.</summary>
        ViewerTouchToClose,

        /// <summary>Label of the piece counter on the jigsaw (small capitals).</summary>
        JigsawPieceLabel,

        /// <summary>Label of a painting that is whole on the jigsaw (small capitals).</summary>
        JigsawRestoredLabel,

        /// <summary>Hint on the tray while one piece waits.</summary>
        JigsawDragThePiece,

        /// <summary>Hint on the tray while several pieces wait.</summary>
        JigsawDragThePieces,

        /// <summary>Hint on the tray while no piece waits.</summary>
        JigsawNoPieceWaits,

        /// <summary>The jigsaw's headline when the painting becomes whole.</summary>
        JigsawPaintingRestored,

        /// <summary>Jigsaw button that places every waiting piece.</summary>
        JigsawPlaceThemAll,

        /// <summary>The tray's words over the reward of a painting that has just become whole.</summary>
        JigsawPaintingsReward,

        /// <summary>Board plaque label over the level number (small capitals).</summary>
        BoardLevel,

        /// <summary>Label on the moves medallion (small capitals).</summary>
        BoardMoves,

        /// <summary>Board plaque label over the goals (small capitals).</summary>
        BoardGoals,

        /// <summary>Board plaque label over the goals where the level has no plaque of its own (small capitals).</summary>
        BoardLevelNumbered,

        /// <summary>Board plaque over the painting whose piece the level gives (small capitals).</summary>
        BoardPieceOf,

        /// <summary>Shelf line under the board on a first try: how to play. "taş" is a piece on the board (a tile), not a jigsaw piece.</summary>
        ShelfHowToPlay,

        /// <summary>Shelf line on a later try: which try, and that the board is dealt anew.</summary>
        ShelfLaterAttempt,

        /// <summary>Booster name: removes one tile or one layer (a painter's spatula).</summary>
        BoosterHitCell,

        /// <summary>Booster name: paints a whole row.</summary>
        BoosterHitRow,

        /// <summary>Booster name: paints a whole column.</summary>
        BoosterHitColumn,

        /// <summary>Booster name: deals the tiles anew.</summary>
        BoosterShuffle,

        /// <summary>What the spatula does, after its name on the pill (lower case).</summary>
        BoosterHitCellDoes,

        /// <summary>What the row brush does, after its name (lower case).</summary>
        BoosterHitRowDoes,

        /// <summary>What the column brush does, after its name (lower case).</summary>
        BoosterHitColumnDoes,

        /// <summary>What the shuffle does, after its name (lower case).</summary>
        BoosterShuffleDoes,

        /// <summary>What the spatula does, as a sentence of its own on the card that offers it.</summary>
        BoosterHitCellOnTheCard,

        /// <summary>What the row brush does, as a sentence on its card.</summary>
        BoosterHitRowOnTheCard,

        /// <summary>What the column brush does, as a sentence on its card.</summary>
        BoosterHitColumnOnTheCard,

        /// <summary>What the shuffle does, as a sentence on its card.</summary>
        BoosterShuffleOnTheCard,

        /// <summary>Second line of the armed spatula's pill: what to pick.</summary>
        BoosterPickACell,

        /// <summary>Second line of the armed row brush's pill.</summary>
        BoosterPickARow,

        /// <summary>Second line of the armed column brush's pill.</summary>
        BoosterPickAColumn,

        /// <summary>Why a spatula or brush did nothing (nothing was spent).</summary>
        BoosterNothingToHit,

        /// <summary>Why the shuffle did nothing (nothing was spent).</summary>
        BoosterCannotShuffle,

        /// <summary>The one sentence on the level the spatula opens on, until it is first touched.</summary>
        BoosterFirstUseHitCell,

        /// <summary>The sentence on the level the row brush opens on.</summary>
        BoosterFirstUseHitRow,

        /// <summary>The sentence on the level the column brush opens on.</summary>
        BoosterFirstUseHitColumn,

        /// <summary>The sentence on the level the shuffle opens on.</summary>
        BoosterFirstUseShuffle,

        /// <summary>Smaller line beside the first booster's first use: the levels are made to be won without boosters.</summary>
        BoosterEveryLevelWithout,

        /// <summary>Smaller line of the card that offers a booster used during play.</summary>
        BoosterCostsNoMove,

        /// <summary>On the disc of a booster that is not open yet: the level it opens on.</summary>
        BoosterOpensOn,

        /// <summary>Level card: header over the goals (small capitals).</summary>
        StartGoals,

        /// <summary>Level card: header over the start boosters (small capitals).</summary>
        StartChooseBoosters,

        /// <summary>Level card: label over the moves (small capitals).</summary>
        StartMoves,

        /// <summary>Level card nameplate, second line: the jigsaw piece winning gives (small capitals). {pieces} is "7" or "7–8".</summary>
        StartReward,

        /// <summary>Start booster name under its disc, on two lines: a brush (rocket) and a splash (bomb).</summary>
        StartRocketAndBomb,

        /// <summary>Start booster name under its disc, on two lines: the rainbow palette (colour bomb).</summary>
        StartColorBomb,

        /// <summary>What the brush-and-splash start booster does, on the card that offers it.</summary>
        StartRocketAndBombDoes,

        /// <summary>What the rainbow palette start booster does.</summary>
        StartColorBombDoes,

        /// <summary>What the extra moves start booster does.</summary>
        StartExtraMovesDoes,

        /// <summary>Smaller line of the card that offers a start booster the player has none of.</summary>
        GetMoreBuyToChoose,

        /// <summary>Result card nameplate: the level is won.</summary>
        ResultWonTitle,

        /// <summary>Card that asks whether to leave the level.</summary>
        ResultLeavingTitle,

        /// <summary>Card nameplate: the moves ran out.</summary>
        ResultOutOfMovesTitle,

        /// <summary>Card nameplate: no move is possible on the board.</summary>
        ResultNoMoveLeftTitle,

        /// <summary>Offer of extra moves, over the goals that are left, when the game was close.</summary>
        ResultSoClose,

        /// <summary>Offer of extra moves, over the goals that are left.</summary>
        ResultGoalsLeft,

        /// <summary>Lost card: under the goals that were not reached.</summary>
        ResultWhatIsLeft,

        /// <summary>Lost card when the extra moves could not be given; nothing was paid.</summary>
        ResultExtraMovesRefused,

        /// <summary>Beside the heart: giving up costs a life.</summary>
        ResultCostsALife,

        /// <summary>Beside the heart: the loss took a life.</summary>
        ResultCostALife,

        /// <summary>Leaving card, beside the heart: leaving after a move costs a life.</summary>
        ResultLeavingCostsALife,

        /// <summary>Leaving card before any move: leaving costs nothing.</summary>
        ResultLeavingCostsNothing,

        /// <summary>Where a card would say a life goes, in the tester mode.</summary>
        ResultNoLifeInTesterMode,

        /// <summary>Offer card: the underlined way to give up.</summary>
        ResultGiveUp,

        /// <summary>Offer card's blue button: an ad pays for the extra moves.</summary>
        ResultWatchAnAd,

        /// <summary>Offer card's dimmed blue button: no free ad left today.</summary>
        ResultNoAdLeftToday,

        /// <summary>Result card button that leads back to the museum's hall.</summary>
        ResultBackToTheMuseum,

        /// <summary>Won card: what the level gave, before the figures "7 / 10": one piece.</summary>
        ResultNewPiece,

        /// <summary>Won card: several pieces, before the figures.</summary>
        ResultNewPieces,

        /// <summary>Won card's blue button to the jigsaw, on two lines: one piece waits.</summary>
        ResultPlaceThePiece,

        /// <summary>Won card's blue button to the jigsaw, on two lines: several pieces wait.</summary>
        ResultPlaceThePieces,

        /// <summary>Won card beside the coins: no move was left over.</summary>
        ResultWonOnTheLastMove,

        /// <summary>Won card beside the brush: the moves left over became brushes (rockets), on two lines.</summary>
        ResultMovesLeftOver,

        /// <summary>Won card that gave no piece: the level and the moves it took.</summary>
        ResultLevelAndMoves,

        /// <summary>Lost or leaving card: the level and which try this was.</summary>
        ResultLevelAndAttempt,

        /// <summary>The sign over the shop.</summary>
        ShopTitle,

        /// <summary>Shop section header (small capitals): coins.</summary>
        ShopCoinsHeader,

        /// <summary>Shop section header (small capitals): boosters.</summary>
        ShopBoostersHeader,

        /// <summary>Shop section header (small capitals): free coins.</summary>
        ShopFreeHeader,

        /// <summary>Free strip's small capitals: ads left today.</summary>
        ShopFreeToday,

        /// <summary>Free strip's small capitals: no ad left today.</summary>
        ShopFreeNoneToday,

        /// <summary>Button of a coin pack, which the test build does not sell.</summary>
        ShopComingSoon,

        /// <summary>Free strip's blue button: watch an ad.</summary>
        ShopWatch,

        /// <summary>Free strip's button once today's ads are used.</summary>
        ShopTomorrowAgain,

        /// <summary>Free strip's line.</summary>
        ShopWatchForCoins,

        /// <summary>Name of the starter booster set.</summary>
        SetStarter,

        /// <summary>Name of the master booster set.</summary>
        SetMaster,

        /// <summary>Under boosters a reward gave, when they are of several kinds.</summary>
        BoostersCount,

        /// <summary>Card a coin pack answers with: its nameplate.</summary>
        NoticePacksTitle,

        /// <summary>Coin pack card: the test build sells nothing for money.</summary>
        NoticeNotOnSale,

        /// <summary>Coin pack card: where coins come from.</summary>
        NoticeWhereCoinsComeFrom,

        /// <summary>Card of a set the coins do not cover: its nameplate.</summary>
        NoticeCoinsShortTitle,

        /// <summary>Card of a set the coins do not cover: how many more coins it needs.</summary>
        NoticeCoinsShortFor,

        /// <summary>Card of a set the coins do not cover: how to gather coins.</summary>
        NoticeGatherCoins,

        /// <summary>Card of a set that was bought: its nameplate.</summary>
        NoticeBoughtTitle,

        /// <summary>Card of a set that was bought.</summary>
        NoticeInYourBag,

        /// <summary>Where start boosters just received are used.</summary>
        NoticeUseOnTheLevelCard,

        /// <summary>Where boosters used during play just received are used.</summary>
        NoticeUseFromTheShelf,

        /// <summary>Card of the daily gift: its nameplate.</summary>
        NoticeGiftTitle,

        /// <summary>Card of the daily gift that was taken already today.</summary>
        NoticeGiftTaken,

        /// <summary>Card of the daily gift: the next one comes tomorrow.</summary>
        NoticeNextGiftTomorrow,

        /// <summary>Card of the daily gift: the next one comes in several days.</summary>
        NoticeNextGiftInDays,

        /// <summary>Card of a gift that is not due yet: "The next one in 5h 12m."</summary>
        NoticeNextGiftIn,

        /// <summary>Card of an ad for coins when none is left today: its nameplate.</summary>
        NoticeNoAdTitle,

        /// <summary>Card of an ad for coins when none is left today.</summary>
        NoticeNoAdLeft,

        /// <summary>Card of an ad for coins when none is left today: second line.</summary>
        NoticeComeTomorrow,

        /// <summary>Lives pill on the wallet row while every life is there.</summary>
        LivesFull,

        /// <summary>Lives card nameplate.</summary>
        LivesTitle,

        /// <summary>Lives card while every life is there.</summary>
        LivesAllThere,

        /// <summary>Lives card for a player without a life.</summary>
        LivesPlayWhenALifeComes,

        /// <summary>Lives card: when the next life is back, "Next life 12:34".</summary>
        LivesNextLife,

        /// <summary>Lives card: when every life is back, "Full lives in 1h 42m".</summary>
        LivesFullAfter,

        /// <summary>Lives pill in the tester mode.</summary>
        TesterPill,

        /// <summary>Lives card under the heart in the tester mode.</summary>
        TesterUnlimitedLives,

        /// <summary>Lives card in the tester mode.</summary>
        TesterModeIsOn,

        /// <summary>Lives card in the tester mode: a loss costs no life.</summary>
        TesterNoLifeIsLost,

        /// <summary>Support card after seven taps on the version: the question.</summary>
        TesterQuestion,

        /// <summary>Support card: the line under the question.</summary>
        TesterTells,

        /// <summary>Support card: the answer that turns the tester mode on.</summary>
        TesterTurnOn,

        /// <summary>Support card: the answer that leaves it off.</summary>
        TesterLeaveOff,

        /// <summary>A wait under an hour, "42 min".</summary>
        DurationMinutes,

        /// <summary>A wait of whole hours.</summary>
        DurationHours,

        /// <summary>A wait in hours and minutes, "1h 42m".</summary>
        DurationHoursAndMinutes,

        /// <summary>Seconds left on the stand-in ad's bar.</summary>
        DurationSeconds,

        /// <summary>Nameplate of the card that stands in for an ad in the test build.</summary>
        AdTitle,

        /// <summary>Second line of that nameplate (small capitals).</summary>
        AdTestBuild,

        /// <summary>Under the stand-in ad's alcove: no ad is shown.</summary>
        AdNothingIsShown,

        /// <summary>What the stand-in ad pays: extra moves.</summary>
        AdPaysMoves,

        /// <summary>What the stand-in ad pays: coins.</summary>
        AdPaysCoins,

        /// <summary>Settings card nameplate.</summary>
        SettingsTitle,

        /// <summary>Settings row that opens the language card, and that card's nameplate.</summary>
        SettingsLanguage,

        /// <summary>This language's name in itself, on the language card and the settings row. Never translated: each table has its own.</summary>
        LanguageOwnName,

        /// <summary>Settings row with a switch: the sounds of play.</summary>
        SettingSound,

        /// <summary>Settings row with a switch: the phone's short buzz.</summary>
        SettingVibration,

        /// <summary>Settings row with a switch: the tester mode, while it is on.</summary>
        SettingTesterMode,

        /// <summary>Smaller line under the privacy row.</summary>
        SettingsPrivacyLine,

        /// <summary>Settings row that opens the support card, and that card's nameplate.</summary>
        SettingsSupport,

        /// <summary>Settings card's foot: the player's ID and the game's version.</summary>
        SettingsFoot,

        /// <summary>Under the foot: what kind of build this is.</summary>
        SettingsBuildLine,

        /// <summary>Build line: the ads are stand-ins.</summary>
        BuildAdsSimulated,

        /// <summary>Build line: nothing is sold for money.</summary>
        BuildNoBuying,

        /// <summary>Support card row: the player's ID.</summary>
        SupportPlayerId,

        /// <summary>Support card row: the game's version (seven taps here open the tester mode's question).</summary>
        SupportGameVersion,

        /// <summary>Support card row: the version of the levels.</summary>
        SupportContentVersion,

        /// <summary>Support card: the levels' version, "version 1".</summary>
        SupportContentVersionValue,

        /// <summary>Support card: label over the path of the attempt records.</summary>
        SupportRecordsFile,

        /// <summary>Support card: the records are kept in memory (the Editor's tools).</summary>
        SupportRecordsInMemory,

        /// <summary>Support card: what to tell when reporting a problem.</summary>
        SupportWhatToTell,

        /// <summary>Privacy card nameplate and the settings row that opens it.</summary>
        PrivacyTitle,

        /// <summary>Privacy card, paragraph 1 (approved text: keep every fact).</summary>
        PrivacyParagraph1,

        /// <summary>Privacy card, paragraph 2 (approved text: keep every fact).</summary>
        PrivacyParagraph2,

        /// <summary>Privacy card, paragraph 3 (approved text: keep every fact).</summary>
        PrivacyParagraph3,

        /// <summary>Privacy card, paragraph 4 (approved text: keep every fact).</summary>
        PrivacyParagraph4,

        /// <summary>Privacy card button that deletes the attempt records, after a question.</summary>
        PrivacyDeleteTheRecords,

        /// <summary>Privacy card question: the answer that keeps the records.</summary>
        PrivacyKeep,

        /// <summary>Privacy card question: the answer that deletes them.</summary>
        PrivacyDelete,

        /// <summary>Privacy card question.</summary>
        PrivacyQuestion,

        /// <summary>Privacy card, under the question: what deleting leaves alone.</summary>
        PrivacyWhatStays,

        /// <summary>Privacy card after the records were deleted.</summary>
        PrivacyDeleted,

        /// <summary>Privacy card when the records cannot be read to count them.</summary>
        PrivacyCannotCount,

        /// <summary>Privacy card: how many attempt records the phone keeps.</summary>
        PrivacyRecordsKept,

        /// <summary>Privacy card: the count when the records could not be read.</summary>
        PrivacyRecordsUncounted,

        /// <summary>Privacy card: the file's name and what a line of it is.</summary>
        PrivacyFileLine,
    }
}
