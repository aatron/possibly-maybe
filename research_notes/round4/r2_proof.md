# R2 proof: append-only decision log

**Date:** 2026-11-07  
**Owns:** `Cli/Variants/VariantLedger.cs` (`DecisionLogEntry`, `DecisionSettlement`), `VariantEvaluate`, settle path.

## What shipped

| Piece | Behavior |
|---|---|
| `DecisionLogEntries` | Append-only: run_id, decided_at, git SHA, variant id, registry version, watermark fields, venue JSON, injury/roster JSON+hash |
| `DecisionSettlements` | Separate table keyed by `DecisionLogId`; log rows never updated |
| Evaluate / backfill | Each decision inserts a new log row (shared `run_id` per slate / candidate write) |
| Shadow upsert | Kept for dashboard / shutoff; log is the immutable record |
| Settle | Still mutates shadow for compatibility; also inserts `DecisionSettlement` for newest unsettled log row |

## Schema / append proof (`DecisionLogTests`)

| Claim | Result |
|---|---|
| Tables exist after OpenAsync | PASS |
| Re-run doubles log rows; first-run rows byte-equal on key fields | PASS |
| Settlement lives in separate table; log DecidedAt / VenueLinesJson unchanged | PASS |

```bash
dotnet PossiblyMaybe.Tests/bin/Debug/net10.0/PossiblyMaybe.Tests.dll --disable-logo
# 260 succeeded (incl. DecisionLogTests)
```

## Limits / risks

- shortcut: venue capture times default to `decided_at` until tracker snapshot ids are threaded (R7 AsOfView); upgrade when live odds snapshots feed watermark `OddsSnapshotIds`.
- Shadow rows remain upserted (mutable) for existing UI/radar; immutable contract is the DecisionLog.
- Git SHA from `GIT_COMMIT` / `GITHUB_SHA` / `git rev-parse HEAD`, else `"unknown"`.
