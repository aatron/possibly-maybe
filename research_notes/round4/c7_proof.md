# C7 proof: spreads + moneylines (candidate store)

**Landed on main** from `../pm-r4-c7` work.

## What shipped

1. `CandidateEntry.Market` (`TOTAL`/`SPREAD`/`ML`) — unique on (Sport, GameKey, Market); additive ALTER for A1 DBs.
2. `SlateCandidate.Market`; launch-set `Evaluate` / shadow eval skip non-TOTAL (`WRONG_MARKET`).
3. `variants-backfill-spreadml` — store SPREAD+ML candidates (NBA/NFL/NCAA); Oregon skipped.
4. Gate1 combo + spread-edge backtests scoped to NBA + Oregon exclusion.

## Tests

`SpreadMlCandidateTests` + existing Gate1 suite. No paper-go for SPREAD/ML until a market family lands.
