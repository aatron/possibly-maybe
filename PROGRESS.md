# PossiblyMaybe — progress (read this first after any interruption)

## Snapshot

| Field | Value |
|---|---|
| **Last updated** | 2026-10-25 (PaceEff totals beat Scoring) |
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
- CLI: `import-box`, `sweep-totals` compares Scoring vs PaceEff
- Script: `scripts/fetch_nba_box.py` (needs `.venv` + nba_api)

## Still missing (not in champ yet)

Injuries/lineups, four factors beyond 3PAr stored on book, shot-zone matchups, rest/B2B in totals path.

## Other sports (soft-spot hunt — 2026-10-25)

| Sport | Imported | Model | Holdout | Result |
|---|---|---|---|---|
| **NBA totals** | yes | PaceEff | 2024–25 | **PASS** (+16.3u / 62) — primary money |
| **MLB totals** | 2290 games + SP prior FIP | SP FIP × market | Sep–Nov 2025 | **FAIL** (train +21u, hold −2.2u / 94) |
| **NFL totals** | 6502 close + 92 open/close | scoring + roof/wind | 2024 | **FAIL** (hold −0.6u / 14; CLV thin) |

CLI: `import-mlb` / `import-nfl` / `sweep-mlb` / `sweep-nfl`

**Next soft spots:** see `SportPacks/GAPS_MLB_NFL.md` (park factors; nflverse EPA). MLS pack notes: `NOTES_MLS.md` / `SportPacks/MlsPack.md`. Do **not** bet MLB/NFL/MLS real until HOLD PASS.

## Nightly

```bash
dotnet run --project PossiblyMaybe.Cli -- advise-ou <paceModelExp> <openTotal>
dotnet run --project PossiblyMaybe.Cli -- paper-bet "DATE AWAY@HOME" OVER|UNDER <total> -110 <stake$>
```
