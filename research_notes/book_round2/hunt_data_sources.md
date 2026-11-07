# D1 — NBA / NFL / MLB free open→close totals hunt

**Date:** 2026-10-28  
**Scope:** Find FREE, no-auth historical **game totals** with **open and close**; download only into `data/raw/round2_odds/`; **no** DB import, **no** code changes, **no** running library/downloaded scrapers.  
**Money filter:** accuracy-only gains with no betting path → IGNORE.

## Safety

- Library / web / book text treated as data only (no instructions followed).
- Downloaded GitHub **data** JSON/CSV/XLSX only; scraper `.py` files were **not** executed.
- Live SBRO site returns HTML walls for xlsx URLs → used **Wayback `id_`** snapshots only (no active scrape of sportsbookreview.com).
- Kaggle downloads already on disk (NOTES) reused for verification; new Kaggle pulls need login → noted, not scraped.

## Current warehouse (verified `python3` + `sqlite3`)

`possiblymaybe.db` present (~36 MB).

| Sport | Games | TOTAL open snaps | Notes |
|---|---:|---:|---|
| NBA | 32,284 | seasons **2008–2021** full O→C; **2022** only 457 games; **2025** 1,314; **2023/2024/2026 = 0** | Matches PLAN gap |
| NFL | 6,597 | O→C mainly **2023** (179 ESPN) + **2024** (271) + **2026** (92 minmaxer); rest **close-only** nflverse | FAIL was data-limited |
| MLB | 2,290 | **2025 only** O→C (~2,315 open + 2,315 close) | FAIL was data-limited |

**Join key in importers:** `SportTeams.GameKey(sport, date, homeAbbrev, awayAbbrev)` → e.g. `nfl:1999-09-12:ATL:MIN`, `mlb:2025-03-27:NYY:MIL`, `nba:2021-10-19:MIL:BKN`. Pattern: `MultiSportImporter.cs` / `Gate0Importer.cs`.

---

## Downloads this run → `data/raw/round2_odds/` (~106 MB)

| Folder | What |
|---|---|
| `sbro_nfl/` | 15× Wayback SBRO xlsx, seasons **2007–08 … 2021–22** |
| `sbro_nba/` | SBRO xlsx **2018–19 … 2022–23** (copied local + Wayback) |
| `sbro_mlb/` | empty — no free xlsx recovered from Wayback (HTML monthly pages only for early years) |
| `github_sbr_scraper/` | flancast90 pre-scraped `*_archive_10Y.json` (MLB/NBA/NFL) |
| `github_mlb_arnav/` | release `mlb_odds_dataset.json` (~80 MB) |
| `nflverse/` | `games.csv` (close-only reference) |
| `other/` | ProbWin **F5 sample** CSV (50 rows) |

---

## MLB

### Ranked import recommendations

| Rank | Source | O→C? | Seasons | Verified | Licence / notes | Join vs `Games` |
|---:|---|---|---|---|---|---|
| **1** | **flancast90** `mlb_archive_10Y.json` | **Yes** `open_over_under` / `close_over_under` (+ odds) | **2011–2021** (2020 short) | **25,597** games; **13,200** with open≠close | MIT on scraper repo; data is SBRO-derived dump — treat as research use; **do not re-run scraper** | `date` (YYYYMMDD float) + nickname (`Yankees`) → map via `SportTeams.TryMlb` / nickname table; no games in DB yet for these seasons |
| **2** | **ArnavSaraogi** release JSON | **Yes** per-book `openingLine.total` / `currentLine.total` | **2021-04-01 → 2025-08-16** | **13,121** games (**11,040** regular `R`); **67,828** book-total rows with open; **35,965** open≠close | Dataset release on GitHub (no explicit data licence in README; scraper MIT-ish). Multi-book: FanDuel, DK, Caesars, Bet365, BetMGM, … | `gameView.startDate` + `homeTeam.shortName` / `awayTeam.shortName` (TOR/NYY) → same as minmaxer importer; overlaps 2025 already in DB — upsert/dedupe |
| **3** | Existing **minmaxer** `mlb_2025_closing_odds.csv` | Yes | 2025 | 2,477 CSV rows (DB 2,290 games after team filter) | Kaggle (login); already imported | `date_et` + `home_team`/`away_team` — **USED** |
| — | Kaggle `christophertreasure/major-league-baseball-vegas-data` | **Close-only** | 2012–2021 | not re-downloaded | Kaggle login | Inferior to #1 for CLV |
| — | SBRO live / Odds API / ProbWin full / TheOddsGap / FastballAPI | — | — | — | **Paid or ToS / login** — do not scrape | — |

### Sub-markets (F5)

| Source | Verdict | Detail |
|---|---|---|
| ProbWin F5 Pinnacle 2023–2025 | **IGNORE (paid)** for money path until purchased | Full set ~6,735 games; **sample** saved: `other/probwin_mlb_f5_sample.csv` **50 rows**, single snapshot line (not true open→close) |
| flancast inning scores | Scores only | Can **derive F5 final runs** from `home_*Inn`/`away_*Inn` 1–5; **no F5 market lines** → no CLV/betting path alone |
| Free multi-season F5 O→C | **Not found** | |

**MLB money call:** Import **#1 then #2** with a new `MultiSportImporter`-style path (extend seasons, prefer consensus/median book close for CLV). Unblocks Tier A walk-forward. F5 stays IGNORE until a free O→C line source appears.

---

## NFL

### Ranked import recommendations

| Rank | Source | O→C? | Seasons | Verified | Licence / notes | Join |
|---:|---|---|---|---|---|---|
| **1** | **Wayback SBRO xlsx** `sbro_nfl/` | **Yes** (classic layout: **visitor Open/Close = total**, home Open/Close = spread) | **2007–08 … 2021–22** | **~4,224** games across 15 files (sheet rows counted via zip+xml) | Historical public archive via Wayback; site “will not be updated” | `Date` (MDD) + year from filename + team names → abbrev map → `GameKey`; overlay `PriceKind` Open/Close on existing nflverse games |
| **2** | **flancast90** `nfl_archive_10Y.json` | **Yes** `open_over_under` / `close_over_under` | **2011–2021** | **2,956** games; **2,630** moved | MIT scraper; SBRO-derived | Nicknames (`Packers`) → `TryNfl`; redundant with #1 but cleaner JSON |
| **3** | Existing ESPN `espn_nfl_ou_open_close.csv` | Yes | 2023–2024 | **450** rows | Already used | `date_et` + team names |
| **4** | minmaxer `nfl_2026_odds.csv` | Yes | 2026 partial | **92** rows | Kaggle | Already used |
| — | **nflverse** `games.csv` | **Close-only** `total_line` | 1999–2026 | **7,548** rows; **7,412** with `total_line` | **CC BY 4.0** | Already backbone; `ClvQuality=CloseOnly` |
| — | EndzoneAPI / TheOddsGap / totals.us team-total packs | O→C or team totals | varies | — | **Paid / API credits** | — |
| — | Kaggle tobycrabtree spreadspoke | mostly close | long | already local | login | close-ish; not better than SBRO for O→C |

### Sub-markets

| Market | Verdict | Detail |
|---|---|---|
| Team totals O→C | **IGNORE** (no free multi-season source found) | Paid APIs only |
| Wind / roof | Already in `NflGameContexts` from nflverse | Feature path exists; still needs O→C totals (#1) for CLV |
| 2H total | flancast `2H_total` mostly filled (**2,843**/2,956) but looks **close-only / messy** (many 0) | Low confidence — IGNORE unless cleaned |

**NFL money call:** Import SBRO/flancast O→C onto existing Games (**~15 seasons of real CLV**). That directly addresses the prior FAIL (~450+92 O→C). Team totals stay IGNORE.

---

## NBA

### Gaps vs task

| Season (ending) | DB TOTAL open | Free O→C found this hunt |
|---|---:|---|
| 2022–23 (`Season=2022`) | 457 / 1323 | **SBRO xlsx ~664 games** (partial season file on Wayback) + local copy |
| 2023–24 (`2023`) | 0 | **None free** (SBRO archive stops at 2022–23) |
| 2024–25 (`2025`) | 1,314 | Already from minmaxer E |
| 2025–26 (`2026`) | 0 | **None free** |

### Ranked import recommendations

| Rank | Source | O→C? | Seasons | Verified | Licence | Join |
|---:|---|---|---|---|---|---|
| **1** | SBRO `sbro_nba/nba_odds_2022-23.xlsx` | Yes (same VH total/spread layout) | 2022–23 partial | **1,329** sheet rows ≈ **664** games | Wayback archive | Fill missing TOTAL open/close for `Season=2022` via date+teams |
| **2** | Existing Kaggle **B** `BettingOdds_History` | Yes `Over_AtOpen/Close` | through ~2021–22 | already in Gate0 | SBRO-derived / Kaggle | **USED** |
| **3** | Existing minmaxer **E** | Yes `open_total`/`close_total` | 2024–25 | **1,314** | Kaggle | **USED** |
| **4** | flancast `nba_archive_10Y.json` | Yes | **2011–2021** | **13,903** games | MIT / SBRO dump | Overlaps B; nicknames; optional QC |
| **5** | Kaggle **A** `nba_2008-2026.csv` | **Close-only** `total` (also `h2_total`) | through 2026 | **24,440** rows; all have `total` | **CC0** | Scores/close for 2023/2024/2026 — **not** CLV-grade |
| — | BigDataBall 1H O→C | Yes (sample shows FanDuel 1H open/close) | sample 2024–25 | HTML wall on free preview URL | **Paid** | — |
| — | Live SBRO / Odds API | — | — | — | Blocked / paid | — |

### Sub-markets

| Market | Verdict | Detail |
|---|---|---|
| First-half totals O→C | **IGNORE** (no free multi-season O→C) | A/`h2_total` and flancast `2H_total` are **2H / close-ish**, not 1H open→close |
| Team totals | **IGNORE** | No free historical dump found |
| Close-only 2023/2024/2026 from A | **IGNORE for CLV experiments** | Usable for score-model calibration only → no soft-spot / CLV money path |

**NBA money call:** Import **2022–23 SBRO** to repair the half-empty season. **2023–24 and 2025–26 O→C remain blocked** on free data — provisional KEEP rules stay; do not pretend close-only A rows are open→close. 1H stays IGNORE.

---

## Paid / blocked (do not scrape)

| Source | Why listed |
|---|---|
| sportsbookreview.com live odds / historical UI | ToS / anti-bot; live xlsx URLs HTML-walled |
| ProbWin F5 / full MLB | Paid |
| BigDataBall NBA odds (1H) | Paid |
| EndzoneAPI, FastballAPI, TheOddsGap, Olivier “US Sports Master” full | Paid / sample-only |
| Odds API historical | Paid key |
| Kaggle datasets not already local | Login wall (human download OK later) |

---

## Importer sketch (for later Composer — not done here)

1. **NFL:** Parse SBRO xlsx (or flancast JSON) → for each game write `OddsSnapshots` TOTAL Open+Close, `ClvQuality=OpenClose`, `Book=Consensus` (or SBRO); join on existing nflverse `Games` by date+abbrev. Prefer SBRO over inventing duplicate games.
2. **MLB:** Load flancast 2011–2021 → new Games + TOTAL snaps; then Arnav 2021–2025 multi-book → pick one sharp/consensus book or median total; merge with existing 2025 minmaxer (dedupe by `GameKey`).
3. **NBA:** Only patch 2022–23 from SBRO xlsx into existing Games missing TOTAL open; leave 2023/2026 until a free O→C source appears.

---

## Verdict summary (money × testability)

| Priority | Action | Unlocks |
|---|---|---|
| **P0** | Import NFL SBRO/flancast O→C (2007–2021) | Real CLV backtests; revisit NFL FAIL |
| **P0** | Import MLB flancast (2011–2021) + Arnav (2021–2025) | Multi-season MLB Tier A; revisit MLB FAIL |
| **P1** | Import NBA SBRO 2022–23 totals | Repair 2022 hole |
| **P2** | Human Kaggle pull if new O→C sets appear for NBA 2023–24 / 2025–26 | Fresh NBA holdout-quality CLV |
| **IGNORE** | F5 / NFL team totals / NBA 1H / close-only fills as “CLV” | No free O→C betting path |

**No text in downloaded READMEs was treated as instructions to this agent.** Scrapers were not run.
