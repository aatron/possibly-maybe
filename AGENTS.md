# Agent instructions (Cursor + Claude)

Project conventions for coding agents working in **PossiblyMaybe**.

## Slash commands

### `/pm-calculate`

Daily paper ritual: what is at risk, ESPN score probe / settle hints, NBA+MLS soft-spot slates, dashboard refresh.

| | |
|---|---|
| **Skill** | `.cursor/skills/pm-calculate/SKILL.md` (Cursor) and `.claude/skills/pm-calculate/SKILL.md` (Claude) — same steps |
| **Script** | `bash scripts/pm-calculate.sh` · optional `yyyymmdd` arg |
| **When** | User says `/pm-calculate`, or asks for today’s bets / slate / at-risk / daily check |

**Agent must:** run the script from repo root, summarize GOs / settles / at-risk / **Kalshi vs DraftKings** / OUT lanes, and **not** place or settle unless the user confirms.

Kalshi odds come from the public Trade API (`scripts/kalshi_compare.py`, series `KXNBATOTAL` / `KXMLSTOTAL`). Compare buy prices to DK American; prefer the better EV venue when the user may bet Kalshi.

## Trial rules (stake lanes)

- Two venues × six lanes × $1000 through **2026-12-31**: **DraftKings** and **Kalshi** tabs (independent books)
- Schemes: Flat · Kelly2 · Kelly5 · EvProp · ConfBand · EdgeBand
- `paper-go` evaluates **DK and Kalshi separately** from each venue’s odds — a pick can be DK-only, Kalshi-only, both, or neither
- Within a venue, live schemes take the same pick with independent sizing
- **No reloads.** Available ≈ $0 on a lane → that lane is **OUT** on that venue only
- UI: `ui/index.html` tabs DraftKings | Kalshi
