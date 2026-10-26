# Library: empirical-Bayes partial pooling (shrink)

**Branch:** `exp/partial-pooling-shrink`  
**Sources:** `library/articles/bayes_stan_partial_pooling.md` (concepts only — no Stan/MCMC); BDA3 / ROS hierarchical shrink.  
**Idea:** after `k` games, team rates → `w·EMA + (1−w)·leagueMean` with `w = k/(k+k0)`. Tune `k0` on **train only**; lock holdout once.

## Implementation

- `ScoringBook` / `PaceEfficiencyBook`: optional `k0` (default 0 = legacy, no shrink).
- CLI: `sweep-totals --shrink`, `sweep-mls --shrink`, `sweep-soccer <SPORT> <hold> --shrink`.
- Unit tests: `k=0` → league mean; large `k` → near raw EMA.

## Kill rule

Holdout n≥40, PnL>0, Mean CLV>0, **and** hold PnL improves vs that league’s current champ (or newly PASS a FAIL league). Else **FAIL** — no ChampConfig change.

## Results (1u = $10 on $1000 flat)

| League | Model | Hold n | CLV | PnL u | $ | vs baseline | KEEP/REVERT |
|---|---|---|---|---|---|---|---|
| NBA | PaceEff + k0=10 (train) | 62 | +2.64 | +16.3 | +$163 | = PaceEff +16.3u | **REVERT** |
| MLS | xG EMA + k0=1 (train) | 92 | +0.18 | +11.3 | +$113 | −0.9u vs +12.2 | **REVERT** |
| Ligue1 | goals EMA + k0=1 (train) | 50 | +0.03 | +7.4 | +$74 | −1.0u vs +8.4 | **REVERT** |
| EFL | goals + k0=5 w=0.4 e=0.35 | 24 | +0.04 | −4.8 | −$48 | n&lt;40 / worse | **REVERT** |
| LigaMX | goals + k0=5 w=0.6 e=0.25 | 21 | +0.20 | +0.1 | +$1 | n&lt;40 | **REVERT** |
| LaLiga | goals + k0=5 w=0.6 e=0.25 | 27 | −0.09 | −5.9 | −$59 | FAIL gate | **REVERT** |
| SerieA | goals + k0=2 w=0.4 e=0.35 | 30 | +0.05 | −7.1 | −$71 | FAIL gate | **REVERT** |

## Verdict

**FAIL across the board.** ChampConfig / MlsChampConfig / Ligue1ChampConfig **unchanged**.

Train often likes mild shrink (better train PnL / volume); holdout either ties the champ (NBA) or loses money vs baseline. A few non-selected k0 cells look slightly better on hold (e.g. MLS k0=2/5 hold +13.1u) — **not promoted** (would be holdout peeking).

## CLI

```bash
dotnet run --project PossiblyMaybe.Cli -- sweep-totals [db] --shrink
dotnet run --project PossiblyMaybe.Cli -- sweep-mls [db] --shrink
dotnet run --project PossiblyMaybe.Cli -- sweep-soccer LIGUE1 2024-2025 [db] --shrink
```
