# Mini-game types for short events: a catalogue (October 2026)

Research for the museum match-3 (Unity client, .NET engine, 20 rooms, 1000 levels, jigsaw pieces of public-domain paintings).
Written 2026-10-24. This is step one of the owner's request: **which kinds of small puzzle games could live inside the game as short events**, several in one week, on the phone's clock, paying gold, lives, boosters and more.

**What this file adds, and what it does not repeat**

- `docs/research/monetization-and-retention.md` §5 and §7 already propose Restorer's Bench (varnish cleaning, spot the forgery, detail hunt), Gala night, Damage boss, Night Watch, Archive Dig, Catalogue Match, Curator's Challenge, Painting of the Day, Who painted it?, Pigment Studio (K-numbers there).
- `docs/research/monetization-2026-10/minigames.md` already goes game by game (King's Nightmare, Homescapes and Fishdom mini-games, Toon Blast help-centre events, Dragon Nest, Hidden Temple, June's Journey; S-numbers there).
- This file goes **type by type**: the puzzle genres that sell in 2024–2026, their standalone hits, where the big match-3 and casual games embed them, how they reward them, and what each would need here. When a fact is already in minigames.md I point to it ("minigames.md S41") instead of restating it.

**Labels**

- **FACT**: seen on the source itself: an official store page, an official help page, or a page that shows the game's own rules.
- **REPORTED**: a secondary source (analyst, trade press, blog, fan guide, video). Their numbers are mostly Sensor Tower, AppMagic or data.ai estimates, not company data.
- **ESTIMATE**: my own inference. All "fit for us" rows, all session lengths and all build sizes are ESTIMATE.

**How to read a citation.** `[T5, 2026-01]` = source T5 in §8, followed by the source's own date. Every source was opened on 2026-10-24 unless the row says "search snippet". App Store facts (seller, release date, rating count, rules in the description) come from Apple's public iTunes Search API for the US store, read 2026-10-24 [T25]. **A rating count is not a download count**; it only shows the rough size of a game's audience.

---

## Kısa özet (TR)

- **2025'te bulmaca pazarını büyüten match-3 değildi.** Gelir artışının %60'ı birleştirme (çoğu Gossip Harbor), %13'ü sıralama (Magic Sort), %13'ü blok (Color Block Jam), %8'i vida (Screwdom) türünden geldi [T1, 2026-01] (REPORTED). Color Block Jam, Screwdom ve Match Factory 2025'te en çok kazanan 25 bulmaca arasına girdi [T1] (REPORTED).
- **ABD, 2025 son çeyrek, en çok indirilen bulmacalar:** Block Blast (blok yerleştirme), Jigsolitaire (fotoğraf kartlarıyla yapboz), Pixel Flow! (renkli atıcılarla piksel resmi temizleme), Magic Sort! (sıvı sıralama), Word Search Explorer [T5, 2026-01] (REPORTED).
- **Büyük match-3'ler bu hit türleri olduğu gibi içine koymuyor.** Gömdükleri: kazı, plinko (top düşürme), renk sırası bulmacası (Mastermind benzeri: Royal Match Magic Cauldron, Monopoly GO Juggle Jam, Gossip Harbor Clay Color Combo), yapboz (Royal Match Puzzle Break), hafıza eşleştirme, balon patlatma, fizik yıkma (Toon Blast Cannon Fest), birleştirme, Tetris benzeri blok (Royal Kingdom), mahjong (Whiteout Survival) ve pim çekme (Playrix) [T2, T3, T8, T9, T10] (REPORTED). Klasik tüp sıralama, otopark/otobüs sıkışması, nonogram, fark bulma ve çizgi bağlamanın büyük bir match-3'e gömüldüğünü bulamadım; bu bir fırsat da olabilir, risk de.
- **Kalıp hep aynı:** bölüm kazanınca etkinlik parası gelir, para ayrı bir mini tahtada harcanır, aşamalar sandık açar, sonda büyük ödül. Mini oyun ana döngünün yerine geçmez; onun ödül kuyusu olur (Puzzle Break, Dragon Nest, Cannon Fest, Juggle Jam).
- **Reklamdan oyuna geçenler:** pim çekme (Playrix, 2020 ASA kararı), King's Nightmare (Royal Match), Last War'ın Frontline Breakthrough'u. **Ters yön:** Toon Blast'in Cannon Fest etkinliği önce reklam oldu, sonra başka bir şirketin bağımsız hiti Smash Fest! [T10] (REPORTED).
- **Tek oyunculu bulmaca türlerinin hiçbiri sunucu istemez.** Sunucu isteyenler: takım ve ortak etkinlikler (Dragon Nest, Monopoly GO ortaklıkları), turnuvalar, sıralamalar.
- **Kurallarımızla çatışmalar:** popüler hitlerin çoğunda süre sınırı var (Color Block Jam, Triple Match 3D) → bizde süresiz ya da hamle sınırlı olur. Plinko ve kazıda sonuç rastgele; hakkı satılırsa "ücretli rastgele ürün" olur (§17) → hak yalnız oynayarak kazanılır. Kelime oyunları 13 dilde içerik ister.
- **İlk dalga için en uygun türler (ESTIMATE, §6):** dil istemeyen, süresiz, seviyesi bilgisayarla üretilip çözücüyle denetlenebilen ve tablolarımızı kullanan türler: pigment sıralama (tüp), sandık açma (vida), takas yapbozu (Jigsolitaire tipi) ve etkinlik yapbozu (Puzzle Break tipi), katalog karo eşleştirme, pigment formülü (Mastermind), yaldız çizgisi (Flow), resim mantığı (nonogram), ziyaretçi kuyruğu (bus jam), duvar asma (blok yerleştirme).

---

## 1. What sold in 2024–2026 (context)

| Signal | What it says | Label |
|---|---|---|
| Sub-genre growth, 2025 vs 2024 | Puzzle IAP revenue over $10B in 2025 (+14%). Growth came from Merge 60% (mostly Gossip Harbor), Sort 13% (Magic Sort), Block 13% (Color Block Jam), Screw 8% (Screwdom); new sub-genres (Match Pair, Sort, Block, Screw) drove most puzzle downloads that were not match-3, merge or blast | REPORTED [T1, 2026-01], Sensor Tower data |
| Top 25 grossing puzzle 2025 | Match Factory, Color Block Jam and Screwdom are in it, beside the match-3 and merge leaders | REPORTED [T1] |
| Highest-grossing hybrid-casual puzzles 2025 (IAP) | 1 Color Block Jam (Rollic), 2 All in Hole (Homa), 3 Screwdom (iKame / Zego), 4 Magic Sort! (Grand Games), 5 Goods Puzzle: Sort Challenge (1SOFT), 6 Hexa Sort (Lion Studios Plus), 7 Hole People (Rollic), 8 Blossom Sort (Gloryway), 9 Knit Out (Rollic), 10 Pixel Flow! (Loom Games); further down Bus Escape, Crowd Express, Car Match, Coin Sort, Block Jam (Voodoo), Screw Jam, Seat Away, Wool Craze, Knot Busters 3D, Hexa Away, Solitaire Associations Journey | REPORTED [T4, 2026-01-15], YouTube channel, data source not named |
| US top 5 puzzle downloads, Q4 2025 | Block Blast (Hungry Studio; weekly downloads about 565K rising past 1M by late December), Jigsolitaire (GAMINCAT; over 1M a week in late October), Pixel Flow! (Loom Games; 457K a week), Magic Sort! (Grand Games; 381K a week), Word Search Explorer (PlaySimple; peak 349K) | REPORTED [T5, 2026-01], Sensor Tower estimates in an AI-written blog post |
| US top 5 puzzle, Q1 2025 (revenue view) | June's Journey (Wooga; peak about $2.2M a week), Color Block Jam (Rollic; nearly $2M a week by end of March, downloads 21.4K → 512K a week), NYT Games, Words With Friends (Zynga), Wordscapes (PeopleFun) | REPORTED [T6, 2025-04], same caveat |
| Mini-game events in top casual games | Mini-game events rose in top-grossing casual titles; "thinking" mini-games with varied mechanics had the highest adoption; only digging and Wordle-like formats reached wide adoption | REPORTED [T3, ≈2025-04], GameRefinery data, Feb 2024–Feb 2025 |
| Mini-games in the biggest games | 10 of the top 16 grossing mobile games of 2025 (year to date when written) use mini-games | REPORTED [T2, 2025-08], Sensor Tower |
| Audience | Puzzle players: 84% seek relaxation, 81% are women (Mistplay survey) | REPORTED [T1] |
| Older context (2023) | Puzzle revenue by sub-genre: Swap 61%, Blast 10%, Chain 6%, Hidden Objects 4%, Merge 4%, Bubble Shooter 4%. Downloads: Swap 16%, Physics 14%, Riddle 10%, Word 9%, Trivia 6% | REPORTED [T26, 2023-09-08], Sensor Tower |

ESTIMATE: the 2025–2026 winners outside match-3 share four traits that also suit a 30+ casual audience and our rules: one-tap or one-drag input, a level solved in one to three minutes, a visible "tidy-up" goal (sort, clear, unpack, complete a picture), and strong colour coding. Many of them add a timer for tension; we would not (§5 of the report: no time pressure).

---

## 2. Catalogue by type

Each block: rule, session, why players like it, standalone hits, where it is embedded, how that is rewarded, server need, ad history, and a short fit note. Session lengths are ESTIMATE everywhere: no source measured them per type. The only reported figure is a Royal Match bubble-shooter mini-game of about one minute [T9, undated] (REPORTED).

### 2.1 Tube sort (ball sort, water / colour sort)

- **Rule.** Containers hold stacked colours; you may move only the top unit onto the same colour or into an empty container. The level ends when every container holds one colour. FACT [T25: Water Sort Puzzle, Ball Sort Puzzle, Magic Sort! descriptions]
- **Session.** 1–3 minutes per level. ESTIMATE
- **Why players like it.** Calm, no luck, visible order out of chaos; undo and "add a tube" helpers soften dead ends. ESTIMATE from the store descriptions [T25]
- **Hits.** Magic Sort! (Grand Games A.Ş.; 2024-05-28; 699,605 ratings) FACT [T25]; Sort growth example and US top 5 downloads Q4 2025 REPORTED [T1, T5]; named among the top new casual games of 2024 REPORTED [T3]. Water Sort Puzzle (IEC Global; 2020-06-26; 324,196 ratings), Water Sort Puzzle: Get Color (Tripledot; 2021), Ball Sort Puzzle (HM Games; 2020-01-13; 232,660 ratings), Ball Sort (Guru; 2022) FACT [T25].
- **Embedded.** No match-3 or casual leader found that embeds classic tube sorting. The nearest relative that is embedded is the colour-order code (§2.5). ESTIMATE / gap
- **Server.** None.
- **Fit for us (ESTIMATE).** "Pigment jars": sort the palette of the painting just finished into jars. Language-free, untimed by nature, levels can be generated backwards from a solved state and checked by a solver in the engine. Build **S–M**.

### 2.2 Goods / shelf sort and stack sort

- **Rule.** Goods sort: items sit on shelves; move or swap them so three identical items share one shelf, which clears it. FACT [T27: Goods Puzzle Google Play listing]. Stack sort (Hexa Sort, Coin Sort): place stacks of coloured hexagons or coins next to each other; same colours slide together and a full stack clears. FACT [T25: Hexa Sort]
- **Session.** 1–3 minutes. ESTIMATE
- **Why players like it.** The "tidy a supermarket shelf" fantasy and satisfying clears; easy to read at a glance. ESTIMATE
- **Hits.** Goods Puzzle: Sort Challenge (1SOFT; #5 hybrid-casual IAP 2025) REPORTED [T4]; Hexa Sort (Lion Studios Plus; 2023-11-01; 218,639 ratings) FACT [T25], #6 and listed under Tripledot REPORTED [T4]; Blossom Sort (Gloryway) and Coin Sort (Lion Studios Plus) REPORTED [T4]; Goods Puzzle and Blossom Sort among the top new casual games of 2024 REPORTED [T3].
- **Embedded.** Not found inside a match-3. Hexa Sort itself runs a Wordle-like mini-game as an event REPORTED [T3].
- **Server.** None.
- **Fit for us (ESTIMATE).** "Storeroom shelves": sort museum objects (vases, busts, frames, brushes) three of a kind. Needs a set of object icons (objects only, no people). Build **M** (art).

### 2.3 Screw / nuts-and-bolts sort (and yarn sort)

- **Rule.** A 2D or 3D object is held together by coloured screws; tap a screw to move it into a box or hole of its colour; planks fall when free. Planning matters because a wrong order fills the free slots. FACT [T25: Screwdom, Wood Nuts & Bolts]
- **Session.** 1–3 minutes. ESTIMATE
- **Why players like it.** Tactile "unscrew" feedback, a visible object coming apart, and the same colour-slot planning as tube sort. ESTIMATE
- **Hits.** Screwdom (Zego Global; 2025-01-02; 116,628 ratings) FACT [T25]; screw sub-genre 8% of 2025 puzzle growth and Screwdom in the top 25 grossing REPORTED [T1]. Wood Nuts & Bolts Puzzle (ABI Global; 2023-11-22; 234,358 ratings) FACT [T25]; in South Korea's top 5 downloads of 2024 REPORTED [T28]. Screw Jam (Rollic), Screw Sort Puzzle (BitEpoch) REPORTED [T4]. Yarn variants (Knit Out by Rollic, Wool Craze) use the same slot logic with wool REPORTED [T4].
- **Embedded.** Not found inside a match-3. Screwdom borrows casual event formats (piggy bank, win streaks) REPORTED [T10].
- **Server.** None.
- **Fit for us (ESTIMATE).** "Open the loan crate": unscrew a shipping crate to free a painting arriving on loan; the last plank reveals the painting. Strong theme fit, language-free, generatable. Build **M** (2D crate art and a fall animation).

### 2.4 Colour-order code (Mastermind-like), the sort relative leaders actually embed

- **Rule.** A hidden order of 3–7 coloured items; you submit a guess and the game marks which positions are right, then you try again. FACT for Monopoly GO's version [T15, 2025-01-21]
- **Session.** 1–2 minutes per code. ESTIMATE
- **Why players like it.** A short logic puzzle with a clear "aha"; feedback makes every guess useful. ESTIMATE
- **Embedded.**
  - Royal Match **Magic Cauldron** (since September 2023): rearrange coloured items into the correct sequence; each attempt costs event currency earned in levels. REPORTED [T2, 2025-08; T13, 2025-09]
  - Monopoly GO **Juggle Jam** (November 2024): 3–7 balls; one token per ball per guess; correct answers pay tickets for a prize shelf (dice, sticker packs, cash); tokens come from tournaments, daily quick wins and the store every 8 hours. REPORTED [T15]. Its currency can be bought REPORTED [T2].
  - Gossip Harbor **Clay Color Combo** REPORTED [T2]; "Wordle-like" mini-games in Gossip Harbor and Hexa Sort REPORTED [T3].
- **Server.** None.
- **Fit for us (ESTIMATE).** "Pigment formula": guess the order in which the master laid down 3–5 pigments. Language-free, tiny build (**S**). Unlike plinko it is deterministic logic, so it does not touch the §17 random-item rule.

### 2.5 Tile match / triple tile, and mahjong pairs

- **Rule.** Tiles lie in layers; tap free tiles into a tray (usually 7 slots); three of a kind clear; a full tray loses. Mahjong solitaire uses pairs instead of a tray. REPORTED [T23, 2023-10] for the tray rule; FACT for pairs [T25: Vita Mahjong]
- **Session.** 2–4 minutes. ESTIMATE
- **Why players like it.** Layout is deterministic, so the player plans; less luck than match-3 cascades REPORTED [T23]. Large-tile mahjong is sold to older players FACT [T25: Vita Mahjong "for seniors", "large, clear tiles"].
- **Hits.** Triple Tile (Tripledot; 2021-08-20; 271,976 ratings) FACT [T25], the most downloaded tile match in 2023 and ad-funded REPORTED [T23]. Zen Match (now sold by Moon Active; 2021-07-07; 255,395 ratings) FACT [T25]. Tile Busters (Spyke Games; 2022-09-22; tournaments and time challenges) FACT [T25]. Vita Mahjong (Vita Studio; 2023-12-01; 1,787,722 ratings) FACT [T25]; it reportedly held #1 on the US free iOS games chart for seven straight days in 2025 REPORTED, search snippet [T29]. Tile match earned about $60M IAP in the year before October 2023 (data.ai) REPORTED [T23].
- **Embedded.** Whiteout Survival runs **Marvelous Fantasia**, a recurring mahjong-style tile matching mini-game; Sensor Tower saw a revenue lift when it ran in mid-July 2025 REPORTED [T2]. Royal Kingdom's Spooky Carnival is a pairs game (minigames.md S14).
- **Server.** None (Tile Busters' tournaments would need one).
- **Fit for us (ESTIMATE).** "Catalogue cards": tiles show details of paintings already restored; triples go to the archive tray. Or a large-tile pairs version for the 30+ audience. Overlaps with the report's Catalogue Match (memory pairs); this is a different, deterministic rule. Generator must guarantee a solvable deal. Build **M**.

### 2.6 3D match ("Match Factory" type)

- **Rule.** A physics pile of 3D objects; pick identical items into a tray of slots; three of a kind clear; usually against a timer. FACT [T25: Triple Match 3D, Match Triple 3D descriptions]; physics-pile distinction REPORTED [T23]
- **Session.** 1–3 minutes, timed. ESTIMATE
- **Why players like it.** Hunting in a messy pile; quick, tactile. ESTIMATE
- **Hits.** Match Factory! (Peak Games / Zynga) FACT [T25 search result], in the 2025 top 25 grossing puzzle REPORTED [T1]; Triple Match 3D (Boombox Games; 2022-04-13; 358,166 ratings) FACT [T25].
- **Embedded.** Not found inside a match-3. Match Factory itself runs physics mini-game events REPORTED [T3].
- **Server.** None.
- **Fit for us (ESTIMATE).** Weak: needs many 3D models and a physics pile, and the genre leans on a timer. Build **L**. Low priority.

### 2.7 Jam games (bus jam, car parking jam, block jam, seat / crowd)

- **Rule.** Parking jam: cars leave a crowded lot only in their facing direction; find the order. Bus jam: tap buses or lines of passengers so each colour boards the matching bus. Block jam (Color Block Jam): slide coloured blocks through doors of the same colour. FACT [T25: Parking Jam 3D, Bus Jam, Bus Escape; T30: Color Block Jam]
- **Session.** 1–3 minutes. Color Block Jam has a time limit on each level FACT [T30].
- **Why players like it.** Gridlock to empty board in a few taps; colour plus spatial planning. ESTIMATE
- **Hits.** Color Block Jam (Rollic Games) FACT [T30]; #1 hybrid-casual IAP 2025 REPORTED [T4]; Block 13% of 2025 puzzle growth REPORTED [T1]. Parking Jam 3D (Popcore; 2020-02-08; 478,734 ratings) FACT [T25], about 62M downloads in 2022 REPORTED [T31]. Bus Jam (Rollic; 2023-06-26), Bus Escape: Traffic Jam (ABI Global; 2024-09-21) FACT [T25]. Seat Away, Crowd Express, Hole People, Gecko Out, Car Match REPORTED [T4]; Color Block Jam, Screw Jam and Seat Away among the top new casual games of 2024 REPORTED [T3].
- **Embedded.** Not found inside a match-3.
- **Server.** None.
- **Fit for us (ESTIMATE).** "Visitor queue": colour-coded visitor groups to the right gallery door, or seat a lecture audience (Seat Away type) drawn as simple tokens, not people. Or slide coloured frames to matching doors (block jam). Untimed here. Build **M**.

### 2.8 Pixel Flow type (conveyor colour shooter)

- **Rule.** Shooters ride a conveyor and fire at pixel cubes of their own colour; each shooter's number is its ammo; spare shooters wait in five slots; the belt has a capacity limit, so order and timing matter. FACT [T25: Pixel Flow!]
- **Session.** 1–3 minutes. ESTIMATE
- **Why players like it.** A picture dissolves colour by colour; one-tap input. ESTIMATE
- **Hits.** Pixel Flow! (Loom Games Oyun Yazılım ve Pazarlama A.Ş.; 2025-08-17; 170,553 ratings) FACT [T25]; US top 5 downloads Q4 2025, revenue figure rising from about $11K to over $2M by December (unit not stated on the page) REPORTED [T5]; #10 hybrid-casual IAP 2025 REPORTED [T4].
- **Embedded.** Not found.
- **Fit for us (ESTIMATE).** "Cleaning machine": clear a pixelated grime layer off a painting colour by colour, revealing the real painting underneath. Very strong theme fit and the newest chart hit, but the belt timing makes difficulty harder to measure with bots. Build **M**.

### 2.9 Pin pull / rescue

- **Rule.** Pins hold back balls, water, lava or hazards; pull them in the right order so gravity sends the good things to the goal and the bad things away. FACT [T25: Pull the Pin]
- **Session.** 15–60 seconds. ESTIMATE
- **Why players like it.** One-move "aha" and a small story (save someone). ESTIMATE
- **Hits.** Pull the Pin (Popcore; 2020-01-24; 250,164 ratings) FACT [T25]. 2022 downloads: Pull the Pin about 69M, Save the Doge 66.8M, Thief Puzzle 71M, DOP 4 95M REPORTED [T31] (Sensor Tower). "Physics" was 14% of puzzle downloads in 2023 REPORTED [T26].
- **Embedded.** Homescapes, Gardenscapes, Fishdom (pins and propellers; wins pay diamonds and coins; a wrong move loses, replay allowed) REPORTED [T21, 2022-11-29]; details and cadence in minigames.md S15, S23, S29. Township and other Playrix titles use pin-pull and other mini-games heavily in ads REPORTED [T2]. Hero Wars: a fan guide says the advertised pin puzzles now exist as side content, unlocked through campaign chapters, paying gear and hero shards REPORTED, search snippet [T22].
- **Server.** None.
- **Ad history.** The clearest case of an ad format moved into the game (§3).
- **Fit for us (ESTIMATE).** "Roof leak": pull pins so the rain goes to the bucket and not onto the painting. Already partly covered by the report's "Save the painting" bench item and Night Watch; a 2D physics toy with hand-made levels. Build **M**.

### 2.10 Hidden object

- **Rule.** Find listed objects in a busy illustrated scene; faster finds score more, wrong taps cost. FACT for June's Journey (minigames.md S52, S53)
- **Session.** 1–3 minutes per scene. ESTIMATE
- **Why players like it.** Slow looking, story, beautiful art; strongly 30+ and female. ESTIMATE
- **Hits.** June's Journey (wooga) FACT [T25], a Playtika title REPORTED [T34]; peak about $2.2M a week in the US in Q1 2025 REPORTED [T6]. Hidden Objects: Puzzle Games (Azur Interactive) FACT [T25]. Hidden Objects were 4% of puzzle revenue in 2023 REPORTED [T26].
- **Embedded.** Gardenscapes began as hidden object and pivoted to match-3 (minigames.md S28); Matchington Mansion's store text names hidden objects as part of its meta REPORTED (search summary of the US App Store page, not opened) [T32].
- **Server.** None.
- **Fit for us (ESTIMATE).** Already proposed as Detail hunt. Paintings are ideal scenes, but every painting needs hand-placed coordinates. Build **S–M** per painting.

### 2.11 Spot the difference

- **Rule.** Two nearly identical pictures; tap each difference (often five). FACT [T25: Differences (Easybrain), Find the Difference 1000+]
- **Session.** 1–2 minutes. ESTIMATE
- **Why players like it.** Relaxed attention; no failure needed (several hits have no timer and unlimited hints FACT [T25: Find the Difference 1000+]).
- **Hits.** Differences – Find & Spot Them (Easybrain; 2019-03-16; 600,711 ratings; daily challenges and seasonal events) FACT [T25]; Differences – Find & Spot It (Guru; 2024-09-13; 346,750 ratings) FACT [T25].
- **Embedded.** Not found inside a match-3 in this search.
- **Server.** None.
- **Fit for us (ESTIMATE).** Already proposed as Spot the forgery. Altered copies of paintings need each museum's licence check (minigames.md §5 note). Build **S–M**.

### 2.12 Merge

- **Rule.** Two identical items merge into the next tier on a board; generators spawn items; orders ask for specific tiers. FACT [T25: Gossip Harbor, Merge Mansion, Travel Town descriptions]
- **Session.** Many short sessions over days (energy-gated). ESTIMATE
- **Why players like it.** Constant small progress plus story. ESTIMATE
- **Hits.** Gossip Harbor (Microfun; over $650M in 2025, beating Gardenscapes) REPORTED [T1]; Merge Mansion (Metacore), Travel Town (Moon Active) FACT [T25].
- **Embedded.** Royal Match **Merge Smith** as a secondary progression system REPORTED [T7, 2026-05]; Gardenscapes Secret Ingredient, Homescapes Austin's Childhood, Fishdom merge event, Toon Blast Temple Guardians (minigames.md S24, S30, S43); Merge Mansion runs a physics "drop & merge" mini-game REPORTED [T3].
- **Server.** None for solo; Temple Guardians is with partners.
- **Fit for us (ESTIMATE).** Already proposed as Pigment Studio. Biggest build of all (**L**) and its own economy. Later, not first wave.

### 2.13 Nonogram / picture logic

- **Rule.** Numbers beside each row and column tell how many filled cells run in sequence; logic alone fills the grid and reveals a picture. FACT [T25: Nonogram.com]
- **Session.** 5×5 under a minute, 10×10 about 3–8 minutes. ESTIMATE
- **Why players like it.** Pure logic, no luck, a picture as the prize. ESTIMATE
- **Hits.** Nonogram.com (Easybrain; 2019-03-08; 196,040 ratings; time-limited seasonal events with collectible postcards, daily challenges that earn a monthly trophy, tournaments) FACT [T25].
- **Embedded.** Not found inside a match-3.
- **Server.** None (tournaments aside).
- **Fit for us (ESTIMATE).** "Under-drawing": the solved grid shows a pixel sketch of a painting detail or a signature. Language-free; the generator must check that each puzzle has exactly one solution. Build **S–M**.

### 2.14 Jigsaw variants (closest to our core meta)

- **Rules seen.**
  - Classic jigsaw with a large library, a free daily puzzle, seasonal events that award postcards, and "mystery" puzzles that reveal hidden pictures; Art is a category. FACT [T25: Jigsaw Puzzles (Easybrain; 2018-06-26; 1,112,065 ratings), Jigsaw Puzzles for Adults HD (Veraxen), Jigsawscapes (Oakever)]
  - **Swap jigsaw**: a photo is cut into cards; you swap and slide cards until the picture is whole; pieces snap and chain. FACT [T25: Jigsolitaire (GAMINCAT; 2025-03-07; 75,224 ratings)]; US top 5 downloads Q4 2025, over 1M a week in late October REPORTED [T5].
- **Session.** 9–25 pieces about 1–4 minutes. ESTIMATE
- **Embedded.**
  - Royal Match **Puzzle Break**: 3 days to finish 5 jigsaws growing from 9 to 25 pieces; placing a piece costs 1 point even when wrong; points come from beating levels (1 free point at the start, bonuses from other events); each picture opens a chest (boosters, coins, card packs, tokens for other events) and all five open a grand prize; about 53 levels to finish the event. REPORTED [T8, 2025-06]
  - Royal Match also runs a jigsaw mini-game event (named among bubble shooter, Monopoly-style board and memory events) REPORTED [T9].
  - June's Journey character jigsaws and Toon Blast's "Puzzle Pieces" board element (minigames.md S55, S46).
- **Server.** None.
- **Fit for us (ESTIMATE).** The best theme fit of all: our whole meta is jigsaw pieces of paintings. The report has a 12-piece daily "Painting of the Day". New here: (a) a **Puzzle Break–style event week**: level wins pay placement points for a set of side paintings; (b) a **swap jigsaw** (Jigsolitaire type), which is also a swap mechanic like our match-3. Watch: wrong placements costing points would feel punishing to our audience; a free wrong guess is kinder. Build **S–M**; image licences as for any new painting.

### 2.15 Word and trivia

- **Rule.** Word: link letters into words that fill a crossword or a search grid. FACT [T25: Wordscapes]. Trivia: multiple-choice questions by category, solo or against friends. FACT [T25: Trivia Crack]
- **Session.** Word level 1–3 minutes; a trivia question 10–20 seconds. ESTIMATE
- **Hits.** Wordscapes (PeopleFun; 2017; 1,139,946 ratings) FACT [T25] and US top 5 Q1 2025 REPORTED [T6]; Word Search Explorer (PlaySimple) REPORTED [T5]; NYT Games and Words With Friends (Zynga) REPORTED [T6]; NYT added Pips, a domino logic puzzle, on 2025-08-18 REPORTED, search snippet [T33]; Trivia Crack (Etermax; 2013; 748,898 ratings) FACT [T25]. Word 9% and Trivia 6% of puzzle downloads in 2023 REPORTED [T26].
- **Embedded.** "Wordle-like" mini-games in Gossip Harbor and Hexa Sort REPORTED [T3]; trivia among PUBG Mobile's mini-games REPORTED [T2].
- **Server.** None for solo; Trivia Crack duels need one.
- **Fit for us (ESTIMATE).** Words need content in 13 languages and each language has its own letter and word lists; the cost is in content, not code. Trivia is already proposed (Who painted it?). A Connections-like "group these 16 paintings into 4 sets (painter, century, place)" uses only catalogue data the game already holds. Build **S** code, **M** translation.

### 2.16 Connect / flow lines

- **Rule.** Draw a path between each pair of same-colour dots; paths may not cross and must fill the whole board. FACT [T25: Flow Free]. Two Dots: connect same-colour dots in lines or squares to clear them. FACT [T25: Two Dots]
- **Session.** 30 seconds to 2 minutes. ESTIMATE
- **Hits.** Flow Free (Big Duck Games; 2012-06-07; 407,217 ratings; daily puzzles) and Flow Free: Shapes (2024-12-15) FACT [T25]; Two Dots (Playdots; 2014) FACT [T25]. Chain games were 6% of puzzle revenue in 2023 REPORTED [T26].
- **Embedded.** Fishdom's "rotate pipes or propellers to route water" mini-games are the nearest (minigames.md S29).
- **Server.** None.
- **Fit for us (ESTIMATE).** "Gilding the frame": join the gold-leaf ends of each colour around a frame without crossing. Language-free, generatable, no timer needed. Build **S**.

### 2.17 Block placement (Block Blast type)

- **Rule.** Drag given block shapes onto an 8×8 grid; full rows or columns clear; chains score more. FACT [T25: Block Blast!]
- **Session.** An endless run lasts several minutes; a level-based variant 1–3 minutes. ESTIMATE
- **Hits.** Block Blast! (Hungry Studio; 2022-04-03; 2,778,560 ratings) FACT [T25]; in the US top 5 puzzle downloads of Q4 2025, weekly downloads rising past 1M by late December REPORTED [T5]; top publisher by ad revenue in Jan–May 2026 REPORTED [T34] (market-report site, weak).
- **Embedded.** Royal Kingdom runs a Tetris-like block puzzle mini-game REPORTED [T3] (screenshot caption only).
- **Server.** None.
- **Fit for us (ESTIMATE).** "Hanging day": fit frame shapes onto a gallery wall grid; a filled wall row is "hung". The owner's next game is a block puzzle built in a separate session; ideas may transfer, code probably not (different project). Build **S–M**.

### 2.18 Other event mini-games the leaders run (not classic puzzles)

| Type | Rule in short | Who runs it | Reward pattern | Label |
|---|---|---|---|---|
| Plinko / peg drop | Spend balls or chips; they bounce into slots with multipliers | Royal Match Dragon Nest (balls 1 / 3 / 5 per Normal / Hard / Super Hard level, x2 bonus; slots x1–x10; level 75+, 10 days; prizes include a frame, a card pack, 5,000 coins and boosters) [T14, 2025-09]; Monopoly GO Prize Drop / Peg-E (chips "can be bought or earned") [T12, 2025-02; T2]; Whiteout Survival Jolly Marble [T2] | Milestone rewards toward a shared or solo goal | REPORTED |
| Digging | Spend shovels or pickaxes on a grid; find treasure; clear stages | Royal Match Hidden Temple, Toon Blast Space Miner (minigames.md S7, S42); Monopoly GO Treasure Hunt (20 fields) [T12]; Angry Birds Dream Blast [T3] | Stage chests, final prize | REPORTED |
| Physics smash | Aim a cannon and knock down stacks | Toon Blast Cannon Fest (cannonballs from won levels; stage rewards; minigames.md S41), seen from late 2024 [T10, 2026-08] | Stage rewards | REPORTED |
| Drop & merge (Suika-like) | Drop balls that merge on contact | Merge Mansion, Match Factory [T3]; Toon Blast Temple Guardians with partners (minigames.md S43) | Points to milestones | REPORTED |
| Bubble shooter | Shoot coloured bubbles to pop groups | Royal Match, about 1 minute a round [T9] | Not stated | REPORTED |
| Memory pairs | Flip cards to find pairs | Royal Kingdom Spooky Carnival (minigames.md S14); Royal Match memory event [T9] | Boosters, frame | FACT / REPORTED |
| Board / dice | Roll or spin to move on a board | Royal Match Monopoly-style board [T9]; Royal Match Train Journey: 10 days, trains from won levels are spun on a roulette to move along a shared rail track with three friends [T35, search snippet] | Board-tile rewards, grand prize | REPORTED |
| Expedition | Spend energy to clear fog tiles on a story map | Royal Match King's Tales, July 2026, 10 days; energy about 1 per normal level, sold directly for the first time ($2.99–$9.99 packs, a $0.99 daily offer, a $4.99 / $9.99 pass); revenue +10.1% in the event window (Sensor Tower) [T11, 2026-09]; Playrix games, Zen Match and Mystery Town used similar formats earlier [T11] | Story steps, pass rewards | REPORTED |

**Rule check (ESTIMATE).** Plinko and digging hand out random outcomes. When their turns can be bought (Monopoly GO chips; Royal Match's expedition energy since July 2026) a player pays for a chance at a random reward, which §17 forbids. If we use them, turns come only from play, and a dig board's contents can be shown or fixed in advance. The deterministic puzzle types in §2.1–2.17 avoid the question entirely.

---

## 3. Ad creatives that became game features (and the reverse)

| Case | What happened | Label |
|---|---|---|
| Pin pull, Playrix | The UK ASA banned two Facebook ads (March and April 2020) for Homescapes and Gardenscapes that showed pin puzzles. Playrix said Homescapes then had 10 such mini-games, about one per 20 levels, in a game of 4,160 levels; about 0.03% of players reached the levels shown [T16, 2020-10-12; T17, 2020-10]. The game was later changed to put mini-games closer to the start [T18]. Today Homescapes gives them every 5–10 levels up to 100 (minigames.md S15). One ad-analytics blog says Playrix takes gameplay from its best ad creatives and adds it to the games as mini-games [T20, ≈2021] | REPORTED |
| King's Nightmare, Royal Match | Ads have shown the king in danger since the 2021 launch; saving the king exists only in rare "King's Nightmare" levels [T19, 2024-06-14]; it was launched in February 2022 to turn ad puzzles into real goals (minigames.md S2); a blogger finds the Nightmare levels "very similar" to the ads and sees one every 100 levels from level 50 [T24, updated 2025-07-20] | REPORTED |
| Frontline Breakthrough, Last War: Survival | A maths-gate shooter shown in viral user-acquisition videos; most players reportedly installed for it; later added to the game as an occasional event [T2, 2025-08]. Earlier an X community note said the ad did not match the game [T19] | REPORTED |
| Hero Wars pin puzzles | A fan guide says the advertised pin puzzles now exist as side content [T22] | REPORTED, search snippet |
| Reverse: Cannon Fest → ads → Smash Fest! | Toon Blast's Cannon Fest event (seen late 2024) became Toon Blast ad creatives, often playables; Flow Games then released Smash Fest! as a standalone game (early June 2026): over 15M installs and $4M IAP per AppMagic (the article also says 14M), top 10 grossing hybrid-casual in July; pulled from Google Play by end of July for reasons not confirmed. Clones followed from Moon Active, Azur Games, Supercent, Tripledot [T10, 2026-08] | REPORTED |

ESTIMATE: two lessons. (1) A mechanic that wins as an ad can be honest only if players meet it early; the ASA case turned on the 0.03%. (2) Event mini-games are now a source of new standalone genres, so a good mini-game is also a marketing asset; our store videos may show any mini-game only if it appears early and often.

---

## 4. How the leaders reward mini-games

| Pattern | Example | Label |
|---|---|---|
| Fixed small prize for an inline mini-game | King's Nightmare 50 coins; Homescapes 50 coins + 1 star (minigames.md S1, S15); Fishdom diamonds and coins [T21] | FACT / REPORTED |
| Event currency from level wins → spent in a mini-board → stage chests → grand prize | Puzzle Break (points per win, 5 chests, grand prize) [T8]; Dragon Nest (1 / 3 / 5 balls by level difficulty) [T14]; Cannon Fest (cannonballs per win, minigames.md S41) | REPORTED / FACT |
| Harder levels pay more event currency | Dragon Nest 3 balls on Hard, 5 on Super Hard [T14]; Hidden Temple pickaxes (minigames.md S7) | REPORTED / FACT |
| Tickets into a prize shelf | Juggle Jam tickets buy dice, sticker packs, cash; the shelf can be refreshed for rising ticket costs [T15] | REPORTED |
| Collection rewards from a standalone puzzle app | Easybrain: seasonal postcards, a monthly trophy for daily challenges [T25] | FACT |
| Paid turns | Monopoly GO chips [T12]; Juggle Jam currency [T2]; Royal Match expedition energy since July 2026 [T11] | REPORTED |

ESTIMATE for us: rewards should be coins, boosters, frames, album stamps and side-painting pieces; lives are worth little because they rarely run out in a 10-minute session (§25). Every new source of coins or boosters goes through the economy simulator before it ships (report §5 rules).

---

## 5. Matrix: all types at a glance

Session, build size and every "fit" column are ESTIMATE. "Embedded" = found inside a match-3 or big casual game in this research.

| Type | Session | Embedded? | Needs others / server | Timer in the hits | Language-free | Uses our paintings | Levels can be generated + solver-checked | Build |
|---|---|---|---|---|---|---|---|---|
| Tube sort | 1–3 min | No (relative §2.4 is) | No | No | Yes | Palette colours | Yes, easily | S–M |
| Goods / stack sort | 1–3 min | No | No | Some | Yes | Objects, not paintings | Yes | M |
| Screw / crate | 1–3 min | No | No | No | Yes | Reveals a painting | Yes | M |
| Colour-order code | 1–2 min | **Yes** (Royal Match, Monopoly GO, Gossip Harbor) | No | No | Yes | Pigments | Trivial | S |
| Tile match / mahjong | 2–4 min | **Yes** (Whiteout mahjong) | No | Some (Tile Busters) | Yes | Detail tiles | Yes, solvable deals | M |
| 3D match | 1–3 min | No | No | Yes | Yes | No | Partly | L |
| Jam (bus, parking, block) | 1–3 min | No | No | Yes (Color Block Jam) | Yes | Doors, frames | Yes, needs solver | M |
| Pixel Flow type | 1–3 min | No | No | No, but belt timing | Yes | **Reveals a painting** | Yes, harder to rate | M |
| Pin pull | 15–60 s | **Yes** (Playrix, Hero Wars) | No | No | Yes | Protect a painting | Hand-made | M |
| Hidden object | 1–3 min | Partly (origins, meta) | No | Score timer | Item names need text, icons do not | **Paintings are the scene** | Hand-placed | S–M each |
| Spot the difference | 1–2 min | No | No | Usually no | Yes | **Painting vs copy** | Hand-made, licence check | S–M |
| Merge | Days | **Yes** (many) | No (solo) | No | Yes | Frames, tools | Design, not generation | L |
| Nonogram | 1–8 min | No | No | No | Yes | Pixel detail | Yes, uniqueness check | S–M |
| Jigsaw variants | 1–4 min | **Yes** (Royal Match Puzzle Break) | No | No | Yes | **Our core meta** | Trivial | S–M |
| Word / trivia | 1–3 min | **Yes** (Wordle-like) | Duels only | Some | **No** (13 languages) | Catalogue data | Word lists per language | S code, M text |
| Connect / flow | 0.5–2 min | Near (Fishdom pipes) | No | Optional mode | Yes | Frame gilding | Yes | S |
| Block placement | 1–3 min (levels) | **Yes** (Royal Kingdom) | No | No | Yes | Frames on a wall | Yes | S–M |
| Plinko / dig / smash / bubble / memory / board | Seconds to 2 min | **Yes** (many) | Team versions only | Some | Yes | Weak to medium | Random outcomes (§17 care) | S–M |

---

## 6. First reading for the museum (ESTIMATE)

This is a reading of the catalogue, not a plan; the plan is a later step.

- **Strongest fit, first wave candidates:** jigsaw variants (Puzzle Break–style week and swap jigsaw), colour-order code ("pigment formula"), connect / flow ("gilding the frame"), tube sort ("pigment jars"), screw sort ("open the loan crate"), nonogram ("under-drawing"). All are language-free, untimed, deterministic, offline, and their levels can be generated and checked by a solver in the .NET engine, the same way the bots already check match-3 levels. That last point is what lets the range grow cheaply: one mini-game contract (seed in, board, win check, reward out) and a generator per type.
- **Second wave:** tile match / large-tile mahjong ("catalogue cards"), jam ("visitor queue" or frames to doors), Pixel Flow type ("cleaning machine"), block placement ("hanging day"), pin pull ("roof leak").
- **Already proposed in the report, keep there:** spot the forgery, detail hunt, archive dig, catalogue match (memory), pigment studio (merge), who painted it?
- **Low priority:** 3D match (art and timer), merge as a mini-game (cost), word games (13-language content).
- **How it would run:** event currency from level wins (more on Hard and Super Hard), spent in the week's mini-game, stage chests, a final prize; several types in one week; calendar inside the app on the phone's clock; never costs a life; always skippable; no timer; turns never sold for random outcomes.

---

## 7. Gaps and limits

- **The Firecrawl account ran out of credits mid-research.** The rest of the work used WebSearch and WebFetch. The owner may want to top up Firecrawl before the next research run: https://www.firecrawl.dev/billing/s/uB6fs98a0HT_s09izJC5xx_4bwX-fHpNumaXpV2XzJk
- **Session lengths are estimates.** No source measured play time per mini-game type. GameAnalytics cross-genre benchmarks (median session about 5–6 minutes in 2025) were seen only as a search snippet and are not per type.
- **Not found embedded in a match-3:** classic tube sort, goods sort, screw sort, 3D match, jam games, Pixel Flow type, nonogram, spot the difference, flow lines. Absence in searches is not proof of absence.
- **Candy Crush Saga:** no embedded mini-game found for 2025–2026 (only tournaments such as All Stars). **Lily's Garden:** nothing found. **Matchington Mansion:** hidden objects only as part of the meta (store text via a search summary).
- **Weak or unverified items:** Royal Kingdom "Cube Blast" (named in a LinkedIn post snippet, not opened; left out of the catalogue); Royal Kingdom's Tetris-like mini-game rests on one image caption [T3]; names of Royal Match's bubble-shooter, board and memory events are not given [T9]; Hero Wars pin puzzles rest on a fan-guide snippet [T22]; Vita Mahjong's #1 week rests on a search summary [T29]; Last War's ad-to-event story rests on one article [T2].
- **Conflict:** Dragon Nest team size: "invite four other players" [T14] against "one matched partner" (minigames.md S9). Not resolved.
- **Dates:** several Gamigion pages show day and month without a year; years were inferred from image paths and marked "inferred" in §8. Sensor Tower's quarterly US posts are written by an AI assistant over Sensor Tower estimates.
- **No retention or revenue effect per mini-game type** was found, except the event-window figures quoted (Whiteout mahjong lift, King's Tales +10.1%), which are analyst estimates with no control group.
- **US chart data** are Sensor Tower estimates via blog posts; the paid Sensor Tower and AppMagic dashboards were not used.

---

## 8. Sources

All opened 2026-10-24 unless marked "search snippet". Earlier sources are reused by ID from `docs/research/monetization-2026-10/minigames.md` (S-numbers) and `docs/research/monetization-and-retention.md` (K-numbers).

| ID | Source | URL | Source date |
|---|---|---|---|
| T1 | Naavik Digest, "What Leading Match-3 & Merge Games Do Differently" (Oindrila Mandal), Sensor Tower data | https://naavik.co/digest/what-leading-match-3-and-merge-games-do-differently/ | 2026-01-18 |
| T2 | Naavik Digest, "Why Minigames Mean Major Business" (Oindrila Mandal) | https://naavik.co/digest/why-minigames-mean-major-business/ | 2025-08-17 |
| T3 | Liftoff, "2025 Casual Gaming Apps Report" (GameRefinery data Feb 2024 – Feb 2025) | https://liftoff.ai/2025-casual-gaming-apps-report/ | ≈2025-04 (not stated) |
| T4 | Nextbiggames (YouTube), "TOP 28 Highest Grossing HybridCasual Puzzles 2025" | https://www.youtube.com/watch?v=wJ_qXZTyX8A | 2026-01-15 |
| T5 | Sensor Tower blog, "Top 5 Puzzle Games in the US: Q4 2025 Performance on Unified Platform" | https://sensortower.com/blog/2025-q4-unified-top-5-puzzle-games-units-us-64bc1644e1714cfff1e615fc | 2026-01 |
| T6 | Sensor Tower blog, "Top 5 Puzzle Games Performance in the US: Q1 2025" | https://sensortower.com/blog/2025-q1-unified-top-5-puzzle%20games-revenue-us-64bc1644e1714cfff1e615fc | 2025-04 |
| T7 | Sensor Tower blog, "What Royal Match's calendar teaches about live ops in 2026" (Bryan Isagholian) | https://sensortower.com/blog/what-royal-matchs-calendar-teaches-about-live-ops-in-2026 | 2026-05 |
| T8 | Gamigion, "New event in Royal Match: Puzzle Break" (Anton Slashcev) | https://www.gamigion.com/new-event-in-royal-match-puzzle-break/ | June 20, 2025 (year inferred) |
| T9 | Gamigion, "Royal Match runs mini-games as events!" | https://www.gamigion.com/royal-match-runs-mini-games-as-events/ | undated on page |
| T10 | Gamigion, "Smash Fest!: Casual LiveOps Turned Into Genre-Wide Trend" (Andrei Zubov, AppMagic) | https://www.gamigion.com/smash-fest-casual-liveops-turned-into-genre-wide-trend/ | August 20, 2026 (year inferred) |
| T11 | Gamigion, "Royal Match launched its first Expedition event!" (Oxana Fomina), Sensor Tower data | https://www.gamigion.com/royal-match-launched-its-first-expedition-event/ | September 15, 2026 (year inferred) |
| T12 | Gamigion, "Monopoly GO Top Revenue-Driving Events" (Anton Slashcev) | https://www.gamigion.com/monopoly-go-top-revenue-driving-events/ | February 21, 2025 (year inferred) |
| T13 | FoxData, "Royal Match's Mini-Games — The Secret Driving Its Global Success in 2025" | https://foxdata.com/en/blogs/royal-matchs-mini-games-the-secret-driving-its-global-success-in-2025/ | 2025-09-30, updated 2026-01-19 |
| T14 | Old Cynic, "Royal Match: Dragon Nest & Magic Tokens [Guide/Tips]" | https://oldcynic.com/royal-match-dragon-nest-magic-tokens-balls-guide-tips | 2024-10-09, updated 2025-09-05 |
| T15 | TheGamer, "Monopoly Go: Complete Juggle Jam Guide" (Gabrielle Castania) | https://www.thegamer.com/monopoly-go-juggle-jam-tutorial-rewards-carnival/ | 2025-01-21 |
| T16 | BBC News, "Homescapes and Gardenscapes ads banned as misleading" | https://www.bbc.com/news/technology-54509970 | 2020-10-12 |
| T17 | GamesIndustry.biz, "ASA bans misleading Homescapes, Gardenscapes ads" (search excerpt) | https://www.gamesindustry.biz/asa-bans-misleading-homescapes-gardenscapes-ads | 2020-10 |
| T18 | Wikipedia, "Homescapes" | https://en.wikipedia.org/wiki/Homescapes | live page |
| T19 | Sherwood News, "Mobile game ads" (Max Knoblauch) | https://sherwood.news/business/mobile-game-ads-industry-fake-misleading | 2024-06-14 |
| T20 | AppGrowing Global blog, "Misleading Ads of Gardenscapes" (search excerpt) | https://appgrowing.net/blog/en/gardenscapes-ads/ | ≈2021 (refers to the ruling as "last year") |
| T21 | Pocket Gamer, "Fishdom minigames solutions" (Allison Van Oirschot) | https://www.pocketgamer.com/fishdom/minigames-solutions | 2022-11-29 |
| T22 | h-w.fun, "Is Hero Wars like the ads?" (fan guide) | https://h-w.fun/en/hero-wars-is-different-from-ads/?amp=1 | search snippet, undated |
| T23 | Deconstructor of Fun, "Tile Match: The new Match-3 or the new Merge?" (Michail Katkoff; writer Ahmetcan Demirel), data.ai | https://www.deconstructoroffun.com/blog/2023/9/30/tile-match-the-new-match-3-or-the-new-merge | 2023-10-02 |
| T24 | Old Cynic, "Royal Match: Save The King Levels [King's Nightmare]" (used only to confirm minigames.md S3) | https://oldcynic.com/royal-match-save-the-king-levels-kings-nightmare | 2024-01-17, updated 2025-07-20 |
| T25 | Apple iTunes Search API, US store (seller, release date, rating count, description). Queries: water sort, ball sort, screwdom, wood nuts bolts, tile busters, zen match, vita mahjong, triple match 3d, parking jam 3d, bus jam, pull the pin, hexa sort, june's journey, find the difference, nonogram, jigsaw puzzles, jigsolitaire, pixel flow, flow free, two dots, wordscapes, trivia crack, block blast, merge games | e.g. https://itunes.apple.com/search?term=water+sort+puzzle&entity=software&country=us | read 2026-10-24 |
| T26 | Game World Observer, "Sensor Tower: Mobile puzzle games generated $42 billion in 11 years" (search excerpt) | https://gameworldobserver.com/2023/09/08/puzzle-games-mobile-42-billion-revenue-sensor-tower-report | 2023-09-08 |
| T27 | Google Play, Goods Puzzle: Sort Challenge (search excerpt of the listing) | https://play.google.com/store/apps/details?id=com.fc.goods.sort.matching.puzzle.triplemaster | live page |
| T28 | GameDev Reports, "Sensor Tower: Popularity of Puzzles is growing in the South Korean Market" (search excerpt) | https://gamedevreports.substack.com/p/sensor-tower-popularity-of-puzzles | 2025 |
| T29 | FoxData (Korean), "How Vita Mahjong stayed #1 in the US App Store" | https://foxdata.com/kr/blogs/how-vita-mahjong-stayed-1-in-the-us-app-store-and-what-it-means-for-app-growth-in-2025/ | search snippet, 2025 |
| T30 | US App Store, Color Block Jam (Rollic Games) | https://apps.apple.com/us/app/color-block-jam/id6504332779 | live page (search excerpt) |
| T31 | Business of Apps, "Puzzle Games Revenue and Usage Statistics (2026)", Sensor Tower data (search excerpt) | https://www.businessofapps.com/data/puzzle-games-market/ | 2026 page, 2022 data |
| T32 | US App Store, Matchington Mansion (search summary) | https://apps.apple.com/us/app/matchington-mansion/id1216575026 | search snippet |
| T33 | NY1, "New York Times puzzle editor launches new game" (Pips) | https://ny1.com/nyc/all-boroughs/the-rush-hour/2025/08/29/new-york-times-puzzle-editor-launches-new-game | search snippet, 2025-08-29 |
| T34 | Dataintelo, "Puzzle Games Market Size, Share & Forecast Report 2025 to 2033" (search excerpt; market-report site, weak) | https://dataintelo.com/report/puzzle-games-market | 2026 |
| T35 | Old Cynic, "Royal Match: Train Journey [Guide & Tips]" | https://oldcynic.com/royal-match-train-journey-guide | search snippet, undated |
