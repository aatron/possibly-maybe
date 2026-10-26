# PossiblyMaybe — progress (read this first after any interruption)

## Snapshot

| Field | Value |
|---|---|
| **Last updated** | 2026-10-25 (PaceEffEfg tracking FAIL; PaceEff stays champ) |
| **Rollback** | `git checkout 7be68f0` — prior Scoring champ (w=0.4, edge≥3.5, hold +8.4u / 320) |
| **Current gate** | **2** live paper |
| **Champ** | **NBA TOTALS PaceEff** · 50% open + 50% pace×ORtg · `|exp−line|≥6` · EV≥4% |
| **Kill status** | **HOLD PASS** |
| **Holdout 2024–25** | **62 bets**, CLV **+2.64 pts**, PnL **+16.3u** |
| **Bankroll seed** | $1000 paper |

## Scoring vs PaceEff (same train rule)

| Model | Champ (train) | Holdout bets | Holdout PnL | Holdout CLV |
|---|---|---|---|---|
| Scoring (old) | w=0.4 edge=3.5 | 320 | +8.4u | +1.91 |
| **PaceEff (new)** | w=0.5 edge=6.0 | **62** | **+16.3u** | **+2.64** |

PaceEff wins on **dollars** (same flat 1u); fewer tickets (higher bar). Volume-friendly PaceEff alt: w=0.6 edge=3.5 → hold +11.5u / 147 bets.

## Data added

- `data/raw/nba_box/box_games.csv` — B box + `nba_api` LeagueGameFinder (2021–26), gitignored
- `data/raw/nba_tracking/team_pt_shot_*.csv` — prior-season eFG (gitignored); `scripts/fetch_nba_tracking.py` (`SpeedDistance` measure type)
- CLI: `import-box`, `sweep-totals` compares Scoring vs PaceEff vs PaceEffEfg
- Script: `scripts/fetch_nba_box.py` (needs `.venv` + nba_api)

## PaceEff vs PaceEffEfg (2026-10-25)

| Model | Champ (train) | Holdout bets | Holdout PnL | Holdout CLV |
|---|---|---|---|---|
| **PaceEff** | w=0.5 edge=6.0 | **62** | **+16.3u** | **+2.64** |
| PaceEffEfg | w=0.5 edge=6.0 | 74 | +6.2u | +2.59 |
| PaceEffEfg (alt) | w=0.6 edge=3.5 | 201 | +14.7u | +1.86 |

Prior-season tracking eFG does **not** beat PaceEff holdout PnL → no ChampConfig change.

## Still missing (not in champ yet)

Injuries/lineups, four factors beyond 3PAr stored on book, shot-zone matchups, rest/B2B in totals path.

## Other sports (soft-spot hunt — 2026-10-25)

| Sport | Imported | Model | Holdout | Result |
|---|---|---|---|---|
| **NBA totals** | yes | PaceEff | 2024–25 | **PASS** (+16.3u / 62) — primary money |
| **NBA + tracking eFG** | yes (prior-szn pt_shot) | PaceEffEfg | 2024–25 | **FAIL** vs PaceEff (+6.2u/74 &lt; +16.3u/62) |
| **MLS totals** | **3506** + ASA xG | xG EMA × market | **2025** | **PASS** (+12.2u / 93, CLV +0.18) — paper |
| **MLB totals** | 2290 games + SP prior FIP + Savant 3yr PF | SP FIP × home PF × market | Sep–Nov 2025 | **FAIL** (champ w=0.4 edge=0.5: train +18.4u / 473; hold **+1.4u / 100**, CLV **−0.03** — PnL>0 but CLV≤0) |
| **NFL totals** | 6502 close + 92 open/close + pregame EPA CSV | EPA + roof/wind | 2024 | **PASS** (hold +7.1u / 33; CLV 0 — provisional) |

CLI: `import-mlb` / `import-nfl` / `import-mls` / `sweep-mlb` / `sweep-nfl` / `sweep-mls`

**MLS champ:** w=0.40 · `|exp−line|≥0.25` · holdout locked in `NOTES_MLS.md` before peek. Paper only.

## Nightly

```bash
dotnet run --project PossiblyMaybe.Cli -- advise-ou <paceModelExp> <openTotal>
dotnet run --project PossiblyMaybe.Cli -- paper-bet "DATE AWAY@HOME" OVER|UNDER <total> -110 <stake$>
```
