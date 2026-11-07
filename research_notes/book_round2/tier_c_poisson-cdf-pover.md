# Tier C look-once — `poisson-cdf-pover`

**Date:** 2026-10-28 (scored once; do not re-score)  
**Holdout:** soccer `2025-2026`  
**AssumedJuice:** −110  
**Candidates:** SERIEA, LIGUE1, LIGAMX (MLS excluded — Tier A FAIL)

| League | nL | clvL | pnlL | nP | clvP | pnlP | clv≥champ | pnlP>0 | KEEP |
|---|---:|---:|---:|---:|---:|---:|---|---|---|
| SERIEA | 162 | 0.136 | 20.5 | 162 | 0.136 | 20.5 | yes | yes | **YES** |
| LIGUE1 | 57 | 0.145 | 8.9 | 54 | 0.157 | 10.0 | yes | yes | **YES** |
| LIGAMX | 47 | 0.186 | 7.5 | 45 | 0.183 | 6.6 | **no** | yes | **NO** |

**Merge policy:** Poisson CDF `OverProb` for **SERIEA** and **LIGUE1** only; Liga MX + MLS stay logistic sharpness 1.2 (`SoccerOverProbConfig.UsePoissonCdfFor`).
