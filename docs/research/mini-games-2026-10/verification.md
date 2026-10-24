# Fact check: types.md, calendar.md, build.md (mini-games, October 2026)

Checked 2026-10-24 by re-opening each source (or a second source) and reading the repository files the build note names. Nothing in the three files was changed. Firecrawl had no credits; checks used WebFetch, WebSearch, the Royal Match / Homescapes fan-wiki public API (`api.php`, read with curl), King's public help-centre article API, the iTunes Search API, and the Android / Apple / RFC pages directly. Nothing needed a sign-in. NamuWiki (403), BBC (blocked) and Reddit were not reachable; where a claim rests only on them it is marked below.

**Marks used here.** CONFIRMED = the source (or a second source) says it. CORRECTED = the claim is close but wrong in a detail that matters. UNSUPPORTED = the cited source does not say it. CONFLICT = sources disagree. NOT RE-CHECKED = could not be reached today.

## Kısa özet (TR)

- **Genel hüküm: üç dosya da güvenilir.** Kontrol edilen 34 web iddiasının 22'si olduğu gibi doğrulandı. 10'unda düzeltilmesi gereken bir ayrıntı var, 2'si kaynağın kendisinde çelişkili. Uydurma bir sayı bulmadım. Kod notundaki (build.md) dosya, tür ve satır iddialarının hemen hepsi doğru; 4 küçük düzeltme var.
- **En önemli düzeltme (calendar.md):** Royal Match wiki'sinin kendi Pinata Party sayfası etkinliği **hafta sonuna** (Cuma 08:00 → Pazartesi 08:00 UTC) koyuyor, ama metni "Hidden Temple bitince açılır" diyor (Hidden Temple hafta içi). Yani "hafta içi zinciri Hidden Temple → Pinata Party" kesin değil; kaynak kendi içinde çelişkili. "Haftada iki zincir" fikri yine de geçerli: hafta sonu Magic Cauldron → Balloon Rise zinciri wiki'de net.
- **"Etkinlik bitince son bir şans her zaman var" genellemesi yanlış:** Dragon Nest'te yok (wiki: kalan toplar kullanılamaz).
- **Dragon Nest çelişkisi çözüldü:** 4 ejderha var, her biri bir ortakla büyütülüyor; yani dört ortak, her ejderhaya bir tane. İki kaynak da doğru.
- **Juggle Jam:** "Kasım 2024" tarihi T15'te değil, T2'de. "Parası satın alınabilir" iddiası Juggle Jam için desteklenmiyor.
- **ASA kararı** 30 Eylül 2020 tarihli (Ekim değil). 10 mini oyun, 20 bölümde bir, 4.160 bölüm ve %0,03 rakamları kararda Playrix'in kendi beyanı olarak geçiyor.
- **Google Play:** tanıtım içeriği bütün oyunlara açık, ama **öne çıkarma (featuring)** yalnızca "Premium growth tools" ölçütünü karşılayanlara açık.
- **Kod:** Bugün dört `IStage` var (EntryStage da sayılır), üç değil. Bu yüzden mini oyun beşinci olur. `Advance` sekiz değil dokuz adım. `CoinSource` `Decisions.cs` içinde. Takvim simgesi depoda gerçekten yok, ama GAME_DESIGN.md 1114. satır geldiğini söylüyor: bu bir belge-depo uyumsuzluğu.

---

## 1. Verdict

The three notes hold up. Of 34 web claims re-checked, 22 are confirmed as written, 10 need a detail fixed, and 2 are conflicts inside the sources themselves. No invented figure was found: every number I re-opened (growth shares, ladders, unlock levels, platform limits, ASA figures, App Store dates and sellers) is on its source. The build note's code claims are accurate apart from four small slips. The corrections that change a design reading are C1 (the Royal Match weekday chain is uncertain), C2 (no universal "last chance") and C4 (Play featuring is gated); the rest are citation or wording fixes.

## 2. Web claims re-checked (types.md and calendar.md)

| # | Claim (file) | Result | What the source says (checked 2026-10-24) |
|---|---|---|---|
| 1 | Puzzle IAP over $10B in 2025 (+14%); growth Merge 60% (mostly Gossip Harbor), Sort 13% (Magic Sort), Block 13% (Color Block Jam), Screw 8% (Screwdom) (types §1) | CONFIRMED | Naavik, Oindrila Mandal, 2026-01-18, Sensor Tower data. https://naavik.co/digest/what-leading-match-3-and-merge-games-do-differently/ |
| 2 | Match Factory, Color Block Jam, Screwdom in the 2025 top 25 grossing puzzle games (types §1) | CONFIRMED | Same page, same wording |
| 3 | Mistplay survey: 84% seek relaxation, 81% women; Gossip Harbor over $650M in 2025 (types) | CONFIRMED | Same page. "Beating Gardenscapes" was not re-checked |
| 4 | US Q4 2025 top 5 puzzle by downloads: Block Blast, Jigsolitaire, Pixel Flow!, Magic Sort!, Word Search Explorer, with weekly figures (types §1) | CONFIRMED, with a caveat | Sensor Tower blog, January 2026. https://sensortower.com/blog/2025-q4-unified-top-5-puzzle-games-units-us-64bc1644e1714cfff1e615fc. The five games, publishers and figures (565K→1M+, 1M+ late Oct, 457K, 381K, 349K) match. The post lists them without ranking; "by downloads" comes from the URL ("units"). Byline: "Timothy, Your Friendly Neighborhood AI", so types.md's "AI-written" caveat is right |
| 5 | Royal Match Puzzle Break: 3 days, 5 jigsaws from 9 to 25 pieces, 1 point per placement even when wrong, 1 free point, a chest per picture, grand prize, about 53 levels (types §2.14) | CONFIRMED as reported | Gamigion, Anton Slashcev, "June 20" (no year on the page). https://www.gamigion.com/new-event-in-royal-match-puzzle-break/. Also: pieces grow 9→16→25, and the last puzzle starts with 3 pieces placed |
| 6 | Puzzle Break unlocks at level 39, fed by wins, more from hard levels (calendar §2.8, §3) | CONFIRMED | Royal Match Help Center, Puzzle Break. https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/528-puzzle-break/. The help page says "puzzle pieces" collected by beating levels, with extra from Hard, Super Hard and Extremely Hard. It is silent on wrong placements (see correction T9) |
| 7 | Magic Cauldron since September 2023; a colour-order guess game paid with event currency (types §2.4, build §3.3 H) | CONFIRMED | Royal Match wiki infobox: release date 25 September 2023, Fri 08:00 → Mon 08:00 UTC, level 39. Naavik T2 and FoxData T13 say September 2023 too |
| 8 | Magic Cauldron ladder: 15 steps; Balloon Rise ladder: 10 steps; every step's reward as in calendar §4.1; coin totals 1,500 and 4,500 | CONFIRMED, step by step | Royal Match wiki (API), pages "🔮 Magic Cauldron" and "🎈 Balloon Rise". Every row of calendar's table matches, including the chest (Wild card + 1 h unlimited lives + one of each of 7 boosters) |
| 9 | Magic Cauldron alternates every other week with Duke's Fortune; Balloon Rise appears only after Magic Cauldron; Balloon Rise rewards "can be changed based on performance" (calendar) | CONFIRMED | Same wiki pages (Note 2 / Note 3) |
| 10 | Balloon Rise: level 39, cookies from wins, 3 balloons per step, a loss pops one, losing all drops you to the start of the step (calendar §3) | CONFIRMED | Help Center 119-balloon-rise. Help says Hard / Super Hard / Extremely Hard give extra cookies; the wiki says they do not. calendar.md already reports this conflict; the help page is official and more recent, so it should win |
| 11 | Magic Cauldron: elixirs from wins, more on hard levels, quitting loses elixirs, one last chance at the end (calendar §3) | CONFIRMED | Help Center 278-magic-cauldron |
| 12 | Weekday block Mon 08:00 → Fri 08:00 UTC, weekend Fri 08:00 → Mon 08:00 UTC (calendar §2.1) | CONFIRMED for King's Cup, Hidden Temple (Mon→Fri), Magic Cauldron and Balloon Rise (Fri→Mon) | Wiki infoboxes. **But see C1 for Pinata Party** |
| 13 | Weekday chain Hidden Temple → Pinata Party (calendar §1, §2.1, key findings) | **CONFLICT** | Wiki "🍭 Pinata Party": infobox `active_days=Friday 08:00 UTC`, `event_end=Monday 08:00 UTC` (weekend), while its text says "Pinata Party becomes available when the player finishes Hidden Temple event", and Hidden Temple runs Mon→Fri. NamuWiki [C4] (search excerpt only) gives yet another order. See C1 |
| 14 | King's Cup start gives 30 minutes of unlimited lives (calendar §4.2) | CONFIRMED | Wiki "🏆 King's Cup": "Starting the event will also provide the player 30 minutes of unlimited lives" |
| 15 | Dragon Nest: first Friday of the month, 10 days, level 76 (calendar) / 75+ (types); balls 1 / 3 / 5 for Normal / Hard / Super Hard, x2 bonus; prizes frame, card pack, 5,000 coins, boosters (types §2.18) | CONFIRMED | Wiki infobox: "First Friday of every month", "10 days after the start", "76 or above", participants 4. Old Cynic (published 2024-10-09, updated 2025-09-05): "above Level 75" (= 76), balls 1/3/5, x2 doubles. https://oldcynic.com/royal-match-dragon-nest-magic-tokens-balls-guide-tips. Slots are x1, x2, x3 and x10, not a continuous x1–x10 |
| 16 | Dragon Nest team: "four other players" vs "one matched partner" (types gap, unresolved) | **RESOLVED** | Wiki: players raise 4 dragons, "each Dragon by matching with one other player". Old Cynic: four other players, each helping hatch one dragon. Both are true: one partner per dragon, four in all. See T6 |
| 17 | Train Journey: 10 days, trains from won levels spun on a roulette, three friends on a shared track (types §2.18) | CONFIRMED | Old Cynic, published 2024-12-21, updated 2025-04-04. https://oldcynic.com/royal-match-train-journey-guide. "Third Friday of the month" (calendar) is **not** on this page; it rests on the NamuWiki excerpt alone (NOT RE-CHECKED) |
| 18 | Royal Match lives: cap 5, one every 30 min, refill 900 coins, no requests above 10 free lives, Royal Pass raises the cap to 8; Royal Pass 30 steps from the start of the month; Bonus Bank max 5,000 coins (calendar §4.3, §4.4) | CONFIRMED | Wiki "Lives" and "Royal Pass" (API) |
| 19 | Royal Kingdom: 21 events; Diamond Mine and Electro Blitz 36, Cube Blast and Magic Carnival 51, Hill Quest and Canvas Quest 101 (calendar §2.2, §2.8) | CONFIRMED | Royal Kingdom Help Center, Events section (21 articles) |
| 20 | Magic Carnival: memory match, orbs from wins (more on Hard / Super Hard / Kingdom / Dark Kingdom), mismatches fade, quitting loses orbs, one last chance, no carry-over (calendar §3) | CONFIRMED | Help Center 558-magic-carnival |
| 21 | Toon Blast Surprise Calendar: level 35, once a day, tap gifts and avoid TNT, 6 days → surprise gift, online only, killing the app loses the day (calendar §2.4) | CONFIRMED | Toon Blast Help Center 555 |
| 22 | Toon Blast Rescue Mission: no life lost, unlimited retries, same reward, no event / Crown Rush progress, skip loses it (calendar §2.4) | CORRECTED (wording) | Help Center 390: retries allowed "multiple times", no limit stated. Also it is timed ("before time runs out"). See C3 |
| 23 | Toon Blast Cannon Fest: one last chance to use cannonballs (calendar §3) | CONFIRMED | Help Center 610. Carry-over is not mentioned |
| 24 | Propeller Rush: unlimited lives the first time you start a step each day; starts by a button (calendar §2.1, §4.2) | CONFIRMED | Help Center 539. Unlock level 96 |
| 25 | Monopoly GO Juggle Jam: November 2024, 3–7 balls, one token per ball per guess, tickets to a prize shelf (dice, sticker packs, cash), tokens from tournaments, quick wins, the store every 8 hours (types §2.4) | CORRECTED (citation) | TheGamer, Gabrielle Castania, 2025-01-21: all of it **except the launch date**, which is in Naavik T2 ("November 2024"), not T15. See T1, T2 |
| 26 | 10 of the top 16 grossing games of 2025 use mini-games; Whiteout Survival Marvelous Fantasia (mahjong) with a revenue lift mid-July 2025; Jolly Marble plinko; Last War Frontline Breakthrough ad-to-event; Playrix pin-pull ads; PUBG Mobile trivia; Gossip Harbor Clay Color Combo (types) | CONFIRMED | Naavik, Oindrila Mandal, 2025-08-17. https://naavik.co/digest/why-minigames-mean-major-business/ |
| 27 | Liftoff: "thinking" mini-games have the highest adoption; only digging and Wordle-like reached wide adoption; Royal Kingdom Tetris-like block mini-game; Hexa Sort and Gossip Harbor Wordle-like; Merge Mansion and Match Factory physics; top new titles Magic Sort, Goods Puzzle, Blossom Sort, Color Block Jam, Screw Jam, Seat Away (types) | CONFIRMED | https://liftoff.ai/2025-casual-gaming-apps-report/ (no date on page; data Feb 2024 – Feb 2025, GameRefinery snapshot March 2025). The Royal Kingdom line is in the page text, not only a caption, so it is stronger than types.md says |
| 28 | Toon Blast Cannon Fest (late 2024) → ads → Flow Games' Smash Fest! (early June 2026): 15M installs, $4M IAP (also 14M), top 10 grossing hybrid-casual in July, pulled from Google Play by end of July (types §3) | CONFIRMED, with two details | Gamigion, Andrei Zubov (AppMagic), "August 20" (no year). The text does not name AppMagic as the data source; Cypher Games' Royal Smash! (about 9M installs, $1M IAP) is also listed, and most other clones are in testing or soft launch. See T5 |
| 29 | Royal Match King's Tales expedition, July 2026, 10 days; energy sold for the first time; +10.1% revenue (Sensor Tower) (types §2.18) | CONFIRMED, with one misattribution | Gamigion, Oxana Fomina, "September 15" (year from image path). Packs $2.99, $5.99, $9.99; $0.99 daily offer; Crystal Pass $4.99 / $9.99. +10.1% revenue and +10.8% ARPDAU "after controlling for day of week and month". See T4 |
| 30 | Merge Smith is a secondary progression system (types §2.12) | CONFIRMED | Sensor Tower, Bryan Isagholian, May 2026: "extends the mid-term loop into a secondary progression system". The post also names about 13 events in one sample week and says mid-term events are timed to weekends |
| 31 | ASA banned two Playrix pin-pull ads; Homescapes had about 10 such mini-games, about one per 20 levels, 4,160 levels; about 0.03% of players reached them (types §3) | CONFIRMED, date corrected | ASA ruling on PLR Worldwide Sales Ltd, **30 September 2020**. https://www.asa.org.uk/rulings/plr-worldwide-sales-ltd-g20-1061644-plr-worldwide-sales-ltd.html. Figures are Playrix's own, as of April 2020. Homescapes ad seen 31 March 2020, Gardenscapes ad April 2020. Gardenscapes had 5,895 levels. See T3 |
| 32 | App Store facts: Jigsolitaire (GAMINCAT, 2025-03-07, 75,224), Pixel Flow! (Loom Games, 2025-08-17, 170,553; rule text), Magic Sort! (Grand Games, 2024-05-28, 699,605), Screwdom (Zego Global, 2025-01-02, 116,628), Vita Mahjong (Vita Studio, 2023-12-01), Zen Match (Moon Active, 2021-07-07), Block Blast! (2022-04-03) (types §2) | CONFIRMED | iTunes Search API, US store. Exact counts match. Block Blast's **seller is ARETIS LIMITED**; Hungry Studio is the developer name. See T8 |
| 33 | Tile match: tray "usually 7"; Triple Tile the most downloaded tile match, ad-funded; about $60M IAP in a year (data.ai); physics pile separates 3D match (types §2.5, §2.6) | CONFIRMED, with a nuance | Deconstructor of Fun, "Oct 2" (year 2023 inferred), written by Ahmetcan Demirel. "Most downloaded" is over the last 52 weeks, not "in 2023" |
| 34 | Color Block Jam has a time limit on each level (types §2.7) | Weakly supported | App Store description: "plan your moves before running out of time". Reviews mention a per-level limit. Good enough for "timed", not for FACT about every level |

### Platform and store rules

| Claim (calendar §5.1, §2.7) | Result | Source |
|---|---|---|
| Android `elapsedRealtime` counts deep sleep, is monotonic, suits interval timing; ACTION_TIME_CHANGED and ACTION_TIMEZONE_CHANGED exist | CONFIRMED | https://developer.android.com/reference/android/os/SystemClock |
| `currentNetworkTimeClock()`: API 33, synchronised from a remote source, cannot be adjusted by the user, no blocking network operation, throws DateTimeException when no network time; not for security use | CONFIRMED word for word | Same page |
| `Settings.Global.BOOT_COUNT` added in API 24; `AUTO_TIME` (NITZ, GNSS, NTP), prefer TimeManager | CONFIRMED | https://developer.android.com/reference/android/provider/Settings.Global. Note: `android.app.time.TimeManager` is a system API that ordinary apps cannot call, so reading `AUTO_TIME` directly is the only option for us |
| POST_NOTIFICATIONS runtime permission from Android 13 (API 33) | CONFIRMED | https://developer.android.com/develop/ui/views/notifications/notification-permission. Apps targeting 12L or lower get the prompt at first activity after a channel is created |
| iOS `ProcessInfo.systemUptime`: time awake since restart; a required-reason API | CONFIRMED | Apple documentation JSON for `systemuptime` |
| Apple In-App Events: up to 31 days, promoted up to 14 days ahead, 10 published at a time | CONFIRMED | https://developer.apple.com/app-store/in-app-events/. Also: up to 15 approved events in App Store Connect at a time |
| Google Play Promotional content: available for all games; up to 4 weeks; submit at least 4 days ahead, 14 for featuring | CORRECTED | https://support.google.com/googleplay/android-developer/answer/12932541. True, but **featuring, deep links and audience targeting are only for apps and games that meet the Premium growth tools criteria**. Earliest submission is 60 days before start. See C4 |
| RFC 9110: origin servers must send `Date` in 2xx, 3xx, 4xx | CONFIRMED with a qualifier | §6.6.1: the MUST applies to "an origin server with a clock". Static hosts and CDNs do send it in practice; not a design issue |
| Candy Crush timed boosters: real time, stack, not lost on a failed level, can be won or bought; unlimited lives timer runs while away and stacks; long-wait fix after a clock change | CONFIRMED | King help-centre API: articles 360002352858 (updated 2026-06-06), 7332093991069 (2026-05-29), 7353277804189 (2026-08-10) |
| Homescapes: Cupcake Cup / Let's do it / Team Chest Mon–Fri; Weekend Plans / Team Competition Fri–Sun; Laser Busters Thu–Fri; Knight's Race Sat–Fri; Ice Crash and Treasure Hunt in test; expeditions 21 days | CONFIRMED | Homescapes wiki "Events" (API). Ice Crash is a competitive score event in test |

## 3. Corrections and unsupported claims

### types.md

- **T1. Juggle Jam launch date is mis-cited.** "Monopoly GO Juggle Jam (November 2024) … REPORTED [T15]". T15 gives no launch date. The date is in T2 (Naavik). Cite T2 for the date.
- **T2. "Its currency can be bought REPORTED [T2]" is unsupported for Juggle Jam.** T2 only says Monopoly GO sells mini-game event currency in general. T15 lists token sources: tournaments, banner events, daily Quick Wins, the store every 8 hours, and some vaults. Neither page says Juggle Jam tokens are sold. The matrix and §4 "Paid turns" row should drop Juggle Jam or mark it unverified.
- **T3. ASA date.** The ruling is dated 30 September 2020; BBC and other press reported it on 12–13 October 2020. Key finding "ruled in October 2020" should read "ruled on 30 September 2020". All figures (about 10 mini-games, about one per 20 levels, 4,160 levels, about 0.03%) are in the ruling as Playrix's own statements for April 2020, so FACT with that attribution, not only REPORTED.
- **T4. King's Tales energy.** "energy about 1 per normal level" is not about King's Tales. The article says about one unit per normal level was how Royal Match fed event currency **before** this event; it does not say how King's Tales energy is earned. Packs are $2.99, $5.99 and $9.99. The +10.1% is "after controlling for day of week and month" (no control group, but seasonality is adjusted), with ARPDAU +10.8%.
- **T5. Smash Fest! source.** The article does not name AppMagic as the source of 15M installs and $4M IAP (the author works at AppMagic). The clone list should add Cypher Games (Royal Smash!, about 9M installs, $1M IAP) and note that the Moon Active, Azur, Supercent and Tripledot titles were in testing or soft launch.
- **T6. Dragon Nest conflict, resolved.** One partner per dragon, four dragons, so four partners in all (wiki infobox: participants 4; text: "each Dragon by matching with one other player"; Old Cynic: "four other players"). Remove it from the gaps. Slot multipliers are x1, x2, x3 and x10.
- **T7. Sensor Tower Q4 2025.** The post lists the five games without ranking them. "Top 5 by downloads" is implied by the URL ("units"), not stated.
- **T8. Block Blast seller.** The App Store seller is ARETIS LIMITED; Hungry Studio is the developer name (iTunes `artistName`). types.md says T25 gives the "seller", so label Hungry Studio as the developer.
- **T9. Puzzle Break's "even when wrong" may be out of date.** Gamigion (June 2025) says a wrong placement costs a point. calendar.md quotes NamuWiki saying the rule was later changed so mistakes cost nothing, and the official help page is silent. types.md §2.14 should carry that note, because it feeds the design point "a free wrong guess is kinder".
- **T10. Royal Kingdom's Tetris-like mini-game is stronger than marked.** Liftoff's page text says it outright, so "screenshot caption only" can be dropped. It is still unnamed.
- **T11. Triple Tile.** "The most downloaded tile match" is over the article's last 52 weeks (article dated Oct 2, year inferred 2023), not "in 2023".
- **T12. Color Block Jam timer.** The store text says "before running out of time". That supports "timed", but "a time limit on each level FACT" overstates the store text; mark the per-level part REPORTED (reviews).

### calendar.md

- **C1. The weekday chain "Hidden Temple → Pinata Party" is uncertain (affects key findings 1–2 and the §2.1 table).** The Pinata Party wiki infobox, the source calendar.md cites [C1], puts Pinata Party on **Friday 08:00 → Monday 08:00 UTC**. Its text also says it opens after Hidden Temple, which runs Monday → Friday. NamuWiki gives a third order. The weekend chain (Magic Cauldron → Balloon Rise) is clean on the wiki. So "two chains a week, each of two games" is only half confirmed. The design idea (one game open per block, finishing it opens the next) still stands on the weekend chain.
- **C2. "One last chance to spend … tokens never carry over" is not universal.** Confirmed for Magic Cauldron (Royal Match help), Magic Carnival (Royal Kingdom help, which also says no carry-over) and Cannon Fest (Toon Blast help, carry-over not stated). But the Dragon Nest wiki page says "When Dragon Nest is finished, players will not have a chance to use their remaining Magic Balls." Say "most mini-game events" and note the exception.
- **C3. Rescue Mission retries.** The help page says you may restart "multiple times" and gives no limit; "unlimited" is an inference. It is also timed, which matters because our rules avoid timers.
- **C4. Google Play featuring is gated.** Promotional content is open to all games, but featuring, deep links and audience targeting need the Premium growth tools criteria. A new small game may get the card on its listing but no featuring. Also: submit no earlier than 60 days before the start; featuring requests are limited per quarter (4 broad and 2 specific for games).
- **C5. Train Journey "third Friday".** Rests only on the NamuWiki search excerpt. Old Cynic confirms 10 days, a roulette and a team of four, but no start day. Mark REPORTED (weak).
- **C6. RFC 9110.** The rule binds "an origin server with a clock". Wording only.
- **C7. Balloon Rise hard-level conflict.** Already reported in calendar.md. The official help page (extra cookies on Hard, Super Hard and Extremely Hard) should be preferred over the wiki.

## 4. The build note's statements about our code

Files read: `Stage/IStage.cs`, `Flow/GameSession.cs`, `Screens/Destination.cs`, `Screens/NavigationBar.cs`, `Screens/MuseumStage.cs`, `Hall/GiftFrame.cs`, `Flow/WalletLedger.cs`, `Flow/PlayerProgress.cs`, `Flow/ProgressDocument.cs`, `Flow/AttemptLedger.cs`, `Flow/WallClock.cs`, `Flow/SoundCue.cs`, `Layout/ScreenLayout.cs`, `GameRoot.cs`, `src/Puzzle.Meta/Rewards.cs`, `LifeMeter.cs`, `DayStamp.cs`, `src/Puzzle.Core/Puzzle.Core.csproj`, `Engine/Game.cs`, `Engine/RuleSet.cs`, `Levels/GoalSpec.cs`, `Randomness/Rng.cs`, `src/Puzzle.Bot/SkillLadder.cs`, `src/Puzzle.Cli/Commands/SeedsCommand.cs`, `src/Puzzle.Economy/Simulating/Decisions.cs`, `tools/unity/sync-engine.js`, `tests/Puzzle.Core.Tests/Puzzle.Core.Tests.csproj`, `JigsawScreenTests.cs`, `ProjectSettings.asset`, `docs/DEVLOG.md` line 175, `docs/GAME_DESIGN.md` line 1114, `docs/OVERVIEW.md` line 79, and the art folders. I also composed the eight gem sprites into one strip and looked at it.

**Confirmed as written:**
- `IStage` has a camera, `Fit(ScreenShape)`, `Advance`, pointer down / moved / up / cancelled, `GoBack`, `TakeWhatSounded`.
- `FollowThePlayer` is a `switch` on `_front` with `MuseumExit.ToPlay`, `MuseumExit.ToAJigsaw`, `ResultChoice.*` and `JigsawStage { PlayerLeft: true }`. `ChangeTheStage` carries the comment "never two cameras at once".
- `GameStage.cs` is 2,641 lines and `JigsawStage.cs` 1,065 lines. `PieceTray`, `ProgressPips` and `RestoredSparkles` exist.
- `Destination` is Shop, Home, Map, Collection. `Destinations.OnTheBar` says "The canvas' fifth place, the events' calendar, has nothing behind it". `NavigationBar` says the same.
- `MuseumPress`, `WalletRequest`, `DeviceRequest` and `MuseumExit` have private constructors. `IMuseumPage`, `IMovingPage`, `IShopPage` and `IMuseumCard` exist, and `BuildPage(Destination, …)` is a switch.
- `GiftFrame` hangs at the workbench's right with a red dot and a countdown pill.
- `Reward(Coins, IReadOnlyList<BoosterKind>)` has no lives. `LifeMeter` has `WithOneLost` and no way to give a life. `DailyReward.IsDueOn` compares `DayStamp`s.
- `IWallClock` has `SystemWallClock` and `FixedWallClock` and hands out whole seconds in the player's zone.
- `ProgressDocument.CurrentVersion = 5`. A later layout reads as `OfALaterGame`, and `GameRoot` then runs in memory without saving.
- `PlayerProgress.Equals` drives `HasUnsavedProgress`.
- `sync-engine.js` builds Puzzle.Bot (+Core), Museum and Meta as netstandard2.1, and its header says "The game offers a language whose tables are complete". 13 word tables exist.
- `Puzzle.Core.csproj`: netstandard2.1, LangVersion 9.0, "Do not raise LangVersion or add package dependencies".
- The test project compiles `ScreenLayout`, `PageLayout`, `JigsawLayout`, `ResultCardLayout`, `LevelStartLayout`, `PlayerProgress`, `ProgressDocument`, `JigsawTable`, `WalletLedger`, `AttemptLedger`, `Timeline`, `Tween`, `TapSlop` and `PageSwipe`.
- `JigsawScreenTests` uses 1080×2424, 2076×2152, 1080×2340, 1080×1920, 720×1280, 1200×1920 and 1600×2560, with and without insets.
- `Rng` is PCG32 with `Derive`. `GoalSpec` has CollectColor, BreakBoxes, ClearJelly, BreakChains and DropItems. `Game` is "the single entry point shared by the Unity client, the test bot and the level generator". `RuleSet` asks for re-calibration by the bot. `SkillLadder` rung "skill-n" notices n % of moves, with a temperature. `seeds` defaults to 64 games.
- `ProjectSettings.asset` allows all four autorotations and has `AndroidMinSdkVersion: 26`. `ScreenShape` (in `ScreenLayout.cs`) says "The game is played upright". `MuseumStage` refuses swipes from the side edges.
- DEVLOG line 175: 283 MB APK on 16 October, up from 61 MB, because of the 100 full-size paintings.
- `Resources/Paintings` is 88 MB (100 paintings, 101 files). `art/unity` has 115 SVGs.
- The gems are red ladybug, orange moon, yellow daisy, green square, cyan diamond, blue drop, purple triangle and pink tulip.
- The Gallery has tray, shelf, plank, workbench, frame-small, picture-frame, card, nameplate, plaque, medallion, pips, coins, discs, buttons, badges, pills, sparkle and icons, and **no calendar sprite**.
- `JigsawCanvas`, `PaintingGallery`, `PipRow`, `SmallPips`, `GoodsRow`/`GoodsItem`, `WallLamps`, `PaintedShape`, `MeshSketch` and `SeedPoolCalibrator` all exist. `economy-pack.js` exists.

**Corrections:**
- **B1. Four IStage implementations exist today, not three.** `Screens/EntryStage.cs` (the entry page from commit b57fd1e) is `IStage, ITouchable`. `GameRoot` drives it before `GameSession`, and `GameSession` switches among the other three. A `MiniGameStage` would be the fifth `IStage` and the fourth stage inside `GameSession`.
- **B2. `GameSession.Advance` has nine steps, not eight.** `DoWhatWasAskedOfThePhone()` runs between `DoWhatTheStageAskedFor()` and `KeepWhatWasPlaced()`. The plug-in points (keep what happened, wallet requests, follow the player) are unaffected.
- **B3. `CoinSource` is declared in `src/Puzzle.Economy/Simulating/Decisions.cs`** (Finale, LevelWon, PaintingCompleted, Daily, WelcomeGift, …), not in `PlayerSimulator.cs`, which uses it.
- **B4. Small counts and names.** `words/tr.json` holds 266 words (its `words` block), not about 262; the `WordId` enum has roughly 262 members by a rough count. `ScreenShots` is a static class with an internal `ShotPage` enum, and `JigsawShots` uses `JigsawMoment`. Neither changes a conclusion.

**A mismatch the build note found, made sharper:** `docs/GAME_DESIGN.md` line 1114 says 51 drawings came from the design canvas, **including calendar icons** ("takvim simgeleri"). No calendar sprite is in `unity/Assets/Art/Gallery`, `GalleryArt.cs`, `art/unity` or `out/design/icons`. Either the doc line is wrong or the icon was never imported. The owner should know before the fifth bar place is planned.

**Not checked:** effort sizes, sprite counts per kind and the 45–50 WordId estimate (all ESTIMATE by design). The algorithm sources (arXiv:2202.09495, octo/watersort, the Wikipedia Rush Hour and 15-puzzle pages) were not re-opened. The Rush Hour and 15-puzzle figures (51 / 93 moves; 31 and 80 moves) match what I know of those pages, but I did not re-check them today.

## 5. Sources opened for this check (all 2026-10-24)

- Naavik: https://naavik.co/digest/what-leading-match-3-and-merge-games-do-differently/ (2026-01-18); https://naavik.co/digest/why-minigames-mean-major-business/ (2025-08-17)
- Sensor Tower: https://sensortower.com/blog/2025-q4-unified-top-5-puzzle-games-units-us-64bc1644e1714cfff1e615fc (January 2026); https://sensortower.com/blog/what-royal-matchs-calendar-teaches-about-live-ops-in-2026 (May 2026)
- Gamigion: Puzzle Break https://www.gamigion.com/new-event-in-royal-match-puzzle-break/ ; Smash Fest https://www.gamigion.com/smash-fest-casual-liveops-turned-into-genre-wide-trend/ ; Expedition https://www.gamigion.com/royal-match-launched-its-first-expedition-event/
- FoxData: https://foxdata.com/en/blogs/royal-matchs-mini-games-the-secret-driving-its-global-success-in-2025/ (2025-09-30, updated 2026-01-19)
- Liftoff: https://liftoff.ai/2025-casual-gaming-apps-report/
- TheGamer: https://www.thegamer.com/monopoly-go-juggle-jam-tutorial-rewards-carnival/ (2025-01-21)
- Old Cynic: Dragon Nest https://oldcynic.com/royal-match-dragon-nest-magic-tokens-balls-guide-tips ; Train Journey https://oldcynic.com/royal-match-train-journey-guide
- Deconstructor of Fun: https://www.deconstructoroffun.com/blog/2023/9/30/tile-match-the-new-match-3-or-the-new-merge
- ASA ruling: https://www.asa.org.uk/rulings/plr-worldwide-sales-ltd-g20-1061644-plr-worldwide-sales-ltd.html (30 September 2020). Pocketgamer.biz tag summary (search result) also gives "0.03%"
- Royal Match wiki API (`https://royalmatch.fandom.com/api.php?action=parse&page=<title>&prop=wikitext`): Magic Cauldron, Balloon Rise, Pinata Party, Hidden Temple, King's Cup, Dragon Nest, Lives, Royal Pass. Homescapes wiki API: Events
- Royal Match Help Center: 528 Puzzle Break, 119 Balloon Rise, 278 Magic Cauldron, 539 Propeller Rush. Royal Kingdom Help Center: section 46, faq 558 Magic Carnival. Toon Blast Help Center: 555, 390, 610
- King help-centre API: articles 360002352858, 7353277804189, 7332093991069
- iTunes Search API (US): jigsolitaire, pixel flow, magic sort, screwdom, vita mahjong, block blast, zen match. App Store page: Color Block Jam
- Android: SystemClock, Settings.Global, notification permission. Apple: In-App Events, `ProcessInfo.systemUptime`. Google Play Console Help: Promotional content. RFC 9110 (text)
- Not reachable: NamuWiki (403), BBC (blocked), Reddit, Facebook
