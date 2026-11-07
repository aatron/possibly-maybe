# T5 proof: seasonality scan + wave-radar prior

**Branch:** `r5/t5` · **Worktree:** `../pm-r5-t5`  
**Owns:** one-shot `seasonality-scan` fill of A5 BH table; `WaveRadar` CUSUM season-phase prior; `data/seasonality_bh.json`; synthetic tests.

## Protocol

1. Phases remain **pre-registered** in `research_notes/round4/a5_proof.md` — **no new phases** after seeing results.
2. Run `seasonality-scan` **once** over backfilled `variants.db` (BH q=0.10).
3. Wire BH-significant (sport × phase) keys into `variants-radar` / `WaveRadar.Scan` as a **prior only**: lowers CUSUM `h` from `10` → `7` when any observation in the cell carries that phase. Ladder / BH / persistence unchanged — **cannot promote alone**.
4. OregonExclusion untouched.

## Scan result (2026-11-07)

```text
seasonality-scan: N_trials=0  BH q=0.10  signal=V3  MinN=20
Sport    Phase                    N  MeanClv        P     P_BH   BH
seasonality-scan: no settled V3 ClvPts in variants.db — N_trials=0; calendar still written for radar prior wiring.
seasonality-scan: wrote …/data/seasonality_bh.json  BH_sig=0
```

| Fact | Value |
|---|---|
| Shadow rows | 3264 |
| Settled V3 with `ClvPts` | **0** |
| Candidates | 96 (SeasonPhase all null before scan; nothing tagged — no settled CLV to tag) |
| `N_trials` | **0** |
| BH-significant phases | **none** |

A5 results table filled in `a5_proof.md` (empty body). Re-run only after settled same-book CLV exists; do **not** invent phases.

## Radar prior wiring

| Piece | Behavior |
|---|---|
| `ClvObservation.SeasonPhase` | Optional tip-phase id (from candidate write-back or GameKey resolve) |
| `WaveRadar.CusumHSeasonPrior` | `7.0` (default `CusumH=10`) |
| `seasonalityBhKeys` | `HashSet` of `SPORT\|phase` from `data/seasonality_bh.json` |
| `SeasonalityPriorApplied` | Flag on `WaveCellResult` when relaxed `h` used |
| Promote | Still requires WAVE ON + cell BH-sig + ≥3 weeks; prior never grants Paper |

`VariantRadar` loads significant keys beside `variants.db` on every `variants-radar` run.

## Synthetic proof (`WaveRadarTests`)

| Claim | Result |
|---|---|
| Mild +1.2 CLV × 40 bets: S=8 → Off at h=10; On at h=7 when phase is BH-sig | PASS |
| Same prior + mean-0 CLV → Wave Off, Shadow, not promote-eligible | PASS (prior alone cannot promote) |

```bash
dotnet test PossiblyMaybe.Tests
# 301 succeeded (incl. WaveRadar seasonality prior + Seasonality calendar write)
```

## Limits

- Live calendar has **zero** BH-sig phases until shadow settles with CLV; prior path is wired and covered by synthetic tests.
- Relaxed `h=7` is a fixed prior (not re-tuned on holdouts).
