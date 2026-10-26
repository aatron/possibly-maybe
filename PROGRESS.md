# PossiblyMaybe — progress (read this first after any interruption)

## Snapshot

| Field | Value |
|---|---|
| **Last updated** | 2026-10-26 (time-decay α KEEP MLS/Ligue1/LigaMX; SerieA DC; de-vig/pooling REVERT) |
| **Rollback** | `git checkout 311127f` — pre–library soft-spot champs |
| **Current gate** | **2b** live paper month ($1000) — **paper only** until month CLV/PnL trust |
| **Champ** | **NBA TOTALS PaceEff** · 50% open + 50% pace×ORtg · `|exp−line|≥6` · EV≥4% |
| **Kill status** | **HOLD PASS** |
| **Holdout 2024–25** | **62 bets**, CLV **+2.64 pts**, PnL **+16.3u** |
| **Bankroll seed** | **$1000 × 6 stake lanes** through **2026-12-31** (`paper.db`) |
| **Stake lanes** | Flat · Kelly2 · Kelly5 · EvProp · ConfBand · EdgeBand |

### Daily paper ritual (Gate 2b — multi-lane through 2026)

**Preferred:** `/pm-calculate` (see `AGENTS.md`) → `bash scripts/pm-calculate.sh`

Every GO fans out across all six lanes (each sizes from its own $1000 bankroll). Use the printed `paper-go` line as-is. No reloads — lane at ~$0 is OUT.

```bash
bash scripts/pm-calculate.sh                  # today + tomorrow slate, at-risk, ESPN probe, dashboard
# for each GO line printed:
dotnet run --project PossiblyMaybe.Cli -c Release -- paper-go "KEY" OVER|UNDER LINE -110 P ABSEDGE NBA|MLS
# for each FINAL settle hint (or after games):
dotnet run --project PossiblyMaybe.Cli -c Release -- settle-go "KEY" W|L|P [closeLine]
```

Open `ui/index.html` for equity curves + leaderboard.

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
| **MLS totals** | **3506** + ASA xG | **xG EMA α=0.206** × market | **2025** | **PASS** (+15.7u / 100, CLV +0.19) — paper; beats DC +13.2u and α=0.18 +12.2u |
| **EFL Championship OU** | 2026 open/close | goals EMA / DC / α | **2024-2025** | **FAIL** |
| **Liga MX OU** | 1424 open/close | **goals EMA α=0.129** | **2024-2025** | **PASS** (+3.5u / 52, CLV +0.24) — paper only; prior n&lt;40 FAIL |
| **Ligue 1 OU** | 1483 open/close | **goals EMA α=0.206** | **2024-2025** | **PASS** (+10.1u / 53, CLV +0.02) — paper; prior α=0.18 +8.4u |
| **La Liga OU** | 1650 open/close | goals EMA / DC / α | **2024-2025** | **FAIL** |
| **Serie A OU** | 1731 open/close | **Dixon–Coles goals** (α=0.18) | **2024-2025** | **PASS** (+1.7u / 113, CLV +0.03) — prior EMA FAIL; no live slate yet |
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
| MLS xG EMA α=0.206 | +15.7u | **+$157** |
| Ligue 1 EMA α=0.206 | +10.1u | **+$101** |
| Liga MX EMA α=0.129 | +3.5u | **+$35** |
| Serie A Dixon–Coles | +1.7u | **+$17** |

**Shared $1000 soft-spot book (all PASS tickets, 1u=$10):** +16.3+15.7+10.1+3.5+1.7 = **+47.3u → +$473** on those holdout samples (different calendars — not one simultaneous year).

### Library ML experiments (2026-10-26)

| Topic | Verdict | Notes |
|---|---|---|
| Dixon–Coles att-def EMA | **KEEP** Serie A only (MLS later beaten by α) | `67cea7a` |
| EMA half-life / sparsity | **KEEP** MLS α=0.206, Ligue1 α=0.206, LigaMX α=0.129 | NBA/B2B REVERT; `reports/library_sparsity_timedecay.md` |
| De-vig / fair EV juice | **REVERT** | Identical to champ (−110 warehouse); `reports/library_devig.md` |
| EB partial pooling (k0) | **REVERT** | No holdout win; `reports/library_partial_pooling.md` |
| **MLB totals** | 2290 games + SP prior FIP + Savant 3yr PF | SP FIP × home PF × market | Sep–Nov 2025 | **FAIL** (champ w=0.4 edge=0.5: train +18.4u / 473; hold **+1.4u / 100**, CLV **−0.03** — PnL>0 but CLV≤0) |
| **NFL totals** | 6502 close + **450 ESPN** + 92 minmaxer OU open/close + EPA | EPA + roof/wind | 2024 | **FAIL** (real OpenClose: no train config with PnL>0 in band; prior +7.1u was CloseOnly mirage) |

CLI: `import-mlb` / `import-nfl` / `import-mls` / `sweep-mlb` / `sweep-nfl` / `sweep-mls`

**MLS champ:** xG EMA · α=0.206 · w=0.40 · `|exp−line|≥0.25` · hold **+15.7u / 100**. Paper slate uses `ScoringBook`.

**Ligue 1 champ:** goals EMA · α=0.206 · w=0.40 · `|exp−line|≥0.25` · hold **+10.1u / 53**.

**Liga MX champ:** goals EMA · α=0.129 · w=0.40 · `|exp−line|≥0.25` · hold **+3.5u / 52**. Paper only (no slate).

**Serie A champ:** Dixon–Coles goals · α=0.18 · w=0.40 · `|exp−line|≥0.35` · hold **+1.7u / 113**. No live slate CLI yet.

## Nightly

```bash
dotnet run --project PossiblyMaybe.Cli -- advise-ou <paceModelExp> <openTotal>
dotnet run --project PossiblyMaybe.Cli -- paper-bet "DATE AWAY@HOME" OVER|UNDER <total> -110 <stake$>
```
