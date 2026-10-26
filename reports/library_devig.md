# Library experiment: de-vig / EV juice methods

**Branch:** `feat/library-devig-v2`  
**Baseline:** `311127f`  
**Sources:** `devig_penaltyblog_implied`, `devig_oddspapi_no_vig_python`, `devig_implied_r_vignette`  
**Kill gate:** HOLD n≥40, PnL>0, Mean CLV>0; KEEP only if holdout PnL beats champ without CLV≤0.  
**$:** flat 1u = $10 on $1000 bankroll.

## What changed

- `OddsMath.NoVigTwoWay(..., NoVigMethod)` — multiplicative (default), additive (Shin-lite 2-way), power.
- `EvJuiceKind` — Fixed110 / OpenAmerican / FairMultiplicative / FairAdditive / FairPower.
- EV **gate** uses the chosen juice; **settlement PnL** always uses actual open American (warehouse AssumedJuice −110).
- CLI: `sweep-totals --devig`, `sweep-mls --devig`, `sweep-soccer LIGUE1 2024-2025 --devig`.

## Data constraint

All TOTAL `OddsSnapshots` in `possiblymaybe.db` are over-only with `AmericanOdds = -110` (`AssumedJuice`). Missing under side is treated as −110. Therefore fair multi/additive/power all map to **+100** EV gate, and OpenAmerican ≡ Fixed110.

## Holdout results (champ gates locked)

| League | Devig/EV method | Hold n | CLV | PnL u | $ | vs champ | KEEP/REVERT |
|--------|-----------------|-------:|----:|------:|--:|---------:|-------------|
| NBA PaceEff | OpenAmerican (champ) | 62 | +2.64 | +16.3 | +163 | 0 | CHAMP |
| NBA PaceEff | Fixed110 | 62 | +2.64 | +16.3 | +163 | 0 | REVERT |
| NBA PaceEff | FairMultiplicative | 62 | +2.64 | +16.3 | +163 | 0 | REVERT |
| NBA PaceEff | FairAdditive | 62 | +2.64 | +16.3 | +163 | 0 | REVERT |
| NBA PaceEff | FairPower | 62 | +2.64 | +16.3 | +163 | 0 | REVERT |
| MLS xG | Fixed110 (champ) | 93 | +0.18 | +12.2 | +122 | 0 | CHAMP |
| MLS xG | OpenAmerican | 93 | +0.18 | +12.2 | +122 | 0 | REVERT |
| MLS xG | FairMultiplicative | 93 | +0.18 | +12.2 | +122 | 0 | REVERT |
| MLS xG | FairAdditive | 93 | +0.18 | +12.2 | +122 | 0 | REVERT |
| MLS xG | FairPower | 93 | +0.18 | +12.2 | +122 | 0 | REVERT |
| Ligue1 goals EMA | Fixed110 (champ) | 49 | +0.03 | +8.4 | +84 | 0 | CHAMP |
| Ligue1 goals EMA | OpenAmerican | 49 | +0.03 | +8.4 | +84 | 0 | REVERT |
| Ligue1 goals EMA | FairMultiplicative | 49 | +0.03 | +8.4 | +84 | 0 | REVERT |
| Ligue1 goals EMA | FairAdditive | 49 | +0.03 | +8.4 | +84 | 0 | REVERT |
| Ligue1 goals EMA | FairPower | 49 | +0.03 | +8.4 | +84 | 0 | REVERT |

## Verdict

**REVERT-ready.** No method improves holdout PnL vs champ; books are identical at locked gates.

Why identical: champ `|exp−line|` floors already push model `OverProb` above both EV thresholds (p≳0.545 at −110 and p≳0.52 at fair +100). With assumed −110/−110 books, fair methods cannot diverge from each other. Re-run when two-way open prices exist (non-AssumedJuice).

**ChampConfig / MlsChampConfig / Ligue1ChampConfig:** untouched.

## Reproduce

```bash
dotnet run --project PossiblyMaybe.Cli -c Release -- sweep-totals --devig
dotnet run --project PossiblyMaybe.Cli -c Release -- sweep-mls --devig
dotnet run --project PossiblyMaybe.Cli -c Release -- sweep-soccer LIGUE1 2024-2025 --devig
```
