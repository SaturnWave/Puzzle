# Verification of the monetization research (fact-check)

Checked on **2026-10-23** by the fact-checker. Every source below was re-opened on that date.
Files checked: `prices-a.md`, `prices-a.json`, `prices-a-context.json`, `prices-b.md`, `prices-b.json`, `pass-level.md`, `minigames.md`, `meta.md`, `events-social.md`.
Nothing in those files was changed. Corrections are listed in section 3.

## Türkçe kısa özet

- **Fiyatlar:** 15 oyunun ABD App Store sayfası yeniden açıldı. Görünen 150 satın alma kaleminin 150'si de araştırmacıların yazdığı fiyatla aynı çıktı (Royal Pass $9.99, Kingdom Pass $9.99, Gardenscapes Golden ticket $4.99, Project Makeover pass $3.99, tüm başlangıç paketleri, Candy Crush ve Homescapes coin paketleri).
- **Doğrulanamayanlar:** Toy Blast için yazılan "API listesi" (1.625 coin $49.99, 3.500 coin $99.99, 750 coin + 6 saat can + booster içeren $19.99 Super Bundle, $0.99 Super Sale ve iki Special Offer) bugün iki ayrı API çağrısında, web sayfasında ve App Pricing Lab'de görünmüyor. Bu yüzden "$49.99 ve $99.99 fiyat basamakları FACT" ifadesi düzeltilmeli. Toon Pass'in $9.99 / $19.99 fiyatı da hiçbir sayfada görülmedi.
- **AppMagic raporu:** "En çok satan teklifler ve yüzde payları" grafiği App Store değil **ABD Google Play, son altı ay** verisi. Yüzdeler ve ARPPU değerleri ($69/$107/$135) PDF'in metninde yok, yalnızca grafikte; metinden doğrulanamadı. Medyan $1.99, ortalama $6.05 ve "$6–15 arası LiveOps teklifleri" doğrulandı.
- **İddialar:** 31 iddia kaynağından yeniden kontrol edildi; 27'si doğrulandı. Pass kuralları, King's Nightmare, Homescapes mini oyunları, Lava Quest'in simüle rakipleri, takım kuralları, tutundurma kıyasları, Apple ve Google kuralları ve yeni Google Play ücretleri doğru.
- **Eksik bulunan ve doldurulan konu:** ABD'de FTC'nin oyun içi satın almalara yönelik yaptırımları (Epic Games 245 milyon dolar, HoYoverse 20 milyon dolar). Fiyat ve pass tasarımını doğrudan etkiliyor: tek dokunuşla satın alma, karışık sanal para, 16 yaş altına rastgele paket satışı.
- **Hâlâ açık:** Unity IAP / Google Play Billing'in bu build'de INTERNET iznini ekleyip eklemediği. Kaynak bulunamadı; bir test APK'sının birleşik manifest'ine bakmak gerekiyor.

## Labels used in this file

- **CONFIRMED**: the source, re-opened today, says what the researcher wrote.
- **CORRECTED**: the source says something different; the right version is given.
- **NOT REPRODUCED**: the researcher's reading could not be seen again today on the same source or a second one.
- **UNSUPPORTED**: no source found today that says it.
- **PLAUSIBLE**: partly supported; the rest is not on the source.

How the store prices were read: the rendered US App Store page through Firecrawl (directQuote extraction of the "In-App Purchases" list), and for Toy Blast, Magic Jigsaw, Gardenscapes, Lily's Garden and Project Makeover also Apple's catalog API view `top-in-app-purchasables` (Firecrawl provider `apps-apple-com`, storefront `us`). Apple shows only the top 10 in-app purchases per app.

---

## 1. Price checks (US App Store, USD, seen 2026-10-23)

| # | Item(s) checked | File | Researcher's price | Seen today | Verdict | Source |
|---|---|---|---|---|---|---|
| P1 | Royal Match: Mini Coin Package, Royal Favor, Duke's Treasure, Prince's Treasure, Mini Offer, Tiny Concept Offer, Queen's Treasure, **Royal Pass**, Normal Concept Offer, Mini Concept Offer (10 items) | prices-a | 1.99, 6.99, 5.99, 9.99, 2.99, 0.99, 19.99, 9.99, 6.99, 2.99 | same 10 prices | CONFIRMED (10/10) | https://apps.apple.com/us/app/royal-match/id1482155847 |
| P2 | Royal Kingdom: Mini Coin Pack, Splendid Pouch, King's Favor, Noble Pack, Superior Chest, Mini Offer, **Kingdom Pass**, Small Coin Package, Mini Concept Offer, Normal Concept Offer | prices-a | 1.99, 9.99, 6.99, 5.99, 19.99, 2.99, 9.99, 7.99, 2.99, 6.99 | same | CONFIRMED (10/10) | https://apps.apple.com/us/app/royal-kingdom/id1606549505 |
| P3 | Candy Crush Saga coin ladder and items: 10x Gold Bars package, XS Gold Bars Pack, S Gold Bars Pack, Extra Moves ×3, Extra Lives, Extra small offer, Small offer, Lollipop Hammer | prices-a | 0.99, 1.99, 7.99, 0.99 ×3, 0.99, 2.99, 5.99, 1.99 | same | CONFIRMED (10/10) | https://apps.apple.com/us/app/candy-crush-saga/id553834731 |
| P4 | Candy Crush Soda Saga: Dainty offer, Extra Small Gold Bar Package, Hard Currency 2 XS, Hard Currency 3 XS, Small Gold Bar Package, Sweet offer ×3, Offer Small, Juicy offer | prices-a | 1.99, 0.99, 0.99, 1.99, 4.99, 3.99/4.99/5.99, 6.99, 8.99 | same | CONFIRMED (10/10) | https://apps.apple.com/us/app/candy-crush-soda-saga/id850417475 |
| P5 | Toon Blast: Tiny Coin Pack ×2, Super Bundle ×2, Toon Bundle ×2, Small Coin Pack ×2, Mini Bundle, Big Team Bundle | prices-a | 1.99 ×2, 12.99/19.99, 6.99/9.99, 4.99/7.99, 6.99, 4.99 | same | CONFIRMED (10/10) | https://apps.apple.com/us/app/toon-blast/id1176027022 |
| P6 | Toy Blast "web" list: Tiny Coin Pack, 25 Coins, Super Bundle, 140 Coins, Toy Bundle, Small Coin Pack, 300 Coins, Mega Bundle, 625 Coins, **Beginner's Bundle** (starter) | prices-a | 1.99, 0.99, 12.99, 4.99, 6.99, 4.99, 9.99, 24.99, 19.99, 1.99 | same, on the web page and in two API calls | CONFIRMED (10/10) | https://apps.apple.com/us/app/toy-blast/id890378044 ; catalog API (source_url `.../catalog/us/apps/890378044?...views=top-in-app-purchasables`) |
| P7 | Toy Blast "api" list: 1625 Coins, 3500 Coins, Super Bundle (750 coins + 6 h lives + 3 of 4 boosters), Super Sale, Special Offer, Special Offer | prices-a / prices-a.json (product ids 907077228, 1036169827, 1041887593, 1041886365 …) | 49.99, 99.99, 19.99, 0.99, 0.99, 4.99 | **none of these six appear**: two API calls (by id and by URL) return the same 10 items as the web page; App Pricing Lab's 2026-03-25 snapshot has the same 10 | NOT REPRODUCED (0/6) | same App Store page and API; https://apppricinglab.com/iap/apple/890378044 |
| P8 | Homescapes: Handful of Coins ×4 ($0.99 ×2, $1.99, $4.99, $9.99 = 5 SKUs), **Starter Pack**, **Starter Kit**, Gold Reserve, Apprentice Pack, Pro Pack | prices-b | 0.99 ×2, 1.99, 4.99, 9.99, 1.99, 4.99, 2.99, 6.99, 15.99 | same; age-rating block shows "Loot Boxes" | CONFIRMED (10/10) | https://apps.apple.com/us/app/homescapes/id1195621598 |
| P9 | Gardenscapes: Gold Reserve, 1000 Coins ×2, Hot Offer, Botanist Kit, **Golden ticket**, Garden Kit ×3, Florist Kit; contents of Hot Offer (1,500 + 3 power-ups), Botanist (2,000 + 4), Florist (5,000 + 4) | prices-b | 2.99, 1.99/0.99, 1.99, 5.99, 4.99, 1.99/1.99/0.99, 9.99 | same; API marks Golden ticket as not a subscription; contents as stated | CONFIRMED (10/10) | https://apps.apple.com/us/app/gardenscapes/id1105855019 |
| P10 | Fishdom: Wonderous Collection, Diamond Fishy Bank, A few Diamonds ×3, Hatchling's Cache, Handful of Diamonds ×2, Crab's Finds, Marvelous Collection | prices-b | 1.99, 2.99, 1.99/0.99/0.99, 6.99, 4.99 ×2, 15.99, 5.99 | same | CONFIRMED (10/10) | https://apps.apple.com/us/app/fishdom/id664575829 |
| P11 | Matchington Mansion: 1100 Coins, Stack of Coins, Novice Furnisher, Aspiring Decorator, Bag of Coins, Coin Piggy, Coin Bundle 1.99-1, 3000 Coins, Talented Designer, 7000 Coins | prices-b | 1.99, 1.99, 2.99, 5.99, 4.99, 9.99, 1.99, 4.99, 12.99, 9.99 | same | CONFIRMED (10/10) | https://apps.apple.com/us/app/matchington-mansion/id1216575026 |
| P12 | Lily's Garden: Quick Refill ×2, Pack/Pile/Bag/Chest of Coins (1,000 / 5,500 / 12,000 / 25,000), Roxy's Reserve, Special Offer ×3 | prices-b | 0.99/2.99, 0.99/4.99/9.99/19.99, 2.99, 9.99/4.99/6.99 | same; coin amounts as stated in Apple's descriptions | CONFIRMED (10/10) | https://apps.apple.com/us/app/lilys-garden-match-design/id1437783446 |
| P13 | Project Makeover: Pile of Gems (500), Purse (1,500), Tiara (3,300), High Heel (6,700), Special Offer(01), Mini Gem Package, Stylish Stash ×2, **Camping Expedition** (pass), Special Offer | prices-b | 1.99, 4.99, 9.99, 19.99, 1.99, 1.99, 5.99/19.99, 3.99, 4.99 | same; Apple's description calls Camping Expedition a pass; not a subscription | CONFIRMED (10/10) | https://apps.apple.com/us/app/project-makeover/id1483058899 |
| P14 | Jigsaw Puzzles (Easybrain): No Ads, Big/Epic/Small/Giant Pack of Coins, Relaxing Puzzle, Winter Puzzle, Color Burst, Beaches, Hard Jigsaw Puzzle | prices-b | 14.99, 2.99/9.99/0.99/6.99, 0.99, 0.99, 3.99, 3.99, 0.99 | same | CONFIRMED (10/10) | https://apps.apple.com/us/app/jigsaw-puzzles-puzzle-games/id1324604053 |
| P15 | Magic Jigsaw Puzzles: **One Week** subscription (7-day free trial), Gold Pack, Firework Boosters Pack, **One Month**, 5000/1000/3000 coins, Super discount, Expensive boost, Cheap boost | prices-b | 7.99, 4.99, 1.99, 19.99, 4.99/0.99/2.99, 1.99, 4.99, 2.99 | same in the catalog API (P7D free trial on One Week) | CONFIRMED (10/10) | https://apps.apple.com/us/app/magic-jigsaw-puzzles-games/id439873467 (catalog API) |
| P16 | Art Puzzle (Easybrain): Hints S/M/L, No Ads, **Starter Package**, Comfort Package, Progress Package, Coins Mini/Medium/Large | prices-b | 0.99/1.99/4.99, 14.99, 4.99, 4.99, 14.99, 1.99/5.99/9.99 | same | CONFIRMED (10/10) | https://apps.apple.com/us/app/art-puzzle-jigsaw-games/id1519174001 |
| P17 | Toon Blast Golden Pass $9.99 and Diamond Pass $19.99 | pass-level, prices-a | 9.99 / 19.99 (REPORTED) | not in the US top 10; the cited Gambia storefront page today lists 10 items (Tiny Coin Pack, Tiny Coin Wheel, Tiny Offer, Small Coin Pack, Medium Coin Pack $14.99, Super Bundle $19.99, Tiny/Mini Carnival Craze, Big/Small Team Bundle) and no pass; a web search found no Toon Pass price | UNSUPPORTED (0/2) | https://apps.apple.com/gm/app/toon-blast/id1176027022 ; https://apps.apple.com/us/app/toon-blast/id1176027022 |

**Price totals:** 158 prices checked, 150 confirmed, 6 not reproduced (P7), 2 unsupported (P17).
**Royal Match coin ladder:** only the $1.99 Mini Coin Package is on the store; the $7.99 = 5,000 coins pack and the larger packs remain REPORTED (GameMakers), not FACT. A current US App Store review on the same page confirms +5 moves costs 900 coins and the next set 1,900 coins.
**Candy Crush Gold Pass price:** not on the store page; it stays REPORTED (player posts).

---

## 2. Claim checks (the claims the proposal will lean on)

| # | Claim (short) | File(s) | Verdict | What the source says today | Source (seen 2026-10-23) |
|---|---|---|---|---|---|
| C1 | Royal Pass: 30 steps, calendar month, keys from wins, extra keys on Hard / Super Hard / Extremely Hard, unlock level 37, lives 5→8, gold name and frame, teammate gift, Bonus Bank 100 coins per 10 keys up to 5,000, unclaimed rewards go to the inventory | pass-level, events-social, prices-a | CONFIRMED | all items on the official page | https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/112-royal-pass/ |
| C2 | Kingdom Pass: level 31, monthly, extra keys from Hard / Super Hard / Kingdom / Dark Kingdom levels, lives 5→8, Bonus Bank 5,000 claimable only with the pass at the start of next month; stage count not stated | pass-level, prices-a, events-social | CONFIRMED | page says "several stages" | https://dreamgames.helpshift.com/hc/en/6-royal-kingdom-1676903479/faq/315-kingdom-pass-1710944877/ |
| C3 | Toon Pass: level 30, 1 badge per win, Golden (lives 5→8) and Diamond (lives 5→10) tiers, each buyable once per event, both can be bought | pass-level, events-social | CONFIRMED | as stated | https://peakgames.helpshift.com/hc/en/4-toon-blast/faq/322-what-is-toon-pass/ |
| C4 | Toon Pass prices $9.99 Golden / $19.99 Diamond | pass-level, prices-a | UNSUPPORTED | see P17 | as P17 |
| C5 | Homescapes Home Pass: 30 stages, season points from regular or Olympus Cup levels, level 26, 8 lives, golden frame and name, team-chat notice and teammate bonus, one season | pass-level | CONFIRMED | as stated | https://playrix.helpshift.com/hc/en/14-homescapes/faq/15526-home-pass-1755869542/?l=en |
| C6 | Candy Crush Season Pass: Free and Gold Pass; buying Gold later grants earlier Gold rewards; Bonus Deals trade leftover tokens for boosters and unlimited lives | events-social | CONFIRMED | as stated (the page loads; prices-a's note that it returned 403 applies to WebFetch only) | https://candycrush.zendesk.com/hc/en-us/articles/7897323489693--Season-Pass |
| C7 | Candy Crush 2026 quest pass: 10 tiers, points from daily / weekday quests and Star Tournament, 200 points per extra booster, end-of-map players complain; King CM said on 11 Feb 2026 it is a test | pass-level | PLAUSIBLE | tiers, quests, Star Tournament, 200-point boosters and the "4 episodes a week" complaint are on the thread; the staff reply was not in what I could read; another player in the same thread says end-of-map players *can* finish it through Star Tournament | https://community.king.com/en/candy-crush-saga/discussion/comment/3485774 |
| C8 | Candy Crush Soda: one $5.99 pass over 8 stages replaced by 7 / 14 / 21 stages at $4.99–$12.99 | pass-level, prices-a | CONFIRMED (date CORRECTED) | article published **2023-08-28** (modified 2024-07-26), not 2024 | https://www.gamigion.com/?p=2672 |
| C9 | Pass adoption: 21% of US iOS top-100 grossing (Dec 2019); 66% of top-20% games and about 66% of tracked casual games (Oct 2023) | pass-level | CONFIRMED | both figures on the pages; the 51% (Jun 2021) figure was not re-opened | https://www.gamerefinery.com/battle-pass-trend-mobile-games/ ; https://www.gamerefinery.com/12-ways-to-take-battle-passes-to-the-next-level-in-mobile-games/ |
| C10 | Mobile passes cost $5–15 in the basic tier and give 10–20× value; Clash Royale's pass cannibalized revenue | pass-level | CONFIRMED | as stated | https://www.deconstructoroffun.com/blog/2022/6/4/battle-passes-analysis |
| C11 | Royal Match 30-day revenue after launch: Royal Pass +31% (D30 +2%), Endless Treasure +20%, Sky Race +12%, Lightning Rush +9%, Piñata Party +6%, Team Treasure +2%, King's Nightmare +1% with downloads −21% and D14 almost +3% MoM | minigames, meta, events-social, prices-a | CONFIRMED | all numbers present; Naavik itself hedges the D14 attribution | https://naavik.co/deep-dives/royal-match/ (2022-05-25) |
| C12 | In 2022 competitors' passes were cheaper: Homescapes $4.99, Lily's Garden $5.99, Candy Crush Saga $6.99 | events-social | CONFIRMED | as stated | same Naavik page |
| C13 | King's Nightmare: unlocks after a number of wins, skippable, 50 coins for a win, no life lost on a fail | minigames, meta | CONFIRMED | as stated (fan wiki, so REPORTED is the right label in meta.md) | https://royalmatch.fandom.com/wiki/King%27s_Nightmare |
| C14 | Homescapes mini-games: since Jan 2020, every 5–10 levels from 1 to 100 then every 50, skippable, 50 coins + 1 star, 24 h to beat | minigames | CONFIRMED | as stated (fan wiki) | https://homescapes.fandom.com/wiki/Mini_Games |
| C15 | Playrix (2020): about 10 mini-games, about once every 20 levels; about 45% of players reached level 20 and 18% level 100; far levels about 0.03% | minigames | CONFIRMED | as stated | https://www.marketing-interactive.com/popular-mobile-games-gardenscapes-and-homescapes-called-out-for-misrepresentation-of-game-in-ads |
| C16 | Royal Match stars: 1 per level won, cannot be bought, spent on area tasks; Area Chest after all tasks | meta | CONFIRMED | as stated (fan wiki) | https://royalmatch.fandom.com/wiki/Stars_%26_Tasks |
| C17 | D30 Q3 2021 (App Annie): Royal Match 16.5%, HP Puzzles & Spells 16.2%, Best Fiends 12.2%, Gardenscapes 12.1%, Homescapes 11.4%; tied to events and foreshadowed content | meta | CONFIRMED, with one caveat | the article ties retention to events and foreshadowed content; "not to the meta" is the researcher's inference and should be labelled ESTIMATE | http://www.pocketgamer.biz/dream-games-royal-match-leads-match-3-d30-retention-at-165/ |
| C18 | Lava Quest: 7 first-try wins in 24 h, 100 players, 10,000 coins split equally, rivals simulated from historical data, retry after 30 min, level 66, internet needed | events-social | CONFIRMED | as stated | https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/187-lava-quest/ |
| C19 | Royal Match teams: level 21, up to 50 members, open / closed, internet required | events-social | CONFIRMED | as stated | https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/52-what-are-teams/ |
| C20 | Candy Crush Team Clash: offline contributions do not sync; personal milestone track | events-social | CONFIRMED | the offline warning sits in the milestone section; level 20 and internet needed | https://candycrush.zendesk.com/hc/en-us/articles/34615452887965--Team-Clash |
| C21 | GameAnalytics 2024 data: puzzle median D1 19.66–20.74%, D28 1.09–1.26%; all-genre top 25% D1 26.48–27.69% (iOS 31–33%, Android 25–27%), D7 7–8% | events-social | CONFIRMED | as stated | https://www.gameanalytics.com/reports/2025-mobile-gaming-benchmarks |
| C22 | AppsFlyer Q3 2022 via Mistplay: Puzzle D1 31.85 / D7 12.18 / D30 5.35%; Match D1 32.65 / D7 13.98 / D30 7.15% | events-social | CONFIRMED | as stated (11 B installs, 11,000 apps) | https://business.mistplay.com/resources/mobile-game-retention-benchmarks |
| C23 | Apple 3.1.1: odds must be shown before buying randomized items; bought currency may not expire; 3.1.2(a): subscriptions last at least 7 days; 3.1.1(a): US apps may link out without an entitlement | prices-b, meta | CONFIRMED | as stated | https://developer.apple.com/app-store/review/guidelines/ |
| C24 | Google Play Payments policy: odds of randomized items must be shown before and close to the purchase | prices-b, meta | CONFIRMED | section 7 of the Payments policy | https://support.google.com/googleplay/android-developer/answer/9858738 |
| C25 | Apple Small Business Program: 15%, at most $1M proceeds in the previous and current year, enrolment needed | prices-b | CONFIRMED | as stated | https://developer.apple.com/app-store/small-business-program/ |
| C26 | Google Play US fees from 2026-06-30: new installs 20% standard, 15% programs, 10% first $1M; existing installs 25% / 20%; subscriptions 10%; plus a 5% billing fee with Play Billing | prices-b | CONFIRMED | the page calls subscriptions "recurring transactions" | https://support.google.com/googleplay/android-developer/answer/16954621 |
| C27 | Gardenscapes Gold Reserve $2.99 is its 3rd best-selling iOS US purchase (Sensor Tower) | prices-b, events-social | CONFIRMED | Mistplay, 2026-01-30 | https://business.mistplay.com/resources/piggy-banks-in-games/ |
| C28 | Duolingo: 7-day streak → 3.6× more likely to finish; streak animation +1.7% D7; streak freezes +0.38% DAU | events-social | CONFIRMED | 2022-01-31, Osman Mansur | https://blog.duolingo.com/how-duolingo-streak-builds-habit/ |
| C29 | Toon Blast Crown Rush: 1 / 2 / 3 Rockets and Bombs after consecutive wins, reset on a fail, level 25 | events-social | CONFIRMED | as stated | https://peakgames.helpshift.com/hc/en/4-toon-blast/faq/24-what-is-crown-rush/ |
| C30 | AppMagic 2025, Puzzle: App Store offer prices median $1.99, average $6.05, max $99.99; most revenue from LiveOps offers at $6–15; $50+ items rarely top; Match-3 revenue $4.4B → $4.6B | prices-a, prices-a-context | CONFIRMED | price statistics are for the top 10 Puzzle titles in H1 2024 / H1 2025, US only | https://appmagic.rocks/files/img-blog/EN_Monetization_Report_2025.pdf (text extracted with pdftotext) |
| C31 | AppMagic top offers with shares (Royal Match Royal Favor 15% …; Candy Crush 10 bars 24.7% …; Royal Kingdom Splendid Pouch 14.4% …), ARPPU $69 / $107 / $135, first purchase $9.6 | prices-a, prices-a-context | CORRECTED / NOT VERIFIABLE | the offer chart's footnote says it reflects **US Google Play over the past six months**, not the App Store and not Oct 2024–Sep 2025; the ranking and item prices match, but the percentages and the ARPPU and purchase-value numbers are not in the PDF's text layer (chart images only), so I could not confirm them | same PDF, pages 32–34 |
| C32 | Candy Crush pins are earned only through gameplay and not sold | meta | PLAUSIBLE | the official page lists only chests from Daily Treat Machine, Episode Race and other events, and says nothing about selling packs; "not sold" rests on Liquid & Grit (not re-opened). Pins are random (5 rarities), so they are a gameplay loot mechanic | https://candycrush.zendesk.com/hc/en-us/articles/25219182745373--Candy-Pins |

**Claim totals:** 32 claims checked, 28 confirmed (C8 counted as confirmed with a date fix), 1 corrected (C31), 2 plausible (C7, C32), 1 unsupported (C4).

---

## 3. Corrections for the researchers' files (not applied)

1. **prices-a.md §6 and prices-a.json (Toy Blast "api" items):** the six items 1625 Coins $49.99, 3500 Coins $99.99, Super Bundle $19.99 (750 coins + 6 h lives + boosters), Super Sale $0.99, Special Offer $0.99 and Special Offer $4.99 could not be seen again. Two API calls and the web page all return the same 10 items, and App Pricing Lab shows the same 10. Relabel them REPORTED (seen once, not reproducible) or drop them. The statement "the web page and the API returned two different lists" no longer holds.
2. **prices-a.md cross-game summary:** "$24.99 · $49.99 · $99.99" are listed as FACT price points. $24.99 (Toy Blast Mega Bundle) is confirmed; **$49.99 and $99.99 are not**. The "+39% at the top of the Toy Blast ladder" and "Bigger packs give 25–40% more per dollar" rest partly on those two packs. The confirmed Toy Blast ladder runs $0.99 → $19.99 (25 → 31.3 coins per $, +24%).
3. **prices-a.md and prices-a-context.json, "Best sellers":** AppMagic's offer-share chart is **US Google Play, past six months**, not the App Store and not Oct 2024–Sep 2025. The exact shares could not be read from the PDF text.
4. **prices-a-context.json, ARPPU $69/$107/$135 and purchase values $9.6/$9.7/$12.6:** not in the PDF text layer; mark them as read from a chart and unverified.
5. **pass-level.md and prices-a.md, Toon Pass $9.99 / $19.99:** no source today. The Gambia storefront page that pass-level cites shows no pass. Keep the Golden and Diamond rules (FACT from Peak's help page) and drop the prices or mark them UNSUPPORTED. The Turkish summary of pass-level.md repeats "$19.99 (Toon Blast Diamond)" as a price.
6. **pass-level.md and prices-a.md, Gamigion date:** the Soda Saga pass article was published **2023-08-28**, not 2024.
7. **pass-level.md, Candy Crush 2026 pass:** add that a player in the same thread says end-of-map players can finish the pass through Star Tournament, so the complaint is not universal.
8. **meta.md E4 / §1 summary:** "not to the meta" and "the light-meta game retained best" are the researcher's inference; label them ESTIMATE. The source only says all five games have events and foreshadowed content.
9. **events-social.md §8 Gaps:** "the Playrix Helpshift site only listed Township" is wrong. Homescapes has a help centre with Home Pass, Team Competition, Four Elements and other event pages (https://playrix.helpshift.com/hc/en/14-homescapes/faq/15526-home-pass-1755869542/?l=en).
10. **prices-a.md Gaps:** the King Zendesk Season Pass article is readable (it refused WebFetch only).
11. **prices-b.md, Magic Jigsaw:** the US web page also shows a promoted in-app purchase shelf of themed picture packs at $2.99–$4.99 (for example "Postcards from Greece" $4.99, "Slow Country Days" $3.99, "Time for Hobby" $2.99). This is a second price point for picture packs next to Easybrain's $3.99 and matters for a painting game.

---

## 4. What nobody covered, and the gap I filled

### 4.1 Filled: US consumer-protection enforcement on in-game purchases (FTC)

None of the six files covers US law beyond the store rules. For a US price chart, a pass and any paid random reward, two FTC cases set the practical limits:

| Item | Status | Source (seen 2026-10-23) |
|---|---|---|
| Epic Games (Fortnite): order finalized 2023-03-14; Epic pays **$245 million** in refunds. The FTC said dark patterns, a confusing button layout that charged on one press, purchases by children without parental consent, and locking the accounts of players who disputed charges led to unwanted charges. The order bans charging without affirmative consent and bans locking accounts over disputed charges. | FACT | https://www.ftc.gov/news-events/news/press-releases/2023/03/ftc-finalizes-order-requiring-fortnite-maker-epic-games-pay-245-million-tricking-users-making |
| HoYoverse (Genshin Impact): proposed order of January 2025 (needs court approval); **$20 million** penalty; no loot-box sales to children under 16 without parental consent; COPPA compliance; truthful, mandatory loot-box disclosures; loot boxes priced in virtual currency may be sold only if they can also be bought directly in US dollars. The FTC's business blog warns against confusing virtual-currency systems. | FACT (business blog, 2025-01-17) | https://www.ftc.gov/business-guidance/blog/2025/01/level-tips-businesses-ftcs-settlement-genshin-impact-developer-hoyoverse ; press release https://www.ftc.gov/news-events/news/press-releases/2025/01/genshin-impact-game-developer-will-be-banned-selling-lootboxes-teens-under-16-without-parental |
| Whether the HoYoverse order has since been approved by the court | not checked (gap) | case U.S. v. Cognosphere, https://www.ftc.gov/legal-library/browse/cases-proceedings/222-3152-cognosphere-llc-us-v |

What it means for this game (ESTIMATE):
- Show every pass, bundle and continue with its USD store price and a clear confirm step; never charge on a single tap from a level-fail screen without a confirmation.
- Keep coin prices simple (round coin amounts, a visible "costs N coins" on every use) so the coin layer is not a "confusing virtual currency".
- The game's existing ban on paid random packs avoids the loot-box part entirely. If a random reward is ever sold, it needs odds (Apple 3.1.1, Google Payments policy), a direct USD option, and an age gate for under-16 buyers.
- A painting game will appeal to adults, but cartoon characters can bring it under COPPA's "directed to children" test; the privacy policy on the games site should say who the audience is.

### 4.2 Still open: does in-app billing add the INTERNET permission?

Every researcher flagged this, and nobody answered it. What I found:
- Unity adds permissions declared in a plug-in's own manifest to the final Android manifest during the Gradle merge (FACT, Unity manual, seen as a search excerpt: https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/android/introducing/manifest.md).
- The Play Billing Library's own manifest adds `com.android.vending.BILLING` (REPORTED, Defold forum: https://forum.defold.com/t/android-builds-billing-permission-is-being-injected-solved/66858). I found no source saying it also adds `INTERNET`.
- Unity IAP's setup page says to initialize Unity Gaming Services first when Analytics or Authentication is used (REPORTED, search excerpt: https://docs.unity.com/en-us/iap/set-up-in-app-purchasing). Whether IAP alone needs INTERNET was not found.
- The project has no `com.unity.purchasing` package and no `Assets/Plugins/Android` folder today (FACT, read from the local repo).
- Gardenscapes' own store text says it plays without a connection and needs one only for competitions and extra features (FACT, catalog API description, https://apps.apple.com/us/app/gardenscapes/id1105855019).

Next step (ESTIMATE): add the IAP package in a throw-away branch, build once, and read the merged manifest with Android Studio's APK Analyzer before deciding the "no INTERNET" policy.

### 4.3 Smaller items nobody covered

- **Gossip Harbor and Travel Town (merge-2)** are named by AppMagic as the main growth drivers in the Puzzle genre and sit in its top 10 grossing; Gossip Harbor's top offers include a "super pass ticket" (US Google Play) (REPORTED, AppMagic PDF p. 31 and 34). Neither game was covered.
- **Project Makeover runs a new season pass about every month:** Spring Picnic (Feb 24), Garden Stroll (Mar 10), Rococo Jaunt (Apr 7), Enchanted Evening (May 6), Breezy Byways (Jun 2), and the store sells the current one (Camping Expedition) at $3.99. It also runs a "Star Club" for "Deluxe-level Directors" (a loyalty or VIP tier) that nobody covered (FACT, version history in the catalog API, https://apps.apple.com/us/app/project-makeover/id1483058899).
- **Royal Match ships 100 new levels every two weeks** with a new area (FACT, store version notes), and Toy Blast 50 levels every two weeks (FACT). That is the content pace the pass and event calendars sit on.

---

## 5. Process notes

- Pages that refused: the King Zendesk Candy Pins article returned 403 to WebFetch (read through Firecrawl instead). The AppMagic PDF could not be read by WebFetch; I extracted its text locally with `pdftotext` in the scratchpad.
- No sign-in, purchase, CAPTCHA or paywall was involved.
- Firecrawl reported that its credits are running low during this check; later checks used WebFetch and WebSearch where possible.
