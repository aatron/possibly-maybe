# Round 3: progress

## ▶ RESUME HERE
**Next action:** wait for W5 / W7 / W16. W1–W3 / W6 / W8 / W13–W15 done. W4 deferred.
Never redo a `done` row. Specs = `PLAN.md` § Work items.

## Ledger
| Id | Item | Agent / model | Worktree / branch | Value proof | Status | Merge |
|---|---|---|---|---|---|---|
| W0 | Daily refresh all leagues + aliases + stale guard | Sonnet | `../pm-fix-daily-refresh` `fix/daily-refresh` | latest-result date per league: before NBA 10-25, soccer 2026-05 → after NBA 11-07, MLS 11-02, L1 11-06, LMX 11-07, SA 11-06 (live DB) | done | 4dda009 |
| D0 | User decision: PaperLedger.cs / ui WIP | user | — | — | done: parent committed WIP as-is | 3ac0431 |
| W1 | Variant registry + shadow ledger + variants.html + backfill | Composer | `../pm-r3-variants` `r3/variants` | registry + shadow + ui; thresholds aligned to Guards | done | 48b9e5e |
| W2 | Edge-cap + line-move guards + historical proof | Composer | `../pm-r3-guards` `r3/guards` | line-move PASS; edge-cap 25% soccer PASS / NBA FAIL (see w2_guard_proof.md) | done | 1ae2f73 |
| W3 | Tracker importer + doubleheader key | Composer | `../pm-r3-tracker-import` `r3/tracker-import` | smoke 15/15 O+C; key `yyyymmdd AWAY@HOME HHMM` | done | ca7c064 |
| W4 | Always-on tracker (GH Actions) | Composer | `../pm-r3-always-on` | sleep-night coverage | **deferred (user: not yet)** | |
| W5 | Live CLV fix | Composer | `../pm-r3-live-clv` `r3/live-clv` | % true close | **running** | |
| W6 | Kalshi soccer series + RH fee | Composer | `../pm-r3-venues` `r3/venues` | Ligue1/SerieA/LigaMX series + RH fee skip test | done | 4f62fab |
| W7 | Per-league auto-shutoff (V4) | Composer | `../pm-r3-shutoff` `r3/shutoff` | drawdown vs V3 | **running** | |
| W8 | Stake promotion gate | Composer | `../pm-r3-stake-gate` `r3/stake-gate` | rule visible (STAKE GATE in `paper`) | done | b3e6ca2 |
| W9 | Serie A verdict | Opus + Composer | `../pm-r3-seriea` | fold table | pending (W0, W1) | |
| W10 | NCAAB totals pack | Opus + Composer | `../pm-r3-ncaab` | Tier A/C | pending | |
| W13 | Odds-history sources (OddsShark / getstam? / Covers / free APIs) | Opus | main (notes only) | O→C seasons gained | done — getstam/Covers/OddsShark prohibited; SBRO NCAA O→C downloaded (NCAAB 15 szn, NCAAF 16 szn) | notes |
| W14 | Oregon NCAA name gap (OregonState/PortlandState/PortlandU, Pilots) | Composer | `../pm-r3-oregon-ncaa` `r3/oregon-ncaa` | SBRO CamelCase + Pilots match; PSU not excluded | done | 3ba836a |
| W15 | Import on-disk odds (round-2 NFL/MLB/NBA files, SBRO NCAA) | Composer | `../pm-r3-import-odds` `r3/import-odds` | NFL +4, NBA +1, MLB +3, NCAAB +15, NCAAF +16 seasons (live DB) | done | a830d85 |
| W16 | archive.org odds history (search + CDX, download only) | Opus | main (notes + data/raw) | O→C seasons gained | running | |
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
| 2026-11-07 | W14 merged (3ba836a, 111 tests). CamelCase normalize + Pilots; dropped misleading NCAA `psu` code. |
| 2026-11-07 | W15 Composer agent launched (`r3/import-odds`). |
| 2026-11-07 | W3 merged (ca7c064, 128 tests on main). `import-line-tracker`; doubleheader key with HHMM. |
| 2026-11-07 | W2 merged (1ae2f73, 135 tests). Defaults: edge-cap 25%, NBA k=2, soccer k=0.5. NBA edge-cap fails removed-bet proof (visible loser / live absurd defense only). |
| 2026-11-07 | User: archive.org line history → W16 launched (Opus, download only; import = Composer). Wave items are being implemented by another session, not this one. |
| 2026-11-07 | W1 merged (48b9e5e, 141 tests). Shadow ledger + variants.html; Defaults aligned to Guards (25% / NBA k=2). W5/W6/W7 launched. |
| 2026-11-07 | W6 merged (4f62fab, 151 tests). KXLIGUE1TOTAL / KXSERIEATOTAL / KXLIGAMXTOTAL; RH fee already on place path. |
| 2026-11-07 | W15 merged (a830d85, 155 tests) + live `import-disk-odds` (backup possiblymaybe.db.bak-pre-w15-*). NFL 14→18, NBA 16→17, MLB 12→15, NCAAB 0→15, NCAAF 0→16. ~17 NFL rows still unmapped (LosAngeles/NewYork). |
| 2026-11-07 | Wrote revisit_candidates.md (2-month signal math; past approaches by closeness; injury/trade live-only variants). Spec input for Composer. |
