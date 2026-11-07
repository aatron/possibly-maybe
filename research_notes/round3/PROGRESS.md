# Round 3: progress

## ▶ RESUME HERE
**Next action:** wait for W13 (odds-history research, Opus, notes only) to finish and report. Then wave 1 (W1–W3) waits on the user's go. Build agents are **Composer only** (launched from Cursor), never Sonnet.
Never redo a `done` row. Specs = `PLAN.md` § Work items.

## Ledger
| Id | Item | Agent / model | Worktree / branch | Value proof | Status | Merge |
|---|---|---|---|---|---|---|
| W0 | Daily refresh all leagues + aliases + stale guard | Sonnet | `../pm-fix-daily-refresh` `fix/daily-refresh` | latest-result date per league: before NBA 10-25, soccer 2026-05 → after NBA 11-07, MLS 11-02, L1 11-06, LMX 11-07, SA 11-06 (live DB) | done | 4dda009 |
| D0 | User decision: PaperLedger.cs / ui WIP | user | — | — | done: parent committed WIP as-is | 3ac0431 |
| W1 | Variant registry + shadow ledger + variants.html + backfill | Composer | `../pm-r3-variants` | V0 backfill = champ | pending | |
| W2 | Edge-cap + line-move guards + historical proof | Composer | `../pm-r3-guards` | removed bets lose | pending | |
| W3 | Tracker importer + doubleheader key | Composer | `../pm-r3-tracker-import` | % games with O+C | pending | |
| W4 | Always-on tracker (GH Actions) | Composer | `../pm-r3-always-on` | sleep-night coverage | **deferred (user: not yet)** | |
| W5 | Live CLV fix | Composer | `../pm-r3-live-clv` | % true close | blocked (D0) | |
| W6 | Kalshi soccer series + RH fee | Composer | `../pm-r3-venues` | % soccer GOs priced | blocked (D0) | |
| W7 | Per-league auto-shutoff (V4) | Composer | `../pm-r3-shutoff` | drawdown vs V3 | pending (W1) | |
| W8 | Stake promotion gate | Composer | `../pm-r3-stake-gate` | rule visible | pending | |
| W9 | Serie A verdict | Opus + Composer | `../pm-r3-seriea` | fold table | pending (W0, W1) | |
| W10 | NCAAB totals pack | Opus + Composer | `../pm-r3-ncaab` | Tier A/C | pending | |
| W13 | Odds-history sources (OddsShark / getstam? / Covers / free APIs) | Opus | main (notes only) | O→C seasons gained | running | |
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
