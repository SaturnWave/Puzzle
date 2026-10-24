# Rotating mini-game events: calendars, entry, rewards, and running them offline

Research for the museum / painting-restoration match-3 (Unity client, .NET engine, no server, no INTERNET permission).
Written 2026-10-24. Every source was opened (or, where marked "excerpt", seen only as a search excerpt) on **2026-10-24**.

**How to read this file**

- **FACT**: seen on the source itself (the game's own help centre, a platform's own documentation, a store page).
- **REPORTED**: from a secondary source (fan wiki, fan guide, analyst blog, forum). It may be right, but nobody outside that source checked it.
- **ESTIMATE**: my own inference. No source says it directly.
- **IDEA**: a design proposal for this game. Not a fact about any game.
- `[C12]` points to the source list at the end. `[ES]`, `[MG]`, `[MR]` point to earlier work (see below).

**What this file adds to earlier work.** `out/research/monetization-2026-10/events-social.md` [ES] already has the event catalogues of Royal Match, Royal Kingdom, Candy Crush, Toon Blast and Playrix, the AppMagic / Sensor Tower concurrency numbers (about 11 events midweek, about 20 at the Friday / Monday overlap) and the five event families. `minigames.md` [MG] has the mini-game catalogues. `docs/research/monetization-and-retention.md` [MR] sections 5 and 7 have our idea list (Restorer's Bench, Gala night, Archive Dig, Catalogue Match, daily challenge, Exhibition calendar). This file does not repeat those. It adds: **the actual weekly clock** the leaders run on (start times, block lengths, alternating weeks, chained mini-games, monthly fixed weeks), unlock levels side by side, how mini-games are fed and how failure works in each, the **reward ladders** of two mini-game events step by step, what makes a "life" reward worth anything, and **how to run all of this on the phone's clock**, with the platform facts that make clock protection possible.

---

## Kısa özet (TR)

1. **Royal Match haftayı ikiye bölüyor:** Pazartesi 08:00 UTC → Cuma 08:00 UTC (4 gün) ve Cuma 08:00 UTC → Pazartesi 08:00 UTC (3 gün). Her blokta **bir mini oyun zinciri** açık: birini bitirince sıradaki açılıyor (hafta içi Hidden Temple → Pinata Party; hafta sonu Magic Cauldron → Balloon Rise). Yani oyuncu bir haftada birkaç mini oyun oynuyor ama aynı anda yalnız biri ekranda (REPORTED, fan wiki ve forum, birbirini doğruluyor) [C1][C4][C5][C6].
2. **Çeşitlilik üç katmandan geliyor:** her hafta dönen bloklar, iki haftada bir yer değiştiren türler (Magic Cauldron ↔ Duke's Fortune, Sky Race ↔ Space Mission) ve ayın sabit haftaları (ayın 1. cuması 10 günlük Dragon Nest, 3. cuması Train Journey, ay başında Royal Pass) (REPORTED) [C1][C2][C4]. Homescapes'te de aynı Pzt–Cum / Cum–Paz düzeni var (REPORTED) [C13].
3. **Mini oyunlar ~35–40. bölümde açılıyor:** Royal Match 39 (FACT) [C8], Royal Kingdom 36 (FACT) [C9], Toon Blast 35 (FACT) [C12]. Bizim hızımızla (100. bölüm 8. gün) bu 2–3. gün demek (ESTIMATE).
4. **Besleme hep aynı:** kazanılan her bölüm jeton verir, zor bölümler daha çok verir; mini oyun can harcatmaz. Kayıp kuralları değişiyor: bölümü kaybedince o bölümde toplanan jeton sayılmaz, Balloon Rise'ta adım başına 3 balon var, etkinlikten çıkınca jeton gider, etkinlik bitince "son bir şans" ekranı var (FACT) [C8][C9][C12].
5. **Ödül merdiveni:** Magic Cauldron 15 adım, Balloon Rise 10 adım; adımların çoğu tek booster, arada altın, her 3–4 adımda bir **süreli ödül** (15–30 dakika sınırsız can ya da sınırsız Rocket/TNT/Light Ball), sonda büyük altın ve kart (REPORTED, wiki tabloları) [C1].
6. **"Can" ödülü bizde neredeyse değersiz.** Liderlerde sınırsız can, etkinliğin başladığı anda verilen "doya doya oyna" izni (King's Cup başlayınca 30 dk, Lightning Rush 1 saat) (REPORTED/FACT) [C1][C8]. Bizde canlar zaten bitmiyor; yerine **süreli booster** (Candy Crush'ta resmi kuralları var: gerçek zamanla akar, üst üste eklenir, kaybedince gitmez — FACT [C10]), **etkinliğe özel deneme hakkı** (balon gibi), çerçeve/isim süsü, koleksiyon parçası, altın ve XP önerilir (IDEA).
7. **Çevrimdışı takvim mümkün.** Android'de kullanıcının değiştiremediği, uyku dahil işleyen bir sayaç (`elapsedRealtime`) ve Android 13+'ta kullanıcının değiştiremediği ağ saati (`currentNetworkTimeClock`, uygulama ağ isteği yapmadan) var (FACT) [C21]. iOS saat değişince bildirim gönderiyor (FACT) [C23]. Candy Crush'ın can sayacı telefon saatine bakıyor ve saat geri alınınca "100 saat bekle" gibi hatalar veriyor; resmi yardım sayfası çözümü anlatıyor (FACT) [C10] — **ceza değil, kazancı sınırlama** yolu seçilmeli (ESTIMATE).
8. **Öneri (IDEA):** tarih listesi değil **kurala dayalı gömülü takvim** (hafta numarasından tür seçer, hiç bitmez); ayda 2 "Atölye haftası", her birinde 2 + 2 mini oyunluk iki zincir = ayda 8 mini oyun yuvası; tür tablosuna satır eklemek döngüyü uzatır. Etkinlik ve günlük ödül kimlikleri yalnız ileri gider: saati ileri almak geleceği **ödünç alır**, fazladan kazandırmaz. App Store In-App Events ve Google Play tanıtım kartları sunucu istemiyor; önceden bilinen takvim bunları mümkün kılar (FACT) [C26][C27].

---

## 1. Ten findings (EN)

1. **The leaders' week is two blocks, not seven days of chaos.** Royal Match runs a weekday block (Mon 08:00 UTC to Fri 08:00 UTC) and a weekend block (Fri 08:00 UTC to Mon 08:00 UTC); most events belong to one block (REPORTED, fan-wiki infoboxes [C1], corroborated by NamuWiki [C4], a Reddit excerpt [C5] and a Facebook group excerpt [C6]). Homescapes uses the same Mon-Fri / Fri-Sun split (REPORTED [C13]).
2. **"Several mini-games in one week" is done with a chain, not with parallel games.** One mini-game is open per block; finishing it opens the next (weekday: Hidden Temple, then Pinata Party; weekend: Magic Cauldron, then Balloon Rise) (REPORTED [C1][C4]; the Reddit excerpt says finishing one mini-game in a rotation lets you play another [C5]).
3. **Novelty comes from three layers:** weekly blocks, alternate-week swaps (Magic Cauldron with Duke's Fortune; Sky Race with Space Mission), and fixed weeks of the month (Royal Pass at the start of the month, Dragon Nest on the first Friday for 10 days, Train Journey on the third Friday) (REPORTED [C1][C2][C4]).
4. **Mini-game events unlock around level 35-40** in all three Dream / Peak games with official numbers: Royal Match 39 [C8], Royal Kingdom 36 [C9], Toon Blast 35 [C12] (FACT). Later mini-games unlock up to level 85-101 (FACT [C8][C9]).
5. **Every mini-game is fed by wins.** Each won level gives the event's token; Hard / Super Hard (and in Royal Kingdom, Kingdom / Dark Kingdom) levels give more; no mini-game in the official pages costs a life (FACT [C8][C9][C12]). Failure rules vary and are spelled out (section 3).
6. **Ladders are long and mostly small.** Magic Cauldron has 15 steps and Balloon Rise 10; most steps are one or two boosters, coins come in the middle and at the end, and a timed reward (15-30 min of unlimited lives or of one unlimited booster) appears every 3-4 steps (REPORTED, wiki reward tables [C1]).
7. **Unlimited lives are a "go binge now" pass, handed out when an event starts** (King's Cup start: 30 min; Lightning Rush: 1 hour; Propeller Rush: the first start each day) (REPORTED [C1]; Propeller Rush FACT [C8]). They matter only where lives run out. Ours almost never do, so the life reward should be replaced (section 4.3).
8. **Timed boosters are the common alternative.** Candy Crush documents them: they count down in real time even while you are away, stack, and are not lost on a failed level (FACT [C10]). Royal Match ladders use 15 m / 30 m unlimited Rocket, TNT and Light Ball (REPORTED [C1]).
9. **The phone gives enough to protect an offline calendar.** Android has a boot-based clock that the user cannot set and that keeps counting in deep sleep, a boot counter, an auto-time setting, and on Android 13+ a network-synchronised clock the user cannot adjust (FACT [C21][C22]). iOS posts a notification on clock changes (FACT [C23]). Candy Crush's own help shows what goes wrong when a game trusts the wall clock blindly (FACT [C10]).
10. **Announcing events needs no server.** In-game: home-screen icon with countdown, a first-time pop-up with "now / later", Events tab, local notifications (FACT/REPORTED [C7][C10][C11][C31]). In the stores: Apple In-App Events and Google Play Promotional content (the latter available for all games) (FACT [C26][C27]). Both need dates known in advance, which a deterministic calendar gives.

---

## 2. Calendars

### 2.1 Royal Match: the week as it actually runs

Source: infobox fields (`active_days`, `event_end`, `required_level`) of the Royal Match fan wiki, read through the wiki's public API [C1]; order and alternation notes from NamuWiki [C4]. All **REPORTED**: the wiki pages were last edited at different times, and NamuWiki records at least two schedule changes (13 May, year not given, and 13 October 2025), so the exact mapping may be out of date. Where the official help page gives the unlock level, it matches the wiki (FACT [C8]).

| Layer | Window | Events in it (unlock level) | Notes |
|---|---|---|---|
| **Weekday block** | Mon 08:00 UTC → Fri 08:00 UTC (4 days) | King's Cup (25), Book of Treasure (28, 25 steps), Team Treasure (28), **Hidden Temple (39, 5 steps) → Pinata Party (39, 3 steps)** | Pinata Party opens only after Hidden Temple is finished (wiki); NamuWiki gives the weekday quest order as Hidden Temple, Balloon Launch, Piñata Party, so the chain has changed over time |
| Weekday, local time | Mon 00:00 → Thu 23:00 **local** time | Lightning Rush (31): 1-hour race, playable once a day | The only event in the set the wiki lists in local time |
| **Weekend block** | Fri 08:00 UTC → Mon 08:00 UTC (3 days) | Team Battle (23), Propeller Madness (28), Sky Race (25) **or** Space Mission (51), **Magic Cauldron (39, 15 steps) → Balloon Rise (39, 10 steps)**, Archery Arena (56) | Sky Race and Space Mission alternate; Magic Cauldron alternates every other week with Duke's Fortune; Balloon Rise appears only after Magic Cauldron |
| Weekend, daily rounds | Fri, Sat, Sun 08:01 UTC, each to 08:00 next day | Archery Arena (56) | Three separate one-day competitions |
| Weekly | Mon 08:01 UTC → next Mon 08:00 UTC | Weekly Contest (47) | Starts again at once |
| Weekly (new) | 7 days | Weekly Missions: new missions every day for 7 days, missed days stay open until the end (FACT [C8]) | Level and start day not on the page |
| Daily | 08:00 UTC, 24 hours | Lava Quest (66): 7 first-try wins in a row | |
| Monthly | Start of month to start of next | Royal Pass (39, 30 steps) | [C2] |
| Monthly, fixed week | 1st Friday of the month, 10 days | Dragon Nest (76), co-op mini-game | [C1] |
| Monthly, fixed week | 3rd Friday of the month | Train Journey, co-op | [C4] |
| Weekend tasks | Fri 17:00 → Mon 17:00 Korean time (= 08:00 UTC) | Mission Pursuit (task list) | Weekday counterpart: Mission Control, Mon → Fri [C4] |

**What this shows (ESTIMATE).**
- A Royal Match player meets **two mini-game chains a week**, each of two games. That is four mini-games a week for someone who finishes everything, but never more than one open at a time.
- The weekly "shape" is fixed; what changes is **which family fills a slot this week** (alternation) and **which new type was added** in the last update (2026 additions such as Merge Smith and King's Tales are in [ES]).
- **Global UTC start** suits events where players are matched against each other. A solo game has no reason to start at the same instant worldwide; Lightning Rush shows that even Dream Games uses local time where matching does not depend on it (REPORTED [C1]).
- Every block ends **and the next starts at the same instant**, so there is never a day with nothing open.

**How players see it.** Events "appear as you complete levels"; a window describes the event and the player can accept now or activate it later (REPORTED, Google Play editorial [C7]). Ranking is seen by tapping the event's icon on the home screen; when all steps of a solo event are claimed the event ends and the icon disappears (REPORTED [C1]). Several events start only after a button tap on their info screen (FACT, Propeller Rush [C8]); King's Cup starts by itself after the next win (REPORTED [C1]).

### 2.2 Royal Kingdom

The help centre lists 21 events (FACT [C9]). Durations are not on the help pages; the wiki category is empty. Mini-game and collection events with official unlock levels: Diamond Mine and Electro Blitz 36, Cube Blast and Magic Carnival 51, Hill Quest and Canvas Quest 101 (FACT [C9]). A Facebook excerpt says Team Adventure runs Monday to Friday (REPORTED, weak [C9]). Structure and rules are in section 3.

### 2.3 Candy Crush Saga

- Events run all the time but on fixed dates; the **Events tab** at the bottom of the main menu holds the details; events sharing a theme are grouped into **Seasons** with "Season Mastery" ranks (REPORTED, Google Play editorial [C11]).
- King's help says events announce themselves with **pop-ups and messages**, many show a **timer or expiry date**, the timer keeps running while you are away, and players should keep notifications on (FACT [C10], updated 2026-08-09 and 2026-10-07).
- New events reach players **gradually and in test groups**: Deep Dive Dash and Yeti's Draw both say so, and both need level 100 or higher (FACT [C10]).
- A weekly calendar for Candy Crush was not found (gap).

### 2.4 Toon Blast

- The help centre lists 39 event articles, among them the mini-games Cannon Fest, Space Miner, Surprise Calendar, Rescue Mission, Temple Guardians, Magic Disco and Deep Quest (FACT [C12]).
- **Surprise Calendar** (from level 35): tap falling gifts for coins, avoid TNT; **once per day**; playing on 6 days gives a surprise gift; needs to be online; killing the app mid-game loses that day's chance (FACT [C12]). This is the clearest "daily free try" model.
- **Rescue Mission** appears as you progress through main levels; no lives lost on failure, unlimited retries, same reward on replay, does not count toward events or Crown Rush; skipping loses that mission (FACT [C12]).
- A weekly calendar for Toon Blast was not found (gap). Weekly Contest starts on Monday (FACT, in [ES]).

### 2.5 Homescapes and Gardenscapes (Playrix)

From the Homescapes fan wiki's Events page [C13] (all **REPORTED**):

| Kind | Event | Window |
|---|---|---|
| Daily | Swinging Bridge (7 wins in a row, shared prize) | Daily, once a day |
| Weekday | Cupcake Cup (collect and compete), Let's do it (task bar), Team Chest | Monday to Friday |
| Weekend | Weekend Plans (task bar), Team Competition | Friday to Sunday |
| Two days | Laser Busters | Thursday and Friday |
| Week-long race | Knight's Race | Saturday to Friday |
| Bonus | Cat's Gift (booster streak), Super Rainbow Ball (10 wins upgrade) | Always on |
| Mini-game (in test) | Treasure Hunt (pickaxes on a grid), **Ice Crash** (place ice cubes on a grid, clear lines, compete on score) | Not given |
| Long | Expeditions | 21 days each, timings vary |
| Pass | Home Pass 21 days; Season of Wonders up to 35 days, 35 stages | |

Ice Crash is a grid-placement block game inside a match-3, the same genre as the owner's next game (ESTIMATE that this matters: the block-puzzle work could double as a mini-game). The Google Play store shows a Gardenscapes "Spooky Pass" event card with a countdown ("ends in 4 days") (FACT [C14]).

### 2.6 Monopoly GO (Scopely)

No official help page was found; all from fan guides, **REPORTED**.

| Layer | Typical length | Where in the UI | Feeds on | Source |
|---|---|---|---|---|
| Season (sticker album) | 30-90 days [C16]; "around eight weeks" [C18] | Album | Sticker packs from everything | [C16][C18] |
| Main banner event | 2-4 days, 40-50 milestones | Top-centre banner | Landing on given board tiles | [C15][C16] |
| Tournament | 1-2 days, 25-30 milestones plus a rank prize in groups of 50 or 100 | Icon on the right side | Railroad mini-games (Shutdown, Bank Heist) | [C15][C16] |
| Special mini-game (Peg-E, dig boards, partner wheels) | 3-5 days | Own event board | Event currency from play, events, tournaments, daily Quick Wins | [C15][C19] |
| Flash events | 5-60 minutes of play; several a day | Pop-up or indicator | They temporarily change what an action is worth (Cash Boost doubles cash from rolls) | [C15][C17] |

- On 2026-10-23 four multi-day events were live at once (two ending that day, two on 26 October), plus flash events (REPORTED [C17]). Events switch at one global time (1 PM Pacific in the AppGamer schedule excerpt) (REPORTED [C16]).
- Rewards are mostly **dice**, the resource that gates play, plus cash and sticker packs [C15]. The point for us: Monopoly GO's main reward is its own energy, and it works there because dice run out every session (ESTIMATE; regeneration numbers in fan guides disagree, so none are quoted).
- Leftover partner-event tokens reportedly turn into cash; for Peg-E tokens the guides contradict each other (REPORTED, weak [C19]).
- Not for us: dice multipliers and Peg-E are gambling-shaped; tokens and packs can be bought (the Google Play listing marks the game as having random items, see [ES]/meta.md). Our rules (no paid random items, §17/§19) rule these out.

### 2.7 How events are announced

| Channel | Who uses it | Needs a server? | Source |
|---|---|---|---|
| Home-screen icon with countdown; icon disappears when finished | Royal Match, Candy Crush, Monopoly GO | No | [C1][C10][C15] (REPORTED/FACT) |
| First-appearance pop-up with "play now / later" | Royal Match | No | [C7] (REPORTED) |
| Events tab listing current events and rules | Candy Crush | No | [C11] (REPORTED) |
| Top banner for the main event, side icons for the rest | Monopoly GO | No | [C15][C16] (REPORTED) |
| Notifications | Candy Crush asks players to keep them on | **Local** notifications need no server: Unity's Mobile Notifications package schedules one-time or repeating notifications on Android and iOS (FACT [C31]). Android 13+ needs the POST_NOTIFICATIONS runtime permission (FACT [C30]) | [C10][C30][C31] |
| App Store In-App Events | Event cards on the product page, in search, in editorial; up to 31 days long; promoted up to 14 days before the start; 10 published per app at a time | No game server; set up in App Store Connect | [C26] (FACT) |
| Google Play Promotional content | Cards on the store listing, may be featured; **available for all games**; events up to 4 weeks; submit at least 4 days ahead (14 for featuring) | No game server; set up in Play Console | [C27] (FACT); Gardenscapes card [C14] |

### 2.8 Unlock levels side by side

| Game | First event | First mini-game event | Later mini-games | Source |
|---|---|---|---|---|
| Royal Match | Team Battle 23, King's Cup / Sky Race 25 (REPORTED [C1]) | **39**: Hidden Temple, Magic Cauldron, Pinata Party, Balloon Rise, Puzzle Break (FACT [C8]) | Ocean Odyssey 85, Merge Smith 85 (FACT [C8], [ES]) | |
| Royal Kingdom | (see [ES]) | **36**: Diamond Mine, Electro Blitz (FACT [C9]) | Magic Carnival 51, Cube Blast 51, Hill Quest 101 (FACT [C9]) | |
| Toon Blast | Crown Rush, Star Tournament 25 (FACT, in [ES]) | **35**: Surprise Calendar (FACT [C12]); Cannon Fest, Space Miner 35 (FACT snippets, in [ES]) | Temple Guardians 40, Dragon's Treasure 50 (FACT, in [ES]) | |
| Candy Crush | Teams 20 (FACT, in [ES]) | Not found | Deep Dive Dash, Yeti's Draw 100 (FACT [C10]) | |
| Monopoly GO | — | Partners from board 5 (REPORTED, weak [C19]) | — | |

**For us (ESTIMATE).** The average free player reaches level 100 on day 8 (§25). If early levels go a little faster than late ones, level 35-40 falls on day 2-3. That places the first mini-game event at the moment the first week's novelty fades, which matches where the leaders put it. Our Restorer's Bench (in-path, every painting) [MR §5] already covers levels 1-100; the calendar events should start at about level 35 and add one new family every 10-15 levels up to about 100.

---

## 3. Entry and progress

All rows are FACT from the official help pages unless marked.

| Event (game) | What feeds it | Extra from hard levels | Lives | What a failure costs | End of event | Source |
|---|---|---|---|---|---|---|
| Hidden Temple (RM, dig board) | Pickaxes from wins | Yes (Hard, Super Hard, Extremely Hard) | Not mentioned | Quitting the event loses the pickaxes | One last chance to use pickaxes | [C8] |
| Magic Cauldron (RM, find the formula) | Elixirs from wins | Yes | Not mentioned | Quitting loses the elixirs; a wrong formula keeps the right places only (REPORTED [C4]) | One last chance | [C8][C4] |
| Puzzle Break (RM, **jigsaw**) | Puzzle pieces from wins | Yes | Not mentioned | Not stated; NamuWiki says a misplaced piece used to be lost, later changed so a mistake costs nothing (REPORTED [C4]) | Not stated | [C8][C4] |
| Pinata Party (RM, 3 piñatas) | Candies from wins | Yes (wiki: x3 Hard, x5 Super Hard, REPORTED [C1]) | Not mentioned | Not stated | — | [C8][C1] |
| Balloon Rise (RM, 10 steps) | Cookies from wins | Help page: yes; wiki: no (conflict) | **3 balloons per step**; a lost level pops one | Losing all 3 drops you to the start of the current step; earlier steps are kept | — | [C8][C1] |
| Ocean Odyssey (RM) | Ships from wins; bubbles add a ball per throw | Yes | Not mentioned | Quitting loses the ships | One last chance | [C8] |
| Book of Treasure (RM, collect red pieces) | Red pieces destroyed in won levels | Yes | Normal | A lost or quit level's books do not count | — | [C1] (REPORTED) |
| Weekly Missions (RM) | Scrolls from missions | — | — | Missed days stay open | Grand prize when the bar is full | [C8] |
| Diamond Mine (RK) | Red pieces; **leftover moves become diamonds** | Yes (incl. Kingdom / Dark Kingdom) | Normal | A lost or quit level's diamonds do not count | — | [C9] |
| Magic Carnival (RK, memory match) | Orbs from wins | Yes | Not mentioned | Mismatched orbs fade away; quitting loses the orbs and the icon | One last chance; orbs never carry to the next event | [C9] |
| Hill Quest (RK) | Pickaxes from **quests** (earn coins, first-try wins, beat Dark King levels, a partner task) | — | — | — | Grand prize for all quests | [C9] |
| Surprise Calendar (TB, tap gifts) | **One free play a day** | — | Not mentioned | Killing the app mid-game loses that day | 6 days played → surprise gift | [C12] |
| Rescue Mission (TB, timed) | Appears as you progress | — | **No life lost** | Unlimited retries; skip loses the mission | — | [C12] |
| Cannon Fest / Space Miner (TB) | Cannonballs / pickaxes from levels | — | — | — | One last chance | [C12] |
| Don't feed the Troll (CC, collection with decay) | Orange candies, **only from new levels** | — | Normal | Being slow lets the troll eat the bottom reward; each slice has its own timer | Fewer rewards the slower you are | [C10] |
| Yeti's Draw (CC) | 3 wins = 1 ticket, at most 10 tickets | — | — | — | Tickets may hold a booster; one random Gold Bar winner per group | [C10] |

**Patterns (ESTIMATE from the table).**
- The token comes from **winning ordinary levels**; mini-game events never ask for their own levels or for lives. They make an ordinary win worth more.
- **Hard levels pay more tokens** in almost every Dream Games event. This rewards exactly the levels players fear, without changing the level.
- The common **"lose what you collected in a lost level"** rule is invisible to us: our tokens come only from wins.
- **Quit = lose tokens** (Royal Match, Royal Kingdom) is a harsh rule for a 30+ audience; Toon Blast's Rescue Mission shows the friendly end of the range (no life, unlimited retries).
- **Event-local tries** (Balloon Rise's balloons) are a second, private life system that makes a failure cost something inside the event without touching the main lives.
- **"One last chance to spend"** at the end is standard (Royal Match, Royal Kingdom, Toon Blast). Tokens never carry over (Royal Kingdom says so outright).

---

## 4. Rewards

### 4.1 Two reward ladders step by step (Royal Match, REPORTED, wiki tables [C1])

| Step | Magic Cauldron (15 steps) | Balloon Rise (10 steps) |
|---|---|---|
| 1 | 1 Rocket | 2 Rockets |
| 2 | 1 TNT | 2 Jester Hats |
| 3 | 1 Light Ball | 15 m unlimited lives + 15 m unlimited Rocket |
| 4 | **15 m unlimited lives** | 2 Hammers + 2 Arrows |
| 5 | 500 coins | 2 TNT + 2 Light Balls + 2 Rockets |
| 6 | 1 Royal Hammer | 2 Arrows + 2 Cannons + 2 Hammers |
| 7 | 15 m unlimited Rocket | **1,500 coins** |
| 8 | 1 Cannon | 15 m unlimited lives + 15 m unlimited TNT, Rocket and Light Ball |
| 9 | 15 m unlimited TNT | 6x card rewards |
| 10 | **30 m unlimited lives** | **3,000 coins** |
| 11 | 1 Arrow | |
| 12 | 15 m unlimited Light Ball | |
| 13 | 1 Jester Hat | |
| 14 | 9x card rewards | |
| 15 | 1,000 coins | |
| Chest | Wild card + **1 h unlimited lives** + one of each of the 7 boosters | |

Royal Pass (30 steps, monthly) mixes the same kinds: free line with single boosters, 100-coin steps and 15 m unlimited lives; paid line with 15 m unlimited TNT and 30 m unlimited Rocket (REPORTED [C2]). For scale, the same wiki gives a full life refill as 900 coins (REPORTED [C3]).

**Shape (ESTIMATE).** Early steps are cheap and quick (one booster each) so the player feels progress at once; coins come at the middle and the end; a timed reward every 3-4 steps; collection cards near the end; the biggest single prize is the last step or the chest. Total coins per ladder: 1,500 (Magic Cauldron) and 4,500 (Balloon Rise), against a 900-coin life refill. One analyst values whole events at about $1-2 (Team Treasure), about $2 (Lava Quest) and $8-9 (Dragon Nest) of prize value (REPORTED, author estimate [C20]): single events give little; the pull is the steady stream.

**Note on fairness.** The wiki says Balloon Rise and Team Treasure rewards "can be changed based on performance" (REPORTED [C1]). Our rule of no secret adjustment (§17) points the other way: one fixed ladder, shown before the start, the same for everyone (ESTIMATE).

### 4.2 Timed rewards: unlimited lives and unlimited boosters

| Rule | Source |
|---|---|
| Unlimited lives run on a timer that keeps counting while you are not playing; timers can stack when won and bought together (Candy Crush) | FACT [C10] |
| Unlimited lives keep counting when the game is closed (Royal Match wiki) | REPORTED [C3] |
| Timed boosters (Candy Crush): applied automatically to every level until the countdown ends; win more of the same and the timers add up; **not lost when a level fails**; run in real time; event rewards of this kind are delivered at the end of the event or of the goal | FACT [C10] |
| Royal Match ladders give 15 m and 30 m of unlimited Rocket, TNT or Light Ball | REPORTED [C1][C2] |
| Starting King's Cup gives 30 min of unlimited lives; Lightning Rush gives 1 hour; Propeller Rush gives unlimited lives the first time you start a step each day | REPORTED [C1]; Propeller Rush FACT [C8] |
| An analyst calls the mix of consumable rewards and time-limited unlimited rewards the core of Royal Match's engagement loop: players try to use a timed reward fully, which keeps them playing until a hard level or a milestone | REPORTED [C20] |

### 4.3 What makes a "life" reward meaningful, and what to give instead

**Where lives are worth something (FACT/REPORTED).** In Royal Match and Candy Crush the cap is 5 and a life returns every 30 minutes (FACT [C8][C10]); a full refill costs 900 coins in Royal Match (REPORTED [C3]); Royal Pass and Toon Pass raise the cap to 8 or 10 (FACT, in [ES]). Players can ask friends or teammates for lives, with limits (Candy Crush: accept 20 a day, mailbox up to 200; Royal Match: no requests above 10 free lives) (FACT [C10][C8]). Lives are worth something there because failing is frequent enough to empty the bar.

**How the leaders actually use the life reward (ESTIMATE from the rows above).** Not as a stored good. Unlimited lives are mostly handed out **at the moment an event begins**, as permission to play many levels in a row inside the window. In the events where failure really costs (Balloon Rise, Space Mission, Archery Arena multipliers, Lava Quest), the cost is **event progress**, not lives.

**Our situation.** 5 lives, one back every 30 minutes, lives lost only on a loss (docs/OVERVIEW.md, §19); in about 10-minute sessions they almost never run out (§25, task brief). So "N hours of unlimited lives" buys nearly nothing today (ESTIMATE). What could replace it (IDEA, each needs the economy simulator):

| Alternative | Why it means something here | Precedent | Rule check |
|---|---|---|---|
| **Timed free booster** ("next 15 minutes: every level starts with a Brush") | Every player feels it on the next level; value bounded by real time | Candy Crush timed boosters (FACT [C10]); Royal Match 15/30 m unlimited boosters (REPORTED [C1]) | A free reward, not bought, so §17 allows it; it speeds progress, so the simulator must see it. Cap stacking (e.g. at most 60 minutes) |
| **Event-local tries** (a mini-game step gives 3 tries; a reward can be "+1 try") | Makes failure inside the event matter without touching lives | Balloon Rise balloons (FACT [C8]) | No lives, no money |
| **Streak keeper** (one loss does not break the Steady Hand streak) | Our Steady Hand streak [MR §5] is a thing players can lose | Candy Crush sells a save at the moment of loss (REPORTED, weak; search excerpt of a Gamigion article) | Earned only, never sold (§17) |
| **Cosmetic**: gilt frame for a painting, name plate style, gallery wall colour | Permanent, visible, cannot break the economy | Royal Pass gold frame and name; Dragon Nest frame; Train Journey name style (FACT, in [ES]) | No effect on difficulty |
| **Collection piece** (sketch for the side collection / album) | Feeds the meta | Royal Match card rewards in ladders (REPORTED [C1]) | No random packs; fixed piece per step |
| **Coins, XP (rank)** | Simple, already exist / planned | Every ladder | Simulator |
| **Unlimited lives, kept only where they matter** | Inside a streak or "no-fail" event, a loss costs a life *and* the streak | King's Cup / Lightning Rush start gifts (REPORTED [C1]) | Small and rare |

### 4.4 What keeps rewards from breaking the economy

| Guard | Who | Source |
|---|---|---|
| Timed rewards expire in real time even when not played, so their value is bounded by play time | Candy Crush, Royal Match | FACT [C10]; REPORTED [C3] |
| Tokens never carry over between events; one last chance to spend, then they are gone | Royal Kingdom, Royal Match, Toon Blast | FACT [C9][C8][C12] |
| Only new levels count (no farming by replay) | Candy Crush, Don't feed the Troll | FACT [C10] |
| Hard caps on stored lives / gifts / banked coins | Candy Crush 20 lives a day, 200 in mailbox; Royal Match Bonus Bank 5,000 coins | FACT [C10]; [ES] |
| Fixed prize pot split among winners | Lava Quest 10,000 coins shared | REPORTED [C1] |
| Rewards decay if slow | Don't feed the Troll | FACT [C10] |
| A one-time transfer cap when boosters moved to the account | Candy Crush | FACT [C10] |

For us (ESTIMATE): each new reward source goes through the simulator [MR §5 rules]; ladders are fixed and visible; cap the stacking of timed rewards; tokens from wins only, one per win (more on Hard / Super Hard), which ties the faucet to levels the simulator already models.

---

## 5. Running it offline

### 5.1 What the platforms give (FACT)

| Platform fact | Use for us | Source |
|---|---|---|
| Android has three clocks. The wall clock can be set by the user or the network and may jump backwards or forwards. `elapsedRealtime()` counts from boot, **includes deep sleep, is monotonic**, and is the recommended basis for interval timing | Measure real time between two moments of the same boot, immune to clock changes | [C21] |
| Android broadcasts ACTION_TIME_CHANGED and ACTION_TIMEZONE_CHANGED when the wall clock or zone changes | Know that a change happened | [C21] |
| `SystemClock.currentNetworkTimeClock()` (Android 13 / API 33+): time synchronised from a remote network source, **cannot be adjusted by the user**, makes no blocking network call (uses the last successful sync), throws when no network time is available; not for security use | A trusted "now" on newer phones, without our app doing any network request | [C21] |
| `Settings.Global.AUTO_TIME`: whether the UTC clock is set automatically (NITZ, GNSS, NTP); docs prefer the TimeManager API to read it. `BOOT_COUNT` (API 24+): number of boots | Know whether the user turned automatic time off; know whether `elapsedRealtime` restarted | [C22] |
| iOS posts `significantTimeChangeNotification` on a new day, a carrier time update, or a daylight-saving change | Re-evaluate the calendar on these moments | [C23] |
| iOS `ProcessInfo.systemUptime` is the time the system has been **awake** since restart (so it pauses in sleep) and is a "required reason" API: using it must be declared in the app's privacy manifest | A weaker monotonic check on iOS; must be declared in the iOS preparation | [C24] |
| HTTP: an origin server must send a `Date` header in all 2xx, 3xx and 4xx responses | Any later download (calendar file, patch pack) also brings a trusted time | [C29] |

Project facts: the Unity client already reads time through one seam, `IWallClock` (device clock with the player's time zone, whole seconds), used for lives and for the calendar day of ads; the Editor and tests use a fixed clock (FACT, `unity/Assets/Scripts/Flow/WallClock.cs` [C33]). The Android minimum is API 26 (FACT, ProjectSettings [C33]): `elapsedRealtime` and `BOOT_COUNT` exist on every supported phone; `currentNetworkTimeClock` only on Android 13+.

### 5.2 How the big games behave with the clock

- **Candy Crush's lives timer follows the device clock**, and King's own help page tells players who changed time (for example when travelling) and now see a very long wait to move the clock forward by that wait, return to the map, set it back and restart the phone (FACT [C10], updated 2026-08-10). A blunt wall-clock rule punished honest travellers.
- Candy Crush's event help says the event timer counts down while you are away "as other players are participating too" and events vanish if the account is disconnected (FACT [C10]); the fan wiki says all events need online (REPORTED, in [ES]). A Reddit excerpt describes moving the device date a week ahead to farm the Weekly Contest (REPORTED, weak [C32]).
- A Unity forum thread (Oct-Nov 2025) gives the standard advice: save the last time seen to catch backward jumps; forward jumps cannot be caught fully offline; the only tamper-proof time is from a server (own back end, an HTTP header, NTP); tier features by how reliable the time is: events and daily rewards need reliable time, life refills can live with unreliable time (REPORTED [C25]).
- Royal Match's cheating policy names bugs and third-party software, not clock changes; the penalty is losing social / tournament access and inventory (FACT [C8]).

### 5.3 An offline design for us (ESTIMATE / IDEA)

**A. The calendar is a rule, not a list.** A list of dates baked into the build runs out ([ES] already noted this). Instead: the event of a slot is computed from the date. Example: `slotIndex = number of workshop slots since a fixed epoch Monday`; `type = rotation[slotIndex mod rotation.Length]`. Adding a type to the table in an update lengthens the cycle without breaking it. A short, dated **override list** (Halloween, New Year, museum-themed dates) sits on top for the next two years. The same calendar can be printed by a CLI tool so we know the dates in advance (store cards, see 2.7).

**B. Local time, block edges at 00:00 on Monday and Friday.** The game already counts "the player's own calendar day" (DayStamp, [C33]). No one competes, so a worldwide instant buys nothing (Lightning Rush runs on local time too, REPORTED [C1]). A time-zone change shifts the edge by hours; accept it.

**C. Ids only move forward.** Each event instance has an id (the slot index). The save keeps `highestStartedEventId` and `highestClaimedDayId`. A finished or expired id never reopens; a day id is claimed once. Consequence: moving the clock forward only **borrows from the future**. The player gets next week's event early, but when real time catches up there is nothing new until the real date passes the highest id. Moving the clock back gains nothing. No penalty is needed.

**D. A cheap jump check.** At every save store `(wallClock, elapsedRealtime, bootCount)`. On resume in the same boot, if the wall clock moved more than `elapsedRealtime` moved (plus a few minutes of slack), the clock jumped forward; if the wall clock is earlier than the saved one, it jumped back. On Android 13+, if `currentNetworkTimeClock` answers, prefer it for event windows. Response to a detected jump: **no punishment and no message**. Timed rewards (unlimited booster minutes) are consumed by the monotonic clock inside a boot, so a forward jump does not burn them and a backward jump does not extend them. A life refill never waits longer than 30 minutes per missing life, whatever the clock says (the Candy Crush failure above).

**E. Keep the stakes low enough that cheating is not worth it.** Event rewards are small per event (as at the leaders, section 4.1); nothing is shared with other players, so a cheater only changes his own game. The rule from [C25]: events and daily rewards get the guarded clock; lives can live with the plain one.

**F. The existing seam is where it goes.** A "trusted now" service beside `IWallClock` (monotonic check + optional network clock), with a fixed version for the Editor and tests, keeps the calendar deterministic in tests (ESTIMATE; no code changed in this research).

### 5.4 What a later remote calendar file would add (ESTIMATE)

Path B in [MR §7.3] (a static JSON on the games site, the same road as the planned patch packs, §20):

| Adds | How | Source / note |
|---|---|---|
| Move, fix or cancel an event without an app update | The file carries overrides: `{id, type, start, end, ladder}`; the baked rule stays the default | Firebase's guidance: always ship in-app defaults; the "load for next start-up" pattern (activate what was fetched last time, fetch a new copy in the background) avoids changing the screen mid-play (FACT [C28]) |
| Trusted time | The HTTP `Date` header of the same download | FACT [C29] |
| New themed events whose art is in the build or in a patch pack | Event type ids must already exist in the client | — |
| A kill switch for a broken event | `enabled: false` per id | — |
| A/B tests, per-country timing | Only with an analytics service; Firebase Remote Config offers conditions by app version, language, audience and percentage rollouts (FACT [C28]) | Needs consent screen |
| Cost | INTERNET permission, privacy text, consent; the game sends nothing today. Ads and purchases will bring INTERNET back anyway ([MR §7.3]) | — |

The baked calendar must keep working forever without the file; the file only overrides.

---

## 6. A calendar for our game (IDEA)

Built on sections 2-5. Reward sizes are left to the economy simulator.

**Week.** Weekday block Monday 00:00 → Friday 00:00 local (4 days); weekend block Friday 00:00 → Monday 00:00 (3 days). The same split as Royal Match and Homescapes (REPORTED [C1][C13]).

**Month.** Weeks are counted by the Mondays of the month.

| Week | Weekday block (4 days) | Weekend block (3 days) | Always on |
|---|---|---|---|
| 1 "Atölye haftası" (workshop week) | Mini-game chain: A → B | Mini-game chain: C → D | Daily free try; Steady Hand streak; pass / season |
| 2 | One collection bar (e.g. "collect 60 brushes", 10-15 steps, like Book of Treasure) | One collection bar or a no-fail streak night | Same |
| 3 "Atölye haftası" | E → F | G → H | Same |
| 4 | Collection bar | Collection bar | Same |
| 5 (when the month has one) | Repeat week 2, or a seasonal override | | Same |

- That gives **8 mini-game slots a month** in two chosen weeks, several in one week, as the owner asked; a type returns about once a month with 8 types, less often as types are added.
- A **long event** (10 days, solo) can start on the first Friday of the month, as Dragon Nest does (REPORTED [C1]), for example the Archive Dig from [MR §5].
- **Rotation table** (examples from [MR §5] and this research; game design is another agent's job): varnish cleaning, spot the forgery, detail hunt, Archive Dig (pickaxes on a grid), Catalogue Match (memory pairs), "whose work?" quiz, **jigsaw of a restored painting** (Royal Match's Puzzle Break is a jigsaw mini-game, FACT [C8]), **block placement on a grid** (Homescapes' Ice Crash, REPORTED [C13]; reuses the block-puzzle work). Rule: never the same type twice in a row; never two of the same family in one week.
- **Entry.** Every win gives 1 token, Hard 2, Super Hard 3 (the leaders give extra on hard levels, FACT [C8]; the exact multipliers are a simulator question). No lives. A token is spent in the mini-game; a step may give 3 event tries. Leaving the mini-game keeps the tokens (friendlier than Royal Match). At the end, a "last chance to spend" screen; then tokens go (no carry-over, no conversion into coins unless the simulator allows it).
- **Unlock.** First mini-game event at about level 35 (day 2-3); one more family every 10-15 levels to about level 100 (ESTIMATE from section 2.8).
- **Ladder.** 5-10 steps per mini-game; mostly single boosters; coins in the middle and at the end; a **timed free booster** instead of unlimited lives every 3-4 steps; a cosmetic or a collection piece as the final prize; fixed, shown before the start, the same for everyone.
- **Showing it.** An event icon with countdown on the hall / map; a pop-up the first time with "now / later"; a small calendar card that shows this week and next week (the "show what is coming" principle of [MR §6]); an optional local notification at the start of a workshop week; App Store and Google Play event cards for workshop weeks, submitted from the printed calendar.

---

## 7. Gaps

- **Firecrawl ran out of credits mid-task.** After that, Reddit, Fandom web pages and some fan guides could not be opened (403 / 402). Royal Match facts come from the fan wiki's public API and from the Dream Games help centre; Candy Crush facts from King's help-centre public article API. The Reddit and Facebook Royal Match calendar posts were seen only as search excerpts.
- **Royal Match schedule is from fan sources** and has changed over time (two changes are recorded); the order of Pinata Party and Balloon Rise conflicts between the English and Korean wikis. The official help pages give unlock levels and rules but never durations.
- **No weekly calendar found for Candy Crush or Toon Blast**; Royal Kingdom durations not found.
- **Monopoly GO** data are from fan guides only; dice regeneration numbers disagree between sources and are not used; leftover-token rules conflict.
- **No public data** on how many mini-games a player finishes per week, completion rates per step, or the effect of a single mini-game event on retention (the only effect numbers remain the analyst estimates in [ES] / [MR]).
- **No evidence** comparing the value of a timed booster with unlimited lives or coins.
- Firebase's default minimum fetch interval and pricing pages render with JavaScript and could not be read; not used.
- Not checked: whether `currentNetworkTimeClock` can be called from Unity (through the Android Java bridge) without extra permissions; the docs list no permission and say no network call is made, but this is an ESTIMATE until tried on a phone.
- The InvestGame "LiveOps for the Holiday Season" PDF (Dec 2025) was too large to open; a claim from its search excerpt ("every 2nd or 3rd reward is time-limited") is not used.

---

## 8. Sources (all seen 2026-10-24)

- **[C1]** Royal Match Wiki (Fandom), event pages read through the wiki's public API (`api.php?action=parse&prop=wikitext`): Magic Cauldron (https://royalmatch.fandom.com/wiki/%F0%9F%94%AE_Magic_Cauldron), Balloon Rise (https://royalmatch.fandom.com/wiki/%F0%9F%8E%88_Balloon_Rise), Hidden Temple (https://royalmatch.fandom.com/wiki/%E2%9B%BA%EF%B8%8F_Hidden_Temple), Pinata Party, Book of Treasure, Propeller Madness, King's Cup, Lightning Rush, Lava Quest, Sky Race, Space Mission, Archery Arena, Team Battle, Team Treasure, Weekly Contest, Dragon Nest (same wiki, titles as listed in Category:Events). Infobox release dates 2021-2024; page edit dates vary.
- **[C2]** Royal Match Wiki, Royal Pass: https://royalmatch.fandom.com/wiki/Royal_Pass (via API).
- **[C3]** Royal Match Wiki, Lives: https://royalmatch.fandom.com/wiki/Lives (via API; page timestamp 2026-07-07).
- **[C4]** NamuWiki, Royal Match (English machine translation), search excerpt: https://en.namu.wiki/w/%EB%A1%9C%EC%96%84%20%EB%A7%A4%EC%B9%98
- **[C5]** Reddit r/RoyalMatch, "All Royal Match Events: When They Unlock and Appear", search excerpt only: https://www.reddit.com/r/RoyalMatch/comments/1l8umrt/all_royal_match_events_when_they_unlock_and_appear/
- **[C6]** Facebook group "Royal Match fan", post on event schedule, search excerpt only: https://www.facebook.com/groups/royalmatchfan/posts/947209700881005/
- **[C7]** Google Play editorial, "Here's how Events work in Royal Match" (no date): https://play.google.com/store/apps/editorial?id=mc_games_editorialevergreen_post_install_royal_match_events_now_fcp&hl=en_US
- **[C8]** Dream Games, Royal Match Help Center, base https://dreamgames.helpshift.com/hc/en/3-royal-match/ : Events and Tournaments section (33 articles) `section/23-events-and-tournaments/`; Hidden Temple `faq/235-hidden-temple/`; Magic Cauldron `faq/278-magic-cauldron/`; Puzzle Break `faq/528-puzzle-break/`; Weekly Missions `faq/798-weekly-missions/`; Balloon Rise `faq/119-balloon-rise/`; Pinata Party `faq/114-pinata-party/`; Ocean Odyssey `faq/497-ocean-odyssey/`; Propeller Rush `faq/539-propeller-rush/`; Lives `faq/5-lives/`; Using Boosters `faq/8-using-boosters/`; Cheating `faq/81-cheating/`.
- **[C9]** Dream Games, Royal Kingdom Help Center, base https://dreamgames.helpshift.com/hc/en/6-royal-kingdom-1676903479/ : Events section `section/46-events/`; Diamond Mine `faq/321-diamond-mine/`; Magic Carnival `faq/558-magic-carnival-1759768750/`; Hill Quest `faq/574-hill-quest/`. Facebook excerpt on Team Adventure: https://www.facebook.com/royalkingdom/videos/1160660968999715/
- **[C10]** King, Candy Crush Saga Help Center (read through the public Help Center article API, `candycrush.zendesk.com/api/v2/help_center/en-us/articles/<id>.json`): My special event just disappeared! (updated 2026-08-09) https://candycrush.zendesk.com/hc/en-us/articles/7390039623965 ; What extra game features are available? (2026-10-07) …/7391185946909 ; How do Unlimited Lives work? (2026-05-29) …/7332093991069 ; What are timed Boosters? (2026-06-06) …/360002352858 ; How do lives work? (2026-09-04) …/360000750878 ; How many lives can I store and use? (2026-08-03) …/5468771286429 ; Why do I have to wait so long for my next life? (2026-08-10) …/7353277804189 ; Don't feed the Troll! (2026-07-31) …/23238941854877 ; Deep Dive Dash (2026-09-25) …/37044987536157 ; Yeti's Draw (2026-08-07) …/35191885357981 ; Your boosters are now saved to your account (2026-10-15) …/38840349973277
- **[C11]** Google Play editorial, "Make the most of events in Candy Crush Saga" (no date): https://play.google.com/store/apps/editorial?id=mc_editorialevergreen_post_install_candy_crush_saga_events_fcp&hl=en_US
- **[C12]** Peak, Toon Blast Help Center, base https://peakgames.helpshift.com/hc/en/4-toon-blast/ : Events section `section/8-events-1656338033/` (39 articles); Surprise Calendar `faq/555-what-is-surprise-calendar/`; Cannon Fest leftovers `faq/610-can-i-use-my-cannonballs-when-cannon-fest-is-finished/`; Wheel of Luck `faq/323-what-is-wheel-of-luck/` (paid random multiplier on a purchase: each spin costs the purchase price; not for us); Rescue Mission `faq/390-what-is-the-rescue-mission/`.
- **[C13]** Homescapes Wiki (Fandom), Events, via API: https://homescapes.fandom.com/wiki/Events
- **[C14]** Google Play event card, Gardenscapes "New season rewards await you!" (Spooky Pass): https://play.google.com/store/apps/eventdetails/4829901340446192959
- **[C15]** MMO Culture, José Garcia, "MONOPOLY GO Events Today: Daily Schedule & Strategy Guide", published 2026-09-22, modified 2026-10-01: https://mmoculture.com/tips-guides/monopoly-go-events/
- **[C16]** AppGamer, "All Monopoly Go Events Today - Updated Daily", search excerpt (schedule of 2026-10-21): https://www.appgamer.com/monopoly-go-all-events-explained
- **[C17]** PCGamesN, Danielle Rose, "All Monopoly Go events, tournaments, dates, and rewards (October 2026)", updated 2026-10-23: https://www.pcgamesn.com/monopoly-go/events
- **[C18]** SuperCheats, "Monopoly Go Events Today Schedule" (2026-10-22), search excerpt: https://www.supercheats.com/monopoly-go-walkthrough-guide/monopoly-go-todays-events-schedule-daily-updates ; Shattered Vault events guide, search excerpt: https://shatteredvault.com/monopoly-go/events-guide/
- **[C19]** Monopoly GO Peg-E and Partners events, from web-search summaries of fan guides (not opened): Gfinity Esports Peg-E guide https://www.gfinityesports.com/article/monopoly-go-complete-peg-e-prize-drop-guide-march-7-10 ; Dexerto Peg-E tokens https://www.dexerto.com/monopoly-go/how-to-get-peg-e-tokens-in-monopoly-go-2801918 ; Destructoid partner tokens https://www.destructoid.com/monopoly-go-what-happens-to-tokens-at-end-of-partner-event/ ; Monopoly Wiki partner events https://monopoly.fandom.com/wiki/Monopoly_Go!_Partner_Events (weak).
- **[C20]** Gamigion, Taras Svityk, "Royal Match: Billion Dollar Success", 28 February (year not shown): https://www.gamigion.com/?p=16204
- **[C21]** Android Developers, `SystemClock` reference (class overview; `currentNetworkTimeClock`, API 33; `currentGnssTimeClock`, API 29): https://developer.android.com/reference/android/os/SystemClock
- **[C22]** Android Developers, `Settings.Global` reference (`AUTO_TIME`, `AUTO_TIME_ZONE`, `BOOT_COUNT`): https://developer.android.com/reference/android/provider/Settings.Global
- **[C23]** Apple Developer, `UIApplication.significantTimeChangeNotification` (read through the documentation JSON): https://developer.apple.com/documentation/uikit/uiapplication/significanttimechangenotification
- **[C24]** Apple Developer, `ProcessInfo.systemUptime` (required-reason API note): https://developer.apple.com/documentation/foundation/processinfo/systemuptime
- **[C25]** Unity Discussions, "How can I detect the passage of time even when the game is closed?", 2025-10-29 to 2025-11-03: https://discussions.unity.com/t/how-can-i-detect-the-passage-of-time-even-when-the-game-is-closed/1692831
- **[C26]** Apple Developer, In-App Events: https://developer.apple.com/app-store/in-app-events/
- **[C27]** Play Console Help, Promotional content: https://support.google.com/googleplay/android-developer/answer/12932541?hl=en
- **[C28]** Firebase, Remote Config loading strategies: https://firebase.google.com/docs/remote-config/loading ; overview: https://firebase.google.com/docs/remote-config
- **[C29]** RFC 9110, HTTP Semantics, section 6.6.1 Date: https://www.rfc-editor.org/rfc/rfc9110.html#field.date
- **[C30]** Android Developers, Notification runtime permission: https://developer.android.com/develop/ui/views/notifications/notification-permission
- **[C31]** Unity, Mobile Notifications package 2.4 manual: https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/index.html
- **[C32]** Reddit r/candycrush, "I just saw someone climb 150 levels in a span of 3 hours in weekly contest", search excerpt only: https://www.reddit.com/r/candycrush/comments/uny05l/
- **[C33]** This repository: `unity/Assets/Scripts/Flow/WallClock.cs` (IWallClock seam); `unity/ProjectSettings/ProjectSettings.asset` (`AndroidMinSdkVersion: 26`); `docs/OVERVIEW.md` line 79 (lives rule); `docs/GAME_DESIGN.md` §19, §25.
- **[ES]** `out/research/monetization-2026-10/events-social.md` (2026-10-23). **[MG]** `out/research/monetization-2026-10/minigames.md` (2026-10-23). **[MR]** `docs/research/monetization-and-retention.md` sections 5-7.
