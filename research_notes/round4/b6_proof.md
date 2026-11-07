# B6 proof: market structure (live shadow)

**Branch:** `r4/b6` · **Worktree:** `../pm-r4-b6`  
**Owns:** `PossiblyMaybe.Domain/Variants/MarketStructureFamily.cs` (+ Evaluate hooks in `VariantCore.cs`)

## Variants

| Id | Label | Rule |
|---|---|---|
| `consensus-outlier` | kaunitz-venue-soft | After V3-style edge-cap + line-move + real DK juice: require ≥2 venue decimal odds for the model side; pick only when Kaunitz soft-outlier holds (`max(Ω) > 1/(p_cons − α)`, `α=0.05`, `p_cons=1/mean(Ω)`). |
| `line-move-follow` | follow-open-to-current | Edge-cap on; **require** open→current moved ≥ sport k **toward** our side (NBA k=2.0, soccer k=0.5). Flat / against → `NO_FOLLOW`. |

Venue decimals for Kaunitz (when present on `SlateCandidate`):

- DK: `AmericanToDecimal(DkOver/UnderAmerican)`
- Kalshi / Crypto.com: `1 / ask` from tracker or live quote fields (`Kalshi*Ask`, `Crypto*Ask`)

Missing &lt;2 venues → `NO_VENUE_ASKS` (live-only; historical Consensus TOTAL rows cannot reconstruct this).

## Proof type

**Live shadow CLV** (PLAN B6). No Tier A: warehouse `OddsSnapshots` TOTAL is Consensus-only (book_round2 `eval_forecast_markets.md`).

## Unit checks

```bash
dotnet test --filter "FullyQualifiedName~Kaunitz_IsSoftOutlier|FullyQualifiedName~Filter_ConsensusOutlier|FullyQualifiedName~Filter_LineMoveFollow|FullyQualifiedName~Registry_LaunchSet"
```

- Soft fixture (DK −110 + Kalshi over ask 0.45) → `consensus-outlier` PICK OVER.
- Tight Kalshi ask 0.52 → `NO_OUTLIER`; DK alone → `NO_VENUE_ASKS`.
- `line-move-follow`: open=220 / line=222 / model=236 OVER PICK; flat or against → `NO_FOLLOW`.

## Live path

Shadow ledger already evaluates every registered variant (`VariantEvaluate.RecordAsync`). When DailySlate / tracker populate ask fields, these ids write PICK/skip rows automatically; settle path unchanged → CLV when close exists.

shortcut: CandidateEntries does not yet persist Kalshi/Crypto asks — reeval from store alone will `NO_VENUE_ASKS` until the store gains those columns (or slate emission fills them each day).

## Not in B6

Wiring DailySlate HTTP quotes into candidate asks; joining `line_snapshots.db` at evaluate time; RH ask (same as Kalshi).
