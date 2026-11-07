# R5 proof: checkpoint reporter

**Branch:** `r4/r5` · **Date:** 2026-11-07  
**Owns:** `PossiblyMaybe.Domain/Checkpoints.cs`, Waves/radar columns in `Cli/Variants/*` + `ui/variants.html` (via A3), stake gate n-floor aligned to 1,500.

## Pre-registered pass rules

| n (approx) | Stage | Action |
|---|---|---|
| &lt; 50 | `TooSoon` | wait |
| ~50 (50–99) | `BailCheck` | **bail** only if CLV clearly &lt; 0 (`mean + 1.96·SE &lt; 0`); else hold |
| 100–200 (through 499) | `ClvVerdict` | **keep** if mean CLV &gt; 0 else **stop** |
| ~500 (500–1499) | `PnlLoose` | PnL loosely informative (`pnl-loose`) |
| 1,500+ | `PnlConfirms` | PnL confirms; only then `AllowsWideStake` / W8 Kelly5 &gt; 2% |

Leader persistence: ≥ **3 weeks** ahead (`Checkpoints.LeaderPersistenceWeeks` = `WaveRadar.PersistenceWeeks`).

## What shipped

| Piece | Behavior |
|---|---|
| Domain | `Checkpoints.StageFor` / `Evaluate` / `AllowsWideStake` / SE |
| Radar cell | `WaveCellResult` carries `ClvSe`, `Checkpoint`, `CheckpointVerdict`, `AllowsWideStake` |
| Persist | `radar.db` Cells columns + additive ALTER for existing DBs |
| CLI | `variants-radar` prints `CLV±SE`, checkpoint, verdict; pm-calculate summary includes `wide_stake` + top ckpt |
| UI | Waves table: CLV ± SE, Checkpoint, Verdict; subtitle notes 50/100–200/500/1500 |
| Stake gate | `StakeGate.MinClvN` = `Checkpoints.PnlConfirmsN` (1500) |

## Unit proof (`CheckpointsTests`)

| Claim | Result |
|---|---|
| Stage boundaries at 50 / 100 / 500 / 1500 | PASS |
| Bail only when clearly negative | PASS |
| CLV verdict keep / stop | PASS |
| Wide stake only at 1500+ with mean &gt; 0 | PASS |
| SE from series | PASS |
| Persistence weeks = A3 (3) | PASS |

```bash
dotnet test --disable-logo
# 178 succeeded (incl. CheckpointsTests + WaveRadarTests + StakeSchemesTests)
```

## How to run

```bash
dotnet run -c Release --project PossiblyMaybe.Cli -- variants-radar
bash scripts/pm-calculate.sh   # radar summary includes checkpoint tags on top cells
```

## Limits / risks

- CLV-verdict stage spans 100–499 (plan's 100–200 band + gap to 500); `ClvVerdictEndN=200` is documented, not a hard stage cut.
- Existing `data/radar.db` gets columns via best-effort ALTER; delete the file if SQLite rejects a migrate.
- Stake gate was n≥100 (r3 W8); R5 raises the floor to 1,500 — paper Kelly5 stays soft-capped longer.
