# Dixon–Coles / independent Poisson att-def (EMA time-weight)

**Branch:** `exp/dixon-coles-att-def`  
**Rollback SHA (pre-champ):** `311127f`  
**Model:** `DixonColesBook` — λ_h=μ·att_h·def_a·γ, λ_a=μ·att_a·def_h; EMA α ≈ dashee87 ξ (no ρ / no batch MLE).  
**CLI:** `sweep-soccer-dc <SPORT> <holdoutSeason> [db]`  
**Gate:** flat 1u=$10 on $1000; HOLD PASS needs n≥40, PnL>0, Mean CLV>0. KEEP only if hold PnL **and** CLV beat baseline, or newly PASS a prior FAIL league.

## Holdout table

| League | Model | Hold n | Hold CLV | Hold PnL (u) | $ on 1000 | vs baseline | KEEP/REVERT |
|---|---|---:|---:|---:|---:|---|---|
| MLS | DC α=0.12 w=0.50 edge=0.25 | 116 | +0.183 | **+13.2** | **+$132** | beats xG-EMA +12.2u/93 CLV+0.18 | **KEEP** |
| Ligue1 | DC α=0.18 w=0.40 edge=1.00 | 21 | −0.012 | +1.0 | +$10 | worse than goals-EMA +8.4u/49 CLV+0.03; n&lt;40 | **REVERT** |
| EFL | DC α=0.12 w=0.50 edge=0.75 | 6 | +0.083 | −2.2 | −$22 | still FAIL (was −0.5u/54) | **REVERT** |
| LigaMX | DC α=0.06 w=0.40 edge=0.35 | 45 | +0.233 | −9.6 | −$96 | still FAIL (was +2.0u/21 n&lt;40) | **REVERT** |
| LaLiga | DC | — | — | — | — | no train config in band | **REVERT** |
| SerieA | DC α=0.18 w=0.40 edge=0.35 | 113 | +0.033 | **+1.7** | **+$17** | newly PASS (was FAIL −8.5u/40) | **KEEP** |

## Champ updates

- `MlsChampConfig` → Dixon–Coles xG α=0.12 w=0.50 edge≥0.25; slate uses `DixonColesBook`.
- `SerieAChampConfig` (new) → DC goals α=0.18 w=0.40 edge≥0.35. No live slate CLI yet.
- Ligue1 / EFL / LigaMX / LaLiga ChampConfig **unchanged**.

## Skipped

Full bivariate ρ, Weibull, copula; batch MLE ξ re-fit each tip (EMA proxy only).
