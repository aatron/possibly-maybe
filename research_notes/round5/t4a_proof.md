# T4a proof: soccer Tier A once (B4 dists + B2 pooling)

**Date:** 2026-11-07  
**Branch:** `r5/t4a` · **Worktree:** `../pm-r5-t4a`  
**Owns:** `PossiblyMaybe.Cli/Backtest/SoccerR5TierA.cs`, CLI `tier-a-r5-soccer`, `SoccerR5TierATests`.  
**OregonExclusion:** unchanged (MLS Portland Timbers still excluded in fold path).  
**No paper-go / settle. No re-tune after numbers. PASS → Shadow only.**

## Command (one run)

```bash
dotnet run -c Release --project PossiblyMaybe.Cli -- tier-a-r5-soccer all \
  /path/to/possiblymaybe.db data/vault.db
```

Protocol: walk-forward pre-holdout folds vs frozen league V0 champ gates; **same open-line tickets** as V0 (side + EV gate); AssumedJuice −110; φ for `+negbin` fit on train only (`FitNegBinPhi`, fallback `TrainPhi=0.12`); ρ locked `TrainRho=−0.10`. Every variant×league logged in durable TrialLedger (`data/vault.db`).

Pass gate (pre-declared): n≥40, mean CLV ≥ V0, and (majority-fold Kelly uplift **or** identical-to-V0 tie when the curve is already the live KEEP).

## Verdict table (Shadow only / Shadow-FAIL)

| League | Variant | n | folds won (Kelly) | mean CLV | PnL u | ll V0→var | Kelly V0→var | Verdict |
|---|---|---:|---:|---:|---:|---|---|---|
| MLS | `mls-dc` | 267 | 4/6 | +0.280 | +21.3 | 0.6906→0.7092 | 421.7→485.4 | **PASS → Shadow** |
| MLS | `+poisson-all` | 267 | 4/6 | +0.280 | +21.3 | 0.6906→0.7092 | 421.7→485.4 | **PASS → Shadow** |
| MLS | `+negbin` | 267 | 4/6 | +0.280 | +21.3 | 0.6906→0.7077 | 421.7→476.9 | **PASS → Shadow** |
| MLS | `+dc-rho` | 267 | 4/6 | +0.280 | +21.3 | 0.6906→0.7092 | 421.7→485.4 | **PASS → Shadow** |
| MLS | `+pooling` | 8 | 1/6 | +0.312 | +5.4 | 0.5917→0.5634 | 89.7→108.2 | **FAIL** (n&lt;40) |
| LIGUE1 | `+poisson-all` | 344 | 0/6 | +0.142 | +14.9 | identical | identical | **PASS → Shadow** (tie / already Poisson KEEP) |
| LIGUE1 | `+negbin` | 344 | 3/6 | +0.142 | +14.9 | 0.7388→0.7344 | 274.4→302.7 | **FAIL** (no majority) |
| LIGUE1 | `+dc-rho` | 344 | 0/6 | +0.142 | +14.9 | identical @ 2.5 | identical | **PASS → Shadow** (tie; τ unused at common lines) |
| LIGUE1 | `+pooling` | 37 | 1/6 | +0.230 | −0.7 | 0.7891→0.7606 | −22.8→−20.1 | **FAIL** (n&lt;40) |
| LIGAMX | `+poisson-all` | 266 | 4/6 | +0.148 | +18.5 | 0.6862→0.6840 | 399.9→496.6 | **PASS → Shadow** |
| LIGAMX | `+negbin` | 266 | 4/6 | +0.148 | +18.5 | 0.6862→0.6862 | 399.9→494.3 | **PASS → Shadow** |
| LIGAMX | `+dc-rho` | 266 | 4/6 | +0.148 | +18.5 | 0.6862→0.6840 | 399.9→496.6 | **PASS → Shadow** |
| LIGAMX | `+pooling` | 63 | 2/6 | +0.171 | +13.4 | 0.6464→0.6584 | 259.1→270.3 | **FAIL** (no majority) |
| SERIEA | `+poisson-all` | 975 | 0/6 | +0.129 | −10.9 | identical | identical | **PASS → Shadow** (tie / already Poisson; V-SA0 paper context) |
| SERIEA | `+negbin` | 975 | 5/6 | +0.129 | −10.9 | 1.0609→0.9749 | −151.1→−123.5 | **PASS → Shadow** |
| SERIEA | `+dc-rho` | 975 | 0/6 | +0.129 | −10.9 | identical @ 2.5 | identical | **PASS → Shadow** (tie) |
| SERIEA | `+pooling` | 65 | 0/6 | +0.192 | −0.1 | 1.1016→0.7935 | −11.5→−17.3 | **FAIL** (no majority) |

`mls-dc` N/A on Ligue 1 / Liga MX / Serie A (LeagueOff). Flat CLV/PnL match V0 on same tickets; discrimination is Kelly + log-loss/Brier.

## Trial ledger / multiplicity

```
N_trials=17 DSR=0.034 BH_q=0.10 (adj over 17 trials)
```

17 = one durable `TrialEntries` row per scored variant×league (MLS×5 + three leagues×4). Footer from `TrialLedger.FormatReportFooter` after the single run. No second look; no grid.

## Tests

```bash
dotnet test PossiblyMaybe.Tests -c Release
```

304 passed (includes `SoccerR5TierATests`: pooling phase aliases, variant id set, durable TrialLedger record, Shadow-only promotion, Oregon Timbers exclusion).

## Limits

- Same-ticket flat CLV/PnL cannot beat V0; PASS here means Kelly uplift / already-live curve, not a new money edge on flat units.
- `+dc-rho` at common O/U 2.5 often bit-matches Poisson (τ only touches 0–1 goals) — documented in `b4_proof.md`.
- `+pooling` opener tags via A5 calendars (Liga MX `apertura-opening` / `clausura-opening` aliased); promoted-club list still deferred → thin n on MLS/Ligue 1 openers.
- Means for `mls-dc` remain champ λ (no alt DC att–def column) — P-curve only, per B4.

## Pointers

- Round-4 B4: `research_notes/round4/b4_proof.md` (fold table row → this file)
- Round-4 B2: `research_notes/round4/b2_proof.md` (pooling Tier A row → this file)
