# A3 proof: wave radar

**Branch:** `r4/a3` · **Date:** 2026-11-07  
**Owns:** `PossiblyMaybe.Domain/WaveRadar.cs`, `Cli/Variants/Radar*`, Waves section in `ui/variants.html`, `pm-calculate.sh` one-liner.

## What shipped

| Piece | Behavior |
|---|---|
| Cells | league × market × variant (`market=TOTAL` for now) from settled shadow picks with `ClvPts` |
| Schema | Reads current r3 `ShadowEntry` via `RadarShadowSource` — no A1 candidate-store dependency |
| Sequential test | One-sided CUSUM on CLV residuals (`k=1`, `h=10`); WAVE OFF when rolling mean (40) &lt; 0 (W7 generalised) |
| FDR | Benjamini–Hochberg at **q=0.10**; every row prints **`N_trials`** |
| Ladder | shadow→paper: WAVE ON + BH-sig + ≥3 weeks on; paper→real: user flag only; demotion automatic on WAVE OFF / neg rolling CLV |
| Persist | `data/radar.db` trial registry beside `variants.db` |
| CLI | `variants-radar [variants.db] [radar.db]` |
| UI / ritual | Waves table in `variants.html`; `pm-calculate.sh` runs radar after shadow |

## Synthetic proof (`WaveRadarTests`)

| Claim | Result |
|---|---|
| Injected **+2pt** CLV wave detected within **60** bets | PASS — `Wave=On`, CUSUM first alarm index &lt; 60, BH significant |
| Null cells (100 cells × 80 bets, CLV ~ N(0,3²)) false-alarm ≤ **10%** | PASS — BH_sig rate ≤ 10%; WAVE ON rate ≤ 10% |
| Strong cell among nulls still BH + WAVE ON | PASS |
| Promote after persistence; demote on neg rolling | PASS |
| Paper→real never auto | PASS |

```bash
dotnet test --disable-logo
# 166 succeeded (incl. WaveRadarTests)
```

## How to run

```bash
dotnet run -c Release --project PossiblyMaybe.Cli -- variants-radar
# optional paths: variants-radar data/variants.db data/radar.db
bash scripts/pm-calculate.sh   # includes waves summary after shadow
```

## Limits / risks

- CUSUM `h=10` tuned on synthetic σ≈3; live CLV scale may need a recalibration pass.
- p-values use normal approx (n≥20); small-n cells stay non-significant by design.
- Seasonality prior (A5) not wired — radar is CLV-only for now.
- Empty shadow ledger → graceful empty waves line (N_trials=0).
