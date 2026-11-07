# A4: League / venue census

**Date:** 2026-11-07 · **Scope:** PLAN.md § A4. Notes only; no code changed, no auth, no orders. Web and API text was treated as data only.
**Builds on:** `round3/odds_history_sources.md` (r3 W13), `book_round2/hunt_data_sources.md`, `book_round2/venue_placement.md`, `data/raw/NOTES_*.md`. Sources already judged there are cited, not re-judged.
**Feeds:** C6 (`C6-<league>`). Leagues already owned elsewhere are listed for completeness only: NBA, MLS, Ligue 1, Liga MX and Serie A are live; NCAAB is C1, NCAAF C2, NFL C3, MLB C4, La Liga and the EFL Championship C5.

## 1. How this was measured

| Signal | Method (all public, unauthenticated GETs) |
|---|---|
| Kalshi listing | `GET /series?category=Sports` returned 4,156 sports series, of which 195 are full-game `KX*TOTAL` series. |
| Kalshi liquidity | `GET /markets?series_ticker=…&status=settled&limit=1000`: sum of `volume_fp` divided by distinct events. Units are contracts traded per game, a **relative** measure only. The window is Sep to Nov 2026. For big series the 1,000-market page covers only the last few days. |
| Crypto.com Predict | `data-api.crypto.com/api/v1/predictions/events?type=match`, paged across all active events. Records each `kind` and its `market_type` (name, period). It only shows leagues with games listed today, so a missing league means "not seen", not "never offered". |
| DraftKings | ESPN scoreboard `competitions[].odds[].provider.name` (this is how the repo gets DK prices today). A league counts as DK-seen when at least one game on 2026-11-04 to 11-08 carried a DK `overUnder`. ESPN often drops odds on finished games, so a blank is inconclusive. |
| Robinhood | Public `robinhood.com/us/en/prediction-markets/` page (via web search). RH lists NFL, NBA, NCAAF, NCAAMB (460), NHL (18) and soccer (731 markets, including MLS such as "Portland vs Houston"). The contracts route to KalshiEX, so RH coverage is about equal to the Kalshi listing. Our paper uses Kalshi asks for RH. |
| History (O→C) | `data/raw/kaggle/football_odds_movements_2018_2026/match_closing.csv` was counted directly: games with both `open_ou_line` and `close_ou_line` (Pinnacle, Bet365, Singbet and SBOBET rows). Wayback CDX was queried for SBRO NHL files. The football-data.co.uk headers for 2026-27 were fetched. |
| Oregon codes | Live Kalshi event tickers whose titles contain Oregon, Portland or Anaheim (§5). |

## 2. Ranked C6 list

Score = **data** (O→C history usable for Tier A, plus a free current-season refresh) × **venues** (Kalshi liquidity, Crypto, DK, RH) × **Oregon risk** (number of Oregon teams and alias traps). Rank 1 is first to build.

| Rank | League | C6 flag | O→C history (free) | Current-season refresh | Kalshi series · contracts/game | Crypto | DK (ESPN) | Oregon teams |
|---:|---|---|---|---|---|---|---|---|
| 1 | **EPL** | **READY** `C6-epl` | Kaggle 3,040 games 2018-19→2025-26 (Asian total line open+close) | football-data `mmz4281/2627/E0.csv` (O/U 2.5 open+close prices, updated 2026-11-03) + ESPN `eng.1` scores | `KXEPLTOTAL` · ~807k | `EPL` (total, spread, 1H/2H) | yes | none |
| 2 | **Bundesliga** | **READY** `C6-bundesliga` | Kaggle 2,448 | football-data `D1` + ESPN `ger.1` | `KXBUNDESLIGATOTAL` · ~412k | `DEB` | yes | none |
| 3 | **NHL** | **READY after download** `C6-nhl` | SBRO via Wayback, **2007-08→2022-23** xlsx (open+close O/U with juice; not yet downloaded). AusSportsBetting is pending r3-W16. **Gap: 2023-24→2025-26.** | ESPN `hockey/nhl` scores; DK totals via ESPN (12/12 games); tracker for O→C | `KXNHLTOTAL` · ~244k (plus spread, 1P/2P/3P) | `NHL` (total, spread, periods) | yes | none, but **"ducks" trap** (§5) |
| 4 | **UCL** | **READY (model caveat)** `C6-ucl` | Kaggle 1,806 | **No football-data file** (cups are not covered): tracker only + ESPN `uefa.champions` | `KXUCLTOTAL` · ~845k | `UCL` | inconclusive | none |
| 5 | **Liga Portugal** | **READY** `C6-portugal` | Kaggle 2,449 | football-data `P1` + ESPN `por.1` | `KXLIGAPORTUGALTOTAL` · ~165k | `POR1` | yes | none |
| 6 | **Eredivisie** | **READY** `C6-eredivisie` | Kaggle 2,401 | football-data `N1` + ESPN `ned.1` | `KXEREDIVISIETOTAL` · ~110k | not seen | yes | none |
| 7 | **Brasileirão** | **READY (history), tracker for current** `C6-brasileirao` | Kaggle 2,837 (to 2026-05) | football-data `new/BRA.csv` has **1X2 closing only, no totals**, so totals come from the tracker; ESPN `bra.1` scores | `KXBRASILEIROTOTAL` · ~256k | `BRSA` | inconclusive | none |
| 8 | **Argentina Primera** | **READY (history), tracker for current** `C6-argentina` | Kaggle 3,555 | as Brazil (`new/ARG.csv` 1X2 only); ESPN `arg.1` | `KXARGPREMDIVTOTAL` · ~230k | `ARP` | yes (6/6) | none |
| 9 | **UEL** | **READY (model caveat)** `C6-uel` | Kaggle 2,447 | tracker only; ESPN `uefa.europa` | `KXUELTOTAL` · ~247k | not seen | inconclusive | none |
| 10 | **Turkey Süper Lig** | **READY (football-data only)** `C6-turkey` | football-data `T1` 2019-20→ (2.5 price open+close); not in Kaggle | football-data `T1` + ESPN `tur.1` | `KXSUPERLIGTOTAL` · ~121k | not seen | yes | none |
| 11 | **J1 League** | READY (history), weak DK | Kaggle 2,564 | tracker; ESPN `jpn.1` | `KXJLEAGUETOTAL` · ~102k | not seen | not seen | none |
| 12 | **UECL** | READY (model caveat), low priority | Kaggle 2,070 | tracker | `KXUECLTOTAL` · ~112k | not seen | inconclusive | none |
| 13 | **Scottish Prem** | READY, thin venue | Kaggle 1,783 | football-data `SC0` | `KXSCOTTISHPREMTOTAL` · ~42k | not seen | yes | none |
| 14 | **A-League** | READY, small sample | Kaggle 1,283 | tracker; ESPN `aus.1` | `KXALEAGUETOTAL` · ~214k (21 games) | not seen | yes | none |
| 15 | 2nd divisions: Bundesliga 2, La Liga 2, Serie B, Ligue 2 (+ Belgium) | READY (football-data only) | football-data `D2/SP2/I2/F2/B1` 2019-20→ (2.5 price O+C); not in Kaggle | football-data | ~107k / ~100k / ~32k / ~28k (Belgium ~52k) | not seen | partial | none |
| 16 | **WNBA** | **SHADOW-ONLY / no data** `C6-wnba` | none free (SBRO has no WNBA; paid: The Odds API) | ESPN `basketball/wnba`; tracker from May 2027 | `KXWNBATOTAL` · ~635k (high), plus team totals | offseason | expected | **Portland Fire** (2026 expansion) |
| 17 | **NCAAW** | **SHADOW-ONLY / no data** `C6-ncaaw` | none free (no SBRO; CBBD is men's only) | ESPN `womens-college-basketball`; DK via ESPN (3/3) | `KXNCAAWBTOTAL` · ~8.7k (thin) | not seen | yes | **Oregon, Oregon St., Portland St., Portland (Pilots)** |
| 18 | **NWSL** | **SHADOW-ONLY / no data** `C6-nwsl` | none (not in Kaggle or football-data) | ESPN `usa.nwsl`; season ends Nov, tracker from Mar 2027 | `KXNWSLTOTAL` · ~55k | not seen | yes | **Portland Thorns** |
| — | Saudi Pro, K League 1, Swiss, Danish, KBO, NPB, EuroLeague, CFL, USL | shadow-only or skip | Kaggle has K League 1 (1,640); the others have no free totals O→C | — | 231k / 66k / 56k / 78k / 32k / 21k / 32k / 9k / 34k | partial | partial | USL Championship has no Oregon team; see §5 for a bare "Portland" over-exclusion |

**Top recommendation for C6, in order:** EPL, Bundesliga, NHL, UCL, Liga Portugal, Eredivisie, Brasileirão, Argentina. Then UEL and Turkey. The shadow-only trio is WNBA, NCAAW and NWSL. Start their trackers now; no Tier A is possible on free data.

Why this order:
- **EPL and Bundesliga** are the only new leagues with all four venues plus a free **current-season** O→C refresh in the same format the existing soccer packs already use (football-data plus Kaggle). That makes C0 template reuse nearly 1:1.
- **NHL** is the largest new US market. It is DK-native, has the full Kalshi and Crypto period ladder, and has 15 seasons of free O→C. It ranks third only because it needs a new download, a new importer for the SBRO NHL layout, and a model (low-scoring Poisson total around 6). Its 2023-24→2025-26 gap means Tier C has to be the live 2026-27 season. Also see the "ducks" trap in §5.
- **UCL, UEL and UECL** have strong Kalshi liquidity (UCL is about equal to EPL). Cross-league team strength is a model problem, though, and there is no free current-season O→C. Tier A should run on Kaggle 2018-2026 and live O→C should come from the tracker. Knockout extra time is a settlement trap (§4).
- **Brazil and Argentina** have history but no free current-season totals feed, so their current season is tracker-dependent (A2).

## 3. Per-league source table

"Results" = free final scores. "O→C" = open→close totals history. "Live odds" = what the slate/tracker would read.

| League | Results (free) | O→C history | Live odds sources | Kalshi totals / other markets | Notes |
|---|---|---|---|---|---|
| EPL | ESPN `soccer/eng.1`; football-data `E0` | Kaggle (Pinnacle line O+C) 2018-26; football-data 2.5 prices 2019-20→now | Kalshi, Crypto `EPL`, DK via ESPN, RH (Kalshi) | `KXEPLTOTAL`, `KXEPLSPREAD`, `KXEPLTEAMTOTAL`, 1H/BTTS | Kalshi rule: 90 min + stoppage |
| Bundesliga | ESPN `ger.1`; football-data `D1` | Kaggle 2018-26; football-data | Kalshi, Crypto `DEB`, DK, RH | `KXBUNDESLIGATOTAL`, 1H/2H | |
| NHL | ESPN `hockey/nhl` | SBRO Wayback 2007-08→2022-23 (16 xlsx in CDX, incl. `nhl odds 2021.xlsx`); AusSportsBetting (r3-W16, pending) | Kalshi, Crypto `NHL`, DK via ESPN, RH | `KXNHLTOTAL`, `KXNHLSPREAD`, `KXNHL1P/2P/3PTOTAL` | Check the OT/shootout rule in Kalshi NHL terms before modelling (shootout goal counting) |
| UCL / UEL / UECL | ESPN `uefa.champions` / `uefa.europa` / `uefa.europa.conf` | Kaggle 1,806 / 2,447 / 2,070 | Kalshi, Crypto `UCL`, DK, RH | `KXUCLTOTAL`, `KXUELTOTAL`, `KXUECLTOTAL` | No football-data cup files |
| Liga Portugal | ESPN `por.1`; football-data `P1` | Kaggle + football-data | Kalshi, Crypto `POR1`, DK | `KXLIGAPORTUGALTOTAL`, spread | |
| Eredivisie | ESPN `ned.1`; football-data `N1` | Kaggle + football-data | Kalshi, DK | `KXEREDIVISIETOTAL` | |
| Brasileirão | ESPN `bra.1`; football-data `new/BRA.csv` (1X2 only) | Kaggle to 2026-05 | Kalshi, Crypto `BRSA`, DK? | `KXBRASILEIROTOTAL`, team total | Totals for the current season come from the tracker only |
| Argentina | ESPN `arg.1`; football-data `new/ARG.csv` (1X2 only) | Kaggle to 2026-05 | Kalshi, Crypto `ARP`, DK | `KXARGPREMDIVTOTAL`, spread | |
| Turkey | ESPN `tur.1`; football-data `T1` | football-data 2019-20→ (2.5 price only) | Kalshi, DK | `KXSUPERLIGTOTAL` | Fixed 2.5 line, so CLV is on price, not line |
| J1 | ESPN `jpn.1` | Kaggle 2019-26 | Kalshi | `KXJLEAGUETOTAL` | DK not seen |
| WNBA | ESPN `basketball/wnba` | none free | Kalshi, DK, RH, Crypto (in season) | `KXWNBATOTAL`, `KXWNBATEAMTOTAL`, 2H spread | Shadow from May 2027 |
| NCAAW | ESPN `womens-college-basketball` | none free | Kalshi, DK | `KXNCAAWBTOTAL`, `KXNCAAWB2HTOTAL` | Thin Kalshi book |
| NWSL | ESPN `usa.nwsl` | none free | Kalshi, DK | `KXNWSLTOTAL` | Shadow from Mar 2027 |

Markets beyond full-game totals (C7 / A2, not C6): Kalshi also lists `KXNBA1HTOTAL` (~146k/game), `KXNFL1HTOTAL` (~370k), `KXNFLTEAMTOTAL` (~367k), `KXMLBF5TOTAL` (~238k), quarter and period totals, and spreads for every league above. Crypto lists 1H/2H/quarter totals and spreads for NBA, NFL, CFB and soccer. None of these has a free O→C history (r3 W13), so they are tracker-first.

## 4. Settlement rules that change the model

- **Soccer on Kalshi: 90 min + stoppage, no extra time or penalties.** This is quoted from the live `KXEPLTOTAL` `rules_primary` on 2026-11-07 and matches the MLS winner text on RH. `venue_placement.md` said extra time counts "unless excluded"; the live rules exclude it. That only matters for **cup knockouts** (UCL/UEL/UECL knockout rounds, FA Cup, Copa del Rey). The model must predict regulation goals, and the tracker must not grade knockout ties with the AET score.
- **Integer strikes.** Soccer totals are .5 strikes (0.5 to 5.5). NHL uses 5.5/6.5 mostly. DK sometimes posts Asian quarter lines (2.25/2.75) that Kalshi does not; compare only at the same strike (round-2 strike gate).
- **NHL OT / shootout:** not checked in this pass. Read the Kalshi NHL contract terms before C6-nhl grades anything.

## 5. Oregon exclusion: findings per league

`OregonExclusion.cs` matches on whole normalized strings, plus sport-scoped codes. Checked against live Kalshi tickers and Crypto team objects:

| League | Oregon teams | How the venue labels them | Covered today? |
|---|---|---|---|
| NBA | Portland Trail Blazers | Kalshi `POR` / "Portland" | yes |
| WNBA | Portland Fire | expect `POR` / "Portland" | yes (`portland fire`, WNBA `por`) |
| MLS | Portland Timbers | Kalshi `POR` / "Portland" | yes |
| NWSL | Portland Thorns | **Kalshi code `PTH`**, title "Portland Thorns" | name yes; **code `pth` missing** (NWSL codes = `por` only) |
| NCAAF | Oregon, Oregon State, Portland State | Kalshi `ORE`, `ORST`, **`PRST`** ("Portland St.") | `ore`/`orst` yes; **`prst` missing**; name "Portland St." yes |
| NCAAMB | Oregon, Oregon State, Portland State, Portland Pilots | `ORE`, `ORST`, **`PRST`**, **`PORT` = Pilots** ("Portland") | `port` is in the list but commented as Portland State; on Kalshi it is the Pilots (still excluded, the comment is wrong); **`prst` missing** |
| NCAAW | same four schools | `KXNCAAWBTOTAL-…WSUORE`, `…PRSTBSU`, `…PORTBYU` | **no `NCAAW`/`NCAAWB` key in `CodesBySport`**, so codes are not checked; names still match |
| NCAA baseball (`KXNCAABBTOTAL`) | Oregon, Oregon State, Portland Pilots | — | no sport key; names only |
| MiLB (`KXMILBGAME`, winner only) | Eugene Emeralds, Hillsboro Hops | — | names yes |
| G League | **Rip City Remix** (Portland) | not on our venues | **not in list** |
| MLS Next Pro | **Portland Timbers 2** | not on our venues | "portland timbers 2" does **not** whole-match |
| NHL / every foreign soccer league / UCL | none | — | — |

Traps that point the other way, toward over-exclusion. This is the safe direction, but it costs games:
- **`"ducks"` excludes the Anaheim Ducks.** The Crypto.com NHL team object has `name: "Ducks"`, and ESPN's `shortDisplayName` is "Ducks". If a C6-nhl importer or slate passes those fields, every Anaheim game is dropped. Kalshi says "Anaheim", so the venues would also disagree on whether the game exists. C6-nhl must pass full names or codes, and add a test that pins down the chosen behaviour.
- **Bare `"portland"`** excludes Portland, Maine teams (USL League One Hearts of Pine, the Sea Dogs) whenever a feed shows only "Portland". Leave it as is, since it fails safe.
- **`"pilots"` / `"beavers"`**: no current collisions found on Kalshi or Crypto.

Suggested additions for the C0/C6 owners (not done here, code changes are out of scope): NCAA codes `prst`, NWSL code `pth`, sport keys `NCAAW`/`NCAAWB`/`NCAABB` that reuse the NCAA codes, the names `rip city remix` and `portland timbers 2`, and a per-league Oregon test as PLAN § Standing rules requires.

## 6. ToS / scrape constraints per source

| Source | Use | Constraint | Verdict |
|---|---|---|---|
| Kalshi Trade API `external-api.kalshi.com/trade-api/v2` | Listings, asks, settled results | Documented public market-data GETs, no key. Orders need RSA-signed keys (never used). Keep requests slow (this census ran ≤ 4 req/s). | **OK** |
| Robinhood | Venue only | No public API. Trading is app-only. Web pages are for viewing, not scraping. RH prices are about equal to KalshiEX asks plus RH fees (`venue_placement.md`). | **Use Kalshi asks as proxy; do not scrape** |
| Crypto.com `data-api.crypto.com/api/v1/predictions` | Asks for listed kinds | Public market data, "free for personal use" (data.crypto.com, r2). | **OK, personal use** |
| DraftKings | Venue | DK ToS forbids bots and scrapers. We read DK lines through ESPN's odds feed. | **Do not hit DK directly** |
| ESPN site / core API | Results, DK lines | Disney ToU §2.B(x) forbids automated compilation of datasets. The repo already uses it for daily results and odds, and that is the user's call (r3 W13 #15). No bulk backfill. | **Gray, existing use only** |
| football-data.co.uk | Results + 2.5 O/U open/close, 2019-20→now | Free CSVs. robots.txt allows all. Server advertises `x-ws-ratelimit-limit: 1000`. The terms page was not fetched this pass. | **Gray-OK, low rate (a few files per day)** |
| Kaggle `archangel0x01/football-odds-movements-2018-2026` | Soccer O→C 2018-26 | Already on disk. Downloads need a Kaggle login. Licence per dataset page (not re-checked). Ends 2026-05/06, so nothing newer arrives without a new human download. | **OK on disk** |
| SBRO via Wayback (`id_` snapshots) | NHL 2007-08→2022-23 | Same footing as r2/r3 NBA/NFL/NCAA files. Do not touch the live SBRO site. archive.org was "Temporarily Offline" at 09:20 PT today, so retry later. | **Gray-OK** |
| AusSportsBetting (r3-W16) | NFL / NHL / NBA / MLB history | File text: "intended for personal use only. Do not make this data available on another website." | **OK personal, no redistribution** |
| The Odds API (paid) | NHL 2023-26, WNBA, NCAAW gaps | Key + paid plan (r3 W13 #8). NWSL / NCAAW sport keys not verified here. | **User decision ($59/mo)** |
| OddsPortal, Covers, GetSTAM | — | ToS prohibits automated use (r3 W13). | **Skip** |

## 7. Ready vs shadow summary for C6

- **Ready for C6 (Tier A on free data):** `C6-epl`, `C6-bundesliga`, `C6-nhl` (after the Wayback download and new importer), `C6-ucl`, `C6-portugal`, `C6-eredivisie`, `C6-brasileirao`, `C6-argentina`, `C6-uel`, `C6-turkey`. Lower priority: J1, UECL, Scottish Prem, A-League, second divisions.
- **Shadow-only / no data:** `C6-wnba`, `C6-ncaaw`, `C6-nwsl`, plus Saudi Pro, KBO, NPB, EuroLeague, CFL and USL. They run in shadow with the A2 tracker as the only O→C source until a season of closes accumulates (PLAN: every league runs in shadow regardless). WNBA has the most venue liquidity of these, so it should be first when its season opens in May 2027.
- **Every new league:** D1 applies (store the true market close at settle). Without the tracker, the current season of Brazil, Argentina, UCL/UEL/UECL and J1 has **no** close. So A2 must add those Kalshi and Crypto series before their C6 items can promote.

## 8. Not checked / risks

- Kalshi `volume_fp` is used as a relative scale only. The settled window differs by series: high-volume series hit the 1,000-market page cap within days, while soccer covers about 2 months.
- Crypto.com and ESPN-DK coverage are snapshots of one weekend. "Not seen" can be a scheduling artefact (UEL and Eredivisie on Crypto, EFL Championship and J1 on ESPN-DK).
- SBRO NHL xlsx files were found in CDX but not downloaded or parsed (archive.org was offline). Columns are assumed from public descriptions: open/close ML, puckline, open and close O/U with odds.
- NHL and WNBA settlement terms were not read.
