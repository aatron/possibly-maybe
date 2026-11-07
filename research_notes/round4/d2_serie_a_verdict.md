# D2 — Serie A verdict (r3 W9)

**Date:** 2026-11-07 · **Branch:** `main` · **DB:** `possiblymaybe.db`  
**Question:** Keep, cut, or drop Serie A from the V0 successor (paper HOLD-PASS today; V-SA0 = V3 with Serie A off).  
**OregonExclusion:** unchanged (no Serie A Oregon clubs; rule not lifted).

## Verdict: **cut**

| Option | Meaning | Chosen? |
|---|---|---|
| **keep** | Unconstrained paper champ / V0 successor includes Serie A | no |
| **cut** | Demote paper role: V-SA0 for paper champion portfolio; Serie A stays on for **shadow** + radar; re-promote only on WAVE ON / same-book CLV | **yes** |
| **drop** | Delete league from tracking / models | no |

**Why cut (not keep):** live paper 0–2 Flat (−$20) plus open tickets with ModelProb up to **0.91** (same absurd-EV class as the 11-07 Frosinone@Cagliari W9 trigger); replay week 2–5 / −3.18u; holdout CI historically crosses 0; soccer edge-cap does **not** pass majority-neg on Serie A (W2).  
**Why cut (not drop):** Tier A **PASS** on refreshed DB (re-run today); Tier C **KEEP** (look-once, do not re-score); burned holdout 2024–25 at frozen champ **+9.7u / 171, CLV +0.12**; shutoff V3 stream **+10.9u / 98** with no 40-bet negative-CLV pause. Soft spot may be real under denser same-book closes — keep shadow, do not erase.

**Paper action (recommendation only — no `paper-go` / settle in this item):** treat V-SA0 as the paper multi-league default; leave Serie A on shadow ledger + wave radar. Line-move k=0.5 stays (W2 PASS). Do not raise stake gates.

## Fold table A — Tier A poisson-cdf vs logistic (re-run)

Command (2026-11-07):

```bash
dotnet run -c Release --project PossiblyMaybe.Cli -- tier-a-poisson SERIEA possiblymaybe.db
```

Champ frozen: DC α=0.18, w=0.40, edge≥0.35, EV≥4%. Holdout burned = `2024-2025`. L = logistic, P = Poisson (live KEEP for SERIEA). AssumedJuice −110. N_trials=1.

| fold | nL | clvL | pnlL | kellyL | nP | clvP | pnlP | kellyP | dPnL | dCLV |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 2018-2019 | 151 | 0.070 | −7.6 | −164.4 | 151 | 0.070 | −7.6 | −164.4 | 0.0 | 0.000 |
| 2019-2020 | 149 | −0.062 | −33.2 | −498.5 | 148 | −0.064 | −32.2 | −488.3 | +1.0 | −0.002 |
| 2020-2021 | 184 | 0.076 | −1.3 | −55.7 | 182 | 0.078 | +0.7 | −16.8 | +2.0 | +0.002 |
| 2021-2022 | 184 | 0.215 | +14.1 | +284.8 | 182 | 0.220 | +14.2 | +287.6 | +0.1 | +0.005 |
| 2022-2023 | 194 | 0.249 | +6.8 | +108.1 | 193 | 0.247 | +5.9 | +88.3 | −0.9 | −0.001 |
| 2023-2024 | 160 | 0.167 | +7.1 | +119.6 | 159 | 0.170 | +8.1 | +142.5 | +1.0 | +0.003 |

**SUMMARY:** winFolds **4/6**, nP=1015, meanClv L=0.127 / P=**0.128**, flat PnL L=−14.1 / P=**−10.9**, Kelly L=−206.1 / P=**−151.1**.  
**VERDICT Tier A = PASS** (majority PnLΔ, clv≥champ, n≥40). Matches `book_round2/exp_poisson-cdf-pover.md` bit-for-bit on this warehouse.

## Fold table B — frozen champ absolute (include Serie A ≈ V3 side)

Same tickets as Poisson column above (= live OverProb). V-SA0 on Serie A alone is always n=0 / PnL 0 — portfolio win for V-SA0 only when Serie A tickets lose.

| season / slice | n | CLV | PnL u | source |
|---|---:|---:|---:|---|
| Tier A folds pooled (P) | 1015 | +0.128 | −10.9 | re-run `tier-a-poisson` |
| Holdout 2024–2025 (frozen α/w/edge) | 171 | +0.12 | **+9.7** | `sweep-soccer-dc SERIEA 2024-2025` cell α=0.18 w=0.40 edge=0.35 |
| Tier C 2025–2026 (look-once) | 162 | +0.136 | **+20.5** | **carried** `book_round2/tier_c_poisson-cdf-pover.md` — **do not re-score** |
| V3 chronological warehouse | 98 | (open≈close thin) | **+10.9** | re-run `tier-a-shutoff` (SERIEA row); V4 same (no pause) |
| Replay 2026-10-30→11-06 | 7 | — | **−3.18** | carried `reports/replay_2026-10-30_to_11-06.md` |
| Live paper Flat DK settled | 2 | null closes | **−2.0u (−$20)** | `paper.db` 2026-10-29; 3 open ticket-groups 2026-11-07 unsettled |

**V3 vs V-SA0:** warehouse backfill favors **including** Serie A (+10.9u). Live + replay favor **V-SA0**. Cut = paper follows V-SA0 until radar CLV says otherwise; shadow keeps the V3 Serie A stream for measurement.

## B4 soccer distributions (+poisson-all / +negbin / +dc-rho / mls-dc)

| Variant | Serie A role | Evidence here |
|---|---|---|
| `+poisson-all` | Already live for SERIEA (and Ligue 1) | **Re-run** = fold table A (PASS) |
| `mls-dc` | MLS-only | N/A for this verdict |
| `+negbin` (φ=0.12) | Optional curve on same λ | **No dedicated Tier A CLI** — fold table **deferred** (`b4_proof.md`). **Carried evidence / re-run deferred.** |
| `+dc-rho` (ρ=−0.10) | Optional DC τ on same λ | Same — at common O/U 2.5, P(T>2.5) often unchanged vs Poisson (`b4_proof.md`). **Carried evidence / re-run deferred.** |

Do not promote `+negbin` / `+dc-rho` on Serie A without a later same-ticket fold table (log-loss + Kelly + CLV). Shadow-only until then.

## A1 / guards / shutoff notes

- **A1:** V-SA0 remains the league-off filter (`LeagueFilterFamily`); candidate store covers SERIEA seasons in warehouse. `data/variants.db` was **missing** on this machine — no fresh multi-variant backfill PnL table; V3/V-SA0 contrast uses shutoff + paper + Tier A instead.
- **W2:** Serie A line-move k=0.5 PASS (6/6 removed PnL −10.7); edge-cap majority **FAIL** on Serie A — live absurd EV still a known hole.
- **W7:** SERIEA never entered a 40-bet negative-CLV shutoff window on this warehouse (re-confirmed).

## What was re-run vs carried

| Artifact | Status |
|---|---|
| `tier-a-poisson SERIEA` | **Re-run** 2026-11-07 → PASS |
| `tier-a-shutoff` (SERIEA row) | **Re-run** → +10.9u / 98 |
| Frozen champ holdout cell 2024–25 | **Re-run** via sweep cell → +9.7u / 171 |
| Tier C 2025–26 | **Carried** (look-once) |
| B4 +negbin / +dc-rho folds | **Carried evidence / re-run deferred** |
| Replay week / audit CI | **Carried** |
| Live `paper.db` | Read-only snapshot (no place/settle) |

## OregonExclusion

Not modified. No Oregon-based Serie A clubs in scope.
