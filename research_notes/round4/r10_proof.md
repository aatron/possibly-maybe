# R10 proof: vault + trial ledger + nested tuning + no sim-only promotion

**Date:** 2026-11-07  
**Owns:** `PossiblyMaybe.Domain/Vault.cs`, `TrialLedger.cs`, `DayEngine` gate, `VaultTests`.

## What shipped

| Piece | Behavior |
|---|---|
| `Vault` | Seals each pack's `TierCHoldoutSeason`; sim dates in that season need `--final` + strategy hash; one use per league×hash |
| `VaultStore` | In-process record of burns; second `--final` → `VaultAlreadyUsedException` |
| `TrialLedger` | Every sim run logs hash + params + date range; `N_trials` = Σ grid sizes; footer prints `N_trials` / DSR / BH_q |
| `NestedWalkForward` | Train folds exclude vault + SkipSeason; `SelectOnTrain` charges `grid.Count` as trials |
| `PromotionPolicy` | Tier A + vault → `LadderStage.Shadow` only; paper/real from sim refused |
| `EngineRunOptions` | `Live` (default engine-day) vs `Sim(..., final)` |
| `engine-day --sim [--final]` | CLI vault path (no paper-go / settle) |

Oregon exclusion untouched.

## Proof (`VaultTests`)

| Claim | Result |
|---|---|
| Vault double-use refused for same strategy hash + league | PASS |
| Engine sim on MLS holdout day without `--final` → `VaultSealedException` | PASS |
| Engine sim with `--final` logs `N_trials` / DSR; second final refused | PASS |
| Nested fold: grid size → `N_trials`; vault/skip not in train folds | PASS |
| Promotion from sim → Shadow only; paper/real refused | PASS |

```bash
dotnet build PossiblyMaybe.Tests -c Debug
dotnet PossiblyMaybe.Tests/bin/Debug/net10.0/PossiblyMaybe.Tests.dll --disable-logo
```

## Limits / risks

- shortcut: `VaultStore` is in-process only (CLI `--sim` gets a fresh store per invocation) — upgrade to a durable vault ledger file when look-once burns must survive restarts.
- shortcut: DSR uses non-skewed Lo SE + Winitzki Φ^{-1}; upgrade if skew/kurtosis matter for stake gates.
- shortcut: live `engine-day` does not burn vault (correct for unfolding seasons); only `--sim` enforces look-once.
- shortcut: SkipSeason is excluded from train folds but is not a vault burn (live/incomplete season).
