# W7 per-league auto-shutoff proof (2026-11-07)

**Rule:** pause a sport when mean CLV of the last **40** settled signal bets is &lt; 0; resume when ≥ 0.
**Signal:** V3 shadow stream (edge-cap + line-move, no pause). Using V4's own bets would freeze the window while paused and never resume.
**Live:** `VariantLedger.PausedLeaguesAsync` reads `data/variants.db` ClvPts for variant `V3`.
**CLI:** `dotnet run -c Release --project PossiblyMaybe.Cli -- tier-a-shutoff [db]`

Warehouse: `possiblymaybe.db` (main). Flat 1u PnL / max drawdown on chronological V3 GO stream.

| sport | n (V3) | pnlV3 | ddV3 | pnlV4 | ddV4 | takenV4 | dd≤V3 | pnl≥V3 |
|---|---:|---:|---:|---:|---:|---:|:---:|:---:|
| NBA | 565 | 29.9 | 22.8 | 32.3 | 20.5 | 554 | yes | yes |
| MLS | 623 | 33.6 | 12.3 | 33.6 | 12.3 | 623 | yes | yes |
| LIGUE1 | 276 | 27.5 | 8.5 | 32.9 | 8.2 | 263 | yes | yes |
| LIGAMX | 366 | 31.6 | 10.5 | 31.6 | 10.5 | 366 | yes | yes |
| SERIEA | 144 | 8.8 | 13.3 | 8.8 | 13.3 | 144 | yes | yes |

**PASS:** every league max DD ≤ V3 and PnL ≥ V3. Material cuts on NBA (−2.3u DD, +2.4u PnL) and Ligue 1 (−0.3u DD, +5.4u PnL). MLS / Liga MX / Serie A never entered a 40-bet negative-CLV window on this warehouse (open≈close CLV often near 0 until denser mid-snap closes).

Unit: `ShutoffTests` (window / resume-at-zero + synthetic DD cut); `VariantsTests.Filter_LeaguePaused_OnV4`.
