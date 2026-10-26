# PossiblyMaybe — progress (read this first after any interruption)

## Snapshot

| Field | Value |
|---|---|
| **Last updated** | 2026-10-25 (TOTALS = primary money path) |
| **Current gate** | **2** live paper |
| **Champ** | **NBA TOTALS (O/U)** · 40% open + 60% scoring EMA · `|exp−line|≥3.5` · EV≥4% |
| **Kill status** | **HOLD PASS** |
| **Holdout 2024–25** | **320 bets**, CLV **+1.91 pts**, PnL **+8.4u** flat 1u |
| **Next action** | Paper every GO with `advise-ou` → `paper-bet` → `settle`; Gate 2b ~1 month |
| **Bankroll seed** | $1000 paper |

## Why totals (not “obvious moneylines”)

Favorites that “should win” are priced at −300/−500; being right most nights still loses after vig. Soft spot = **totals**, where scoring pace disagrees with the posted number by ≥3 points.

## Champ rule

1. Maintain team points-for / points-against EMA (`ScoringBook`).
2. `modelExp = ExpectedTotal(home, away)`; `exp = 0.4*openTotal + 0.6*modelExp`.
3. Bet **OVER** if `exp−open ≥ 3.5` and EV≥4%; **UNDER** if `open−exp ≥ 3.5` and EV≥4%.
4. Stake: ¼ Kelly, ≤2% bankroll. Log close total for CLV.

```bash
dotnet run --project PossiblyMaybe.Cli -- sweep-totals
dotnet run --project PossiblyMaybe.Cli -- advise-ou <modelExp> <openTotal>
dotnet run --project PossiblyMaybe.Cli -- paper-bet "DATE AWAY@HOME" OVER|UNDER <total> -110 <stake$>
```

## Secondary (low volume)

Selective ATS (`advise-ats`) still exists (~19 holdout bets). Not the money engine.

## Props / rebounds

No historical open→close prop lines in raw data yet. Next acquisition after Gate 2b totals paper is stable.

## Gate checklist

| Gate | Status |
|---|---|
| 0 | passed |
| 1 ML | failed |
| 1 ATS selective | thin pass (niche) |
| 1 TOTALS volume | **PASS** (primary) |
| 2 Live paper | **open** |
| 2b Trust month | pending |
| 3 Real tiny bets | locked until 2b |
