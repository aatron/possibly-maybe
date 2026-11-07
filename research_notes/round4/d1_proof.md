# D1 proof: true market close on every league's settle path

**Rule (r3 W5 + R3):** settle stores **FinalTotal** from scores and **CloseLine** only from a market close (tracker / warehouse labelled Close). Same-book close → `ClvPts`; cross-book → `CrossBookClose`, `ClvPts` null. **No promotion without same-book true close.**

## Paths audited

| Path | Role | Sport-agnostic? |
|---|---|---|
| `PaperLedger.ResolveSettleLinesAsync` | Paper settle / `backfill-clv` | Yes — warehouse OddsSnapshots + `line_snapshots` |
| `VariantSettle.SettleOpenAsync` | Shadow auto-settle | Yes — warehouse DK Close, else tracker ESPN, else cross-book |
| `ResultsRefresh.Leagues` | Finals for settle matching | NBA + soccer HOLD-PASS + LALIGA/EFL/EPL/BUNDESLIGA + NHL |
| `scripts/line_tracker.py` `SPORTS` | Live O→C captures | All paper/shadow leagues below |
| `LineTrackerImporter` | Snapshots → warehouse; settle fallback | ResolveTeam per league + full-name↔abbr match (D1) |
| `RadarShadowSource` | Wave radar CLV feed | **Requires** `CloseLine` + `!CrossBookClose` + `ClvPts` (D1 gate) |

## League matrix — can settle store a true close?

| League | Tracker `SPORTS` | ResultsRefresh | ResolveTeam | True-close on settle | Notes |
|---|---|---|---|---|---|
| NBA | espn+kalshi+cdc | yes | TeamCodes | **yes** | codes match paper keys |
| NFL | espn+kalshi+cdc | (import) | TryNfl | **yes** | |
| MLB | espn+kalshi+cdc | (import) | TryMlb | **yes** | off-season often 0 snaps |
| NCAAB | espn+kalshi | (import) | NcaaCode | **yes** | |
| NCAAF | espn+kalshi | (import) | NcaaCode | **yes** | |
| MLS | espn+kalshi+cdc | yes | TryMls | **yes** | paper codes = tracker |
| LIGUE1 | espn+kalshi | yes | TryLigue1 | **yes** | full-name keys ↔ ESPN abbr (D1) |
| LIGAMX | espn+kalshi | yes | TryLigaMx | **yes** | |
| SERIEA | espn+kalshi | yes | TrySerieA | **yes** | |
| LALIGA | espn only | yes | **TryLaLiga (fixed D1)** | **yes** | no public Kalshi/CDC series |
| EFL | espn only | yes | **TryEfl (fixed D1)** | **path yes; 0 snaps today** | ESPN configured; no captures in `line_snapshots` yet |
| EPL | espn+kalshi+cdc | yes | TryEpl | **yes** | |
| BUNDESLIGA | espn+kalshi | yes | TryBundesliga | **path yes; 0 warehouse games** | tracker has snaps; run `refresh-results` then import |
| NHL | espn+kalshi+cdc | yes | TryNhl | **path yes; 0 warehouse games** | same — refresh then import |

Historical warehouse TOTAL closes for soccer are almost all `Book.Consensus` → settle stores them as **cross-book** (audit only). Live true closes come from tracker ESPN→DK (and Kalshi/Crypto when series exist) via import or settle fallback.

## Gaps fixed (this item)

1. **`ResolveTeam`:** LALIGA + EFL aliases (were passthrough → import never matched warehouse full names).
2. **`TryCloseFromSnapshots`:** match paper/shadow **full-name** keys to tracker **abbr** keys via `ResolveTeam` (soccer settle was blind to tracker).
3. **`VariantSettle`:** same tracker fallback as paper; default `data/line_snapshots.db`; SQLite-safe game find (±1 day).
4. **Promotion:** `RadarShadowSource` drops cross-book / missing-close rows — radar cannot promote on Consensus-as-close.

## Tests

```bash
dotnet test PossiblyMaybe.Tests -c Release -- \
  --treenode-filter "/*/PossiblyMaybe.Tests/LineTrackerImporterTests|VariantSettleCloseTests|PaperSettleClvTests/*"
python3 -m unittest scripts.tests.test_line_tracker -v
```

## Shortcuts

- `shortcut:` EFL has 0 tracker rows and BUNDESLIGA/NHL have 0 warehouse games in this checkout — settle path is wired; coverage needs cron + `refresh-results` / `import-line-tracker`. Upgrade when those leagues have live O+C ≥ 80% (A2 target).
- `shortcut:` tracker freshness ≤15m before tip is still rare (cron cadence); D1 stores whatever last pre-start snap exists, same as R3.
