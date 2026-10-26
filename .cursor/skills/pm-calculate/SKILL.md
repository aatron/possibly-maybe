---
name: pm-calculate
description: >-
  Run the daily PossiblyMaybe paper ritual (/pm-calculate): open at-risk
  tickets, ESPN score probe + settle hints, NBA/MLS soft-spot slates, and
  dashboard refresh. Use when the user says /pm-calculate, asks for today's
  bets, slate, what is at risk, or the daily paper check.
disable-model-invocation: true
---

# /pm-calculate

Daily Gate 2b paper check for this repo.

## Do this

1. From the repo root, run:

```bash
bash scripts/pm-calculate.sh
```

Optional date override (`yyyymmdd`):

```bash
bash scripts/pm-calculate.sh 20261028
```

2. Summarize for the user in chat:
   - **At risk:** open tickets by game + total reserved per lane
   - **Settles:** any `SETTLE …` / `settle-go` hints from finals
   - **GOs:** every printed `paper-go` line (same pick across all six lanes)
   - **Kalshi vs DK:** for open tickets and GOs — which venue is cheaper to buy (EV / ask gap). Kalshi is binary YES=over via public `trade-api/v2` (`scripts/kalshi_compare.py`)
   - **NO-GO / sit out:** note discipline wins
   - **OUT lanes:** available ≈ $0 → scheme is dead (no reload)

3. Do **not** place or settle bets unless the user asks. Print the exact commands.

4. After user confirms places/settles, re-run `dashboard` (or the full script) so `ui/index.html` updates.

## Rules baked into the trial

- Two venues (DraftKings | Kalshi tabs) × six $1000 lanes each
- `paper-go` places independently per venue from that venue’s odds (DK-only / Kalshi-only / both / neither)
- Within a venue, live schemes share the pick; sizing differs
- No extra capital; ruined lane stays out on that venue through 2026-12-31
