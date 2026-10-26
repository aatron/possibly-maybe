# Confidence-weighted staking vs flat (PASS holdouts)

## Term

- **Level staking** = flat size every bet (our $10 / 1u).
- **Variable staking** = bigger when more confident, smaller when less.
- The growth-optimal variable rule (if `p` is calibrated) is the **Kelly criterion** (we use **fractional Kelly**).
- Informal synonyms: confidence-weighted staking, edge-proportional betting.

## Shared $1000 (409 PASS tickets, chronological)

| Scheme | End | PnL | Stake range |
|---|---:|---:|---|
| Flat $10 (level) | $1,557 | +$557 | $10–$10 |
| ¼ Kelly ≤2% | **$2,842** | **+$1,842** | $15–$60 (cap binds) |
| ¼ Kelly ≤5% | $8,445 | +$7,445 | varies more; higher DD risk |
| EV-proportional ≤2% | $2,842 | +$1,842 | same as ¼K≤2% (EV always hits cap) |
| Conf \|p−0.5\| → 0.5–2% | $1,876 | +$876 | $7–$31 |
| \|edge\| → 0.5–2% by sport | $1,642 | +$642 | $5–$31 |

## Per league ending bankroll ($1000 seed)

| League | n | Flat $10 | ¼ Kelly ≤2% | ¼ Kelly ≤5% | EV∝ ≤2% | Conf \|p−0.5\| | \|edge\|∝ |
|---|---:|---:|---:|---:|---:|---:|---:|
| NBA | 31 | 1110 | **1239** | 1676 | 1239 | 1222 | 1087 |
| MLS | 103 | 1223 | **1534** | 2686 | 1534 | 1228 | 1218 |
| Ligue 1 | 52 | 1092 | **1191** | 1305 | 1191 | 1071 | 1054 |
| Liga MX | 52 | 1036 | **1064** | 1070 | 1064 | 1028 | 1023 |
| Serie A | 171 | 1097 | **1179** | 1344 | 1179 | 1135 | 1150 |

## Verdict

True “more on high conf / less on low” (|p−0.5| or |edge|) **does not beat** capped ¼ Kelly on this book. Highest-confidence quintile is not reliably the best hit rate. Stick with **¼ Kelly ≤2%** for live paper.
