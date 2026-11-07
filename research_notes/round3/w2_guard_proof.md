# W2 guard proof (Tier A, 2026-11-07)

Warehouse: `possiblymaybe.db` (main). Command: `dotnet run --project PossiblyMaybe.Cli -- tier-a-guards`.
Open→close proxies open→current until W3 mid-snaps. N_trials(edge caps)=4.

## Chosen defaults → `Guards.cs`

| Guard | Value | Why |
|---|---|---|
| Edge cap | **25%** | MLS 4/6 + Ligue 1 4/6 majority-neg removed; NBA alone does not pass |
| NBA line-move k | **2.0** | 8/8 folds removed PnL or CLV &lt; 0; worse removed PnL than k=1.5 |
| Soccer line-move k | **0.5** (or strike ≥ 8¢) | All four leagues: majority-neg when removals exist |

## NBA edge-cap (PaceEffNormal, line-move off)

| cap | folds w/ rem | neg | rem n | rem PnL | rem CLV | kept n | kept PnL |
|---|---|---|---|---|---|---|---|
| 20% | 8/8 | 2 | 154 | +31.3 | +22.0 | 29 | +3.5 |
| 25% | 8/8 | 4 | 70 | +8.3 | +47.4 | 113 | +26.5 |
| 30% | 7/8 | 2 | 26 | +10.3 | +125.6 | 157 | +24.5 |
| 35% | 5/8 | 1 | 11 | +8.1 | +296.0 | 172 | +26.6 |

**NBA edge-cap FAIL:** removed high-EV tickets historically still made money / beat the close. Cap still ships for live absurds (e.g. 74% EV) and soccer proof leagues.

## NBA line-move (edge-cap off)

| k | folds w/ rem | neg | rem n | rem PnL | rem CLV |
|---|---|---|---|---|---|
| 1.5 | 8/8 | **8** | 48 | −0.3 | −2.60 |
| 2.0 | 8/8 | **8** | 32 | −5.3 | −3.16 |

**PASS** at k=2.0.

## Soccer (summary)

| League | Best edge-cap majority? | k=0.5 majority? |
|---|---|---|
| SERIEA | no (max 3/6) | **yes** 6/6, rem PnL −10.7 |
| MLS | **yes** @25% 4/6 and @30% 5/6 | **yes** 4/4 |
| LIGUE1 | **yes** @20–25% 4/6 | **yes** 5/5 |
| LIGAMX | no | **yes** 1/1 |

## Blocker

NBA edge-cap does not meet the “removed lose in a majority of folds” bar on this warehouse; rely on line-move for NBA, edge-cap for MLS/Ligue 1 + live absurd-EV defense.
