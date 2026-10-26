# Library ML soft-spot experiments (2026-10-26)

Baseline: `59e380e` on `main`.

| Worktree agent | Topic cluster | Library sources | Target |
|---|---|---|---|
| Dixon-Coles | Poisson attack–defense + time weight | `soccer_dashee87_*`, `soccer_opisthokonta_*`, `soccer_penaltyblog_models_overview` | MLS, Ligue1, EFL, LigaMX, LaLiga, SerieA |
| De-vig | Additive/power/no-vig EV | `devig_*`, `calib_opticodds_*` | NBA, MLS, Ligue1 |
| Partial pooling | James–Stein / EB shrinkage | `bayes_stan_partial_pooling`, BDA3 / ROS concepts | NBA + soccer |
| Sparsity / time-decay | ESL sparsity, CASI, α half-life; DSR caution | ESL, Sparsity, `backtest_*` | NBA + soccer |

**Keep rule:** holdout n≥40, PnL>0, Mean CLV>0, and beat that league’s current champ PnL (or newly PASS a FAIL league). Useless branches: do not merge; revert ChampConfig if touched.

**Skip this round (already tested or not soft-spot creators):** Platt/ridge cal, PaceEffEfg, full Kelly papers (sizing only), CLV definition articles (already kill-gated), Stan MCMC, Weibull-copula.