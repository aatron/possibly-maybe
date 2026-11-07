# T1 proof: durable vault + trial ledger

**Date:** 2026-11-07  
**Owns:** `PossiblyMaybe.Domain/VaultDb.cs`, durable `VaultStore` / `TrialLedger`, `engine-day --sim` wiring, `VaultTests`.

## What shipped

| Piece | Behavior |
|---|---|
| `VaultDb` | Append-only SQLite at `data/vault.db` (`VaultBurns` + `TrialEntries`) |
| `VaultStore.Open` | Loads burns from file; `RecordUse` INSERTs with UNIQUE(league, hash) |
| `TrialLedger.Open` | Loads prior rows; `Record` appends (never updates) |
| `engine-day --sim` | Opens `VaultDb.DefaultPath(repoRoot)` instead of in-process stores |
| Restart safety | Second `--final` for same league × strategy hash refused across process restarts |

Removes R10 shortcut (in-process-only vault). Oregon exclusion untouched. No paper-go / settle.

## Proof (`VaultTests`)

| Claim | Result |
|---|---|
| In-process vault double-use refused | PASS |
| Two separate store instances on the same file → second final refused; trials reload | PASS |
| Engine sim without `--final` → `VaultSealedException` | PASS |
| Engine sim `--final` logs N_trials / DSR; second final refused | PASS |
| Nested fold / promotion policy (R10) unchanged | PASS |

```bash
dotnet test PossiblyMaybe.Tests -c Debug --filter VaultTests
```

## Limits / risks

- shortcut: DSR uses non-skewed Lo SE + Winitzki Φ^{-1} (unchanged from R10).
- shortcut: live `engine-day` does not burn vault (correct for unfolding seasons); only `--sim` enforces look-once.
- shortcut: SkipSeason is excluded from train folds but is not a vault burn.
