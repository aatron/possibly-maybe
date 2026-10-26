# PossiblyMaybe — progress (read this first after any interruption)

## Snapshot

| Field | Value |
|---|---|
| **Last updated** | 2026-10-26 (NBA PaceEff ISLP Platt/Ridge FAIL; champ unchanged) |
| **Rollback** | `git checkout 7be68f0` — prior Scoring champ (w=0.4, edge≥3.5, hold +8.4u / 320) |
| **Current gate** | **2b** live paper month ($1000) — **paper only** until month CLV/PnL trust |
| **Champ** | **NBA TOTALS PaceEff** · 50% open + 50% pace×ORtg · `|exp−line|≥6` · EV≥4% |
| **Kill status** | **HOLD PASS** |
| **Holdout 2024–25** | **62 bets**, CLV **+2.64 pts**, PnL **+16.3u** |
| **Bankroll seed** | **$1000 paper** (`paper.db`) |

### Daily paper ritual (Gate 2b)

```bash
# after tip / next morning: refresh boxes then slate
.venv/bin/python -c "..."   # or scripts/fetch_nba_box.py for 2026-27
dotnet run --project PossiblyMaybe.Cli -c Release -- import-box
dotnet run --project PossiblyMaybe.Cli -c Release -- slate-nba \$(date -u +%Y%m%d) 1000
dotnet run --project PossiblyMaybe.Cli -c Release -- slate-mls \$(date -u +%Y%m%d) 1000
# for each GO line printed:
dotnet run --project PossiblyMaybe.Cli -c Release -- paper-bet "KEY" OVER|UNDER LINE -110 STAKE
# after games: settle <id> W|L|P [closeLine]
dotnet run --project PossiblyMaybe.Cli -c Release -- paper
```

Ask in chat each day for the slate + settles if you want me to run it.

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

## PaceEff vs ISLP cal (same gate w=0.5 edge=6.0 EV≥4%) — 2026-10-26

Walk-forward Platt (`OuPlattCalibrator` on blended edge) and ridge-logistic on `[line, modelExp]` (no new NuGet). LogLoss/Brier on all scored non-push games (secondary; kill rule unchanged).

| Model | Hold n | Hold CLV | Hold PnL | Hold Brier | Hold LogLoss |
|---|---|---|---|---|---|
| **PaceEff (champ)** | **62** | **+2.64** | **+16.3u** | **0.267** | **0.737** |
| PaceEffPlatt | 31 | +3.19 | +9.1u | 0.490 | 6.763 |
| PaceEffRidge | 30 | +2.10 | +8.2u | 0.250 | 0.692 |

**FAIL** vs PaceEff → **ChampConfig unchanged**. Neither beats holdout PnL. Platt’s higher CLV is on half the book with collapsed log-loss (miscalibrated EV gate) — not a money-path upgrade. Ridge loses CLV and PnL (Brier/LL slightly better only).

CLI: `sweep-totals [db] --cal` for same-gate table; full `sweep-totals` prints ll/br on Scoring/PaceEff/PaceEffEfg rows.

## Still missing (not in champ yet)

Injuries/lineups, four factors beyond 3PAr stored on book, shot-zone matchups, rest/B2B in totals path.

## Other sports (soft-spot hunt — 2026-10-25)

| Sport | Imported | Model | Holdout | Result |
|---|---|---|---|---|
| **NBA totals** | yes | PaceEff | 2024–25 | **PASS** (+16.3u / 62) — primary money |
| **NBA + tracking eFG** | yes (prior-szn pt_shot) | PaceEffEfg | 2024–25 | **FAIL** vs PaceEff (+6.2u/74 &lt; +16.3u/62) |
| **NBA + Platt / ridge cal** | yes (walk-forward) | PaceEffPlatt / Ridge | 2024–25 | **FAIL** vs PaceEff (PnL 9.1u/8.2u &lt; 16.3u; Platt LL broken) |
| **MLS totals** | **3506** + ASA xG | xG EMA × market | **2025** | **PASS** (+12.2u / 93, CLV +0.18, ll=0.681 br=0.244) — paper; **Platt FAIL** (same PnL/CLV, worse ll) |
| **EFL Championship OU** | 2026 open/close | goals EMA | **2024-2025** | **FAIL** (champ hold −0.5u / 54) |
| **Liga MX OU** | 1424 open/close | goals EMA | **2024-2025** | **FAIL** (hold +2.0u / 21, CLV +0.23 — n&lt;40) |
| **Ligue 1 OU** | 1483 open/close | goals EMA | **2024-2025** | **PASS** (+8.4u / 49, CLV +0.03, ll=0.692 br=0.249) — paper; **Platt FAIL** (1 bet −1.0u) |
| **La Liga OU** | 1650 open/close | goals EMA | **2024-2025** | **FAIL** (hold −5.0u / 28, CLV −0.10) |
| **Serie A OU** | 1731 open/close | goals EMA | **2024-2025** | **FAIL** (hold −8.5u / 40, CLV +0.09) |
| **Cricket / AFL / Darts** | outcomes free; **no free DK open→close** | — | — | **NO Gate 0** |
| **TWA–Tokyo** | = **WTA Pan Pacific (Tokyo)** (WTA misread) | tennis open/close exists (Valuebet) | — | **No Gate 0 for one event**; full WTA/ATP pack later if prioritized |
| **TT Elite Series** | results free; no free O→C | — | — | **NO Gate 0** |
| **Boxing** | small-n + no free O→C | — | — | **NO Gate 0** |
| **Japan B.League Premier** | boxes partial; no free O→C; Premier=2026–27 regime break | — | — | **NO Gate 0** |
| **Colombia Primera A** | scores free; **no free open→close** in our dumps | — | — | **NO Gate 0** |

### $1000-per-sport holdout (flat 1u=$10) — PASS only

| Sport | Holdout PnL | On $1000 |
|---|---|---|
| NBA PaceEff | +16.3u | **+$163** |
| MLS xG | +12.2u | **+$122** |
| Ligue 1 goals EMA | +8.4u | **+$84** |

**Shared $1000 soft-spot book (all PASS tickets, 1u=$10):** +16.3+12.2+8.4 = **+36.9u → +$369** on those holdout samples (different calendars — not one simultaneous year).
| **MLB totals** | 2290 games + SP prior FIP + Savant 3yr PF | SP FIP × home PF × market | Sep–Nov 2025 | **FAIL** (champ w=0.4 edge=0.5: train +18.4u / 473; hold **+1.4u / 100**, CLV **−0.03** — PnL>0 but CLV≤0) |
| **NFL totals** | 6502 close + **450 ESPN** + 92 minmaxer OU open/close + EPA | EPA + roof/wind | 2024 | **FAIL** (real OpenClose: no train config with PnL>0 in band; prior +7.1u was CloseOnly mirage) |

CLI: `import-mlb` / `import-nfl` / `import-mls` / `sweep-mlb` / `sweep-nfl` / `sweep-mls`

**MLS champ:** w=0.40 · `|exp−line|≥0.25` · holdout locked in `NOTES_MLS.md` before peek. Paper only. Walk-forward Platt on `(modelExp−line)` does **not** beat base → no `MlsChampConfig` change.

**Ligue 1 champ:** w=0.40 · `|exp−line|≥0.25` · hold **+8.4u / 49**. Same Platt kill → no `Ligue1ChampConfig` change. Sweeps print hold bet-slice log loss + Brier.

## Nightly

```bash
dotnet run --project PossiblyMaybe.Cli -- advise-ou <paceModelExp> <openTotal>
dotnet run --project PossiblyMaybe.Cli -- paper-bet "DATE AWAY@HOME" OVER|UNDER <total> -110 <stake$>
```
