# B3 proof: NBA volume (`nba-volume`, `nba-volume-efg`)

**Branch:** `r4/b3` · **Worktree:** `../pm-r4-b3`

## What shipped

1. `PossiblyMaybe.Domain/Variants/NbaVolumeFamily.cs` — immutable ids:
   - `nba-volume` — PaceEff, w=0.6, |edge|≥3.5, NBA-only
   - `nba-volume-efg` — PaceEffEfg arm, same gates, NBA-only
2. `Variant` optional overrides: `MarketWeight`, `MinAbsEdge`, `NbaModel`; `SlateCandidate.ModelExpEfg` (optional). Evaluate blends with variant weight / edge when set; eFG uses `ModelExpEfg` when present (else falls back to PaceEff `ModelExp` until candidate store backfills).
3. CLI: `tier-a-nba-volume [db]`

## Tier A (ran 2026-11-07)

```bash
dotnet run -c Release --project PossiblyMaybe.Cli -- tier-a-nba-volume \
  ../possibly-maybe/possiblymaybe.db
```

Pre-declared folds 2015–2022; holdout 2025 report-only. `N_trials=2`.

### Fold CLV/bet (PaceEff volume vs V0)

| fold | n V0 | CLV/bet V0 | n vol | CLV/bet vol | Δ CLV/bet |
|---|---:|---:|---:|---:|---:|
| 2015 | 15 | 14.20 | 68 | 3.89 | −10.31 |
| 2016 | 22 | 0.80 | 89 | 1.21 | +0.42 |
| 2017 | 19 | 94.32 | 104 | 17.66 | −76.65 |
| 2018 | 20 | −0.15 | 87 | 0.64 | +0.79 |
| 2019 | 48 | 0.88 | 134 | 0.81 | −0.07 |
| 2020 | 29 | 0.38 | 84 | 0.65 | +0.27 |
| 2021 | 33 | 26.80 | 79 | 12.14 | −14.66 |
| 2022 | 57 | 15.66 | 103 | 9.22 | −6.43 |
| **mean** | **243** | **19.11** | **748** | **5.78** | **−13.33** |

Sum flat PnL folds: V0 +30.1u vs volume +11.4u. Volume takes ~3× the bets; mean CLV/bet is lower than V0 (V0 fold means are noisy at small n — 2017/2021 outliers).

### Holdout 2025 (report-only; matches r3 order of magnitude)

| arm | n | CLV/bet | flat PnL |
|---|---:|---:|---:|
| V0 (w=0.5 e=6 PaceEff) | 61 | 2.66 | +17.3u |
| `nba-volume` | 145 | 2.53 | +13.5u |
| `nba-volume-efg` | 187 | 1.99 | +13.5u |

r3 published hold: V0 +16.3u/62, volume +11.5u/147, efg-volume +14.7u/201 — same story: volume close on PnL with more tickets; CLV/bet slightly under V0.

## Verdict

Keep as **shadow variants** (many-small vs few-big). Not ahead of V0 on CLV/bet in Tier A folds or holdout; do not promote. `nba-volume-efg` needs candidate `ModelExpEfg` backfill for live filter parity with the Tier A PaceEffEfg path.

## Tests

```bash
dotnet test PossiblyMaybe.Tests/PossiblyMaybe.Tests.csproj
```

170 passed (registry ids + volume edge / league-off / eFG ModelExp).
