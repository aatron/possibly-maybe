# Gate 0 raw data

Download date: **2026-10-25** (manual Kaggle zips from ~/Downloads + sportsdataverse curl).

## Status summary

| ID | Source | Status | Path |
|---|---|---|---|
| **A** | Kaggle `cviaxmiwnptr/nba-betting-data-october-2007-to-june-2024` | **Downloaded** | `kaggle/A_nba-betting-data/nba_2008-2026.csv` |
| **B** | Kaggle `visualize25/basketball-betting-dataset` | **Downloaded** | `kaggle/B_basketball-betting-dataset/basketball-final.sqlite` |
| **C** | Kaggle `caseydurfee/mgm-grand-nba-betting-data` | **Downloaded** | `kaggle/C_mgm-grand-nba-betting-data/all_odds.csv` |
| **D** | GitHub sportsdataverse `espn_nba_officials` | **Downloaded** | `espn_nba_officials/` |
| **E** | Kaggle `minmaxer/nba-closing-lines-ats-2024-25` (optional) | **Downloaded** | `kaggle/E_nba-closing-lines-ats-2024-25/` |
| **extra** | Unlabeled `archive.zip` (2019 CSVs; not a locked pick) | Kept local | `kaggle/extra_nba_betting_csvs_2019/` |

Zip → folder mapping (from ~/Downloads timestamps):
- `archive(4).zip` → A
- `archive(3).zip` → B
- `archive(2).zip` → C
- `archive(1).zip` → E
- `archive.zip` → extra (not required for Gate 0)

---

## A — cviaxmiwnptr games + odds

- **URL:** https://www.kaggle.com/datasets/cviaxmiwnptr/nba-betting-data-october-2007-to-june-2024
- **File:** `nba_2008-2026.csv` (~2.4 MB, **24,440** data rows)
- **Seasons:** 2008–2026
- **License:** CC0 (per Kaggle/PLAN)
- **Caveat:** ~4,630 rows missing both ML fields (ESPN-sourced gap after Jan 2023) — merge with C for moneyline

---

## B — visualize25 open/close + officials + box

- **URL:** https://www.kaggle.com/datasets/visualize25/basketball-betting-dataset
- **File:** `basketball-final.sqlite` (~80 MB; gitignored — keep local)
- **Tables (counts):** Game 62,862 · BettingOdds_History 18,292 · Game_Officials 66,610
- **Odds columns:** HomeSpread_AtOpen/Close, Over_AtOpen/Close, HomeML, AwayML
- **License:** confirm on Kaggle page (SBRO-derived)

---

## C — MGM Grand closing odds

- **URL:** https://www.kaggle.com/datasets/caseydurfee/mgm-grand-nba-betting-data
- **File:** `all_odds.csv` (~1.6 MB, **6,081** data rows)
- **Use:** close-only ML/spread fill → `ClvQuality=CloseOnly`, Book=Mgm

---

## D — ESPN NBA officials (sportsdataverse)

- **URL:** https://github.com/sportsdataverse/sportsdataverse-data/releases/tag/espn_nba_officials
- **Files:** CSV seasons **2010–2026**
- **Size:** ~3.4 MB · **64,835** assignment rows
- **Schema:** `season,game_id,official_full_name,official_display_name,official_position,official_position_id,official_order`

---

## E — optional 2024–25 closing lines ATS

- **URL:** https://www.kaggle.com/datasets/minmaxer/nba-closing-lines-ats-2024-25
- **File:** `nba_2024_25_closing_lines_ats.csv` (~324 KB, **1,314** rows)
- Open/close fair probs + spreads/totals for live-paper era

---

## ESPN open/close spreads (NBA ATS)

- **Path:** `espn_ml_open_close/espn_spread_open_close.csv`
- **Source:** same ESPN core odds API as ML open/close
- **Coverage:** seasons 2024–2026; ~1313/1319 holdout games with open≠close spreads
- Plus B `HomeSpread_AtOpen/Close` for earlier history (through ~2021)

---

## ESPN open/close moneyline (unblocks holdout CLV)

- **Path:** `espn_ml_open_close/espn_ml_open_close.csv`
- **Source:** ESPN public core API `…/events/{id}/competitions/{id}/odds` (same family as sportsdataverse)
- **Pulled:** 2026-10-25 for seasons 2021–2026 with `EspnGameId`
- **Open ML retained by ESPN roughly from 2023–24 onward:** season 2024 ≈1317/1319 open; 2025–26 similar; 2021–23 close-only in this API
- Prefer non-live books with both open+close (MGM / ESPN BET / PointsBet / …)

---

## Schedules (local join key for D officials)

Downloaded 2026-10-25 from sportsdataverse `espn_nba_schedules` release into `espn_nba_schedules/nba_schedule_2010.csv` … `2026.csv` (gitignored). Used only to attach `EspnGameId` onto games for referee joins.

---

## Extra (not a locked source)

`extra_nba_betting_csvs_2019/` from unlabeled `archive.zip` (money_line/spread/totals + player stats). Player game stats CSV is ~194 MB and gitignored. Do not treat as primary Gate 0 input unless ingest needs it.

---

## Git

**All raw datasets stay local** (`data/raw/**` gitignored). Only this `NOTES.md` is tracked. Re-download from the URLs above (or unpack zips into the paths in the status table) on each machine. Generated `*.db` is also gitignored.
