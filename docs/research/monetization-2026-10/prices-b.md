# US in-app purchase prices: Playrix, Magic Tavern, Tactile and three jigsaw games

Checked on 2026-10-23 (US App Store, USD). Machine-readable copy: `prices-b.json` in this folder.

**Türkçe özet.** Dokuz oyunun ABD App Store'daki en çok satan 10'ar satın alması toplandı (90 kalem). En sık fiyatlar $1.99, $4.99, $0.99 ve $9.99. Kumbara (piggy bank) neredeyse her yerde $2.99. Sezon kartı (pass) $3.99 (Project Makeover) ile $4.99 (Gardenscapes Golden ticket) arasında. Reklam kaldırma jigsaw oyunlarında $14.99. Yalnız Magic Jigsaw abonelik satıyor (haftalık $7.99, 7 gün ücretsiz deneme; aylık $19.99). Mağaza kuralları: Apple'da küçük işletme programıyla %15; Google Play'de ABD'de 30 Haziran 2026'dan beri yeni kurulumlarda ilk 1 milyon dolara %10 + %5 faturalama ücreti. Rastgele ödül (loot box) satılırsa iki mağaza da olasılıkların satın almadan önce gösterilmesini istiyor.

## How to read this

- **FACT**: seen on the source named in the row (for prices: the US App Store listing).
- **REPORTED**: from a secondary source (wiki, blog, analyst, player post).
- **ESTIMATE**: my own inference, for example the *kind* of an item whose store name gives no contents, or a coins-per-dollar figure I computed.
- Every price below is a FACT from the US App Store listing on 2026-10-23. The App Store lists only the **top 10 in-app purchases** of an app (the catalog marks the list as truncated), so bigger packs such as $49.99 or $99.99 may exist but are not visible.
- Store data came from the US App Store catalog (read through the firecrawl `apps-apple-com` provider, storefront `us`). For Homescapes, Gardenscapes and Fishdom I also scraped the rendered page; both gave the same lists.
- "units/$" = coins (or gems) per dollar, computed by me (ESTIMATE) from the contents the store states.

## Summary across the nine games

| Kind | Prices seen (USD) | Note |
|---|---|---|
| Coins / gems / diamonds | 0.99 ×8, 1.99 ×9, 2.99 ×2, 4.99 ×8, 5.99, 6.99, 9.99 ×7, 19.99 ×2 | Entry is always $0.99 or $1.99; $9.99 is the usual top of the visible list |
| Bundles (coins + boosters, offers) | 0.99 – 15.99 | Playrix "kits" and "packs" sit at 0.99 / 1.99 / 5.99 / 6.99 / 9.99 / 15.99 |
| Starter | 1.99, 4.99, 4.99 | Homescapes has both a $1.99 Starter Pack and a $4.99 Starter Kit |
| Piggy bank | 2.99 ×4 (+ 5.99 and 19.99 if Project Makeover's "Stylish Stash" is one) | Gardenscapes Gold Reserve, Homescapes Gold Reserve, Fishdom Diamond Fishy Bank, Lily's Roxy's Reserve |
| Pass | 3.99 (Project Makeover "Camping Expedition"), 4.99 (Gardenscapes "Golden ticket") | Both are one-off purchases, not subscriptions |
| No ads | 14.99 (both Easybrain games), 4.99 (ZiMAD Gold Pack, which also adds content) | |
| Subscription | 7.99 / week (7-day free trial), 19.99 / month | Magic Jigsaw only |
| Content packs (puzzle pictures) | 0.99 single puzzle, 3.99 themed pack | Easybrain Jigsaw Puzzles |

Price ladder over all 90 visible items (FACT counts): $0.99 ×14, $1.99 ×18, $2.99 ×9, $3.99 ×3, $4.99 ×17, $5.99 ×5, $6.99 ×4, $7.99 ×1, $9.99 ×9, $12.99 ×1, $14.99 ×3, $15.99 ×2, $19.99 ×4. Median $4.99, mean $5.55.

Volume bonus on coin packs (ESTIMATE, computed from store contents):

| Game | Smallest pack | Largest visible pack | Bonus per dollar at the top |
|---|---|---|---|
| Lily's Garden | $0.99 → 1,000 (1,010/$) | $19.99 → 25,000 (1,251/$) | +24 % |
| Matchington Mansion | $1.99 → 1,100 (553/$) | $9.99 → 7,000 (701/$) | +27 % |
| Project Makeover (gems) | $1.99 → 500 (251/$) | $19.99 → 6,700 (335/$) | +33 % |
| Jigsaw Puzzles (Easybrain) | $0.99 → 1,200 (1,212/$) | $9.99 → 20,000 (2,002/$) | +65 % |
| Magic Jigsaw Puzzles | $0.99 → 1,000 (1,010/$) | $4.99 → 5,000 (1,002/$) | none |
| Gardenscapes | $0.99 → 1,000 coins | $9.99 → 5,000 coins + 4 power-up types (bundle) | not comparable |

## Homescapes (Playrix)

Store: https://apps.apple.com/us/app/homescapes/id1195621598 · checked 2026-10-23 · version 9.2.102 (2026-10-14) · 4.6 from 2.18 M ratings. In-app events on the page: **Spooky Pass** (new season; stages, levels, keys), Four Elements (team event), Night in the Castle (expedition). The age-rating block says the app contains loot boxes.

| Item | USD | Contents | Kind | Status |
|---|---|---|---|---|
| Handful of Coins | 0.99 | coins (amount not shown) | coins | FACT |
| Starter Pack | 1.99 | not shown | starter | FACT |
| Handful of Coins | 1.99 | coins | coins | FACT |
| Gold Reserve | 2.99 | not shown; same name as Gardenscapes' piggy bank | piggy_bank (kind ESTIMATE) | FACT |
| Apprentice Pack | 6.99 | not shown | bundle (ESTIMATE) | FACT |
| Handful of Coins | 4.99 | coins | coins | FACT |
| Handful of Coins | 0.99 | coins (older product id) | coins | FACT |
| Pro Pack | 15.99 | not shown | bundle (ESTIMATE) | FACT |
| Handful of Coins | 9.99 | coins | coins | FACT |
| Starter Kit | 4.99 | not shown | starter | FACT |

Secondary:
- REPORTED (PocketGamer.biz, 2017-10-13, seen 2026-10-23): coin packs then ran from $0.99 for 1,000 coins to $99.99 for 150,000 coins; one life cost 900 coins, about $0.90. https://www.pocketgamer.biz/how-does-homescapes-monetise/ . Old; today's pack sizes are not on the store.
- REPORTED (Homescapes wiki, seen 2026-10-23): coins buy extra moves (900–3,800), boosters (900–3,800) and a lives refill (900); a level win pays 50–150 coins. https://homescapes.fandom.com/wiki/Coins
- Gap: the Spooky Pass premium price does not appear in the top-10 list and I found no source for it.

## Gardenscapes (Playrix)

Store: https://apps.apple.com/us/app/gardenscapes/id1105855019 · checked 2026-10-23 · version 10.0.0 (2026-10-15) · 4.7 from 1.69 M ratings · #25 Casual (top free). Events on the page: Magic Collection (card sets), **Spooky Pass** (new season; levels and keys), Polar Station Mystery (expedition). The age-rating block lists loot boxes; the description says some purchasable items are randomized.

| Item | USD | Contents | Kind | Status |
|---|---|---|---|---|
| Gold Reserve | 2.99 | piggy bank: part of the gold won on levels goes into a safe, buyable once full enough (REPORTED, Mistplay) | piggy_bank | FACT |
| 1000 Coins | 1.99 | 1,000 coins | coins | FACT |
| Hot Offer | 1.99 | 1,500 coins + 3 types of power-ups | bundle | FACT |
| 1000 Coins | 0.99 | 1,000 coins | coins | FACT |
| Botanist Kit | 5.99 | 2,000 coins + 4 types of power-ups | bundle | FACT |
| Golden ticket | 4.99 | premium track of the season / Garden Pass (REPORTED) | pass | FACT |
| Garden Kit | 1.99 | not shown | bundle (ESTIMATE) | FACT |
| Garden Kit | 1.99 | not shown (second product id) | bundle (ESTIMATE) | FACT |
| Florist Kit | 9.99 | 5,000 coins + 4 types of power-ups | bundle | FACT |
| Garden Kit | 0.99 | not shown | bundle (ESTIMATE) | FACT |

Note: the store sells "1000 Coins" both at $0.99 and at $1.99 (two product ids); which one a player sees is not visible from outside (ESTIMATE: an older and a newer price, or a price test).

Secondary:
- REPORTED (Mistplay, 2026-01-30): Gold Reserve sells at $2.99 and is the game's third best-selling in-app purchase on iOS in the US, per Sensor Tower. https://business.mistplay.com/resources/piggy-banks-in-games/
- REPORTED (Gardenscapes wiki, Furry Season, first run Nov 2019): 30 stages of 30 points each; a free chain and a golden chain; points per level depend on difficulty and attempts. Golden chain: 8 lives instead of 5, boosters, timed unlimited boosters, a pet at stage 25 and about 3,500 coins in total (2,000 at the last stage). Free chain: about 500 coins, 30–60 minute unlimited lives and single boosters. Coin totals summed by me. https://gardenscapes.fandom.com/wiki/Furry_Season
- REPORTED (Facebook group, search excerpt): a player values the Golden Ticket at $4.99 for almost 30 days of boosters; another says it is a season ticket, not a subscription. https://www.facebook.com/groups/gardenscapes.family/posts/1270352150343648/
- REPORTED (Reddit, search excerpt, thread date not verified): some players saw a season at $9.99 while others still saw $4.99. https://www.reddit.com/r/Gardenscapes/comments/1eue0wm/price_increase_for_new_season/
- REPORTED (Gardenscapes Strategy blog, 2026-08-22, citing the Playrix Help Center): current pass perks are 8 lives instead of 5, a gold frame and name colour, extra moves on hard levels, Bonus Bank rewards and a 50-coin gift to teammates, all ending with the season. https://www.gardenscapesstrategy.com/2026/10/garden-pass-more-important-than-levels.html?m=1

## Fishdom (Playrix)

Store: https://apps.apple.com/us/app/fishdom/id664575829 · checked 2026-10-23 · version 10.1.6 (2026-10-12) · 4.6 from 1.15 M ratings. Events on the page: **Spooky Pass** (new season; levels and keys), Toxic Business (expedition). Age-rating block lists loot boxes.

| Item | USD | Contents | Kind | Status |
|---|---|---|---|---|
| Wonderous Collection | 1.99 | not shown | bundle (ESTIMATE) | FACT |
| Diamond Fishy Bank | 2.99 | not shown; name points to a diamond piggy bank | piggy_bank (ESTIMATE) | FACT |
| A few Diamonds | 1.99 | store text: "Get the Diamond Stash bundle" | coins | FACT |
| A few Diamonds | 0.99 | diamonds, amount not shown | coins | FACT |
| Hatchling's Cache | 6.99 | not shown | bundle (ESTIMATE) | FACT |
| A few Diamonds! | 0.99 | diamonds (older product id) | coins | FACT |
| Handful of Diamonds | 4.99 | diamonds, amount not shown | coins | FACT |
| Crab's Finds | 15.99 | not shown | bundle (ESTIMATE) | FACT |
| Marvelous Collection | 5.99 | store text: "Get the Diamond Stash bundle" | bundle | FACT |
| Handful of Diamonds | 4.99 | diamonds (older product id) | coins | FACT |

Gap: no reliable source found for diamond counts, the Fishy Bank mechanics or the pass price.

## Matchington Mansion (Magic Tavern)

Store: https://apps.apple.com/us/app/matchington-mansion/id1216575026 · checked 2026-10-23 · version 1.212.0 (2026-10-16) · 4.3 from 301 K ratings · #156 Family (top free).

| Item | USD | Contents | Kind | Status | units/$ (ESTIMATE) |
|---|---|---|---|---|---|
| 1100 Matchington Mansion Coins | 1.99 | 1,100 coins | coins | FACT | 553 |
| Stack of Coins | 1.99 | 1,100 coins | coins | FACT | 553 |
| Novice Furnisher | 2.99 | 1,600 coins + pre-level boosters | bundle | FACT | |
| Aspiring Decorator | 5.99 | 3,000 coins + pre-level boosters | bundle | FACT | |
| Bag of Coins | 4.99 | 3,000 coins | coins | FACT | 601 |
| Coin Piggy | 9.99 | 7,000 coins (a plain coin pack despite the name) | coins | FACT | 701 |
| Coin Bundle 1.99-1 | 1.99 | store text: "Bag of Coins" | coins | FACT | |
| 3000 Matchington Mansion Coins | 4.99 | 3,000 coins | coins | FACT | 601 |
| Talented Designer | 12.99 | 7,000 coins + some boosters | bundle | FACT | |
| 7000 Matchington Mansion Coins | 9.99 | 7,000 coins | coins | FACT | 701 |

Secondary: REPORTED (search excerpt of a Playing for the Planet participant page): the game ran a Battle Pass tied to a June–July 2025 event; no price given. https://www.playing4theplanet.org/2025-participants/matchington-mansion

## Lily's Garden: Match & Design (Tactile Games)

Store: https://apps.apple.com/us/app/lilys-garden-match-design/id1437783446 · checked 2026-10-23 · version 3.86.0 (2026-10-21) · 4.8 from 272 K ratings.

| Item | USD | Contents | Kind | Status | units/$ (ESTIMATE) |
|---|---|---|---|---|---|
| Quick Refill | 0.99 | not shown (name suggests a lives or moves refill: ESTIMATE) | other | FACT | |
| Pack of Coins | 0.99 | 1,000 coins | coins | FACT | 1,010 |
| Pile of Coins | 4.99 | 5,500 coins | coins | FACT | 1,102 |
| Quick Refill | 2.99 | not shown | other | FACT | |
| Bag of Coins | 9.99 | 12,000 coins | coins | FACT | 1,201 |
| Roxy's Reserve | 2.99 | store text: buy coins from Roxy's Reserve (coin reserve) | piggy_bank (ESTIMATE) | FACT | |
| Chest of Coins | 19.99 | 25,000 coins | coins | FACT | 1,251 |
| Special Offer | 9.99 | not shown | bundle (ESTIMATE) | FACT | |
| Special Offer | 4.99 | not shown | bundle (ESTIMATE) | FACT | |
| Special Offer | 6.99 | not shown | bundle (ESTIMATE) | FACT | |

Gap: two player Q&A pages ask what Roxy's Reserve is but carry no answer (appgamer.com); no source for Quick Refill.

## Project Makeover (Magic Tavern)

Store: https://apps.apple.com/us/app/project-makeover/id1483058899 · checked 2026-10-23 · version 2.149.1 (2026-10-12) · 4.5 from 800 K ratings.

| Item | USD | Contents | Kind | Status | units/$ (ESTIMATE) |
|---|---|---|---|---|---|
| Pile of Gems | 1.99 | 500 gems | coins | FACT | 251 |
| Purse Full of Gems | 4.99 | 1,500 gems | coins | FACT | 301 |
| Tiara Class Package | 9.99 | 3,300 gems | coins | FACT | 330 |
| Special Offer(01) | 1.99 | gems | bundle (ESTIMATE) | FACT | |
| Mini Gem Package | 1.99 | gems, amount not shown | coins | FACT | |
| High Heel Package | 19.99 | 6,700 gems | coins | FACT | 335 |
| Stylish Stash | 5.99 | store text: contains a lot of gems | piggy_bank (ESTIMATE) | FACT | |
| Camping Expedition | 3.99 | store text: a pass for boosters and gems | pass | FACT | |
| Stylish Stash | 19.99 | store text: contains a lot of gems | piggy_bank (ESTIMATE) | FACT | |
| Special Offer | 4.99 | gems and boosters | bundle | FACT | |

The pass is a one-off purchase (the catalog marks it as not a subscription).

## Jigsaw Puzzles - Puzzle Games (Easybrain)

Store: https://apps.apple.com/us/app/jigsaw-puzzles-puzzle-games/id1324604053 · checked 2026-10-23 · version 4.10.0 (2026-10-06) · 4.7 from 1.11 M ratings · #48 Board (top free).

| Item | USD | Contents | Kind | Status | units/$ (ESTIMATE) |
|---|---|---|---|---|---|
| Jigsaw Puzzles No Ads | 14.99 | removes ads | no_ads | FACT | |
| Big Pack of Coins | 2.99 | 4,500 coins | coins | FACT | 1,505 |
| Epic Pack of Coins | 9.99 | 20,000 coins | coins | FACT | 2,002 |
| Small Pack of Coins | 0.99 | 1,200 coins | coins | FACT | 1,212 |
| Relaxing Puzzle | 0.99 | one puzzle picture | other | FACT | |
| Winter Puzzle - Premium Jigsaw | 0.99 | one premium puzzle | other | FACT | |
| Giant Pack of Coins | 6.99 | 12,000 coins | coins | FACT | 1,717 |
| Color Burst | 3.99 | themed puzzle pack | other | FACT | |
| Beaches | 3.99 | themed puzzle pack | other | FACT | |
| Hard Jigsaw Puzzle | 0.99 | one hard puzzle | other | FACT | |

Worth noting for a painting game: single pictures sell at $0.99 and themed picture packs at $3.99.

## Magic Jigsaw Puzzles - Games (ZiMAD)

Store: https://apps.apple.com/us/app/magic-jigsaw-puzzles-games/id439873467 · checked 2026-10-23 · version 4.31.1 (2026-10-15) · 4.8 from 388 K ratings · seller XIMAD, Inc.

| Item | USD | Contents | Kind | Status |
|---|---|---|---|---|
| One Week | 7.99 | unlimited access to 21,000+ puzzles; renews weekly; 7-day free trial | subscription | FACT |
| Gold Pack | 4.99 | no ads, puzzles from own photos, 3 packs (80 pictures), 4 music tracks | no_ads | FACT |
| Firework Boosters Pack | 1.99 | firework boosters | boosters | FACT |
| One Month | 19.99 | unlimited access to 21,000+ puzzles; renews monthly | subscription | FACT |
| 5000 coins | 4.99 | 5,000 coins | coins | FACT |
| Super discount | 1.99 | not shown | bundle (ESTIMATE) | FACT |
| 1000 coins | 0.99 | 1,000 coins | coins | FACT |
| Expensive boost | 4.99 | not shown beyond the name | boosters (ESTIMATE) | FACT |
| Cheap boost | 2.99 | not shown beyond the name | boosters (ESTIMATE) | FACT |
| 3000 coins | 2.99 | 3,000 coins | coins | FACT |

## Art Puzzle - Jigsaw Games (Easybrain)

Store: https://apps.apple.com/us/app/art-puzzle-jigsaw-games/id1519174001 · checked 2026-10-23 · version 5.3.0 (2026-10-12) · 4.6 from 289 K ratings · #69 Board (top free).

| Item | USD | Contents | Kind | Status |
|---|---|---|---|---|
| Art Puzzle Hints (Medium Package) | 1.99 | hints | boosters | FACT |
| Art Puzzle Hints (Large) | 4.99 | hints | boosters | FACT |
| Art Puzzle No Ads | 14.99 | removes ads | no_ads | FACT |
| Art Puzzle Hints (Small Package) | 0.99 | hints | boosters | FACT |
| Art Puzzle Starter Package | 4.99 | head start for the collection | starter | FACT |
| Art Puzzle Comfort Package | 4.99 | for long, stress-free sessions | bundle | FACT |
| Art Puzzle Progress Package | 14.99 | to reveal art faster | bundle | FACT |
| Art Puzzle Coins (Large Pack) | 9.99 | coins, amount not shown | coins | FACT |
| Art Puzzle Coins (Mini Pack) | 1.99 | coins, amount not shown | coins | FACT |
| Art Puzzle Coins (Medium Pack) | 5.99 | coins, amount not shown | coins | FACT |

## Store rules that shape a price chart (status as of 2026-10-23)

### Apple App Store

- **Price points** (FACT): 900 price points from US $0.29 to $10,000 (https://developer.apple.com/in-app-purchase/). By default 800 are open; the 100 highest (up to $10,000) need a request (https://developer.apple.com/help/app-store-connect/manage-in-app-purchases/set-a-price-for-an-in-app-purchase/). Every price in the sample ends in .99.
- **Purchase types** (FACT): consumable, non-consumable, auto-renewable subscription and non-renewing subscription (https://developer.apple.com/in-app-purchase/).
- **Commission** (FACT): 30 % standard; 15 % in the App Store Small Business Program; 15 % for qualifying subscriptions (https://developer.apple.com/programs/whats-included/). The program needs enrolment; proceeds must stay at or under $1 M both in the previous and in the current calendar year; going over it moves you to the standard rate for the rest of that year, and you can re-qualify the year after falling back under (https://developer.apple.com/app-store/small-business-program/). Apple's own news describes the subscription rate as the one for auto-renewals after the first year (FACT, seen in the 2026-03-12 China-storefront note, https://developer.apple.com/news/?id=dadukodv; I assume the same definition holds in the US: ESTIMATE).
- **Subscriptions** (FACT, guideline 3.1.2(a)): an auto-renewable subscription must give ongoing value, last at least seven days and work on all the user's devices (https://developer.apple.com/app-store/review/guidelines/).
- **Virtual currency** (FACT, guideline 3.1.1): currencies bought with in-app purchase may not expire, and restorable purchases need a restore mechanism.
- **Loot boxes** (FACT, guideline 3.1.1): apps that sell randomized virtual items must show the odds of each item type before purchase. The US store pages of Homescapes, Gardenscapes and Fishdom now show "Loot Boxes" in their age-rating block (FACT, seen on the pages).
- **US link-out** (FACT, guideline 3.1.1(a)): on the US storefront an app may include buttons, links and other calls to action to outside purchase methods without an entitlement. Commission on those web purchases: 0 % for now, pending a court decision on a "reasonable" rate (REPORTED, https://www.neonpay.com/blog/apple-app-store-alternative-payment-fees-what-developers-pay-in-2026). In an August 2026 court filing Apple proposed 15 % for standard apps and 5 % for Small Business Program apps (REPORTED, TechCrunch 2026-08-14, https://techcrunch.com/2026/08/14/apple-proposes-to-take-a-15-cut-of-purchases-made-outside-the-app-store/). This is still open.
- Recent Apple developer news (FACT, https://developer.apple.com/news/): App Review Guidelines updated on 2026-10-16; tax and price updates on 2026-08-27; no US commission change announced between August and October 2026.

### Google Play

- **Price range** (FACT): United States, USD 0.05 – 999.99 (https://play.google.com/supported-locations?hl=en).
- **Service fees in the US since 2026-06-30** (FACT, https://support.google.com/googleplay/android-developer/answer/16954621 and https://support.google.com/googleplay/android-developer/answer/112622). The rate depends on whether the user's install came before or after 2026-06-30:

| | Non-recurring, new installs | Non-recurring, existing installs | Auto-renewing subscriptions |
|---|---|---|---|
| First $1 M of annual earnings | 10 % | 10 % | 10 % |
| Standard | 20 % | 25 % in-app / 20 % external web link | 10 % |
| Games Level Up or Apps Experience program | 15 % | 20 % in-app / 15 % external web link | 10 % |

  Plus a **5 % billing fee** when the purchase goes through Google Play Billing (US, UK, EEA, AU, JP). Outside these regions the older model still applies: 15 % on the first $1 M, 30 % above it, 15 % on subscriptions.
- **Loot boxes** (FACT, Payments policy, seen as an excerpt of the official page): apps that offer randomized virtual items from a purchase must clearly show the odds before, and close to, the purchase (https://support.google.com/googleplay/android-developer/answer/9858738).
- Google also documents alternative billing for US users (https://support.google.com/googleplay/android-developer/answer/16497028; existence seen, details not read).

### What this means for a new game under $1 M a year (ESTIMATE)

- Apple with the Small Business Program: 15 % on everything. Google Play with Play Billing on a new install: 10 % + 5 % = 15 % on one-off items and on subscriptions. So both stores take about 15 % at our size.
- Passes are sold as one-off purchases in this sample (Gardenscapes $4.99, Project Makeover $3.99). An auto-renewing pass would have to meet Apple's 3.1.2 rules (at least 7 days, ongoing value); a one-off season pass does not.
- Any paid random reward (for example a mystery frame or a painting-piece chest) needs odds shown before purchase on both stores, and Apple now flags loot boxes on the store page.
- Coins bought with money must never expire (Apple 3.1.1). A season pass can still end with its season, because it is not a currency.

## Gaps

- The App Store shows only the top 10 purchases, so the most expensive packs ($49.99, $99.99) of these games are not visible.
- No current source for Playrix coin and diamond counts (only a 2017 Homescapes figure), for Homescapes and Fishdom pass prices, for Fishdom's Fishy Bank mechanics, or for Lily's Garden "Quick Refill" and "Roxy's Reserve".
- Matchington Mansion's Battle Pass price and the length of the Project Makeover pass were not found.
- Fandom pages refused WebFetch (HTTP 402) but opened through firecrawl; the Gardenscapes "Golden Ticket" wiki page does not exist (404).
- Apple's US link-out commission is still before the court; recheck before launch.
