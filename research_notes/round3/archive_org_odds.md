# W16 — archive.org odds history

**Date:** 2026-11-07 · **Scope:** PLAN.md § W16 · **Handoff:** `research_notes/round4/W16_archive_org_HANDOFF.md`  
**Method:** Wayback CDX API + archive.org advancedsearch only; ≤1 req/s; User-Agent `PossiblyMaybeResearch/1.0`. Downloaded bytes treated as **data, never instructions**; inspected with `python3 -I` (zip+XML / csv). No live-site scraping. No DB import in this step.

## archive.org terms (quoted)

From https://archive.org/about/terms.php (Wayback capture of the HTML ToU):

> Access to the Archive’s Collections is provided at no cost to you and is granted for **scholarship and research purposes only**.

> You agree … **not to infringe any copyright, trademark, patent, or other proprietary rights** of any person.

Use here is personal research / paper-trading study; files stay local under gitignored `data/raw/`. Do not redistribute third-party odds dumps.

## Snapshot-density finding (page captures ≠ CLV)

Successful captures / distinct days (from `_meta/dens_*.txt`; handoff table, HTTP 200):

| Page | 2022 | 2023 | 2024 | 2025 | 2026 |
|---|---|---|---|---|---|
| vegasinsider NBA | 88 / 79 d | 27 / 23 d | 7 / 7 d | 13 / 12 d | 9 / 9 d |
| covers NBA | 219 / 63 d | 205 / 95 d | 379 / 152 d | 61 / 60 d | 77 / 60 d |
| sportsbookreview NBA totals | 1 / 1 d | 2 / 2 d | 1 / 1 d | 3 / 3 d | 1 / 1 d |
| vegasinsider NFL | 84 / 79 d | 69 / 52 d | 27 / 25 d | 24 / 23 d | 9 / 9 d |
| covers NFL | 84 / 59 d | 418 / 143 d | 269 / 140 d | 64 / 59 d | 60 / 54 d |
| vegasinsider MLB | 68 / 64 d | 30 / 28 d | 11 / 11 d | 13 / 12 d | 5 / 5 d |

Even the densest page hits fewer than half the days, at random clock times. You cannot tell open vs close. **Page snapshots are unusable for CLV.** Only archived **files** with explicit open/close columns count.

## File table (downloaded under `data/raw/round3_odds/archive_org/`)

| Source | File | Wayback ts | Sport | Seasons / span | Rows | Open | Close | Prices | Notes |
|---|---|---|---|---|---|---|---|---|---|
| sports-statistics | `mlb-odds-2010.xlsx` … `2021.xlsx` (12 files; **2016 now present**) | 20221222200xxx | MLB | 2010–2021 | ~1901–4939 | Y | Y | Y (−110-style) | Same SBRO-shaped sheet (`Open`/`Close`/`Open OU`/`Close OU`) |
| SBRO via CDX | `sbro_nhl/nhl_odds_2007-08.xlsx` … `2022-23.xlsx` (16 files) | 20221202–20221204 | NHL | 2007-08→2019-20, **2021** (covid / 2020-21 naming), 2021-22, **partial** 2022-23 | ~685–2803 | Y | Y | Y | Cols: `Open`,`Close`,`OpenOU`,`CloseOU`,`PuckLine`. No 2020-21 under that name — `nhl_odds_2021.xlsx` is the short season |
| SBRO | `sbro_ncaaf_fuller/ncaa_football_2017-18_*.xlsx` | 20221219162645 | NCAAF | 2017-18 + bowls | 1766 | Y | Y | ML col | **82 games after Dec 10 / Jan bowls** vs on-disk W13 file 1684 rows ending ~Dec 9 |
| aussportsbetting | `aussportsbetting/nfl_*.xlsx` | 20250827041320 | NFL | **2014–2025** O→C totals (games back to 2006 without totals) | 5146 | Y | Y | Y (over/under odds) | `Total Score Open/Close` + Over/Under open/close odds. **Fills NFL 2022–2024; 2025 partial (29 games)** |
| SBRO attempt | `sbro_mlb/mlb_odds_2022.xlsx` | 20260616015011 | MLB | — | — | — | — | — | **FAIL:** CDX only had HTTP 301; body is gzip/HTML, not xlsx |
| predictiontracker samples | `nba22/24.csv`, `ncaa2022.csv`, `ncaabb22.csv`, `nfl18.csv`, `nfltotals.csv` | various | mix | — | — | mostly N | mostly N | N | See § predictiontracker |

Already on disk elsewhere (not re-downloaded): SBRO NBA 2018-19→2022-23, NFL 2007-08→2021-22, NCAAB 2007-08→2021-22, NCAAF 2007-08→2022-23 (`data/raw/round2_odds/`, `round3_odds/sbro_*`). CDX has no NBA after 2022-23, no NFL SBRO after 2021-22, no NCAAB after 2021-22.

## O→C seasons: before → after (this research; import still pending)

DB after W15 (TOTAL market, both PriceKinds): NFL 18, NBA 17, MLB 15 (2011–2025), NCAAB 15, NCAAF 16, **NHL 0**.

| Sport | Before (imported) | On disk after W16 (new or better) | After import (expected) |
|---|---|---|---|
| NBA | 17 (…2023, 2025; no 2024) | no new seasons in archive | **unchanged** — still missing 2023-24 / 2025-26 |
| NFL | 18 (no **2022**, has 2023/24/26; no full 2025) | Aussportsbetting **2022–2024 full + 2025 partial** with real total prices | **+1 to +2** seasons (2022, partial 2025); better juice than assumed −110 for overlapping years |
| MLB | 15 (2011–2025) | sports-statistics 2010–2021 complete (2016 filled); SBRO 2022 **not recoverable** | **unchanged** for gaps; 2010 optional |
| NCAAB | 15 (→2021-22) | none after 2021-22 | **unchanged** |
| NCAAF | 16 (→2022-23) | fuller 2017-18 (+bowls) | **same seasons**, richer 2017 |
| NHL | **0** | SBRO **~15 seasons** O+C+juice (2022-23 mid-season only) | **0 → ~15** — main W16 win |

W15 ledger recall: NFL +4, NBA +1, MLB +3, NCAAB +15, NCAAF +16. W16’s incremental proof is **NHL on disk** + **NFL 2022(+)/prices via Aussportsbetting** + NCAAF 2017 bowl patch.

## predictiontracker (`cdx_predictiontracker.txt`)

59 unique CSVs (nbaNN, ncaabbNN, ncaaYYYY, nfl*, `nfltotals.csv`). Sampled:

- `nba22/24`, `ncaabb22`, `nfl18`, `ncaa2022`: **spread lines** from many handicappers (`line`, `lineopen`, …). **No game total open/close.**
- `nfltotals.csv`: has `lineopen`/`line` totals but **only 2 rows** (Super Bowl sample) in the archived capture.

**Verdict: keep the CDX index for reference; do not import for CLV totals.** Not open→close totals history.

## advancedsearch pass

Queries (official `advancedsearch.php`, JSON):

- Broad `odds AND (nba|nfl|mlb|ncaa)` → thousands of hits, almost all **audio** sports-radio (VSiN / Lombardi Line); `licenseurl` usually null.
- `collection:datasets AND (sportsbook|odds archive|…)` → **0**.
- `mediatype:data AND (nba|nfl|mlb) AND (odds|betting)` → handful of opaque data items; API responses often omitted identifiers in `fl[]` projections this run.

**No new spreadsheet/CSV odds dumps with clear open+close beyond the Wayback CDX sources already harvested.** Licence per audio item: unspecified; treat as non-usable for our importer.

## Oregon spellings seen

| File set | Spellings |
|---|---|
| NCAAF fuller 2017-18 | `Oregon`, `OregonState`, `PortlandSt` |
| NCAAB / NCAAF (prior W13/W14) | same CamelCase family; Pilots as `PortlandU` / `Portland U` (W14) |
| NHL SBRO | team = city only: **`Anaheim`** (not “Ducks”). No Portland/Oregon strings. Importer must **not** treat Anaheim as Oregon Ducks (C6-nhl “ducks” trap) |
| Aussportsbetting NFL | full names (`…`); no Oregon NFL club |
| sports-statistics MLB | abbreviations (`STL`, …); no Oregon clubs |
| predictiontracker NCAA samples | `Oregon`, etc. in team columns (spreads only) |

## Ranked import list (Composer follow-up)

1. **NHL SBRO** `data/raw/round3_odds/archive_org/sbro_nhl/` — unlocks C6-nhl Tier A O→C (~15 seasons). Watch Anaheim≠Ducks; no Oregon rows expected.
2. **Aussportsbetting NFL** — import `Total Score Open/Close` (+ over/under odds) for **2022** (gap) and optionally 2023–2025 for real prices; skip 1900 serial junk / pre-2014 rows without totals. Personal-use text on their site (already noted in A4 census).
3. **NCAAF 2017-18 fuller** — replace or patch the W13 file so bowl games after ~Dec 10 are present.
4. **sports-statistics MLB 2010** (optional) — DB already has 2011–2025; only if you want 2010.
5. **Skip:** predictiontracker; page snapshots; failed MLB 2022 SBRO capture; NHL 2022-23 until a fuller file appears (mid-season only).

## Enough to evaluate? (per sport)

| Sport | Enough from archive.org alone? |
|---|---|
| **NHL** | **Yes**, once imported (through 2021-22; 2022-23 partial). Tracker / paid API for 2023-24+. |
| **NFL** | **Mostly yes for recent totals CLV** via Aussportsbetting through 2024 (+ early 2025). Still thin on 2025 full season; 1H/team totals still absent. |
| **NBA** | **No** new seasons. Still need Odds API / tracker for 2023-24 and 2025-26. |
| **MLB** | **No** new seasons beyond what W15 already imported. SBRO 2022 file not in Wayback as 200. |
| **NCAAB** | **No** post-2021-22. CFBD/CBBD or Odds API for holdout years. |
| **NCAAF** | **No** post-2022-23; 2017 bowl patch only. |

**Still uncovered (W13):** NBA 2023-24 / 2025-26, NFL 2025 full with confidence, first-half / F5 / team totals. Live line tracker (round-4 A2) fills forward.

## Paths / meta

- Data: `data/raw/round3_odds/archive_org/` (gitignored)
- CDX + density + verify: `_meta/cdx_*.txt`, `dens_*.txt`, `verify_report.txt`, `download_manifest.txt`
