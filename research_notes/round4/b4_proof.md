# B4 proof: soccer distributions

**Branch:** `r4/b4` · **Worktree:** `../pm-r4-b4`

## What shipped

1. **`PossiblyMaybe.Domain/Variants/SoccerDistFamily.cs`** — new immutable ids (Order 130):
   - `mls-dc` — MLS only + Poisson P(over) on stored λ
   - `+poisson-all` — Poisson CDF for every soccer league (MLS / Liga MX leave logistic)
   - `+negbin` — NB2 total, φ = `SoccerDistFamily.TrainPhi` (0.12); fold MoM via `TotalsMath.FitNegBinPhi`
   - `+dc-rho` — Dixon–Coles τ_ρ joint, ρ = `TrainRho` (−0.10) via `DixonColesBook.OverProbWithRho`
2. **`DixonColesBook.OverProbWithRho`** — ρ=0 bit-matches `PoissonTotalOverProb`; book EMA means unchanged.
3. **`TotalsMath.NegBinTotalOverProb` / `FitNegBinPhi`** — NB2; φ→0 collapses to Poisson.
4. **`Variant` / `Evaluate`** — optional `SoccerOverProb` / `SoccerPhi` / `SoccerRho` (defaults keep V0–V-SA0 identical).

Means stay on the candidate row (champ / backfill λ). `mls-dc` does **not** rewrite MLS xG-EMA → DC att–def means until an alt-λ column exists; it scopes MLS and uses the Poisson curve so tickets differ on P / EV only.

**ρ note:** Dixon–Coles τ only touches (0,0)/(0,1)/(1,0)/(1,1). At common O/U 2.5, P(T>2.5) is often unchanged vs independent Poisson; effect shows at 1.5 (and in score markets). Tier A should still score log-loss on the bet side.

## Verify

```bash
cd ../pm-r4-b4
dotnet test --filter "FullyQualifiedName~SoccerDist|FullyQualifiedName~NegBin|FullyQualifiedName~DixonColes_OverProb|FullyQualifiedName~Registry_LaunchSet"
```

Expects: four new ids registered; `mls-dc` LeagueOff on Ligue 1; MLS logistic V0 ModelProb ≠ Poisson / NegBin / ρ; ρ=0 and φ=0 match Poisson.

## Tier A (same tickets protocol)

Per league (MLS, LIGUE1, LIGAMX, SERIEA): walk-forward folds; for each fold fit φ / ρ on train only (`FitNegBinPhi`, ρ MoM or lock), score **log-loss + Kelly + CLV** on the identical open-line ticket set as V0. Pass gate: majority-fold Kelly uplift and mean CLV ≥ V0. Full fold table deferred to D2 / radar once candidates are dual-λ ready for true `mls-dc` means.

| Round 5 | Status |
|---|---|
| **T4a fold table (once)** | Done 2026-11-07 — `research_notes/round5/t4a_proof.md` (`tier-a-r5-soccer`). PASS → Shadow only; FAIL stays Shadow-FAIL. No paper promotion. |

## Not in B4

Alt ModelExp for Dixon–Coles MLS means; batch MLE ξ/ρ; changing other family files.
