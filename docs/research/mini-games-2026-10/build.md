# Mini-games: how they would be built in our code

Written 2026-10-24. I read the code and ran nothing: no Unity, no build, no tests, no git (one exception is noted at the end). Claims about our code point to files. Claims about other games or about algorithms carry a source URL and date. Marks: **FACT** (read in our code, or stated by the source), **REPORTED** (a secondary source says it), **ESTIMATE** (my judgement).

This builds on `docs/research/monetization-and-retention.md` §5 and §7 and on `docs/research/monetization-2026-10/minigames.md` (ideas I1–I13). It doesn't repeat them. It answers a different question: **how would each kind of game be built here, and what would it cost?**

---

## Kısa özet (TR)

- **Ortak çerçeve bir kez yapılır (L, yaklaşık 2–3 arayüz aşaması). Sonra her yeni oyun türü kendi kural motoru ve görünümüyle eklenir.** Çerçevede şunlar var: telefon saatiyle çalışan bir etkinlik takvimi, kayıt dosyasının 6. sürümü (`events` bloğu), çubukta beşinci yer olarak "Etkinlikler" sayfası (tasarım tuvalinde bu yer zaten çizili, arkası boş), salonda hediye çerçevesine benzer bir etkinlik çerçevesi, kural kartı, ortak bir mini oyun sahnesi ve ödülü bir kez ödeyen bir defter. Bu defter `WalletLedger` ve `Reward.AddedTo` kalıbını izler.
- **Kodumuz buna hazır.** Ekranlar `IStage` arayüzüyle değişir. `GameSession.FollowThePlayer` tek bir `switch` ile yeni bir sahneye geçer. Sayfa (`Destination`), basış (`MuseumPress`) ve çıkış (`MuseumExit`) kapalı kümeler; derleyici her eksik yeri gösterir. Yapboz ekranı (`JigsawTable` + `JigsawLayout` + `JigsawStage` + `JigsawShots`) mini oyunlar için birebir şablon. Unity'siz modeli ve yerleşimi testler derler, Unity'li sahne yalnız çizer.
- **Puzzle.Core'u yeniden kullananlar:** Küratörün Sınavı (tek deneme, S–M), Gala gecesi ve "Renk Yağmuru" (kaybı olmayan tahta, M), Gece Nöbeti. Gece Nöbeti motor değişikliği ve yeniden kalibrasyon ister (L–XL); ertelenmeli.
- **Küçük yeni motor isteyenler:** bunlar yeni bir `Puzzle.Minis` derlemesinde olur (netstandard2.1, C# 9, `sync-engine.js` ile Unity'ye kopyalanır):
  - Boya tüpü sıralama (top/su sıralama, L)
  - Raf sıralama ("goods sort", L)
  - Üçlü taş eşleştirme (tile match, L)
  - Depo sıkışıklığı (Rush Hour benzeri kayan sandıklar, M–L)
  - Okla çıkar (tap-away, M)
  - Kaydırmalı tablo (15 bulmacası, M)
  - Döndürmeli tablo (S–M)
  - Pigment tarifi (Mastermind benzeri, S–M)
  - Katalog eşleştirme (hafıza, M)
  - Arşiv kazısı (S–M)
  - Nonogram (M–L)
  - Boru ve sızıntı (M–L)
- **Çözülebilirlik kanıtı:** Kayan sandık ve kaydırmalı tablo hamleleri geri alınabilir. Bu yüzden çözülmüş durumdan geriye yürüyerek üretilen her bölüm çözülebilir. Sıralama bulmacasında hamleler her zaman geri alınamaz. Geriye yürürken yalnızca karşılığı yasal ileri hamle olan "geri hamleler" seçilirse bölüm yine çözülebilir olur. Üçlü taş bölümü bir çözüm sırasından kurulur. Her türün bir çözücüsü (BFS/IDA*) ve "beceri merdiveni" benzeri gürültülü açgözlü botu olur; zorluk böyle ölçülür. Paketler, bugünkü bölümler gibi önceden üretilip ölçülür. O gün herkes aynı bölümü oynar, gizli ayar yok (§17).
- **Görseller:** Taşlarımızın 8 rengi ayrı şekilli (uğur böceği, damla, kare, papatya, üçgen, ay, lale, elmas). Bu sıralama ve eşleştirme oyunları için hazır ve renk körü dostu bir set. Tepsi, raf, tezgâh, çerçeve, plaket, kart ve düğme çizimleri de var. Tablolar parça kesitleri için kullanılabilir. Eksik olanlar: etkinlik takvimi simgesi (tuvalde var, depoda yok), boya tüpü ya da kavanoz, sandık, kazı ızgarası ve bazı oyun işaretleri.
- **Sözler:** 13 dil. Paketleyici yalnızca tablosu tam olan dili sunar. Yani bir türün sözleri 13 dilde birlikte gelmeli, yoksa o dil oyundan düşer. Ayrıca CJK yedek yazı tipleri yeniden kurulmalı.
- **En büyük riskler:**
  - APK 16 Ekim'de zaten 283 MB'tı. Yeni resim eklememeli, var olanı yeniden kullanmalıyız.
  - Ekonomi: her yeni altın kaynağı simülatörden geçmeli. Mini oyunlar 10 dakikalık oturumdan pay yer.
  - Yatay ekran desteği şu an ekleniyor. Mini oyun yerleşimleri kare bir oyun alanı seçmeli ve yatay boyutlarla da test edilmeli.
  - Kamu sözleşmesi değişiklikleri (kayıt v6, `Reward`'a can eklemek, beşinci çubuk yeri) sahibin kararını ister.
- **Önerilen ilk parti (ESTIMATE):** çerçeve + boya tüpü sıralama + depo sıkışıklığı + Küratörün Sınavı + pigment tarifi ya da katalog eşleştirme. Böylece bir etkinlik haftasında 2–3 farklı oyun dönebilir.

---

## 1. What the code has today

### 1.1 Stages and the session (FACT)

- **`unity/Assets/Scripts/GameRoot.cs`** is the scene's only object. It passes the frame time, the screen shape, touches, the way back and pause/resume to one `ITouchable`: the entry page first, then `GameSession`. It saves progress on pause and on quit. A new kind of screen needs nothing here.
- **`unity/Assets/Scripts/Stage/IStage.cs`.** Every screen is an `IStage`: one camera, a clock that only `Advance` moves, `Fit(ScreenShape)`, pointer down/moved/up/cancelled, `GoBack`, and `TakeWhatSounded`. Today's stages are `MuseumStage`, `GameStage` (the board, 2,641 lines) and `JigsawStage`. A mini-game is a fourth `IStage`.
- **`unity/Assets/Scripts/Flow/GameSession.cs`** owns the front stage, the progress and the ledgers. `FollowThePlayer()` is a `switch` on the front stage and what the player left it for (`MuseumExit.ToPlay`, `MuseumExit.ToAJigsaw`, `ResultChoice.*`, `JigsawStage { PlayerLeft: true }`). It calls `ChangeTheStage(Func<IStage>)`, which disposes the old stage before building the next one ("never two cameras at once"). Each step of `Advance()` runs in this order:
  1. tells the stage the wallet
  2. advances the stage
  3. keeps what happened
  4. does what the stage asked of the wallet
  5. keeps placed pieces
  6. keeps tutorial steps
  7. plays sounds
  8. follows the player

  A mini-game stage plugs into steps 3, 4 and 8.
- **Precedent for paying "once, in the same save":** the painting reward. `WalletLedger.WithPiecesInPlace` gives the reward in the same `PlayerProgress` that places the last piece, counted in `Wallet.Rewards`, so it can never be given twice.
- **Precedent for "in memory only":** an attempt in progress is not saved. Closing the game mid-level costs nothing, and the level starts again. Mini-games should behave the same way.

### 1.2 Museum pages, cards, the bar (FACT)

- **`unity/Assets/Scripts/Screens/Destination.cs`:**
  - `Destination` is a closed enum (Shop, Home, Map, Collection). The comment says how to add a page.
  - `Destinations.OnTheBar` says: "The canvas' fifth place, the events' calendar, has nothing behind it." So the design canvas already draws a fifth bar place for events. `NavigationBar.cs` says the same.
  - `MuseumPress`, `WalletRequest`, `DeviceRequest` and `MuseumExit` are closed sets built with private constructors.
- **`unity/Assets/Scripts/Screens/MuseumStage.cs`:**
  - `IMuseumPage` (with `IMovingPage` for pages that tick, `IShopPage` for pages told the shop view)
  - `IMuseumCard` (a `HangingCard<MuseumPress>` that is told the wallet), and a stack of cards over a page
  - `BuildPage(Destination)` is a `switch`.

  Existing cards to copy: `LevelStartCard` (rules and start boosters, green "Oyna"), `NoticeCard` (title, an exhibit or `Goods` with count pills, "Tamam"), `LivesCard`, `QuitCard`.
- **`unity/Assets/Scripts/Hall/HomePage.cs` and `GiftFrame.cs`.** The hall has a painter's workbench on the floor and a small gilt gift frame on the wall at the workbench's right: a red dot while the gift waits, and a pill that counts down to the next one. An event frame on the other side of the workbench is the natural "running event" sign in the hall (ESTIMATE). The `minigames.md` finding E2 (inline beats a separate button, REPORTED) also argues for putting entry points in the hall, not only on a new tab.

### 1.3 Wallet, rewards, ledgers (FACT)

- **`unity/Assets/Scripts/Flow/WalletLedger.cs`** (Unity-free; the tests compile it). `Answer(WalletRequest, Wallet, now, handling)` returns `Received(wallet, Reward)` or `Refused(why)`. It handles boosters, sets, coin packs, ad coins and the daily gift. `RewardForCompleting` and `WithPiecesInPlace` handle paintings.
- **`src/Puzzle.Meta/Rewards.cs`.** `Reward(Coins, IReadOnlyList<BoosterKind>)` with `AddedTo(Wallet)`, plus `RewardRotation`, `PaintingReward` and `DailyReward`. `DailyReward` compares `DayStamp`s, so a phone date that is set back opens nothing.
  - **`Reward` has no lives.**
  - `LifeMeter` (`src/Puzzle.Meta/LifeMeter.cs`) keeps "short of full" plus the time of the next life. It has `WithOneLost` but no way to give a life.
- **`src/Puzzle.Meta/DayStamp.cs` and `unity/Assets/Scripts/Flow/WallClock.cs`.** A calendar day in the player's own time zone. Days are compared, never trusted. `IWallClock` is a seam (`SystemWallClock` in the game, `FixedWallClock` in tools and tests). An event calendar can stand on these as they are.
- **`unity/Assets/Scripts/Flow/AttemptLedger.cs`.** Attempt events (`FirstMove`, `Won`, `LostForGood`, `Left`, ...) go through the economy's rules. This is where "a win also gives an event ticket" would hook in.
- **`src/Puzzle.Economy/Simulating/PlayerSimulator.cs`.** Coin sources are given through `CoinSource` (Daily, PaintingCompleted, ...). A mini-game reward is a new source, and the time it takes comes out of the simulated session.

### 1.4 The progress file (FACT)

`unity/Assets/Scripts/Flow/ProgressDocument.cs`:

- Layout `CurrentVersion = 5`: `{"version":5,"levelsWon":..,"attemptsOnLevel":..,"placed":[..],"wallet":{..},"tutorial":[..]}`.
- Unity's `JsonUtility` maps public fields, so there are no dictionaries and no polymorphism. Whether a block is present is decided by the version number, never by zeros or empty lists.
- Older layouts (1–4) are upgraded on read.
- A file from a **later** game is read "as far as this version can": `ProgressReading.OfALaterGame`, after which `GameRoot` runs that session in memory and does not save.
- Tutorial steps are saved by name. A name this game doesn't know is passed over in a later game's file.

A mini-game block fits this scheme: layout 6 adds `events`.

### 1.5 Words (FACT)

- **`unity/Assets/Scripts/Words/WordId.cs`** is a closed enum. An id is never renamed, and a new meaning gets a new id. About 262 entries are in `words/tr.json` today (counted from the file).
- 13 languages (`Language.cs`).
- `tools/unity/sync-engine.js` packs `words/<code>.json` into Resources. Its header says **"The game offers a language whose tables are complete."** So a mini-game's words must arrive in all 13 tables together, or the languages that lack them drop out of the language card.
- **`unity/Assets/Scripts/Art/LetterSet.cs` and `unity/Assets/Editor/LetteringBuilder.cs`.** A build carries no font files, only pre-made typefaces. The CJK and Cyrillic fallbacks hold exactly the letters the tables use. New words therefore mean rebuilding the typefaces in the Editor (ProjectSetup.Run).

### 1.6 Plain-C# layouts, tests, Editor tools (FACT)

- **Plain-C# classes with "No Unity dependency".** Many such files are compiled straight into `tests/Puzzle.Core.Tests` through `<Compile Include="..\..\unity\Assets\Scripts\...">` in `Puzzle.Core.Tests.csproj`:
  - layouts: `ScreenLayout`, `PageLayout`, `JigsawLayout`, `ResultCardLayout`, `LevelStartLayout`
  - models: `PlayerProgress`, `ProgressDocument`, `JigsawTable`, `WalletLedger`, `AttemptLedger`, `Timeline`, `Tween`, `TapSlop`, `PageSwipe`
- **`tests/Puzzle.Core.Tests/JigsawScreenTests.cs`** checks a model and a layout across a list of screens. The list covers the Pixel 9 Pro Fold's outer screen (1080×2424) and inner screen (2076×2152), plus 1080×2340, 1080×1920, 720×1280, 1200×1920 and 1600×2560, with and without insets. Mini-game layouts would be tested the same way.
- **The jigsaw is the template.** It is split into a model (`Flow/JigsawTable.cs`), a layout (`Layout/JigsawLayout.cs`), a stage (`Jigsaws/JigsawStage.cs`, 1,065 lines, plus `PieceTray`, `ProgressPips`, `RestoredSparkles`) and shots (`Editor/JigsawShots.cs`, an enum of moments reached by simulated touches).
- **Editor tools:**
  - `RenderHarness` takes deterministic pictures of a stage at given sizes and times, with no running game.
  - `ScreenShots` is a closed enum of states.
  - `GameWalk.*.cs` walks the game by touches with a picture at every stop and a contact sheet. It already plays whole levels with the expert bot.
  - `StoryStudio` steps a session's clock at 60 Hz with a fixed Wednesday-noon clock.
  - `PlayModeCheck`
  - `CollectionMemoryCheck`, for the A42 memory fix.

### 1.7 How engine code reaches Unity (FACT)

- `tools/unity/sync-engine.js` builds `src/Puzzle.Bot` (with `Puzzle.Core`), `src/Puzzle.Museum` and `src/Puzzle.Meta` as netstandard2.1 and copies the DLLs into `unity/Assets/Generated/Engine`. It packs levels, catalogue, economy and words into `unity/Assets/Generated/Resources`.
- `src/Puzzle.Core/Puzzle.Core.csproj` says: netstandard2.1, C# 9, "Do not raise LangVersion or add package dependencies".
- `Puzzle.Generator` and `Puzzle.Bank` are **not** shipped. They are tools.
- Randomness everywhere is `Puzzle.Core.Randomness.Rng` (PCG32, `Derive` for independent streams), the same on every platform.

---

## 2. A common frame for mini-games (proposal, ESTIMATE)

### 2.1 Where the code goes

| Place | What | Shipped? |
|---|---|---|
| `src/Puzzle.Minis/` (new, netstandard2.1, C# 9, references Puzzle.Core for `Rng` only) | One folder per kind (`Sorting/`, `Jam/`, `TileMatch/`, `Slide/`, `Recipe/`, ...): the rules (state, move, `IsSolved`, `IsLost`), the level reader, and a small hint solver where a kind has hints. The shared `IPuzzleRules<TState,TMove>` and generic searches (§3.2). | Yes: add `{ project: "src/Puzzle.Minis", assemblies: ["Puzzle.Minis.dll"] }` to `PROJECTS` in `sync-engine.js` |
| `src/Puzzle.Minis.Lab/` (new, net10.0) | Generators, difficulty measurement (solver plus noisy bot ladder) and pack writing. A CLI command `puzzle minis generate --kind sorting --band ...` in `Puzzle.Cli` | No (a tool, like Puzzle.Generator) |
| `src/Puzzle.Meta/` | `EventCalendar`, `EventRules` (rewards per stage, tickets per win), `EventReward` (coins, boosters, lives). This keeps the simulator able to read the same rules | Yes (already synced) |
| `minis/<kind>/pack.json` (new data folder, like `levels/generated`) | Measured levels per kind and band. `sync-engine.js` packs them into `Resources/Minis.json` | Yes, as data |
| `economy/default.json` | An `events` block: calendar, rewards, tickets. Packed by `economy-pack.js` like today's rules | Yes |
| `unity/Assets/Scripts/Minis/` | `MiniGameStage` (the shell) and one view per kind (`SortingView`, `JamView`, ...) | Yes |
| `unity/Assets/Scripts/Flow/` | `EventLedger.cs`, `EventsView.cs`, `EventWords.cs` (Unity-free, compiled by the tests) | Yes |
| `unity/Assets/Scripts/Layout/` | `MiniGameLayout.cs` and one layout per kind (Unity-free) | Yes |
| `unity/Assets/Scripts/Screens/` | `EventsPage.cs` (IMuseumPage, IMovingPage), `EventCard.cs` (rules and reward, IMuseumCard), `EventFrame.cs` in `Hall/` | Yes |
| `unity/Assets/Editor/` | `MiniGameShots.cs` (moments per kind), `GameWalk.Events.cs` | Editor only |
| `tests/Puzzle.Core.Tests/` | One test file per kind, plus calendar, ledger, document v6 and layout tests | — |

### 2.2 The calendar on the phone's clock

The owner wants events "in certain weeks of each month, several in one week". A deterministic proposal:

- **Blocks of the month.** Days 1–7 are block A, 8–14 are B, 15–21 are C, and 22–28 are D. Days 29–31 are rest days, or a "finale" day.
  - These blocks are simpler and safer than ISO weeks, which straddle months (ESTIMATE).
- **The schedule is data**, for example in `economy/default.json`:

```json
"events": {
  "blocks": ["B", "D"],
  "slots": [
    { "days": [1, 4], "kinds": ["sorting", "jam", "recipe"] },
    { "days": [4, 7], "kinds": ["curator", "memory", "slide"] }
  ],
  "stages": 7,
  "ticketsPerWin": 1,
  "freePlaysPerDay": 1
}
```

- **Rotation.** Within a slot, the kind for a month is chosen by `(year*12+month) mod kinds.Count`, or with `Rng.Derive(monthNumber, slotIndex)`. Every player sees the same kind on the same days, and §17 holds: no per-player tuning.
- **Pure function.** `EventCalendar.InEffectOn(DayStamp today) → IReadOnlyList<EventInstance>`. `EventInstance` holds:
  - an `EventInstanceId`: its own type, built like `"2026-11-B1-sorting"`
  - an `EventKind`: a closed enum, one member per mini-game kind
  - `FirstDay` and `LastDay`
  - the `LevelSeed` that picks its stage levels from the kind's pack
- **Clock tricks** (single player, no server):
  - Setting the date forward shows a future event early. It cannot give a reward twice, because every reward is counted per instance id.
  - Setting the date back opens nothing new, because days are compared, never trusted (as in `DailyReward.IsDueOn`).
  - With capped rewards this is acceptable. `minigames.md` §6 reaches the same view.
- **Tests** (`EventCalendarTests`):
  - every day of 2026–2030 maps to 0..n instances
  - no two instances share an id
  - midnight in the player's zone opens the next one, at `+03:00` and at `-08:00`
  - February and 31-day months
  - a day going backwards opens nothing

### 2.3 Screens and the player's path

- **Bar.** Add `Destination.Events`. Then `Destinations.OnTheBar` gets five places, as the canvas draws, with `NameOf` ("Etkinlikler") and `MarkOf` (a calendar mark).
  - **No calendar mark exists in `unity/Assets/Art/Gallery` today**, nor in `GalleryArt.cs`. `docs/GAME_DESIGN.md` line 1114 mentions calendar icons among the canvas drawings, so it should be extracted from the owner's design canvas.
  - `PageSwipe` order and plate widths (`NavigationBar`) change with five places.
  - `ScreenShots` and the `GameWalk` page turns must learn the new page.
- **Events page** (`EventsPage`, IMuseumPage + IMovingPage):
  - one card per running instance: the kind's mark, its name, "3 gün kaldı", stage pips (`PipRow`/`SmallPips`), tickets held, and a green "Oyna"
  - the next block's events greyed out with their start date, so the player knows when to come back
  - told an `EventsView` at every step, the way pages are told `ShopView` today
- **Hall frame** (`EventFrame`, like `GiftFrame`, on the wall at the workbench's left):
  - the running event's mark in a small gilt frame
  - a red dot while a free play or a ticket waits
  - a pill counting down to the end
  - a press opens the event's rules card (or the events page when two events run)
  - Map: nothing (ESTIMATE; the map is about places, not time).
- **Rules card** (`EventCard`, IMuseumCard, built in the language of `LevelStartCard` and `NoticeCard`):
  - nameplate with the kind's name
  - a picture of the goal (for example, a sorted tube)
  - 2–3 short lines of rules
  - the reward of the next stage, with `GoodsRow`/`GoodsItem` pills
  - a green "Oyna" (or "1 bilet" on the button)
  - The first time per kind it is a tutorial step, saved by name in `TutorialSeen`. Later it just hangs before play.
- **New closed-set members:**
  - `MuseumPress.OpenAnEvent(EventInstanceId)`
  - `MuseumPress.PlayAnEvent(EventInstanceId)`
  - `MuseumExit.ToAMiniGame(MiniGameTicket)`, where a ticket holds the instance, the stage number and the level it plays
  - The compiler then names every `switch` that must learn of them. This is the point of the closed sets (FACT: the comment in `Destination.cs`).
- **Mini-game stage** (`MiniGameStage : IStage`, a shell around `IMiniGameView`). The shell owns:
  - the gallery wall and lamps (`Gallery/Wall`, `WallLamps`)
  - a top plaque (stage n of 7, moves against par)
  - a bottom shelf (`Shelf`/`plank`) with the pause disc and the kind's tools, such as undo and hint
  - the dark in and out (`StageOpening.OutOfTheDark`)
  - the result card
  - `Timeline`/`Tween` for animation and `SoundCue` for sound

  Each view owns the board of its kind and turns touches into moves on the kind's rules (in `Puzzle.Minis`). Like `GameStage`, the stage plays the move on the model at once and shows it afterwards. The model is checked against what is drawn after every move.
- **Way back.** Mini-games cost no life, so the pause disc and the back gesture ask "Çık?" only when moves were made. Leaving loses the stage's progress, and nothing else.
- **Result card.** Built on `ResultCardLayout`'s rhythm: the alcove shows the reward (coins pile, booster disc, heart), with stage pips under it and a green "Devam". The reward is already in the wallet when the card comes (§2.4), as with a level win.

### 2.4 Paying: a ledger that gives each reward once

- **`EventLedger`** (Unity-free, in `Flow/`, next to `WalletLedger`):
  - `PlayerProgress WithStageWon(PlayerProgress p, EventInstance e, int stage, EventRules rules, DateTimeOffset now)` returns the progress with that instance's `StagesWon` raised and the stage's reward added, **in the same progress**. A stage already won gives nothing. This is the `WithPiecesInPlace` pattern.
  - `PlayerProgress WithTicketsForAWin(PlayerProgress p, IReadOnlyList<EventInstance> running)` is called from the session when `AttemptEvent.Won` is kept, so a level win and its tickets are one save.
  - `WalletAnswer Answer(EventRequest ...)` covers the free play of the day and spending a ticket. A refusal gives a reason (`NoTicket`, `EventOver`), as `WalletRefusal` does.
- **Session wiring** (sketch only):

```csharp
case MuseumStage { PlayerLeftFor: MuseumExit.ToAMiniGame mini }:
    ChangeTheStage(() => OpenTheMiniGame(mini.Ticket));
    _paintings.LetGoOfUnusedPictures();
    break;
case MiniGameStage { PlayerLeft: true } mini:
    ChangeTheStage(() => OpenTheMuseum(StageOpening.OutOfTheDark, MuseumDoor.AtTheEvents(mini.Ticket.Instance)));
    break;
```

  The win is kept in `KeepWhatHappened()`: `if (Front is MiniGameStage { JustWon: true } m) { _progress = _eventLedger.WithStageWon(...); SaveProgress(); }`. That is the moment the last move is made, before the celebration, as with a level.
- **Lives as a reward.** `Reward` holds coins and boosters only (FACT). Two options:
  - Add `EventReward(Coins, Boosters, Lives)` in Puzzle.Meta, with `AddedTo(Wallet, LifeRules, now)`, and a `LifeMeter.WithLivesGiven(int, now, LifeRules)`. That is an additive change to Puzzle.Meta's public contract.
  - Keep lives out of mini-game rewards.

  The lives meter is capped at 5, and lives almost never run out in a session (task context, economy §25). A life given to a full meter would be wasted, so the card would have to say "canların dolu". **Coins and boosters first, lives rarely (ESTIMATE).** This is a decision for the owner (§6).
- **Economy.** A new `CoinSource.Event` is needed in `Puzzle.Economy`, plus a play-rate assumption (how many stages a player plays per event day) and the session minutes they take. The simulator must keep "level 100 on day 8" or say how far it moves. Same rule as `monetization-and-retention.md` §5: every new coin source goes through the simulator.

### 2.5 Saving: progress layout 6

```json
{"version":6, "levelsWon":..., "attemptsOnLevel":..., "placed":[...], "wallet":{...}, "tutorial":[...],
 "events":[{"instance":"2026-11-B1-sorting","tickets":2,"stagesWon":3,"freePlayDay":739920}]}
```

- **New types:** `EventDocument` (public fields; `instance` as a string; numbers; no dictionary). `PlayerProgress` gets an `Events` property (`EventProgress`) and `WithEvents(...)`. Its `Equals` and `GetHashCode` must include it. Otherwise `HasUnsavedProgress` misses a change. (FACT: progress equality is how the session decides whether to save.)
- **Reading:**
  - A version below 6 reads as no events.
  - A later game's file passes over instance ids or kinds this game doesn't know, as tutorial names are passed over today.
  - A damaged entry (negative count, `stagesWon` above the event's stage count) makes the reading `Damaged`, as other blocks do.
- **Bound** (CLAUDE.md, "every cache has an upper bound"): when saving, drop instances whose `LastDay` is more than 35 days before today. That leaves at most about 4–8 entries (ESTIMATE). An expired instance's tickets are lost. The rules card says so.
- **In memory only:** the board of a mini-game in progress. A closed game restarts the stage (as with attempts, FACT).
- **Tests** (`ProgressDocumentTests`): v6 round trip; v5 file reads with no events; v7 later-game read keeps known events; damaged entries; pruning.
- **This changes a persistent schema.** It is additive (a new block), but it needs the owner's yes (CLAUDE.md).

### 2.6 Tutorial, sound, music (ESTIMATE, building on FACT)

- One `TutorialStep` per kind (`RulesOfSorting`, ...), shown once on the rules card, with one hand-guided first move like the jigsaw's first piece. `TutorialStep` is a closed enum saved by name (FACT), so adding members is forward-compatible.
- **Sounds:** the existing cues cover most needs: `Press`, `Refuse`, `Swap`, `Match`, `PiecePlaced`, `Win`, `Coin` and `CardDown` (FACT, `Flow/SoundCue.cs`). A sorted tube can sound `Match`, a stage won `Win`, coins counted `Coin`.
- **Music:** the board music (M2) can play under mini-games. New untracked sound files are in `art/sounds` (git status) and were not looked at.

### 2.7 Editor pictures and walks

- **`MiniGameShots`** (like `JigsawShots`): for each kind, a closed enum of moments: opening, an item in the hand, a move just made, stuck (if the kind can stick), stage won with its card, and the rules card. Each moment is reached by the touches a player makes, at the sizes already used: 1080×2424, 2076×2152 and 1080×2340. Landscape sizes are added once rotation lands.
- **`GameWalk.Events`:** the hall's event frame, then the rules card, then a stage played to its end by the kind's solver as "a finger" (as `GameWalk` plays a level with the expert bot), then the result card, then the coins counted on the wallet row. A second walk covers no ticket left: the card says why.
- `ScreenShots` gets the events page state.

---

## 3. Rules engines: what each kind needs

### 3.1 Kinds that reuse Puzzle.Core (the match-3 engine)

`Game` is "the single entry point shared by the Unity client, the test bot and the level generator" (FACT, `src/Puzzle.Core/Engine/Game.cs`). Its goal types are `CollectColor`, `BreakBoxes`, `ClearJelly`, `BreakChains` and `DropItems` (FACT, `GoalSpec.cs`). These kinds need no new rules, only a different ledger mode around the board:

| Kind | How it plays | What changes | Proof and difficulty | Effort |
|---|---|---|---|---|
| **Curator's Challenge** (one-shot past level; I12 in `minigames.md`) | A level the player has already won, one attempt, picked by date seed from levels 1..LevelsWon | `AttemptLedger` mode "challenge": no life, no `LevelsWon` change, no "+5" offer. Its record gets a mark | Free: the level and its seed pool are already calibrated (`SeedPoolCalibrator`, §17) | S–M |
| **Gala night / Colour Rain** (I3) | A no-loss board: every gem of the "gold" colour matched pays coins; the board ends when the moves run out | Bonus mode in `GameStage`: `CollectColor` with an unreachable target, out-of-moves means "finished, pay what was collected", no `OutOfMovesOffer`, a result card that counts coins. `GameStage` is large and coupled to `AttemptBrief` and `LevelResult`, so the mode flag has to be threaded carefully | Pay out is measured by running the existing bots on the board (expected coins per rung), so the simulator gets a number, not a guess | M |
| **Night Watch** (I2) | Advancing threat | A new tile or goal kind in Puzzle.Core. `RuleSet` says a rule change means levels "must be re-calibrated by the bot" (FACT) | Generator + bank + bot re-measurement | L–XL; defer |

### 3.2 New engines: one small shape for all of them (ESTIMATE)

```csharp
namespace Puzzle.Minis
{
    /// <summary>The rules of one kind of mini-game: no Unity, no randomness after the deal.</summary>
    public interface IPuzzleRules<TState, TMove> where TState : IEquatable<TState>
    {
        IReadOnlyList<TMove> MovesFrom(TState state);
        TState After(TState state, TMove move);       // throws on an illegal move
        bool IsSolved(TState state);
        bool IsLost(TState state);                     // tray full; most kinds never lose
        TState Canonical(TState state);                // e.g. tubes sorted, so the search sees fewer states
    }
}
```

Shared tools on top of it, written once:

- **`BreadthFirst.ShortestSolution`:** the minimum moves, or a proof that there are none. It works where the reachable states are few: sorting, jam, 3×3 slide.
- **`IterativeDeepening` (IDA\*)** with a per-kind heuristic, for larger slide puzzles. Only in `Puzzle.Minis.Lab`.
- **`DeadEnds`:** the share of reachable states from which no solution exists. It measures how "trappy" a level is.
- **`NoisyGreedyPlayer(skill)`:** the mini-game twin of `SkillLadder` (FACT: the rung "skill-n" notices n % of legal moves, with a temperature). Each kind gives a move score, for example "items left unsorted". The ladder plays 64 deals per level, as `puzzle seeds` does, and reads the moves used, the stuck rate and the win rate under a move budget. A band is chosen like the level bank's bands.
- **Determinism:** deals use `Rng` and `Rng.Derive` only (FACT: PCG32, the same on every platform).

**Shipping measured packs, not on-phone generation (ESTIMATE).** Generators run in `Puzzle.Minis.Lab`. Each kind's pack holds a few hundred measured levels per band, and the calendar picks from the pack by seed. That way:

- difficulty is measured before release
- everybody gets the same level on the same day
- the phone needs no generator or solver beyond a hint
- packs are a few hundred KB of JSON

### 3.3 Kind by kind

Rules shown in the game are ours. Market facts about other games are kept to what I could check today. `minigames.md` §1 has the game-by-game survey.

#### A. Paint-tube sorting (ball/water sort rules) — "Boya Tüpleri"

- **Rules:** n tubes of capacity k (4). Items are our gems. A move takes the top item of one tube onto another tube that has room and is empty or has the same item on top. The stage is won when every tube holds one kind or is empty. Free undo; no loss. The stars are moves against par.
- **Theory:**
  - Ball sort and water sort have the same solvability, both are NP-complete in general, and the paper bounds how many empty bins guarantee solvability (FACT: Ito et al., "Sorting Balls and Water: Equivalence and Computational Complexity", arXiv:2202.09495, submitted 2022-02-19, https://arxiv.org/abs/2202.09495, seen 2026-10-24).
  - At phone sizes the search is small. One public A\* solver reports 20,637 states evaluated for a sample hard level (FACT: octo/watersort, https://pkg.go.dev/github.com/octo/watersort, version published 2022-06-06, seen 2026-10-24).
- **Generation backward from a solved state** (my derivation; ESTIMATE that it gives good levels):
  1. Start sorted: n−e full tubes and e empty ones.
  2. Apply un-moves. Take the top item of a tube Y only if what lies under it is the same kind or nothing, and put it on any tube X that has room.
  3. The forward move X→Y is then legal, because Y's top is the same kind or Y is empty. So every state reached this way can be solved by playing the un-moves back.

  A free un-move (any top to any tube) does **not** keep this guarantee, because a ball-sort move cannot always be undone.

  After generation: run BFS for the shortest solution and `DeadEnds`. Reject levels whose shortest solution is too short or that leave a tube already sorted. Band by tubes (4–12), kinds (2–10) and empty tubes (2, then 1, which is much harder).
- **Tests** (`SortingTests`):
  - every un-move yields a legal forward move (property test over 10,000 random walks)
  - every level in the pack is solved by the solver, and replaying that solution reaches solved
  - canonical states are equal across tube orders
  - the bands' measured difficulty rises
- **Effort:** rules S; lab (generator, solver, ladder) M; view M–L (tap source, tap target, the item flies; landscape fit). **Total L.**

#### B. Shelf sorting ("goods sort") — "Müze Dükkânı Rafları"

- **Rules:** shelves with three front slots and hidden rows behind. Any front item moves to any free front slot. Three of a kind on a shelf clear, and the next row comes forward. Win when all are cleared.
- **Generation:** moves between slots are reversible. Clears are not, so generate by "un-clearing": put a triple on an empty front, pushing that shelf's current front row into its hidden stack, interleaved with random reversible moves. Solvable by construction. Measure with DFS + memo for small levels, and with the noisy greedy ladder for larger ones.
- **Effort:** L. There are more states than in A, and a 2.5D look with rows behind needs care on 1080×2424.

#### C. Triple tile match — "Detay Avı Taşları"

- **Rules:** layered tiles. A tile is free when no higher tile overlaps it. Tap a free tile and it goes to a 7-slot tray. Three of a kind in the tray clear. A full tray with no triple is lost. Mini-games cost no life, so a loss means "try again", with one free "+1 slot" (ESTIMATE).
- **Generation by construction:**
  1. Lay out the positions (half-grid offsets, layers).
  2. Find a removal order: repeatedly take any tile that is free in what remains.
  3. Give kinds in consecutive threes along that order. Playing that order never holds more than two tiles in the tray, so the level is solvable.

  Then measure with a solver over (removed set, tray contents) with memo, and the noisy ladder (how often a player fills the tray).
- **Faces:** gems and specials, or crops of restored paintings ("find the detail" flavour).
- **Effort:** L (overlap geometry, which is plain C# and testable; tray animation).

#### D. Storeroom jam (sliding blocks, Rush Hour rules) — "Depo Sıkışıklığı"

- **Rules:** a 6×6 storeroom. Crates of length 2–3 slide along their own axis. Move the painting's crate out of the door. Fits the theme: the museum's store after the disaster.
- **Theory:** generalised Rush Hour is PSPACE-complete (Flake and Baum, 2002). The hardest 6×6 start needs 51 moves, or 93 counted in single steps (REPORTED: Wikipedia, "Rush Hour (puzzle)", https://en.wikipedia.org/wiki/Rush_Hour_(puzzle), seen 2026-10-24). At 6×6 the whole connected set of positions can be searched.
- **Generation:**
  1. Place crates at random.
  2. BFS the connected component. All moves are reversible, so every state in it is solvable if any solved state is in it.
  3. Pick a state at the wanted distance from the nearest solved state.

  The distance is the exact minimum number of moves. Bands from about 5 to 25 moves for casual players (ESTIMATE).
- **Tests:** BFS distances, the solution replays to the door, every pack level's distance matches its band.
- **Effort:** rules S, lab M, view M (drag along an axis with `TapSlop`). **Total M–L.**

#### E. Tap-away ("arrow jam") — "Kapıları Aç"

- **Rules:** each crate has an arrow and leaves when its path to the edge is clear.
- **Generation:** place crates in reverse removal order, making sure each path is clear when it is removed. It is solvable exactly when the "blocks" graph has no cycle. Difficulty is the depth of that graph.
- **Effort:** M. A simple, satisfying, text-free kind.

#### F. Sliding painting (15-puzzle) — "Kayan Tablo"

- **Rules:** a 3×3 or 4×4 of a restored painting.
- **Theory:** half of all arrangements are impossible (a parity rule). Any solvable 3×3 needs at most 31 moves and any 4×4 at most 80 (FACT: Wikipedia, "15 puzzle", https://en.wikipedia.org/wiki/15_puzzle, seen 2026-10-24).
- **Generation:** random legal slides from solved keep solvability, or use a random permutation with parity fixed. Measure exact length with BFS (3×3) or IDA\* (4×4) in the lab.
- **Art:** cells are UV rectangles of the painting's picture, the way `JigsawCanvas` cuts pieces (FACT: the jigsaw already cuts paintings at runtime).
- **Effort:** M.

#### G. Turning tiles — "Döndür"

- **Rules:** a painting in 3×3 to 5×5 square tiles, each turned 90/180/270°. Always solvable; difficulty is the number of tiles turned.
- **Effort:** S–M. Good as a gentle first or rest-day kind.

#### H. Pigment recipe (Mastermind-like) — "Pigment Tarifi"

- **Rules:** a hidden order of 4 gems from 6. Each guess gets "right place / right kind" marks.
- **Similar in other games:** Royal Match's "Magic Cauldron" (from September 2023) is a colour-ordering guess game played with event currency (REPORTED: FoxData, "Royal Match's Mini-Games: The Secret Driving Its Global Success in 2025", 2025-09-30, updated 2026-01-19, https://foxdata.com/en/blogs/royal-matchs-mini-games-the-secret-driving-its-global-success-in-2025/, seen 2026-10-24).
- **Generation:** the secret comes from the date seed. A guess limit (8, ESTIMATE). A solver for the hint and for measuring.
- **Effort:** S–M. Note: no paid random items. Tickets come from wins, the secret is fixed per day, and there is nothing to buy.

#### I. Catalogue memory (I7) and Archive Dig (I6)

- **Memory:** deal pairs of painting crops by seed. **Dig:** a grid with fixed hidden finds per stage, so rewards are deterministic, not a lottery.
- No solver is needed. Difficulty is grid size and, for memory, the flip budget.
- **Effort:** M and S–M.

#### J. Nonogram — "Restorasyon Izgarası"

- **Rules:** fill a grid from row and column clues to reveal a small motif, such as a frame ornament.
- **Generation:** a random or hand-made motif is kept only when a line solver alone solves it (unique, no guessing).
- **Effort:** M–L. Marking cells on a 1080×2424 screen needs a pan/zoom or ≤10×10 grids. Logic fans in the 30+ audience may like it (ESTIMATE).

#### K. Pipes / route the leak (I1d) — "Sızıntıyı Yönlendir"

- **Rules:** turn pipe tiles so water from the leak reaches the drain and not the painting.
- **Generation:** build a path first, then scramble the turns. A backtracking solver checks the result.
- **Effort:** M–L.

**Not recommended (ESTIMATE):**

- **Pin-pull or physics games.** Device-independent physics is hard, bots can't measure them, and the effort is L–XL.
- **Wheels and plinko boards.** They are random rewards. That clashes with the spirit of §17 and §19 even when the currency is earned, and it is a poor fit for a family audience.
- **Timed games.** `monetization-and-retention.md` §5 already decided against time pressure.

---

## 4. Art and texts per kind

**What we have (FACT, `unity/Assets/Art`):**

- **Gems:** `Pieces/gem-*.png`, 8 colours at 512×512. Each colour has its own shape: ladybug, drop, square, daisy, triangle, moon, tulip, diamond. I looked at them in a contact strip. This makes them colour-blind-safe items for sorting and matching.
- **Specials** (`sp-*`), goal marks (`goal-*`), `item.png` and `mark-star.png`.
- **Gallery parts:** `tray`, `shelf`, `plank`, `workbench`, `frame-small`, `picture-frame`, `card`, `nameplate`, `plaque`, `medallion`, `pip-*`, `coins-chest/pile/pouch`, `disc-*`, `button-*`, `badge-*`, `pill*`, `sparkle` and `icon-*` (heart, coin, lock, help, shuffle, …).
- **Paintings:** `Resources/Paintings/room-01` … (100 paintings, about 88 MB of sources on disk), already cut at runtime for jigsaws.
- **SVG sources:** `art/unity/*.svg`, 115 files.

| Kind | Reuses | New art (small) | Words (ESTIMATE, Turkish first, then 12 more) |
|---|---|---|---|
| Frame (bar, events page, hall frame, cards) | card, nameplate, pills, pips, frame-small, coins, discs | calendar mark for the bar (from the canvas); event marks, one per kind (about 64–100 px) | about 20: tab name, "ends in", "next event", tickets, stage n/7, rules-card buttons, rewards, "lives full" |
| A Paint-tube sorting | gems | a glass tube or jar sprite (one, tinted), a cork | about 6 |
| B Shelf sorting | gems, specials, shelf, plank | a shelf back and slot shadow | about 6 |
| C Tile match | gems, specials, tray; painting crops for detail faces | a tile base (cream, gilt edge) | about 6 |
| D Storeroom jam | plank or wood texture, picture-frame for the painting's crate | crates 2 and 3 long, a door mark | about 5 |
| E Tap-away | crates from D | an arrow glyph | about 4 |
| F Sliding painting | painting picture (UV crops), frame | none | about 4 |
| G Turning tiles | painting picture, frame | none | about 3 |
| H Pigment recipe | gems, pips (for marks) | a mixing bowl or palette | about 6 |
| I Memory, Dig | painting crops, card back from card.png; dig: plank texture | dust or earth tile, finds | about 6 each |
| J Nonogram | grid lines can be drawn in code (`PaintedShape`, `MeshSketch`) | motifs (data, not pictures) | about 6 |
| K Pipes | drawn in code, or simple sprites | pipe pieces, a drop | about 5 |

**Painting rules:**

- Crops of public-domain paintings are allowed, but changed copies are not automatically: "spot the forgery" (I1b) needs each museum's terms checked in `licences.json` (`monetization-and-retention.md` §5).
- Pictures must be let go when the stage is taken down (`PaintingGallery.LetGoOfUnusedPictures`, the A42 memory fix; FACT, GameSession).
- Per the owner's rule, agents draw no people or mascots. All new art above is objects. The owner reviews it against the commercial polish bar (memory: feedback on character art and the quality bar).

**Texts:** with the frame plus four kinds, roughly 45–50 new `WordId`s (ESTIMATE). All 13 tables must be filled together (FACT: the packer offers only complete languages). Then the typefaces are rebuilt for new CJK letters. Every new kind repeats this.

---

## 5. Effort, risks, and a first batch

**Effort:** S = a few hours, M = about one UI stage (a day), L = 2–3 stages, XL = a week or more. All ESTIMATE.

| Item | Engine and lab | Unity view | Total | Main risk |
|---|---|---|---|---|
| Common frame (calendar, v6 save, ledger, bar place, events page, hall frame, rules and result cards, shots, walk, words) | M | L | **L** | Contract changes (§6); 5-place bar layout; economy simulator work |
| Curator's Challenge (Core) | S | S–M | **S–M** | Ledger mode inside `AttemptLedger` and `GameStage`; the record format must mark it |
| Gala / Colour Rain (Core) | S | M | **M** | Threading a bonus mode through `GameStage` (2,641 lines), `LevelResult` and `OutOfMovesOffer` |
| Night Watch (Core) | L | M | **L–XL** | Engine change means re-calibration (`RuleSet`) |
| A Paint-tube sorting | M | M–L | **L** | Un-move legality (§3.3 A); drag or tap feel |
| B Shelf sorting | M–L | L | **L** | State space; 2.5D rows on a small screen |
| C Tile match | M | L | **L** | Overlap geometry; tray loss must feel fair |
| D Storeroom jam | S–M | M | **M–L** | Drag along an axis near screen edges (back gesture) |
| E Tap-away | S | M | **M** | Little |
| F Sliding painting | S–M | M | **M** | Picture memory |
| G Turning tiles | S | S–M | **S–M** | Little |
| H Pigment recipe | S | M | **S–M** | Must stay deterministic (no lottery feel) |
| I Memory / Dig | S | M / S–M | **M / S–M** | Deterministic rewards |
| J Nonogram | M | M–L | **M–L** | Grid size on phones; rules text in 13 languages |
| K Pipes | M | M | **M–L** | Little |

**Risks across all kinds:**

- **APK size.** The prototype APK was 283 MB on 16 October, and most of that is the 100 full-size paintings (FACT: `docs/DEVLOG.md` line 175). Mini-games should add only small sprites in the existing atlases plus a few hundred KB of JSON packs. No new paintings (ESTIMATE). Any "painting of the day" with new pictures (I8) is the expensive exception.
- **Performance on the cover display (1080×2424).** The kinds above draw far fewer sprites than a match-3 board with its effects. Sorting has under about 60, jam under about 15, tile match about 100–150 (ESTIMATE). The real check is the phone, through `docs/PHONE_TESTS.md`, as for every stage.
- **Landscape.** Autorotation is allowed in all four directions in the working tree's `ProjectSettings.asset` (FACT; the file is modified, uncommitted). `ScreenShape` still says "The game is played upright" (FACT). Mini-game layouts should:
  - ask the page layout for a column or region instead of the whole screen
  - prefer a square play area: tubes in two rows, a 6×6 jam, square tile piles, which fit 2076×2152 and rotated tablets
  - keep drags away from the side edges, where Android's back gesture starts (`MuseumStage` already refuses swipes from there; FACT)

  Layout tests should add landscape sizes once the rotation code lands.
- **Session time and economy.** Mini-games use minutes of a 10-minute session. The simulator needs the play rate, or "level 100 on day 8" silently shifts (ESTIMATE).
- **Fairness rules (§17, §19).**
  - The same level is given to everyone on a day and measured beforehand.
  - Nothing random is sold. A future "extra tube" or "undo" for coins would change a mini-game level's difficulty through spending. That is the same question as the match-3 boosters, so the owner should decide it once for mini-games.
- **Words.** Partial translations remove languages (FACT). Ship a kind only when its 13 tables are done.

**A first batch (ESTIMATE).** It gives "several games in one event week" with the least new art:

1. **The common frame (L).** Nothing works without it.
2. **Paint-tube sorting (L).** The owner's own example; gems as items; the strongest "sorting" fit.
3. **Storeroom jam (M–L).** On theme; exact difficulty from BFS; text-free.
4. **Curator's Challenge (S–M).** Reuses everything: levels, bots and seed pools.
5. **Pigment recipe (S–M)** or **Turning tiles (S–M).** Cheap third or fourth options for the rotation.

After that, each new kind is an M–L addition. Its cost is mainly its view and 13-language words, because the calendar, the ledger, the save and the cards are already there.

---

## 6. What needs the owner's decision (contract and product)

1. **Progress layout 6** with an `events` block. This is a persistent schema change. It is additive, but per CLAUDE.md a human must say yes.
2. **Lives as a reward.** Either extend Puzzle.Meta's `Reward` or `LifeMeter` (a public contract the simulator uses too), or keep lives out of mini-game rewards.
3. **A fifth bar place, "Etkinlikler".** The design canvas has it. Its mark has to be extracted from the canvas.
4. **Ticket model.** Tickets from level wins plus one free play a day (my proposal), or free plays only. This decides how mini-games sit beside levels.
5. **Calendar.** Which blocks of the month (for example B and D), how many kinds per block, and how many stages per event.
6. **Selling mini-game helpers for coins** (undo, extra tube): yes or no, given the "spending never changes difficulty" rule.
7. **Which kinds go first** (§5).

---

## 7. Sources (all seen 2026-10-24)

| Id | Source | Date |
|---|---|---|
| W1 | Ito, Kawahara, Minato, Otachi, Saitoh, Suzuki, Uehara, Uno, Yamanaka, Yoshinaka, "Sorting Balls and Water: Equivalence and Computational Complexity", arXiv:2202.09495 — https://arxiv.org/abs/2202.09495 | 2022-02-19 |
| W2 | octo/watersort (Go solver, A\*, example run "Evaluated 20637 states") — https://pkg.go.dev/github.com/octo/watersort | 2022-06-06 |
| W3 | Wikipedia, "Rush Hour (puzzle)": generalised version PSPACE-complete (Flake & Baum 2002); hardest 6×6 start 51 moves / 93 steps — https://en.wikipedia.org/wiki/Rush_Hour_(puzzle) | live page |
| W4 | Wikipedia, "15 puzzle": half of positions unsolvable; 8-puzzle ≤ 31, 15-puzzle ≤ 80 single-tile moves — https://en.wikipedia.org/wiki/15_puzzle | live page |
| W5 | FoxData, "Royal Match's Mini-Games: The Secret Driving Its Global Success in 2025" (Magic Cauldron colour-ordering game, September 2023, played with event currency) — https://foxdata.com/en/blogs/royal-matchs-mini-games-the-secret-driving-its-global-success-in-2025/ | 2025-09-30, updated 2026-01-19 |

Our own files used: `unity/Assets/Scripts/GameRoot.cs`, `Flow/GameSession.cs`, `Stage/IStage.cs`, `Screens/Destination.cs`, `Screens/MuseumStage.cs`, `Screens/NavigationBar.cs`, `Screens/NoticeCard.cs`, `Hall/HomePage.cs`, `Hall/GiftFrame.cs`, `Flow/WalletLedger.cs`, `Flow/PlayerProgress.cs`, `Flow/ProgressDocument.cs`, `Flow/AttemptLedger.cs`, `Flow/WallClock.cs`, `Flow/JigsawTable.cs`, `Jigsaws/JigsawStage.cs`, `Layout/ScreenLayout.cs`, `Layout/ScreenView.cs`, `Layout/PageLayout.cs`, `Layout/ResultCardLayout.cs`, `Words/WordId.cs`, `Words/Language.cs`, `Art/LetterSet.cs`, `Art/GalleryArt.cs`, `Editor/LetteringBuilder.cs`, `Editor/RenderHarness.cs`, `Editor/GameWalk.cs`, `Editor/JigsawShots.cs`, `Editor/StoryStudio.cs`, `Editor/PlayModeCheck.cs`, `src/Puzzle.Meta/Rewards.cs`, `Wallet.cs`, `LifeMeter.cs`, `DayStamp.cs`, `EconomyRules.cs`, `src/Puzzle.Core/Engine/Game.cs`, `RuleSet.cs`, `Levels/GoalSpec.cs`, `Randomness/Rng.cs`, `src/Puzzle.Bot/*.cs` (docs), `src/Puzzle.Economy/Simulating/PlayerSimulator.cs`, `tools/unity/sync-engine.js`, `tests/Puzzle.Core.Tests/Puzzle.Core.Tests.csproj`, `JigsawScreenTests.cs`, `docs/GAME_DESIGN.md` §17, `docs/DEVLOG.md`, `docs/PHONE_TESTS.md`, `docs/research/monetization-and-retention.md` §5 and §7, `docs/research/monetization-2026-10/minigames.md`.

## 8. Limits of this note

- Nothing was compiled or run. Effort sizes are judgement.
- The landscape work lives only in the modified `ProjectSettings.asset` that I could see. Its layout code may be on another branch, and I didn't look.
- Firecrawl had no credits left. Web checks were done with WebSearch and WebFetch, kept to algorithms and one market example. The market survey is in `minigames.md`.
- I ran one read-only `git branch -a` by mistake (its output was thrown away). Nothing else touched git.
