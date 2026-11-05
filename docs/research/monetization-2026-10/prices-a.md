# US App Store IAP prices: Royal Match, Royal Kingdom, Candy Crush Saga, Candy Crush Soda Saga, Toon Blast, Toy Blast

Checked on **2026-10-23**, US storefront, prices in USD before sales tax.
Machine-readable copy: `prices-a.json` (games and items), `prices-a-context.json` (method, legend, market context).

**Labels.** FACT = seen on the cited source on 2026-10-23 (App Store page, Apple catalog API, or the game's own help page). REPORTED = secondary source (wiki, analyst, newsletter, forum, tracker), possibly older. ESTIMATE = my own inference or arithmetic.

**How the store lists were read.** For each game I read the web page (the "In-App Purchases" list) and Apple's catalog API view `top-in-app-purchasables` (through the Firecrawl `apps-apple-com` provider). Apple only shows the **top 10** IAPs per app, and the API flags the list as truncated for all six games, so the full price ladder is never public. For five games both reads matched exactly. For Toy Blast they returned two different lists, and both are kept below.

**What the store names do not tell you.** Contents (coins, boosters, hours) are almost never in the name. Where no source gave the contents, the cell says "not published".

---

## 1. Royal Match (Dream Games)
Store: https://apps.apple.com/us/app/royal-match/id1482155847 · seen 2026-10-23

| Item (store name) | USD | Kind | Contents | Status |
|---|---|---|---|---|
| Mini Coin Package | 1.99 | coins | 1,000 coins (GameMakers, Jan 2025) | FACT price / REPORTED contents |
| Tiny Concept Offer | 0.99 | other | not published | FACT |
| Mini Offer | 2.99 | other | not published | FACT |
| Mini Concept Offer | 2.99 | other | not published | FACT |
| Duke's Treasure | 5.99 | bundle | "Treasure" family: coins + boosters + time-limited unlimited boosters; amounts not found (NamuWiki) | FACT / REPORTED |
| Royal Favor | 6.99 | bundle (my guess) | not published | FACT |
| Normal Concept Offer | 6.99 | other | not published | FACT |
| Prince's Treasure | 9.99 | bundle | Treasure family (see above) | FACT / REPORTED |
| **Royal Pass** | **9.99** | pass | one monthly season (details below) | FACT |
| Queen's Treasure | 19.99 | bundle | Treasure family; a NamuWiki search snippet ties it to 12 hours (snippet only, not verified) | FACT / REPORTED |
| King's Treasure | 39.99 | bundle | not published; not in today's top 10 | REPORTED (AppMagic 2025) |
| 5,000-coin pack (name unknown) | 7.99 | coins | 5,000 coins | REPORTED (GameMakers) |

**Coins per dollar.** $1.99 → 1,000 coins (≈503/$). $7.99 → 5,000 coins (≈626/$, about 25% more per dollar). Amounts are REPORTED and the per-dollar figures are ESTIMATE. A reader comment under the GameMakers article claims the biggest pack is 100,000 coins for CAD 129 (probably USD 99.99). That is low-reliability REPORTED. OpenTheRank shows the US Google Play range as $0.99–$99.99 (captured 2026-10-10).

**What coins buy.** +5 moves after a loss costs 900 coins, and one review says the next set costs 1,900 (REPORTED, App Store user reviews seen 2026-10-23). A refill of 5 lives costs 900 coins (REPORTED, GameMakers). In 2022 Naavik reported that the first +5 moves cost about $2 of coins and later sets cost $4.35, $6.65 and $8.95. Naavik called that the highest in the genre (REPORTED, 2022-05-25).

**Best sellers.** AppMagic ranks the top offers for Oct 2024–Sep 2025 as follows: Royal Favor $6.99 (15%), Mini Coin Package $1.99 (10.8%), Prince's Treasure $9.99 (7%), Queen's Treasure $19.99 (6.6%), King's Treasure $39.99 (6.4%). REPORTED.

### Royal Pass
- **Price $9.99.** FACT (App Store). On 2026-10-23 the live season is called "Haunted Pass" (FACT, store event).
- **Season and stages.** One calendar month with 30 stages. Keys come from beating levels, and hard and super hard levels give extra keys. REPORTED (fan wiki, GameMakers).
- **Free track vs premium track.** The free track has 30 rewards and the premium track adds 50 (80 in total). REPORTED (GameMakers, 2025-01-18).
- **Perks after buying.** Max lives go from 5 to 8, the name and frame turn gold, teammates get a gift, and the Bonus Bank opens. After the last stage the Bonus Bank saves 100 coins per 10 keys, up to 5,000 coins. REPORTED (wiki, GameMakers).
- **One season's reward table** (wiki; the totals are my sums, ESTIMATE):
  - Free track: single boosters, card packs, 500 coins and 1h45m of unlimited lives in total. Chests come at stages 5, 10, 15, 20, 25 and 30.
  - Premium track: 2–3× booster stacks, timed unlimited boosters (15 min to 1 h), wild cards, 2,500 coins and 3h45m of unlimited lives in total. Stage 30 gives 1 h each of unlimited lives, TNT, rocket and light ball, plus 5 hammers and 1,000 coins.
- **Pricing policy.** The USD price is the same in every country, with no discounts and no second pass version. GameMakers values the rewards at about $219 by its own method. REPORTED.
- **Effect on revenue.** The pass launched in June 2021, and Naavik reports revenue rose 31% in the 30 days after launch. In the same month "Endless Treasure", a free-then-paid reward chain, added 20%. REPORTED.

Sources: [App Store](https://apps.apple.com/us/app/royal-match/id1482155847) · [Royal Pass wiki](https://royalmatch.fandom.com/wiki/Royal_Pass) · [GameMakers 2025-01-18](https://www.gamemakers.com/p/royal-matchs-219-battle-pass-secret) · [Naavik 2022-05-25](https://naavik.co/deep-dives/royal-match/) · [AppMagic report 2025](https://appmagic.rocks/files/img-blog/EN_Monetization_Report_2025.pdf) · [NamuWiki](https://en.namu.wiki/w/%EB%A1%9C%EC%96%84%20%EB%A7%A4%EC%B9%98) · [OpenTheRank](https://opentherank.com/mobile-game-pricing/royal-match/)

---

## 2. Royal Kingdom (Dream Games)
Store: https://apps.apple.com/us/app/royal-kingdom/id1606549505 · seen 2026-10-23

| Item | USD | Kind | Contents | Status |
|---|---|---|---|---|
| Mini Coin Pack | 1.99 | coins | amount not found (1,000 if it mirrors Royal Match: ESTIMATE) | FACT |
| Mini Offer | 2.99 | other | not published | FACT |
| Mini Concept Offer | 2.99 | other | not published | FACT |
| Noble Pack | 5.99 | bundle (guess) | not published | FACT |
| King's Favor | 6.99 | bundle (guess) | not published | FACT |
| Normal Concept Offer | 6.99 | other | not published | FACT |
| Small Coin Package | 7.99 | coins | amount not found (5,000 if it mirrors Royal Match: ESTIMATE) | FACT |
| Splendid Pouch | 9.99 | bundle (guess) | not published | FACT |
| **Kingdom Pass** | **9.99** | pass | monthly pass (below) | FACT |
| Superior Chest | 19.99 | bundle (guess) | not published | FACT |

**Best sellers.** AppMagic ranks the top offers for Oct 2024–Sep 2025 as follows: Splendid Pouch $9.99 (14.4%), Superior Chest $19.99 (12.3%), Mini Coin Package $1.99 (11.0%), King's Favor $6.99 (10.1%), Noble Pack $5.99 (8.4%). REPORTED. The two bestsellers sit at $9.99 and $19.99, which is a higher price mix than Royal Match.

### Kingdom Pass
Source: the official help center (FACT, read 2026-10-23).
- **Unlock and length.** It unlocks at level 31 and runs for one calendar month, ending on the 1st of the next month.
- **Progress.** Keys come from beating levels. Hard, Super Hard, Kingdom and Dark Kingdom levels give extra keys.
- **Perks after buying.** Extra and exclusive rewards, max lives from 5 to 8, gold name and frame, and a gift to teammates.
- **Bonus Bank.** After all stages, keys keep saving coins up to 5,000. The pass is needed to claim them, and they are claimed at the start of the next month.
- **Not stated by the help center:** the number of stages.

On the store the event reads "New Season! Kingdom Pass!", collect keys from levels to unlock rewards (FACT, 2026-10-23).

Sources: [App Store](https://apps.apple.com/us/app/royal-kingdom/id1606549505) · [Help center: Kingdom Pass](https://dreamgames.helpshift.com/hc/en/6-royal-kingdom-1676903479/faq/315-kingdom-pass-1710944877/) · [AppMagic report 2025](https://appmagic.rocks/files/img-blog/EN_Monetization_Report_2025.pdf)

---

## 3. Candy Crush Saga (King)
Store: https://apps.apple.com/us/app/candy-crush-saga/id553834731 · seen 2026-10-23

| Item | USD | Kind | Contents | Status |
|---|---|---|---|---|
| Extra Moves (×3 SKUs) | 0.99 each | other (continue) | 5 extra moves; one SKU adds a striped candy (store descriptions) | FACT |
| Extra Lives | 0.99 | lives | restores all lives (store description) | FACT |
| 10x Gold Bars package | 0.99 | coins | 10 gold bars; the wiki calls it a sale price | FACT / REPORTED |
| XS Gold Bars Pack | 1.99 | coins | 10 gold bars | FACT / REPORTED (AppMagic, wiki) |
| Lollipop Hammer | 1.99 | boosters | hammer booster, a set of 3 per the wiki | FACT / REPORTED |
| Extra small offer | 2.99 | bundle (guess) | not published | FACT |
| Small offer | 5.99 | bundle (guess) | not published | FACT |
| S Gold Bars Pack | 7.99 | coins | 50 gold bars | FACT / REPORTED (AppMagic) |
| Offer | 9.99 | bundle | not published | REPORTED (AppMagic) |
| Offer | 19.99 | bundle | not published | REPORTED (AppMagic) |
| 15x Gold Bars Package | 1.99 | coins | 15 gold bars (older top-10 list, game version 1.281) | REPORTED (wiki) |
| Largest gold pack | 100 (likely 99.99) | coins | 1,000 gold bars; the largest bundle at the same price has 650 gold + 18 h unlimited lives + booster passes + 10+ of each booster | REPORTED (wiki, undated "last known prices") |
| Gold Pass (Season Pass premium track) | varies | pass | see below | REPORTED |

**Gold bars per dollar.** These figures are ESTIMATE from REPORTED amounts:

| Pack | Gold bars | Gold bars per $ |
|---|---|---|
| $1.99 | 10 | ≈5.0 |
| $7.99 | 50 | ≈6.3 |
| $0.99 sale | 10 | ≈10.1 |
| $100 | 1,000 | 10 |

The wiki rule of thumb is 5–10 bars per USD.

**What gold buys** (REPORTED, wiki):
- +5 moves: 10 bars, which is about $1.60–$2.00 depending on the pack (ESTIMATE)
- Lives refill: 12 bars
- 6 h of unlimited lives: 69 bars
- Hammer: 19 bars for 3 (older table)

**Best sellers.** AppMagic ranks the top offers for Oct 2024–Sep 2025 as follows: 10 gold bars $1.99 (24.7%), 50 gold bars $7.99 (14%), small offer $5.99 (8.6%), Offer $19.99 (8%), Offer $9.99 (5.9%). REPORTED. This is the most hard-currency-driven list of the six games.

### Season Pass (Free Pass + Gold Pass)
- **Basics.** Running since November 2022. Points come from winning levels, and the Gold Pass is premium, paid with real money (REPORTED, wiki).
- **October 2026 change.** Only the player's current level earns points; replays no longer count (REPORTED, wiki).
- **Price.** Not shown in the store top 10, and it **varies by player and over time**. Player posts on King Community and Reddit mention $2.99 → $4.99, $6.99, $12.99 and one $27, with no dates visible in what I could read. REPORTED, low precision.

Sources: [App Store](https://apps.apple.com/us/app/candy-crush-saga/id553834731) · [Gold wiki](https://candycrush.fandom.com/wiki/Gold) · [Yeti Shop wiki](https://candycrush.fandom.com/wiki/Yeti_Shop) · [Season Pass wiki](https://candycrush.fandom.com/wiki/Season_Pass) · [King Community thread](https://community.king.com/en/candy-crush-saga/discussion/473894/gold-season-pass-price) · [AppMagic report 2025](https://appmagic.rocks/files/img-blog/EN_Monetization_Report_2025.pdf)

---

## 4. Candy Crush Soda Saga (King)
Store: https://apps.apple.com/us/app/candy-crush-soda-saga/id850417475 · seen 2026-10-23

| Item | USD | Kind | Contents | Status |
|---|---|---|---|---|
| Extra Small Gold Bar Package | 0.99 | coins | not published | FACT |
| Hard Currency 2 XS | 0.99 | coins | not published | FACT |
| Dainty offer | 1.99 | bundle (guess) | not published | FACT |
| Hard Currency 3 XS | 1.99 | coins | not published | FACT |
| Sweet offer | 3.99 | bundle (guess) | not published | FACT |
| Small Gold Bar Package | 4.99 | coins | not published | FACT |
| Sweet offer | 4.99 | bundle (guess) | not published | FACT |
| Sweet offer | 5.99 | bundle (guess) | not published | FACT |
| Offer Small | 6.99 | bundle (guess) | not published | FACT |
| Juicy offer | 8.99 | bundle (guess) | not published | FACT |

### Rewards Pass
- **Tiers.** The player picks Small, Medium or Big (7, 14 or 21 stages).
- **Prices.** "$4.99 to $12.99"; earlier there was a single $5.99 pass for 8 stages.
- **Status.** REPORTED, Gamigion article dated Aug 28; the year is not shown on the page, and search indexes place it in 2024. The wiki confirms the three-size choice.

Sources: [App Store](https://apps.apple.com/us/app/candy-crush-soda-saga/id850417475) · [Gamigion](https://www.gamigion.com/?p=2672) · [Season Pass wiki (trivia)](https://candycrush.fandom.com/wiki/Season_Pass)

---

## 5. Toon Blast (Peak)
Store: https://apps.apple.com/us/app/toon-blast/id1176027022 · seen 2026-10-23

| Item | USD | Kind | Contents | Status |
|---|---|---|---|---|
| Tiny Coin Pack (2 SKUs) | 1.99 | coins | coin amount not found | FACT |
| Small Coin Pack | 4.99 | coins | not found | FACT |
| Big Team Bundle | 4.99 | bundle | not published (the name suggests a team offer) | FACT |
| Toon Bundle | 6.99 | bundle | not published | FACT |
| Mini Bundle | 6.99 | bundle | not published | FACT |
| Small Coin Pack | 7.99 | coins | not found | FACT |
| Toon Bundle | 9.99 | bundle | not published | FACT |
| Super Bundle | 12.99 | bundle | not published | FACT |
| Super Bundle | 19.99 | bundle | not published | FACT |
| Toon Pass | 9.99 (uncertain) | pass | see below | REPORTED |
| Diamond Pass | 19.99 | pass | higher tier added in 2026 | REPORTED |

Several names appear at two prices (Small Coin Pack at $4.99 and $7.99, Super Bundle at $12.99 and $19.99). That probably means price tests or player segments (ESTIMATE).

**What coins buy** (REPORTED, wiki):
- Continue with +5 moves: 100 coins, then 200, then 300 for each further continue
- Lives refill: 100 coins
- Boosters: up to 300 coins for 3
- Creating a team: 100 coins

### Toon Pass
- **Launch and length.** Launched 2022-02-10. A season lasts about 26 days and unlocks at level 30.
- **Stages and progress.** 30 stages. Each level won gives 1 badge, and stages need 1 to 50 badges. A grand prize at 100 badges needs the paid pass.
- **Free track.** Single boosters, 4 × 15 min of unlimited lives, 15 min of double star progress, and 10-coin rewards.
- **Paid track.** Coin rewards of 25, 50 and 100, 30–60 min of unlimited lives, free "+5 moves" tokens, bigger booster stacks, and the grand prize.
- **Perks after buying.** Lives go from 5 to 8, gold name and frame, a team gift, and a sticker.
- **Price conflict.** The wiki section title says $9.99, but its body says "around $5" and that the price changes daily. Treat the price as uncertain. A Diamond Pass at $19.99 was added in 2026.
- **Status.** All of the above is REPORTED from one fan wiki.

Sources: [App Store](https://apps.apple.com/us/app/toon-blast/id1176027022) · [Toon Pass wiki](https://toonblast.fandom.com/wiki/Toon_Pass) · [Coins wiki](https://toonblast.fandom.com/wiki/Coins) · [App Pricing Lab snapshot 2026-03-21](https://apppricinglab.com/iap/apple/1176027022) (same 9 items)

---

## 6. Toy Blast (Peak)
Store: https://apps.apple.com/us/app/toy-blast/id890378044 · seen 2026-10-23

The web page and the API returned two different top-10 lists on the same day, so both are shown. The "List" column says where each item was seen.

| Item | USD | Kind | Contents | List | Status |
|---|---|---|---|---|---|
| 25 Coins | 0.99 | coins | 25 coins | api + web | FACT |
| Super Sale | 0.99 | other | not published | api | FACT |
| Special Offer | 0.99 | other | not published | api | FACT |
| Tiny Coin Pack | 1.99 | coins | not found | web | FACT |
| Beginner's Bundle | 1.99 | starter | not published | web | FACT |
| 140 Coins | 4.99 | coins | 140 coins | api + web | FACT |
| Small Coin Pack | 4.99 | coins | not found | web | FACT |
| Special Offer | 4.99 | other | not published | api | FACT |
| Toy Bundle | 6.99 | bundle | not published | web | FACT |
| 300 Coins | 9.99 | coins | 300 coins | api + web | FACT |
| Super Bundle | 12.99 | bundle | not published | web | FACT |
| 625 Coins | 19.99 | coins | 625 coins | api + web | FACT |
| Super Bundle | 19.99 | bundle | 750 coins + 6 h unlimited lives + 3 each of Drill, Puzzle Cube, TNT, Rotor (Apple description) | api | FACT |
| Mega Bundle | 24.99 | bundle | not published | web | FACT |
| 1625 Coins | 49.99 | coins | 1,625 coins | api | FACT |
| 3500 Coins | 99.99 | coins | 3,500 coins | api | FACT |

**Coin ladder.** Amounts are FACT; coins per dollar and the bonus are my arithmetic (ESTIMATE).

| USD | Coins | Coins per $ | vs smallest pack |
|---|---|---|---|
| 0.99 | 25 | 25.3 | — |
| 4.99 | 140 | 28.1 | +11% |
| 9.99 | 300 | 30.0 | +19% |
| 19.99 | 625 | 31.3 | +24% |
| 49.99 | 1,625 | 32.5 | +29% |
| 99.99 | 3,500 | 35.0 | +39% |

These coin SKUs date from 2015 but Apple still serves them in 2026. The $19.99 Super Bundle gives 750 coins alone (37.5 per dollar) plus lives and boosters, so a bundle beats a plain coin pack at the same price. No Toy Blast pass was found.

Sources: [App Store](https://apps.apple.com/us/app/toy-blast/id890378044) · [App Pricing Lab](https://apppricinglab.com/iap/apple/890378044)

---

## Cross-game summary

### Price points in use (FACT, union of the six US top-10 lists)
$0.99 · $1.99 · $2.99 · $3.99 · $4.99 · $5.99 · $6.99 · $7.99 · $8.99 · $9.99 · $12.99 · $19.99 · $24.99 · $49.99 · $99.99. In addition, $39.99 is REPORTED for Royal Match.

### Passes

| Game | Pass price (USD) | Length | Stages | Perks | Status |
|---|---|---|---|---|---|
| Royal Match | 9.99 | month | 30 (30 free + 50 premium rewards) | 8 lives, gold name, team gift, Bonus Bank ≤5,000 coins | price FACT, rest REPORTED |
| Royal Kingdom | 9.99 | month | not stated | 8 lives, gold name, team gift, Bonus Bank ≤5,000 coins | FACT (official help) |
| Candy Crush Saga | varies, ~2.99–12.99 per player posts | not stated | not stated | Gold Pass premium track | REPORTED |
| Candy Crush Soda Saga | 4.99–12.99 (3 sizes) | not stated | 7 / 14 / 21 | — | REPORTED (2024?) |
| Toon Blast | 9.99 (or ~5, conflicting) + Diamond 19.99 | ~26 days | 30 + grand prize | 8 lives, gold name, team gift | REPORTED |
| Toy Blast | none found | — | — | — | gap |

The common pattern (ESTIMATE, drawn from the rows above):
- A **$9.99 one-season pass** with a free track that everyone sees.
- Perks that **raise the life cap from 5 to 8**.
- A **gold name**, which makes the pass visible to the team, and a **gift to teammates**.
- An end-of-season **coin bank** that only buyers can open.

### Hard currency per dollar
- Royal Match: about 500–625 coins per $ (REPORTED amounts).
- Candy Crush: about 5–10 gold bars per $ (REPORTED).
- Toy Blast: about 25–35 coins per $ (FACT amounts).

Bigger packs give 25–40% more per dollar than the smallest pack (ESTIMATE from these ladders).

### Puzzle-genre context (AppMagic Monetization Report 2025, data Oct 2024–Sep 2025, REPORTED)
- **Offer prices on the App Store, 2025:** median $1.99, average $6.05, maximum $99.99.
- **ARPPU of top puzzle titles on the App Store:** $69 at 30 days, $107 at 60 days, $135 at 90 days.
- **Average App Store purchase:** about $9.6 for the first purchase and $12.6 from the third purchase onward.
- **Match-3 revenue:** grew from $4.4B to $4.6B.
- **Where the money comes from:** mostly LiveOps-tied special offers in the **$6–15** range, while items at $50+ rarely make the top purchases.

---

## Gaps
- Apple publishes only the top 10 IAPs per game. Large packs ($49.99 and $99.99) are visible only for Toy Blast.
- Coin or gold amounts were not found for Royal Kingdom, Candy Crush Soda Saga or Toon Blast packs.
- Royal Match amounts above $7.99 were not found either, apart from a low-reliability reader comment.
- Bundle contents (Treasures, Favors, Pouch, Chest, the "Concept" offers, the Peak bundles) are not published anywhere I could reach. The only exception is Toy Blast's $19.99 Super Bundle.
- Pass prices for Candy Crush Saga (varies per player), Candy Crush Soda Saga (2024 article) and Toon Blast (the wiki contradicts itself) are not confirmed on any store page.
- No starter-pack price was found except Toy Blast's Beginner's Bundle at $1.99. No no-ads or subscription SKUs appear in any of the six top-10 lists.
- These sources refused to load: NamuWiki (403 to WebFetch; read through Firecrawl instead), the King Zendesk Season Pass article (403), u4gm (403), Fandom pages (402 to WebFetch; read through Firecrawl instead), and the LootBar page (empty).
