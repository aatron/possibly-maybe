# R3 proof: same-book close

**Branch:** `r4/r3`  
**Owns:** `PaperLedger.ResolveSettleLinesAsync`, `VariantSettle`, `LineTrackerImporter.TryCloseFromSnapshots`  
**Depends on:** A2 (tracker all leagues) — merged @ `597cd2a`

## Rule

| Ticket venue | Closing book | Notes |
|---|---|---|
| DraftKings | `Book.DraftKings` (tracker `espn`) | Consensus / Pinnacle are **cross-book** |
| Kalshi | `Book.Kalshi` | |
| Robinhood | `Book.Kalshi` | Close American adjusted with RH fee |
| Crypto.com | `Book.CryptoCom` | |

Stored on settle: `CloseBook`, `CloseCapturedAt`, `MinutesBeforeStart`, `CrossBookClose`.  
**CLV (`ClvPts`) only when `SameBook` / `!CrossBookClose`.** Cross-book closes may still be stored for audit.

## What changed

| Before | After |
|---|---|
| Prefer DK/Consensus/Pinnacle, else any book | Prefer **venue’s** book only for CLV |
| One close applied to mixed-venue settle groups | Resolve **per venue** |
| Tracker close mixed all venues | Filter by tracker venue (`espn`/`kalshi`/`cryptocom`) |
| Shadow settle: first TOTAL Close (often Consensus) | Prefer DraftKings; Consensus → `CrossBookClose`, `ClvPts` null |

## Proof metric

**% of tickets (or slate games) with a same-book close ≤ 15 min before start, per league.**

```text
paper status          # prints same-book + ≤15m per sport
backfill-clv …        # refills Close* + ClvPts under R3 rules
```

Tracker SQL (any venue’s last pre-start snap):

```sql
WITH last AS (
  SELECT sport, venue, game_key, MIN(minutes_to_start) AS m
  FROM line_snapshots
  WHERE minutes_to_start > 0
  GROUP BY 1,2,3
)
SELECT sport, venue,
       COUNT(*) AS games,
       SUM(CASE WHEN m <= 15 THEN 1 ELSE 0 END) AS fresh15
FROM last GROUP BY 1,2;
```

## Measured (2026-11-07, main `data/line_snapshots.db`)

Same-book = tracker venue for that paper book present for the game. Fresh = last pre-start capture with `minutes_to_start ≤ 15`.

| League | DK same / ≤15m | Kalshi(+RH) same / ≤15m | Crypto same / ≤15m |
|---|---|---|---|
| NBA | 7/7 (100%) / 0/7 (0%) | 7/7 / 0/7 | 7/7 / 0/7 |
| NFL | 30/30 / 0/30 | 30/30 / 0/30 | 28/30 / 0/30 |
| MLS | 15/15 / 0/15 | 12/15 / 0/15 | 0/15 / 0/15 |
| NCAAF | 43/47 / 2/47 | 36/47 / 1/47 | 0/47 / 0/47 |
| NCAAB | 3/4 / 0/4 | 3/4 / 0/4 | 0/4 / 0/4 |
| Ligue 1 | 8/8 / 0/8 | 2/8 / 0/8 | 0/8 / 0/8 |
| Liga MX | 6/6 / 0/6 | 0/6 / 0/6 | 0/6 / 0/6 |
| Serie A | 6/6 / 0/6 | 3/6 / 0/6 | 0/6 / 0/6 |
| La Liga | 7/7 / 1/7 | 0/7 / 0/7 | 0/7 / 0/7 |

**Same-book availability** is already high for ESPN→DK on live leagues. **≤15m freshness** is near zero: cron captures are mostly hours before tip (see buckets 180–720m / >720m). Need denser pre-tip tracker runs, not more book mixing.

Warehouse historical TOTAL closes are almost all `Consensus` → they count as **cross-book** for every venue and do **not** enter CLV until DraftKings / Kalshi / Crypto.com closes exist (tracker import).

Paper ledger on main still lacks `CloseBook` columns until `backfill-clv` after merge.

## Tests

```bash
dotnet test PossiblyMaybe.Tests -- --treenode-filter "/*/PossiblyMaybe.Tests/PaperSettleClvTests/*"
dotnet test PossiblyMaybe.Tests -- --treenode-filter "/*/PossiblyMaybe.Tests/SameBookCloseTests/*"
```

Covers: same-book preference over Consensus, RH fee on close American, Crypto cross-book flag, `ClvPts` null when cross-book, ApplySettle meta fields.
