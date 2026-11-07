# R6 proof: real price at bet for every venue

**Owns:** `DailySlate.PrintChampGos`, `paper-go`, `PaperLedger`  
**Depends on:** candidate `DkOverAmerican` / `DkUnderAmerican` (A1)

## Rule

| Path | Juice |
|---|---|
| Shadow / variant V0 | Still −110 (`UseRealJuice: false`) |
| Paper champ GO / `paper-go` | `VariantRegistry.PaperChamp` = V0 + `UseRealJuice: true` |
| Real DK present | Ticket stores that american; `AssumedJuice = false` |
| Missing DK | Falls back to −110; labelled `assumed`; `AssumedJuice = true` |
| Kalshi / RH / Crypto | Already real asks when placed (unchanged) |

Assumed place prices: close may still be stored for audit; **`ClvPts` is null** and rows are **excluded from `LiveClvStatsAsync`** (CLV / PnL verdicts). Bankroll equity still includes realized PnL.

## What changed

| Before | After |
|---|---|
| Paper GO printed V0 @ −110 always | Paper GO evaluates with real DK when ESPN/odds fill americans |
| `paper-go` had no assumed label | Trailing `assumed` arg + ledger `AssumedJuice` |
| All DK CLV counted | Assumed juice → no CLV / not in stake-gate CLV |

## Proof metric

**% of DK tickets with a real (non-assumed) place price: before → after.**

```bash
dotnet run --project PossiblyMaybe.Cli -- paper status   # prints DK real price (R6)
```

### Measured (2026-11-07, local `paper.db`)

| | DK tickets | Real price | % |
|---|---|---|---|
| **Before** (legacy places, all @ −110, no `AssumedJuice` column) | 180 | 0 | **0%** |
| **After** (new places) | — | carries `Dk*American` when slate has it; else flagged `assumed` | see status line |

Unit/integration: `PaperRealPriceTests` — PaperChamp uses −102 when present; assumed settles get null `ClvPts` and drop out of live CLV.

```bash
dotnet test PossiblyMaybe.Tests -- --treenode-filter "/*/PossiblyMaybe.Tests/PaperRealPriceTests/*"
```
