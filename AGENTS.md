# Agent instructions (Cursor + Claude)

Project conventions for coding agents working in **PossiblyMaybe**.

## In-flight work

**Round 3** (blindside guards, side-by-side variants + shadow ledger, CLV fix, tracker plumbing, NCAAB) — start at `research_notes/round3/PLAN.md` + `PROGRESS.md`. Background: **Book round 2** — `research_notes/book_round2/HANDOFF.md`.

## Slash commands

### `/pm-calculate`

Daily paper ritual: what is at risk, ESPN score probe / settle hints, NBA + HOLD-PASS soccer (MLS, Ligue 1, Liga MX, Serie A) soft-spot slates, dashboard refresh.

| | |
|---|---|
| **Skill** | `.cursor/skills/pm-calculate/` · `.agents/skills/pm-calculate/` (symlink) · `.claude/skills/pm-calculate/` · slash command `.cursor/commands/pm-calculate.md` |
| **Script** | `bash scripts/pm-calculate.sh` · optional `yyyymmdd` arg |
| **When** | User says `/pm-calculate`, or asks for today’s bets / slate / at-risk / daily check |

**Data refresh:** before the slates the script runs `refresh-results` (incremental, idempotent, finals only): NBA games + box scores (ESPN scoreboard/summary), MLS games + ASA xG, Ligue 1 / Liga MX / Serie A scores (ESPN scoreboard), then prints `latest result in DB` per league. A slate prints `STALE DATA` and emits no GOs for a league whose newest result is >10 days old while it has games that day. Unknown (promoted) clubs get their own name and the default rating, never another club's alias.

**Agent must:** run the script from repo root, summarize GOs / settles / at-risk / **Kalshi vs DraftKings** / OUT lanes, and **not** place or settle unless the user confirms.

Kalshi odds come from the public Trade API (`scripts/kalshi_compare.py`, series `KXNBATOTAL` / `KXMLSTOTAL`). **Robinhood** sports event contracts use the same Kalshi asks (separate bankroll). **Crypto.com** Predict totals come from `data-api.crypto.com` (`CryptoComQuotes`). Compare buy prices across venues; prefer the better EV venue for the pick.

## Cardinal rule

**Never bet on an Oregon-based team** — Oregon Ducks, Oregon State Beavers, Portland Timbers, Portland Trail Blazers (any Oregon team, either side, every sport/venue). Enforced in code; never lift or "test" it.

## Trial rules (stake lanes)

- Four venues × six lanes × $1000 through **2026-12-31**: **DraftKings**, **Kalshi**, **Robinhood**, **Crypto.com** (independent books)
- Schemes: Flat · Kelly2 · Kelly5 · EvProp · ConfBand · EdgeBand
- `paper-go` evaluates **each venue separately** from that venue’s odds — DK / Kalshi / RH / Crypto can each GO or skip
- Within a venue, live schemes take the same pick with independent sizing
- **No reloads.** Available ≈ $0 on a lane → that lane is **OUT** on that venue only
- UI: `ui/index.html` tabs DraftKings | Kalshi | Robinhood | Crypto.com
