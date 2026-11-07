# Round 3: progress

## ▶ RESUME HERE
**Next action:** wait for parallel Composer agents W1 / W2 / W3 / W14 to finish; parent reviews and merges `--no-ff`. Then W5 / W6 / W7 / W15. W4 deferred. W8 + W13 done.
Never redo a `done` row. Specs = `PLAN.md` § Work items.

## Ledger
| Id | Item | Agent / model | Worktree / branch | Value proof | Status | Merge |
|---|---|---|---|---|---|---|
| W0 | Daily refresh all leagues + aliases + stale guard | Sonnet | `../pm-fix-daily-refresh` `fix/daily-refresh` | latest-result date per league: before NBA 10-25, soccer 2026-05 → after NBA 11-07, MLS 11-02, L1 11-06, LMX 11-07, SA 11-06 (live DB) | done | 4dda009 |
| D0 | User decision: PaperLedger.cs / ui WIP | user | — | — | done: parent committed WIP as-is | 3ac0431 |
| W1 | Variant registry + shadow ledger + variants.html + backfill | Composer | `../pm-r3-variants` `r3/variants` | V0 backfill = champ | **running** | |
| W2 | Edge-cap + line-move guards + historical proof | Composer | `../pm-r3-guards` `r3/guards` | removed bets lose | **running** | |
| W3 | Tracker importer + doubleheader key | Composer | `../pm-r3-tracker-import` `r3/tracker-import` | % games with O+C | **running** | |
| W4 | Always-on tracker (GH Actions) | Composer | `../pm-r3-always-on` | sleep-night coverage | **deferred (user: not yet)** | |
| W5 | Live CLV fix | Composer | `../pm-r3-live-clv` | % true close | pending (after W1) | |
| W6 | Kalshi soccer series + RH fee | Composer | `../pm-r3-venues` | % soccer GOs priced | pending (after W1) | |
| W7 | Per-league auto-shutoff (V4) | Composer | `../pm-r3-shutoff` | drawdown vs V3 | pending (W1) | |
| W8 | Stake promotion gate | Composer | `../pm-r3-stake-gate` `r3/stake-gate` | rule visible (STAKE GATE in `paper`) | done | b3e6ca2 |
| W9 | Serie A verdict | Opus + Composer | `../pm-r3-seriea` | fold table | pending (W0, W1) | |
| W10 | NCAAB totals pack | Opus + Composer | `../pm-r3-ncaab` | Tier A/C | pending | |
| W13 | Odds-history sources (OddsShark / getstam? / Covers / free APIs) | Opus | main (notes only) | O→C seasons gained | done — getstam/Covers/OddsShark prohibited; SBRO NCAA O→C downloaded (NCAAB 15 szn, NCAAF 16 szn) | notes |
| W14 | Oregon NCAA name gap (OregonState/PortlandState/PortlandU, Pilots) | Composer | `../pm-r3-oregon-ncaa` `r3/oregon-ncaa` | 0 Oregon games in NCAA data | **running** — **must precede W10/W15 NCAA** | |
| W15 | Import on-disk odds (round-2 NFL/MLB/NBA files, SBRO NCAA) | Composer | `../pm-r3-import-odds` | O→C seasons before→after | pending | |
| W11 | Write-ups (book review, training review, round3 value) | Opus | main | — | pending | |
| W12 | Green tests, clean run, push | parent | main | — | pending | |

## Log
| When | Event |
|---|---|
| 2026-11-07 | Line-tracker cron installed (`*/20`), first run 930 rows. Plan written. |
| 2026-11-07 | User: research OddsShark / getstam / Covers + free odds APIs → W13 launched (Opus). |
| 2026-11-07 | W0 built (eb33257). Conflict with main in DailySlate (Oregon + NBA σ) — same agent merging main into branch. |
| 2026-11-07 | W0 merged (4dda009, 98 tests). Live DB refreshed (backup possiblymaybe.db.bak-pre-refresh-20261107). Slate 11-07/08: 21 GOs, 2 Oregon exclusions; Serie A still EV 74% (Frosinone@Cagliari) → W2 edge cap / W9. |
| 2026-11-07 | User answers: Q1 wait for W13; Q2 parent committed WIP (3ac0431) + push; Q3 no Sonnet (Composer only); Q4 paper-go all 18 GOs (DK 18 games, Kalshi 5, RH 5, Crypto 2); Q5 W4 deferred. |
| 2026-11-07 | User: implement round 3 plan. Parent launched W1/W2/W3/W8 Composer agents in worktrees from `92348f2`. |
| 2026-11-07 | W13 done → odds_history_sources.md. Added W14 (Oregon NCAA gap) + W15 (import on-disk odds). |
| 2026-11-07 | W14 Composer agent launched (`r3/oregon-ncaa` @ e4c2dbc). |
| 2026-11-07 | W8 merged (b3e6ca2, 99 tests). Kelly5 capped 2% until global live CLV n≥100 and mean>0; status via `paper`. |
