# Live-ops events, streaks and social features in top match-3 games

Research for the museum / painting-restoration match-3 (Unity, offline, no server, no INTERNET permission).
Written 2026-10-23. Every source below was opened (or seen in a search excerpt, where marked) on **2026-10-23**.

## How to read this file

- **FACT**: seen on the source itself (the game's own help centre, store page, or the original report page).
- **REPORTED**: from a secondary source (fan wiki, analyst blog, newsletter, LinkedIn, Reddit). The number may be right, but nobody outside that source checked it.
- **ESTIMATE**: my own inference. No source says it directly.
- **IDEA**: a design proposal for this game. Not a fact about any game.
- `[S12]` points to the source list at the end (URL, publisher date, seen date).

Note on the brief: "Star Tournament" is a **Toon Blast** event, not a Royal Match or Royal Kingdom one [S58]. It is covered under Toon Blast.

---

## 1. Short summary (the ten things that matter most)

1. **Events are the main engine, not the levels.** Royal Match runs about 11 events at once midweek and about 20 on Fridays and Mondays, rotating 27 formats over 14 days; Royal Kingdom runs about 8 to 14 and rotates 19 formats (REPORTED, AppMagic calendar data, [S33]). Sensor Tower calls Royal Match a game whose money comes from live ops and says revenue spikes on weekends because of event-driven spending, not because more people play [S34] (REPORTED).
2. **Almost every event is "win levels you would play anyway, collect a token".** Cups, shields, books, keys, crowns, trains, gold balls: the board barely changes; the event changes what a win is worth. FACT across [S2]-[S26], [S35]-[S41], [S43]-[S55], [S58]-[S66].
3. **Five event families** cover nearly everything: streak, competitive (leaderboard / race), collaborative (team or partner), collection (fill a bar), and mini-game (spend tokens in a small side game) (REPORTED, [S33]).
4. **Streaks are everywhere and come in two shapes**: a booster streak (win in a row, start the next level with free boosters, lose it all on a fail: Royal Match Butler's Gift, Toon Blast Crown Rush) and a streak jackpot (win 7 in a row on the first try within 24 hours, split a prize with other winners: Lava Quest, Dungeon Escape, Dragon's Treasure, Candy Royale). FACT [S9][S36][S60][S65][S43]; Butler's Gift details REPORTED [S30].
5. **Top games already use simulated rivals.** Royal Match's Lava Quest help page says rivals are "simulated scenarios based on historical data" (FACT [S9]); the Candy Crush wiki says Candy Royale players race recordings of other players (REPORTED [S44]). This is the strongest precedent for running competitive events offline with bots.
6. **Teams are table stakes in the top games but need a server.** Royal Match teams: up to 50 members, lives sharing, chat, team events from level 21 (FACT [S16]); Candy Crush added Candy Teams with Team Clash and Team Quest, rolled out gradually (FACT [S45][S49][S51]); Toon Blast teams from level 20 with life requests (FACT [S61]).
7. **Monthly pass with free and paid rewards is standard and priced at $9.99 in the Dream Games titles.** Royal Pass $9.99 and Kingdom Pass $9.99 on the US App Store (FACT [S27][S42]); both add lives 5 to 8, a gold frame, a gift to teammates and a coin "Bonus Bank" capped at 5,000 (FACT [S7][S38]).
8. **Piggy bank exists in Playrix games as "Gold Reserve" at $2.99** (FACT, US App Store, Homescapes and Gardenscapes [S71][S72]); one source says it is Gardenscapes' 3rd best-selling iOS US purchase per Sensor Tower (REPORTED [S73]).
9. **Measured evidence of effect is thin and mostly pre/post analyst estimates.** Best public set: after Royal Match added Royal Pass, revenue in the next 30 days was up 31%; Sky Race +12%; Lightning Rush +9%; Team Treasure +2% (REPORTED, GameRefinery data via Naavik 2022, other changes not excluded [S30]). Toon Blast's Shield Clash showed a 13% revenue impact (REPORTED [S68]).
10. **"Good" retention for a puzzle game** (public data): GameAnalytics puzzle **median** in 2024 was about D1 20%, D7 4.5%, D28 1.2% (FACT [S75]); AppsFlyer-based genre averages (Q3 2022, marketed apps) were Puzzle D1 31.85%, D7 12.18%, D30 5.35% and Match D1 32.65%, D7 13.98%, D30 7.15% (REPORTED [S77]). The top-quartile D1 across all genres is 31-33% on iOS and 25-27% on Android (FACT [S75]).

---

## 2. Game by game

### 2.1 Royal Match (Dream Games)

**Event catalogue.** The official help centre lists 27 event articles (FACT [S1]). The ones the brief asked about, plus the 2025-2026 additions:

| Event | How it works | Players / needs others | Length / cadence | Rewards | Tag | Source |
|---|---|---|---|---|---|---|
| King's Cup | Collect cups by beating levels; extra cups from Hard / Super Hard / Extremely Hard levels; rank by cups | 50 players (you + 49); online required; from level 25 | Time-limited; Reddit post says Monday to Thursday | By rank | FACT (cadence REPORTED) | [S2] [S29] |
| Royal League | Opens only when you finish all available levels and area tasks; collect crowns by beating "rounds"; Gold, Emerald, Diamond stages with step rewards | 20 players | Ends when new levels ship; earlier joiners get better rank rewards | Step rewards plus rank rewards | FACT | [S3] |
| Team Battle | Team members collect shields from wins; team ranked against 19 other teams | 20 teams; team needs at least 10 members; level 23; online | Time-limited | Same reward to every member if team is top 5; top 3 contributors get extra | FACT | [S4] |
| Team Tournament | Wins give tokens, tokens convert to lances; team rank by lances | 20 teams; at least 10 members; level 23; online | Time-limited | Top 5 teams rewarded; top 3 members extra; Knight and Grand Knight titles | FACT | [S17] |
| Team Treasure | Team pools ship wheels to open 3 chests | Team required; level 23; online | Time-limited | Same reward per member per chest; top 3 contributors extra | FACT | [S18] |
| Sky Race | Race on how fast you beat levels | 5 players (you + 4); level 51; online | Time-limited | By rank (not listed). Naavik 2022: top 3 who win 15 levels fastest | FACT (15 levels REPORTED) | [S5] [S30] |
| Lightning Rush | Collect lightning bolts by firing Light Balls in levels; several steps, only each step's winner advances | 5 players; level 31; online | Fan wiki: 1 hour (30 min in test groups). Unlimited lives the first time you start a step each day (FACT) | Winner of each step only. Naavik 2022: "all or nothing", 5 boosters | FACT (length REPORTED) | [S6] [S28] [S30] |
| Space Mission | Beat consecutive levels on first try before others; 3 stages, group shrinks each stage, one winner per stage | Level 51; online | Time-limited | Not listed. GameRefinery: streaks of 3, 5 and 7 across phases | FACT (3/5/7 REPORTED) | [S10] [S69] |
| Lava Quest | Beat 7 consecutive levels on first try | 100 players; level 66; online; **rivals are simulated from historical data of players who played the same 7 levels** | 24 hours; if you fail, new attempt after 30 minutes | 10,000 coins split equally among all who finish (an older UserWise newsletter said faster finishers got a bigger share; the help page now says equal split) | FACT (older rule REPORTED) | [S9] [S85] |
| Ancient Adventure | Beat levels on first try; failing restarts from the beginning (immediately) | Level 66 | 24 hours | Not listed | FACT | [S26] |
| Book of Treasure | Collect "books" (red pieces) in levels; several steps, each with a reward; grand prize at the end; losing or quitting a level loses its books | Solo; level 28 | Time-limited | Step rewards plus grand prize | FACT | [S8] |
| Archery Arena | Collect targets from wins; **multiplier rises with each win without a fail, resets on a fail**; step rewards plus final rank reward | 50 players; level 56; online | Time-limited | Step and rank rewards | FACT | [S25] |
| Champions Clash | 3-round knockout bracket, 1-v-1 each round; score Sparks from Propellers / Rockets / TNT / Light Balls, first attempt only | 8 players; level 96; online; unlimited lives on first play each day | Time-limited | Winner rewarded | FACT | [S12] |
| Robot Rivals | 1-v-1; drain the rival robot to zero by winning levels; multiple rounds | 2 players; level 51; online | Time-limited | Not listed | FACT | [S11] |
| Royal Cup (2026) | 100 countries; Qualifiers within your country (top 100,000 advance), Knockouts vs 99 players of other countries, 8-player Finals; countries ranked by Gold Balls | Level 61; online | Time-limited; added in the Jun 9 update | Rewards per round won; all contributors of the winning country | FACT | [S22] [S27] |
| Duke's Treats (2026) | Daily event with 4 stages; collect treats by beating levels | Solo; level 46 | 24 hours, timer starts when you open the game; daily | Reward per stage | FACT | [S21] [S27] |
| Train Journey | Co-op: collect Trains from wins, build 9 rails with 3 partners (3 each) | Partners: friends, suggested players or team members; level 76 | Time-limited | Rewards at 3, 6, 9 rails; grand prize; special name style | FACT | [S13] |
| Dragon Nest | Co-op: raise 4 dragons, each with one partner; 3 stages per dragon; Magic Balls from wins | Partners as above; level 76 | Time-limited | Stage rewards; grand prize; special frame | FACT | [S14] |
| Merge Smith (2026) | **Mini-game**: collect anvils from levels, generate items, merge to discover new objects | Solo; level 85 | Time-limited; one last chance to spend anvils at the end | Rewards for new objects | FACT | [S23] [S27] |
| King's Tales (2026) | **Mini-game / map**: spend Energy Crystals (from wins or bought) to clear obstacles and finish 12 tasks | Solo; level 76 | Time-limited | Extra rewards at tasks 3, 6, 9; grand prize; special name style | FACT | [S24] [S27] |
| Weekly Missions (2026) | Listed in the Sep 28 update as "recurring rewards"; no help article found | Unknown | Weekly | Unknown | FACT (name only) | [S27] |

**Royal Pass (battle pass).** 30 steps per month, starts at the start of each month; keys from beating levels (extra keys from hard levels); you must buy the pass to get the special rewards; from level 37. Paid perks: max lives 5 to 8, gold frame and name, a gift sent to teammates. After all steps, a **Bonus Bank** stores 100 coins per 10 keys up to 5,000 coins. Unclaimed rewards go to the inventory at month end. FACT [S7]. Price on the US App Store: **$9.99** (FACT [S27]). Naavik (2022) reported that competitors charged less at the time (Homescapes $4.99, Lily's Garden $5.99, Candy Crush Saga $6.99) (REPORTED [S30]).

**Win streak (Butler's Gift).** Not described in the official help centre. Naavik (2022): a bar on the win screen fills over up to 3 wins, and each streak step places up to 3 power-ups on the board at the start of the next level; a fail resets it (REPORTED [S30]). A Medium analysis says it unlocks at level 32 and its top tier is 1 Rocket + 1 Propeller + 1 TNT + 1 Light Ball (REPORTED, weak [S86]).

**Teams.** Join or create at level 21 (after beating level 20); up to 50 members; open or closed teams; team chat; send lives to teammates who request them; a weekly "Helps" counter; leadership passes to a co-leader after 28 days of leader inactivity; internet required. FACT [S15] [S16]. Lives: 5 max (8 with Royal Pass), 1 life back every 30 minutes, teammates can send lives (FACT [S19]).

**Card collection.** From level 41; time-limited; cards from friends, events, tournaments and chests; sets give rewards (FACT [S20]). Sensor Tower (May 2026): the "Culinary Collection" ran for two months, and event grand prizes include guaranteed-new card packs that become more valuable as the album fills (REPORTED [S34]). Naavik (Nov 2025): album collections became endlessly repeatable, and Royal Match reportedly cut coin rewards from level wins by about 30% to balance the economy (REPORTED, Reddit-based [S79]).

**Cadence.** Tom Storr (AppMagic Live Ops Calendar): about 11 events live Tue-Thu, about 20 on Fri and Mon (when weekday and weekend sets overlap), about 11 on Sat-Sun; 27 formats over 14 days; six events that need consecutive first-try wins (REPORTED [S33]). Sensor Tower (May 2026): the week is layered into short-term urgency (streaks, sprints, rolling offers), mid-term social / competitive events, and long-term collections and passes; social events are timed to weekends (REPORTED [S34]). A LinkedIn post citing an AppMagic report says Royal Match runs over 150 live-ops events per month and that a $4.99 "Journey Offer" on the fail screen (added Sep 2025) accounts for 8% of payments and 11% of revenue (REPORTED, weak; the AppMagic report itself was not seen [S84]).

**Scale (context).** Royal Match grossed over $1.4B in IAP in 2025, with 2025 IAP ARPDAU of $0.17 vs $0.11 for Candy Crush Saga, and higher monthly revenue than Candy Crush with about half the monthly users (REPORTED, Sensor Tower via Naavik [S70]).

### 2.2 Royal Kingdom (Dream Games)

The official help centre lists 21 event articles (FACT [S35]). Royal Kingdom mirrors Royal Match with fewer formats and an extra "Dark King" boss layer (Kingdom and Dark Kingdom levels give extra tokens in many events, FACT [S38][S39]).

| Event | How it works | Players / needs others | Length | Tag | Source |
|---|---|---|---|---|---|
| Grand Tournament | Leaderboard competition (helmets) | 50 players; level 66; online | Time-limited | FACT | [S35] |
| Weekly Contest | Rank by number of levels won in the week; hard levels give no extra points | 10 players; level 46; online | One week, ends Monday, next starts at once | FACT | [S40] |
| Flying Contest | Leaderboard competition | 50 players; level 46; online | Time-limited | FACT | [S35] |
| Gem Hunt | Competition | 5 players; level 86; online | Time-limited | FACT | [S35] |
| Power Rush | Short power-up competition (Royal Kingdom's Lightning Rush) | Other players; level 66; online | Time-limited | FACT (pairing REPORTED [S33]) | [S35] |
| Island Race | Three stages against other players | Level 86; online | Time-limited | FACT | [S35] |
| Kingdom Duels | 3-round knockout bracket | 8 players | Time-limited; added May 18 update | FACT | [S35] [S42] |
| Champions Arena | Opens after the available levels are cleared (Royal League equivalent) | 10 players | Time-limited | FACT | [S35] |
| Dungeon Escape | Beat 7 levels in a row on first try; qualifiers split the prize | Others who took the same challenge; level 56; online | 24 hours | FACT | [S36] |
| Team Clash | Team members collect shields; rank vs other teams; every member who added at least 1 shield can claim | 10 teams; level 26; online | Time-limited | FACT | [S39] |
| Team Adventure | Team members earn chest rewards together | Team | Time-limited | FACT | [S35] |
| Pearls of Atlantis / Hidden Path | Co-op with partners: save 4 animals / remove 4 spells | Partners; level 76 | Time-limited | FACT | [S35] |
| Canvas Quest | 5 stages of quests (earn coins, break ice in a partner event, beat Dark King levels, win on first try); quests give **brushes**; brush totals open chests | Level 101 | Time-limited | FACT | [S37] |
| Diamond Mine, Electro Blitz, Cube Blast, Magic Carnival, Hill Quest | Multi-stage collection or mini-game events | Solo; levels 36-101 | Time-limited | FACT | [S35] |
| Richard's Journey | Monthly; stages unlocked by beating a set number of levels; each stage played once | Solo | Monthly | FACT | [S41] |
| Spooky Carnival | Seasonal: match pairs to break orbs | Solo | Seasonal | FACT | [S42] |

**Kingdom Pass.** Monthly, from level 31; keys from wins; buying it unlocks extra rewards; paid perks: lives 5 to 8, gold name and frame, gift to teammates; Bonus Bank up to 5,000 coins, paid out at the start of next month (FACT [S38]). US price **$9.99** (FACT [S42]).
**Other meta.** Card collections every season (Spooky Collection, Academy Collection with an "Owl Token"); an "Emote Album" that collects emotes as you play (Jun 15 update) (FACT [S42]).

### 2.3 Candy Crush Saga (King)

| Feature | How it works | Players / needs others | Tag | Source |
|---|---|---|---|---|
| Candy Royale (streak jackpot) | Beat a set number of levels in a row on the first try without losing a life; everyone who finishes shares the prize; the only finisher takes all. Screenshot shows 7 levels for 500 Gold Bars. Cannot be opted out of. | Group of players | FACT | [S43] |
| Candy Royale (wiki details) | 100 players on the same level, 24 hours, launched 27 Jan 2022; **players race recordings of other players, not live players**; levels needed varied from 6 to 9 over time | Recorded players | REPORTED | [S44] |
| Episode Race | Race through the next episode (group of levels) against players who reached it at the same time; fastest win boosters and Gold Bars; may wait until enough similar-level players are found | Matchmaking | FACT | [S53] |
| Weekly Contest / Weekly Race | Who passes the most levels this week, among connected friends | Friends | FACT | [S52] |
| Champions Race | Top 3 of the Weekly Race get a Golden Ticket to a race of top players | Other top players | FACT | [S55] |
| Leaderboard events | Time-limited point competitions, sometimes in rounds with qualifying positions; points may depend on level type | Other players | FACT | [S46] |
| Season Pass | Free Pass and Gold Pass; rewards per tier for winning levels; buying Gold later grants all earlier Gold rewards retroactively; "Bonus Deals" exchange leftover pass tokens for boosters and unlimited lives | Solo | FACT | [S48] |
| Daily Stamps (login streak) | Win a level to get one stamp per day; a full stamp card opens a reward chest (timed boosters, Color Bombs, more) | Solo | FACT | [S47] |
| Daily Booster Wheel | Spin daily for free rewards | Solo | FACT (store text, search excerpt) | [S56] |
| Chocolate Box (quests) | One task at a time; each task can be swapped up to 3 times; some tasks only count on new levels | Solo | FACT | [S50] |
| Win streak | Endless reward track similar to the saga map, with bundles at milestones | Solo | REPORTED | [S32] |
| Candy Teams | Join or create at level 20 via the Social tab; public or private; inactive members removed after 30 days; leaving during a team event loses progress; team events may not appear if you have fewer than 4 Candy Crush friends; internet required; rolled out gradually | Team | FACT | [S45] |
| Team Clash | Team points from level wins; top teams rewarded; optional personal milestone track independent of team rank; **offline play does not sync and cannot be claimed** | Team | FACT | [S49] |
| Team Quest | Co-op: 3 shared milestones; hard levels and first-try wins give more points; top contributor gets extra | Team | FACT | [S51] |
| Candy Pins (collection) | From level 51; Pin Packs from chests and events; sets of 9 pins; 5 rarities; duplicates turn into Pin Sparks for Spark Chests; limited-time Pin Swap trading of duplicates; completing all sets gives a permanent Trophy Pin shown in a **Trophy Room** | Solo (trading needs others) | FACT | [S54] |
| Events need online | The wiki says all events require online functionality | | REPORTED | [S57] |

### 2.4 Toon Blast (Peak, Zynga)

| Feature | How it works | Players / needs others | Tag | Source |
|---|---|---|---|---|
| Crown Rush (booster streak) | Stage 1: 1 Rocket + 1 Bomb at the start of the next level after a win; stage 2: 2 + 2; stage 3: 3 + 3; a fail resets to the start; from level 25 | Solo | FACT | [S60] |
| Star Tournament | Collect stars from levels; 50-player groups of similar level; from level 25; **offline play counts up to 30 stars** | 50 players | FACT | [S58] |
| Shield Clash | Shields from wins; winning without failing raises a score multiplier; a fail resets it; 50 players of similar level; **offline cap 100 shields** | 50 players | FACT | [S64] |
| Shield Clash (impact) | Multiplier reaches 10x after five consecutive wins; 13% revenue impact | | REPORTED | [S68] |
| Dragon's Treasure (streak jackpot) | 7 levels in a row on first try; 100 players; grand prize split among finishers; 24 hours; new try 30 minutes after a fail; from level 50; online | 100 players | FACT | [S65] |
| Weekly Contest | 10 players (may include friends), weekly from Monday, top 3 rewarded, must win at least one level; from level 75 | 10 players | FACT | [S63] |
| Team Tournament | Team stars from levels; top teams get a large reward | Teams | FACT (snippet) | [S59] |
| Team Tournament (wiki) | 1st place 50,000 coins split by 50, so 1,000 per member; one of two weekly team events, the other being the Team Chest | Teams | REPORTED | [S67] |
| Teams and lives | Join or create at level 20; teammates tap "Help" on a life request; up to 5 different teammates can each send 1 life; Helps reset weekly (Sunday night); top 1,000 teams on the leaderboard; leader lost after 21 days inactive | Team | FACT | [S61] |
| Toon Pass | 1 badge per level won; free rewards plus **Golden** pass (lives 5 to 8, gold frame and name, team gift, Golden Grand Prize) and **Diamond** pass (lives 5 to 10, diamond frame, Diamond Grand Prize); each can be bought once per event, both can be bought; from level 30 | Solo | FACT | [S62] |
| Temple Guardians (co-op mini-game) | 10 days; 3 partners; 3 totems x 3 stages; Temple Balls from levels are spent in a Temple mini-game; stage rewards shared with partner; grand prize and frame; from level 40 | Partners | FACT | [S66] |
| Mini-games | Cannon Fest and Space Miner (from level 35, tap to use collected cannonballs / pickaxes on boards), Surprise Calendar (tap falling gifts for coins), Rescue Mission (save Bruno in a time limit) | Solo | FACT (snippets) | [S59] |

### 2.5 Playrix (Homescapes, Gardenscapes)

| Feature | How it works | Tag | Source |
|---|---|---|---|
| Story events every few weeks | Homescapes ships two named story events per major update (e.g. "Night in the Castle", "Treasure Hunt", "Game of Survival" where players have three lives) plus an ongoing story chapter (FACT). Major updates in 2026 version notes: Feb, Mar, Apr, Jun, Jul, Aug, Oct, so a new pair about every 6 weeks (ESTIMATE from those dates) | FACT / ESTIMATE | [S71] |
| Expeditions | Gardenscapes: limited-time story maps (e.g. "Polar Station Mystery", "Sands of Oblivion") played with a currency earned from levels; Naavik credits Playrix with popularising this format | FACT [S72]; REPORTED [S70] |
| Seasonal pass | Gardenscapes "Spooky Pass": complete levels, collect keys, unlock rewards | FACT | [S72] |
| Card collections | Gardenscapes "Magic Collection", "Fair Collection", "Movie Collection": complete card sets for rewards and a profile badge | FACT | [S72] |
| Piggy bank ("Gold Reserve") | US App Store lists "Gold Reserve" at **$2.99** in both Homescapes and Gardenscapes | FACT | [S71] [S72] |
| Gold Reserve detail | Part of the gold from match-3 levels goes into the reserve; it can be bought once it reaches a set amount; 3rd best-selling IAP in Gardenscapes (iOS US, Sensor Tower) | REPORTED | [S73] |
| Race events | Homescapes "Knight's Race" (group at the same starting level races to a target level), Gardenscapes "Airship Race" (medals from wins) | REPORTED | [S69] |
| Merge mini-game | Gardenscapes "Secret Ingredient": energy from levels, merge ingredients into dishes, tokens fill a reward meter; first seen as "Austin's Childhood" in Homescapes; Playrix reuses mini-games across games | REPORTED | [S74] |
| Teams | Gardenscapes players mention team rewards and shrinking teams in App Store reviews (Nov 2024) | REPORTED (weak) | [S72] |

---

## 3. Cross-game patterns by mechanic

### 3.1 Streaks

| Type | Examples | Rule | Offline? |
|---|---|---|---|
| Booster streak | Butler's Gift (REPORTED [S30]), Crown Rush (FACT [S60]), Candy Crush win streak (REPORTED [S32]) | Each consecutive win adds free boosters at the start of the next level, 3 tiers, reset on fail | Yes (ESTIMATE): purely local state |
| Score multiplier streak inside a competition | Archery Arena (FACT [S25]), Shield Clash (FACT [S64], 10x after 5 wins REPORTED [S68]) | Points per win grow while you do not fail | Multiplier yes; ranking needs rivals |
| Streak jackpot (first-try chain) | Lava Quest (FACT [S9]), Dungeon Escape (FACT [S36]), Dragon's Treasure (FACT [S65]), Candy Royale (FACT [S43]), Ancient Adventure (FACT [S26]) | 7 wins in a row on first try within 24 h, prize split among finishers, retry after 30 min | Yes with simulated rivals, as Lava Quest already does (FACT [S9]) |
| Daily login / daily win streak | Candy Crush Daily Stamps (FACT [S47]), Daily Booster Wheel (FACT [S56]), Royal Match Duke's Treats (FACT [S21]) | One stamp per day for a win; full card opens a chest | Yes, on the device clock (ESTIMATE) |
| Quests / missions | Candy Crush Chocolate Box (FACT [S50]), Royal Kingdom Canvas Quest (FACT [S37]), Royal Match Weekly Missions (FACT, name only [S27]) | A short list of tasks with swap option and rewards | Yes (ESTIMATE) |

Design advice from Deconstructor of Fun (REPORTED [S32]): mix high and low stakes; use free power-ups from streaks to teach power-up use before asking players to buy them; always show the next milestone; a streak-protection item (like Duolingo's freeze) may cut revenue from end-of-level offers.

### 3.2 Chests and piggy banks

- **Chests at milestones**: step chests in collection events (Book of Treasure [S8], Team Treasure [S18]); stamp-card chest (Candy Crush [S47]); Spark Chests for duplicate pins (Candy Crush [S54]). FACT.
- **Piggy bank / bank**: Gold Reserve $2.99 (Playrix, FACT [S71][S72]); Royal Pass / Kingdom Pass Bonus Bank, 100 coins per 10 keys, max 5,000, only for pass buyers (FACT [S7][S38]).
- Note: Candy Crush's "Jelly Jar" is a board blocker, not a piggy bank (FACT, King help centre article 4402586471569).

### 3.3 Teams and clans

| Game | Team size | Unlock | Lives sharing | Team events | Source |
|---|---|---|---|---|---|
| Royal Match | up to 50 | level 21 | yes, on request | Team Battle, Team Tournament, Team Treasure; team gift from Royal Pass buyers | FACT [S4][S16][S17][S18][S7] |
| Royal Kingdom | not stated | level 26 for Team Clash | not checked | Team Clash (10 teams), Team Adventure | FACT [S39][S35] |
| Candy Crush Saga | not stated | level 20 | not stated in the Teams article | Team Clash, Team Quest | FACT [S45][S49][S51] |
| Toon Blast | not stated | level 20 | yes, up to 5 teammates x 1 life per request | Team Tournament, Team Chest (wiki), Sky Pirates | FACT [S61][S59]; REPORTED [S67] |

All team features require an internet connection (FACT where stated: [S4][S16][S17][S18][S39][S45][S49]). Candy Crush's help page says offline contributions to Team Clash milestones do not sync and cannot be claimed (FACT [S49]).

### 3.4 Leaderboards and tournaments

Group sizes used by the leaders: 2 (Robot Rivals), 5 (Sky Race, Lightning Rush, Gem Hunt), 8 (Champions Clash, Kingdom Duels), 10 (Weekly Contest in Royal Kingdom and Toon Blast), 20 (Royal League, Team Battle), 50 (King's Cup, Archery Arena, Star Tournament, Shield Clash, Grand Tournament), 100 (Lava Quest, Dragon's Treasure, Candy Royale). FACT across the sources in section 2. Groups are matched to players of similar level (FACT [S58][S64][S53]).

Naavik (Nov 2025) sees a trend toward pairing every leaderboard with a **personal milestone track**, so non-winners still earn rewards and prize cost stays under control (REPORTED [S79]); Candy Crush's Team Clash milestone track is one example (FACT [S49]).

### 3.5 Bots and simulated rivals

- **Royal Match Lava Quest**: rivals are simulated from historical data of other players on the same 7 levels (FACT [S9]).
- **Candy Crush Candy Royale**: players race recordings of other players (REPORTED, fan wiki [S44]).
- **Royal Match Sky Race**: Naavik (2022) wrote that opponents may be bots, real players or a mix and that players cannot tell (REPORTED, speculative [S30]).
- **Toon Blast**: Star Tournament and Shield Clash accept offline play up to a cap (30 stars, 100 shields) and sync later (FACT [S58][S64]), so part of the competition already tolerates offline play.
- No source found that states whether King's Cup, Sky Race, Lightning Rush or the 50-player Toon Blast groups use bots (gap).

---

## 4. Retention benchmarks: what "good" looks like

| Data set | Segment | D1 | D7 | D28 / D30 | Tag | Source |
|---|---|---|---|---|---|---|
| GameAnalytics 2025 report (data Jan-Dec 2024, about 11,600 games) | Puzzle, monthly **median** | 19.66%-20.74% | 4.27%-4.79% | D28 1.09%-1.26% | FACT | [S75] |
| same | Casual, monthly median | 20.02%-21.11% | 3.14%-3.46% | D28 0.65%-0.75% | FACT | [S75] |
| same | All genres, **top 25%** | 26.48%-27.69% (iOS 31-33%, Android 25-27%) | about 7%-8% | 75% of games below 3% D28 | FACT | [S75] |
| GameAnalytics Q1 2024 via GameDev Reports | All genres, median | 22.91% | 4.2% | D28 0.85%; puzzle had the highest D7 of any genre | REPORTED | [S76] |
| AppsFlyer, Q3 2022 (via Mistplay, Oct 2023) | Puzzle, average | 31.85% | 12.18% | D30 5.35% | REPORTED | [S77] |
| same | Match, average | 32.65% | 13.98% | D30 7.15% | REPORTED | [S77] |
| GameAnalytics H1 2019 (old) | Puzzle D1 median / top 25% | 29.9% / 37.4% | Casual-Puzzle D7 median 9.7%, top 15.8% | Casual-Puzzle D28 median 4.2%, top 8.2% | REPORTED (search excerpt of the PDF) | [S78] |
| Naavik (Nov 2025), Sensor Tower iOS US | Top puzzle titles (Royal Match, Candy Crush, Toon Blast and others) | | | D90 is about 25% of D1, average about 38 minutes per day | REPORTED | [S79] |

**Why the numbers disagree (ESTIMATE).** GameAnalytics pools every game that uses its SDK, including many small unmarketed games, so medians are low. AppsFlyer data comes from apps that pay for attribution, mostly marketed games with paid users, so averages are higher. Neither publishes match-3 leaders separately.

**Target band for this game (ESTIMATE).** For a small casual puzzle game, D1 at or above 30%, D7 at or above 10%, D28/D30 at or above 4% would sit in the top quartile of the GameAnalytics data and near the AppsFlyer puzzle average. The big match-3 hits are above that, but no public source gives their cohort retention (gap).

---

## 5. What the evidence says

Strength scale: **A** = first-party measured data (A/B test or company figures); **B** = analyst estimate from market data (Sensor Tower, AppMagic, GameRefinery), usually before/after a release, other changes not excluded; **C** = opinion, survey, or method not shown.

| Claim | Evidence | Strength | Tag | Source |
|---|---|---|---|---|
| A monthly pass lifted revenue | Royal Match revenue +31% in the 30 days after Royal Pass (June 2021); D30 retention +2% that month (no source given for the retention figure) | B (revenue), C (retention) | REPORTED | [S30] |
| Competitive mini-events lift revenue a little each | Sky Race +12%, Lightning Rush +9%, Piñata Party +6%, Team Treasure +2%, Endless Treasure +20% (30 days after each launch) | B | REPORTED | [S30] |
| Variety in levels may help retention | Royal Match D14 retention up almost 3% month over month after "King's Nightmare" timed levels; downloads down 21% in the same window | B/C | REPORTED | [S30] |
| Live ops as a whole doubled monetisation | Royal Match revenue per download up about 100% in a year of adding events (Sensor Tower) | B | REPORTED | [S30] |
| Streak multipliers can lift revenue | Toon Blast Shield Clash: 13% revenue impact; puzzle competitions otherwise have the weakest median impact in casual | B | REPORTED | [S68] |
| Mini-games may lift revenue | Gardenscapes Secret Ingredient: revenue +13% after release, but a downloads push and two passes ran at the same time; sponsored newsletter | C | REPORTED | [S74] |
| Race events are mainstream | 43% of match-3 games in the US top-grossing 200 run race events; races are not sold directly but push spending on moves, lives and boosters | B (adoption), C (effect) | REPORTED | [S69] |
| Weekend revenue comes from events | Royal Match weekend IAP spikes come from higher conversion during events, not more players or longer play | B | REPORTED | [S34] |
| Fail-screen offers are a big share | Journey Offer ($4.99, 75% off, on fail screen) = 8% of payments, 11% of revenue | C (report not seen) | REPORTED | [S84] |
| Piggy bank sells | Gold Reserve is Gardenscapes' 3rd best-selling iOS US IAP | B | REPORTED | [S73] |
| Streaks build habit (non-game, first party) | Duolingo: learners who reach a 7-day streak are "3.6 times more likely to complete their course" (correlation); a new streak animation raised day-7 retention of new learners by 1.7%; allowing two streak freezes raised daily active learners by 0.38% | A (but not a game, and the 3.6x is correlation) | FACT | [S81] |
| Daily rewards have a downside | Survey of 178 players: daily logins and repeatable challenges motivate some players but feel like a chore or fear of missing out to others | C (survey, peer-reviewed) | FACT | [S82] |
| Difficulty plus streaks frustrates | Timed events and win streaks are where players feel difficulty-driven frustration most; difficulty-driven monetisation is Royal Match's most common complaint | C | REPORTED | [S80] [S34] |
| Social ties keep players | Socially connected players stay longer, but players increasingly refuse to connect accounts; guild systems replace friend lists (2017 opinion) | C | REPORTED | [S83] |
| Events fill the gap when lives run out | Deconstructor of Fun (2021): more event types increase session length and should reduce churn when a light meta leaves players with nothing to do | C | REPORTED | [S31] |
| Daily jackpot replaces login bonus | Naavik: a short daily first-try event with a big prize (Lava Quest style) now replaces simple login bonuses and turns new players into daily regulars within the first week | C | REPORTED | [S79] |

**Bottom line (ESTIMATE).** No public source isolates the retention effect of a single event type with a controlled test in a match-3 game. What the evidence does support: (1) the top games all converged on the same five event families and a monthly pass, which is strong revealed-preference evidence that these pay off; (2) each single event adds a few percent of revenue at most, while the pass and the overall event density add more; (3) streak and daily mechanics help habit but can feel like a chore if the rewards are mandatory or punishing.

---

## 6. Ideas that fit a museum and painting-restoration theme

**Everything in this section is an IDEA for this game, not a fact about any game.** Each idea names the top-game precedent it copies. "Tokens" means a per-event item earned by winning normal levels, as in every event above.

| # | IDEA | Theme wrapper | How it plays | Precedent |
|---|---|---|---|---|
| I1 | **Steady Hand** booster streak | The restorer's hand gets steadier with each clean win | 3 tiers: after 1 / 2 / 3 consecutive wins, the next level starts with 1 / 2 / 3 free boosters; a fail resets; shown on the win screen | Butler's Gift [S30], Crown Rush [S60] |
| I2 | **Restorer rank** (player level and XP) | Apprentice, Assistant, Restorer, Conservator, Master Conservator, Museum Director | XP per level won, more for hard levels, first-try wins and few moves used; rank-ups give a frame, a title and a small chest; ranks are permanent | Royal League stages [S3], Toon Pass frames [S62] |
| I3 | **Gallery Visit** stamp card (daily) | A museum visitor card stamped at the door | Win one level per day for a stamp; 7 stamps open a "donor crate"; missing a day does not erase stamps (avoids the chore problem in [S82]) | Daily Stamps [S47] |
| I4 | **Curator's Missions** (weekly) | The curator's to-do list on a clipboard | 3 tasks a week (e.g. "clear 40 dust tiles", "win 5 levels on first try"), each can be swapped once or twice | Chocolate Box [S50], Weekly Missions [S27] |
| I5 | **Night Shift** streak jackpot (daily) | Restore 7 paintings in one night before the guards' rounds | Win 7 levels in a row on first try within 24 h; a pot of coins is split among the restorers who finish; retry after 30 min; rivals are simulated and the rules screen says so | Lava Quest [S9], Candy Royale [S43] |
| I6 | **Auction Race** | Five restorers race to prepare a lot for auction | 5 rivals race to win 10 levels first; top 3 rewarded; rivals simulated | Sky Race [S5], Episode Race [S53] |
| I7 | **Exhibition Week** leaderboard with personal milestones | Your restorations go on show; visitors vote | 10-person weekly board by levels won, plus a personal "visitor count" milestone track so everyone earns something | Weekly Contest [S40][S63], milestone trend [S79], Team Clash milestones [S49] |
| I8 | **Varnish Rush** (1 hour, unlimited lives) | Varnish dries fast, work quickly | 1 hour of unlimited lives; score by special pieces fired; fits the 10-minute session habit only for players who opt in | Lightning Rush [S6][S28] |
| I9 | **Mini-game events** that spend tokens earned in levels | Small restoration crafts | Rotating, each 3-7 days: **Clean the Canvas** (scratch away grime with solvent tokens to reveal a hidden detail), **Gild the Frame** (merge gold-leaf pieces, like Merge Smith), **Mix the Pigment** (guess a colour recipe from clues, like Magic Cauldron), **Spot the Forgery** (find differences between two copies), **Hang the Wall** (fit frames on a wall grid) | Merge Smith [S23], King's Tales [S24], Secret Ingredient [S74], Toon Blast mini-games [S59] |
| I10 | **Restoration Pass** (free + Patron track, monthly) | Museum Patron membership | Keys from wins; free track for everyone; Patron track adds lives 5 to 8, a gold nameplate, extra rewards and a Bonus Bank; buying late grants earlier rewards (Candy Crush rule) | Royal Pass [S7], Kingdom Pass [S38], Season Pass [S48] |
| I11 | **Donation Box** piggy bank | Visitors drop coins into the museum donation box | Part of coins won goes into the box; it can be opened with a purchase once it reaches a target; price set by the pricing workstream | Gold Reserve [S71][S72][S73] |
| I12 | **Museum Postcards** album (seasonal collection) | Postcards of details from the public-domain paintings, sets per art period (matches the 20 rooms going back in time) | Sets of 9 cards, rarities, duplicates turn into "ink" for a chest; finishing a season puts a permanent medal in a **Hall of Honour** room | Candy Pins and Trophy Room [S54], Royal Match Collection [S20] |
| I13 | **Exhibition calendar** (themed seasons) | Two-week shows: "Dutch Golden Age fortnight", "Impressionist spring" | Themes reuse existing event engines with new art and token names; mapped to the room / period structure | Playrix story events [S71], seasonal collections [S42][S72] |
| I14 | **Friends of the Museum** society (teams) | A patron society with a shared restoration fund | Teams of up to 30-50; ask for lives; a shared "fund chest" filled by members' wins; weekly society ranking | Team Battle [S4], Team Quest [S51], Toon Blast lives [S61] |
| I15 | **Co-op restoration** | Two restorers share one large altarpiece | Each partner's wins add to a shared painting; stage rewards for both | Train Journey [S13], Temple Guardians [S66] |
| I16 | **Workshop finale room** (Royal League equivalent) | When all 1,000 levels are done, the director runs a workshop league | Endless replay rounds with crowns and ranks until new levels ship | Royal League [S3], Champions Arena [S35] |

Cadence suggestion (ESTIMATE, scaled down from [S33][S34] for a one-person team without a server): always on: I1, I2, I3, I10, I12. Weekly: I4, I7. Rotating every 2-4 days so that 3-5 events are live at once: I5, I6, I8, one I9 mini-game. Weekend: the most social or competitive one (I7 finale or I6). Seasonal: I13 every two weeks to a month.

---

## 7. Needs a server or not

Context: the game has no backend and no INTERNET permission today. "Offline" below means the whole feature runs on one phone with local save data and the device clock.

| # | IDEA | Fully offline on one phone? | What a server would add or is required for | Offline risks (ESTIMATE) |
|---|---|---|---|---|
| I1 Steady Hand streak | **Yes** | Nothing needed | None worth guarding |
| I2 Restorer rank / XP | **Yes** | Cross-device save, showing rank to others | Lost on reinstall without cloud save |
| I3 Gallery Visit stamps | **Yes** (device date) | Trusted time | Changing the phone clock gives extra stamps; mitigate by allowing only one stamp per calendar day and ignoring backward clock jumps; keep rewards small |
| I4 Curator's Missions | **Yes** | New mission pools without an app update (remote config) | Mission list fixed per app version |
| I5 Night Shift jackpot | **Yes, with simulated rivals** | Real rivals and a real shared pot | Honesty: Lava Quest states rivals are simulated [S9]; say the same in the rules screen. Simulated finish rates must be tuned so the pot share feels fair |
| I6 Auction Race | **Yes, with simulated rivals** | Real matchmaking | Same honesty point; bot pace should depend on the player's own pace history so races stay close |
| I7 Exhibition Week board | **Yes, with simulated rivals**; personal milestones fully offline | Real leaderboard | A purely local board is a simulation; disclose it |
| I8 Varnish Rush | **Yes** (solo score vs simulated rivals or own best) | Real rivals | Clock tampering to restart the hour; low risk |
| I9 Mini-games | **Yes** | New mini-game content only by app update or downloadable content | Content cost: each mini-game is real code and art |
| I10 Restoration Pass | **Mostly**: the free track and the progress are offline | Purchase validation and restoring the pass on a new phone need the store; a server makes receipt checks safer | The store purchase itself needs a network connection on the phone; whether the build then needs the INTERNET permission must be checked in the IAP workstream (open question, not verified here) |
| I11 Donation Box | **Mostly** (fills offline) | Same as I10 for the purchase | Same as I10 |
| I12 Postcards album | **Yes** (without trading) | Trading duplicates, gifting cards to friends | None beyond reinstall loss |
| I13 Exhibition calendar | **Yes** if the calendar is baked into the build | Changing the schedule or adding a theme without an update needs remote config | Events stop when the baked calendar runs out; ship a long calendar or a looping one |
| I14 Friends of the Museum (teams) | **No** | Required: accounts, team membership, chat, life requests, team chests, moderation (Royal Match and Candy Crush state that teams need an internet connection [S16][S45]) | Biggest build and running cost; also moderation and child-safety duties for chat (ESTIMATE) |
| I15 Co-op restoration | **No** for real partners; a fake partner would be a bot | Required: partner matching and shared progress | A simulated partner could work but feels hollow; consider it only after teams exist |
| I16 Workshop finale league | **Yes, with simulated rivals** | Real league | Same honesty point as I5 |

**What top games do that cannot be faked offline (FACT):** team events and team chat (all four games require internet for teams [S4][S16][S39][S45]), Royal Cup country competition [S22], and card trading (Candy Crush Pin Swap [S54]).
**What top games already run against simulated or recorded opponents (FACT / REPORTED):** Lava Quest [S9], Candy Royale [S44]. Toon Blast accepts a capped amount of offline score in its 50-player tournaments [S58][S64].

---

## 8. Gaps (what I could not find or verify)

- Exact event lengths for most Royal Match / Royal Kingdom events (the help pages only say "time-limited").
- Reward amounts for most events (help pages rarely list them).
- Whether King's Cup, Sky Race, Lightning Rush, Toon Blast tournaments or Candy Crush leaderboards use bots.
- Any public controlled test (A/B) of a single event's effect on match-3 retention; all match-3 effect numbers are analyst before/after estimates.
- Cohort retention of the top match-3 games themselves.
- Playrix help centre pages for Homescapes / Gardenscapes (the Playrix Helpshift site only listed Township); Playrix team features are covered only by weak sources.
- Royal Match "Weekly Missions" rules (only the name in the Sep 28 update notes).
- Candy Crush Season Pass and Toon Pass prices (not on the pages seen).
- Adjust 2025/2026 puzzle retention numbers (report seems gated).
- The Reddit Royal Match cadence post could not be opened (the scraper refuses Reddit); its claims come from a search excerpt only.

---

## 9. Sources (all seen 2026-10-23)

Royal Match help centre (Dream Games), publisher pages, no dates shown unless noted:
- [S1] Events and Tournaments section: https://dreamgames.helpshift.com/hc/en/3-royal-match/section/23-events-and-tournaments/
- [S2] King's Cup: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/107-king-s-cup-1721127793/
- [S3] Royal League: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/108-royal-league-1721127793/
- [S4] Team Battle: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/109-team-battle/
- [S5] Sky Race: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/113-sky-race/
- [S6] Lightning Rush: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/116-lightning-rush/
- [S7] Royal Pass: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/112-royal-pass/
- [S8] Book of Treasure: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/111-book-of-treasure/
- [S9] Lava Quest: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/187-lava-quest/
- [S10] Space Mission: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/118-space-mission-1652306365/
- [S11] Robot Rivals: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/559-robot-rivals/
- [S12] Champions Clash: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/581-champions-clash/
- [S13] Train Journey: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/487-train-journey/
- [S14] Dragon Nest: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/294-dragon-nest-1702462966/
- [S15] Teams section: https://dreamgames.helpshift.com/hc/en/3-royal-match/section/21-teams-1608112100/
- [S16] What are teams?: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/52-what-are-teams/
- [S17] Team Tournament: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/341-team-tournament-1713460042/
- [S18] Team Treasure: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/115-team-treasure/
- [S19] Lives: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/5-lives/
- [S20] What is Collection?: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/332-what-is-collection/
- [S21] Duke's Treats: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/793-duke-s-treats-1790351145/
- [S22] Royal Cup: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/633-royal-cup-1780652609/
- [S23] Merge Smith: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/601-merge-smith/
- [S24] King's Tales: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/648-king-s-tales/
- [S25] Archery Arena: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/295-archery-arena/
- [S26] Ancient Adventure: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/560-ancient-adventure/

Royal Match, other:
- [S27] Royal Match, US App Store (description, version notes Oct 2025 to Sep 28 2026, IAP list): https://apps.apple.com/us/app/royal-match/id1482155847
- [S28] Royal Match Wiki (Fandom), Lightning Rush: https://royalmatch.fandom.com/wiki/%E2%9A%A1%EF%B8%8F_Lightning_Rush
- [S29] Reddit r/RoyalMatch, "All Royal Match Events: When They Unlock and Appear" (search excerpt only): https://www.reddit.com/r/RoyalMatch/comments/1l8umrt/all_royal_match_events_when_they_unlock_and_appear/
- [S30] Naavik, "Royal Match: Dream Games' Regal Performance", Niek Tuerlings, 25 May 2022: https://naavik.co/deep-dives/royal-match/
- [S31] Deconstructor of Fun, "Royal Match - The New King from Turkey?", 21 Mar 2021 (search excerpt): https://www.deconstructoroffun.com/blog/2021/3/21/royal-match-the-new-king-from-turkey
- [S32] Deconstructor of Fun, "Win Streak: the Gift That Keeps on Giving", Ahmetcan Demirel, 5 Aug 2024: https://www.deconstructoroffun.com/blog/2024/8/5/win-streak-the-gift-that-keeps-on-giving
- [S33] Tom Storr, "The Dream Live Ops Playbook", Gamigion, 21 April (year not shown; LinkedIn reshare implies 2025), AppMagic Live Ops Calendar data: https://www.gamigion.com/?p=32712 (reshare: https://tr.linkedin.com/in/can-turhan)
- [S34] Sensor Tower, "What Royal Match's calendar teaches about live ops in 2026", Bryan Isagholian, May 2026: https://sensortower.com/blog/what-royal-matchs-calendar-teaches-about-live-ops-in-2026

Royal Kingdom (Dream Games):
- [S35] Events section: https://dreamgames.helpshift.com/hc/en/6-royal-kingdom-1676903479/section/46-events/
- [S36] Dungeon Escape: https://dreamgames.helpshift.com/hc/en/6-royal-kingdom-1676903479/faq/328-dungeon-escape/
- [S37] Canvas Quest: https://dreamgames.helpshift.com/hc/en/6-royal-kingdom-1676903479/faq/578-canvas-quest/
- [S38] Kingdom Pass: https://dreamgames.helpshift.com/hc/en/6-royal-kingdom-1676903479/faq/315-kingdom-pass-1710944877/
- [S39] Team Clash: https://dreamgames.helpshift.com/hc/en/6-royal-kingdom-1676903479/faq/458-team-clash-1727958491/
- [S40] Weekly Contest: https://dreamgames.helpshift.com/hc/en/6-royal-kingdom-1676903479/faq/537-weekly-contest/
- [S41] Richard's Journey: https://dreamgames.helpshift.com/hc/en/6-royal-kingdom-1676903479/faq/687-richard-s-journey/
- [S42] Royal Kingdom, US App Store (events, version notes, IAP list): https://apps.apple.com/us/app/royal-kingdom/id1606549505

Candy Crush Saga (King help centre unless noted):
- [S43] Candy Royale: https://candycrush.zendesk.com/hc/en-us/articles/4404903647517--Candy-Royale
- [S44] Candy Crush Saga Wiki (Fandom), Candy Royale: https://candycrush.fandom.com/wiki/Candy_Royale
- [S45] What is Candy Teams?: https://candycrush.zendesk.com/hc/en-us/articles/34612008106653-What-is-Candy-Teams
- [S46] How do leaderboard events work?: https://candycrush.zendesk.com/hc/en-us/articles/39040841526685-How-do-leaderboard-events-work
- [S47] Daily Stamps: https://candycrush.zendesk.com/hc/en-us/articles/28189843882013--Daily-Stamps
- [S48] Season Pass: https://candycrush.zendesk.com/hc/en-us/articles/7897323489693--Season-Pass
- [S49] Team Clash: https://candycrush.zendesk.com/hc/en-us/articles/34615452887965--Team-Clash
- [S50] Chocolate Box: https://candycrush.zendesk.com/hc/en-us/articles/360020946198--Chocolate-Box
- [S51] Team Quest: https://candycrush.zendesk.com/hc/en-us/articles/34614687627549--Team-Quest
- [S52] Weekly Contest: https://candycrush.zendesk.com/hc/en-us/articles/360001379009--Weekly-Contest
- [S53] Episode Race: https://candycrush.zendesk.com/hc/en-us/articles/360001846038--Episode-Race
- [S54] Candy Pins: https://candycrush.zendesk.com/hc/en-us/articles/25219182745373--Candy-Pins
- [S55] Champions Race: https://candycrush.zendesk.com/hc/en-us/articles/360001793537--Champions-Race
- [S56] Candy Crush Saga, App Store (Hungary listing text, search excerpt, Daily Booster Wheel): https://apps.apple.com/hu/app/candy-crush-saga/id553834731 ; US listing (IAP list): https://apps.apple.com/us/app/candy-crush-saga/id553834731
- [S57] Candy Crush Saga Wiki (Fandom), Event (page description): https://candycrush.fandom.com/wiki/Event

Toon Blast (Peak help centre unless noted):
- [S58] What is the Star Tournament?: https://peakgames.helpshift.com/hc/en/4-toon-blast/faq/181-what-is-the-star-tournament/
- [S59] Events section: https://peakgames.helpshift.com/hc/en/4-toon-blast/section/8-events-1656338033/
- [S60] What is Crown Rush?: https://peakgames.helpshift.com/hc/en/4-toon-blast/faq/24-what-is-crown-rush/
- [S61] Teams section: https://peakgames.helpshift.com/hc/en/4-toon-blast/section/9-teams-1656338014/
- [S62] What is Toon Pass?: https://peakgames.helpshift.com/hc/en/4-toon-blast/faq/322-what-is-toon-pass/
- [S63] What is Weekly Contest?: https://peakgames.helpshift.com/hc/en/4-toon-blast/faq/632-what-is-weekly-contest/
- [S64] What is Shield Clash?: https://peakgames.helpshift.com/hc/en/4-toon-blast/faq/402-what-is-shield-clash/
- [S65] What is Dragon's Treasure?: https://peakgames.helpshift.com/hc/en/4-toon-blast/faq/469-what-is-dragon-s-treasure/
- [S66] What is Temple Guardians?: https://peakgames.helpshift.com/hc/en/4-toon-blast/faq/649-what-is-temple-guardians/
- [S67] Toon Blast Wiki (Fandom), Team Tournament (search excerpt): https://toonblast.fandom.com/wiki/Team_Tournament
- [S68] Liquid and Grit, "September 2023 Casual Report", Sep 2023: https://www.liquidandgrit.com/september-2023-casual-report/

Playrix and cross-genre:
- [S69] GameRefinery, "How mobile game developers are driving player engagement with race events", Mona Hietala, 15 Jun 2023: https://gamerefinery.com/how-mobile-game-developers-are-driving-player-engagement-with-race-events
- [S70] Naavik, "What Leading Match-3 & Merge Games Do Differently", Oindrila Mandal, 18 Jan 2026: https://naavik.co/digest/what-leading-match-3-and-merge-games-do-differently/
- [S71] Homescapes, US App Store (version notes Feb-Oct 2026, IAP list): https://apps.apple.com/us/app/homescapes/id1195621598
- [S72] Gardenscapes, US App Store (events, version notes Jul 2025-Oct 2026, reviews, IAP list): https://apps.apple.com/us/app/gardenscapes/id1105855019
- [S73] Mistplay, "Piggy banks in games", 30 Jan 2026 (moved from maf.ad): https://business.mistplay.com/resources/piggy-banks-in-games/
- [S74] UserWise, "This Week in LiveOps #10: Gardenscapes Secret Ingredient", 24 Aug 2023 (sponsored newsletter): https://thisweekinliveops.substack.com/p/issue-10-gardenscapes-secret-ingredient

Retention benchmarks and evidence:
- [S75] GameAnalytics, "2025 Mobile Gaming Benchmarks" (data 2024; page last updated 24 Aug 2026): https://www.gameanalytics.com/reports/2025-mobile-gaming-benchmarks
- [S76] GameDev Reports, "GameAnalytics: Benchmarks in Mobile Games for Q1'24", 27 May 2024: https://gamedevreports.substack.com/p/gameanalytics-benchmarks-in-mobile
- [S77] Mistplay, "The big list of mobile game retention benchmarks", 3 Oct 2023 (cites AppsFlyer 2022 retention report, Q3 2022): https://business.mistplay.com/resources/mobile-game-retention-benchmarks
- [S78] GameAnalytics, "Mobile Gaming Benchmarks H1 2019" PDF hosted by InvestGame (search excerpt): https://investgame.net/wp-content/uploads/2023/06/H1-2019-Mobile-Benchmarks-Report-GameAnalytics.pdf
- [S79] Naavik, "Converging Live Ops Trends in Mobile Puzzle", Harshal Karvande, 16 Nov 2025: https://naavik.co/digest/live-ops-trends-powering-mobile-puzzle/
- [S80] Naavik, "Why Dream Games' Success Is a Challenge to Replicate", Harshal Karvande, 24 Aug 2025: https://naavik.co/digest/why-dream-games-success-is-a-challenge-to-replicate/
- [S81] Duolingo blog, "The habit-building research behind your Duolingo streak", Osman Mansur, 31 Jan 2022: https://blog.duolingo.com/how-duolingo-streak-builds-habit/
- [S82] Frommel and Mandryk, "Daily Quests or Daily Pests? The Benefits and Pitfalls of Engagement Rewards in Games", CHI PLAY 2022 (ACM): https://dl.acm.org/doi/10.1145/3549489
- [S83] Adam Telfer, "Toon Blast & The Death of Saga", Mobile Free To Play, 6 Nov 2017: https://mobilefreetoplay.com/peak-games-toon-blast-death-of-saga/
- [S84] LinkedIn post on Royal Match Journey Offer (cites AppMagic 2026 Türkiye report; relative date only): https://www.linkedin.com/posts/bashisthadevghosh_royal-match-runs-over-150-liveops-events-activity-7468285466669330432-WPlw
- [S85] UserWise, "This Week in LiveOps #3: Royal Match's new Lava Quest" (search excerpt; date not shown): https://thisweekinliveops.substack.com/p/this-week-in-liveops-royal-matchs
- [S86] Medium, "Game Analysis of Royal Match" (search summary only, weak): https://medium.com/@ekinmelissezer/game-analysis-for-royal-match-and-toon-blast-9c4bff8ef48b
