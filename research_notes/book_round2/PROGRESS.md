# Book round 2 — progress tracker

Started 2026-10-28. Baseline `main` @ `0f43ea7` (user WIP uncommitted in `PaperLedger.cs`, `ui/*` — do not touch/commit).
Final write-up → `reports/book_topics_review.md`.

## ▶ RESUME HERE (read after any interruption)

**Next action:** wait for E2 (+ D1). Phase 4 starts when E1–E4 + A1 are `done` (A1 done; E2 still out). When a session restarts, background agents from the old session are gone:
for each row still `running`, check whether its output file exists and is complete (ends with a shortlist/summary).
- File complete → mark `done`, commit it.
- File missing/partial → relaunch that one agent with the prompt in `agent_prompts.md` (same file name; Auto).
Phase 4 starts only when E1–E4 + audit are `done`. Never re-run a `done` row. Tier C (soccer 2025–26 / MLS 2026) never scored yet — check the Tier C ledger below before scoring.
Code-change agents (phase 4+): Composer. Research/eval/audit: Auto.

### Agent ledger

| ID | Task | Model | Output file | Status |
|---|---|---|---|---|
| E1 | ISL / ESL / Sparsity topics | Auto | `eval_isl_esl_sparsity.md` | done — `78cd220d-6dea-4f92-9be9-8e6fde8283c7` |
| E2 | Bayesian (BDA3, ROS, CASI, Rethinking, BMCP) | Auto | `eval_bayesian.md` | running — `ccb38f53-6076-4767-9446-d235e223b2c8` |
| E3 | General ML (Murphy, Bishop, MacKay, MML, GPML) | Auto | `eval_ml_general.md` | done — `c89ab46e-e292-4565-bdcb-ea33624dcd1e` |
| E4 | Forecasting / markets (fpp3, conformal, scoring rules, Kaunitz, penaltyblog, Thorp) | Auto | `eval_forecast_markets.md` | done — `1e7297b3-a373-4afe-b281-1a1718342cd3` |
| A1 | Overcorrection / training audit | Auto | `audit_overcorrection.md` | done — `7751b52c-3e9d-4e0e-89de-02e2dd285307` |
| V1 | Venue placement (Kalshi / RH / Crypto.com) | Auto | `venue_placement.md` | done — `0f2e5c1d-9b92-42fc-be9d-ed68197fca64` |
| D1 | NBA/NFL/MLB odds data sources (download only → `data/raw/round2_odds/`) | Auto | `hunt_data_sources.md` | running — `b211375a-626d-4f98-97ac-5b8ff9c52719` |

### Experiment ledger (phase 4 / 4b)

| Slug | Branch / worktree | Model | Tier A | Tier B | Tier C | Decision | Merge commit |
|---|---|---|---|---|---|---|---|
| — | | | | | | | |

### Tier C ledger (look-once; append before scoring, never re-score)

| Date | Candidates scored | Result file |
|---|---|---|
| — | | |

## Phases

| # | Phase | Status | Output |
|---|---|---|---|
| 1 | Download free books / repos → `library/` | done | see Downloads |
| 2 | Topic evaluation (4 subagents, by book group) | E1/E3/E4 done; E2 running | `eval_*.md` |
| 3a | Over-correction / training audit | done — protocol tightened in PLAN | `audit_overcorrection.md` |
| 3b | Venue placement research (Kalshi / Robinhood / Crypto.com) | done | `venue_placement.md` |
| 4 | Pick candidates, worktree experiments | pending (waits on 2 + 3a protocol) | `exp_*.md` |
| 4b | NBA / NFL / MLB soft-spot hunt (data first, then models) | D1 running (Auto); hunts after 4 | `hunt_*.md` |
| 5 | Merge winners, write final review | pending | `reports/book_topics_review.md` |
| 6 | (very end) Training-practice review across all books → check our process, fix if needed | pending | `reports/training_process_review.md` |

Plan: `PLAN.md` (this folder).

## Safety scan (prompt injection) — clean

Full-text regex over all PDFs, `articles/*.md`, repo docs/code for AI-directed instructions, chat-template tokens, hidden unicode, agent config files.
Hits were benign: "as an aid" false positives; `<|im_start|>` in pyprobml's own LLM-training script; zero-width spaces in Kaunitz PDF title (export artifact, stripped). No tag-unicode, no CLAUDE.md/AGENTS.md/.cursor in repos.

## Data finding

Totals odds with an **unused 2025–26 season** exist for LIGUE1 / LIGAMX / SERIEA / LALIGA / EFL (+ MLS 2026 partial) → fresh look-once holdout (Tier C). NBA totals odds only 2008–2022 + 2025 → NBA changes provisional.

## Downloads

- New PDFs: ISLP, ISLR2, Kaunitz (arXiv 1710.02824), Angelopoulos–Bates conformal (2107.07511), Gneiting–Raftery scoring rules
- Already present: BDA3, PRML, CASI, ESL, MacKay, MML, Murphy PML1+2, GPML, ROS, Sparsity, Thorp Kelly
- Repos (`library/repos/`, shallow): penaltyblog, ROS-Examples, pymc-resources (Rethinking ports), stat_rethinking_2024, BookCode_Edition1 (BMCP), fpp3, conformal-prediction, pyprobml
- Not downloaded: Statistical Rethinking book & Cameron–Trivedi (paid, no free copy); ABDWR3e repo (URL wrong; MLB already FAIL)
- TOC text (first 30 pages): `library/toc/*.txt`

## Early findings (parent, before agents)

- Holdout 2024–25 reused for every KEEP decision → selection bias risk (audit item).
- `paper.db` `CloseLine`/`ClvPts` on settled tickets look like **final total goals**, not closing lines (COL@SJ CloseLine=4.0) → live CLV may be meaningless.
- 2026-10-28 day: 4/6 ≈ break-even because Kalshi wins were at −163/−150 while losses were near even.
- New DK tickets all logged at −110 — check whether that's the real DK price or a default.

## Log

| When | Event |
|---|---|
| 2026-10-28 | Phase 1 done; phases 2/3 agents launched |
| 2026-10-28 | Plan committed (a078fb8); 6 agents launched (E1–E4, audit, venue). User added phase 4b: NBA/NFL/MLB soft-spot hunt |
| 2026-10-28 | User: code-change agents run on Sonnet; 4b data-source agent launched (download only) |
| 2026-10-28 | User: research agents run on Opus (the 7 running agents already inherit Opus) |
| 2026-10-28 | Added RESUME HERE section, agent/experiment/Tier C ledgers, agent_prompts.md for relaunch |
| 2026-10-28 | Handoff doc added; pointers in AGENTS.md and root PROGRESS.md |
| 2026-10-28 | **PAUSED by user.** All 7 agents stopped; none had written output; data/raw/round2_odds not created; no worktrees. Clean state at this commit. |
| 2026-10-28 | Model directive change: code agents = Composer (was Sonnet); research = Auto (was Opus). Relaunched all 7 research agents (Auto, background). |
| 2026-10-28 | E1 done: `eval_isl_esl_sparsity.md`. Top $: soccer Poisson CDF OverProb (0 params); NBA residual-σ Normal CDF. |
| 2026-10-28 | E4 done: `eval_forecast_markets.md`. Agrees #1 Poisson/grid P(over); Kaunitz live-only (no TOTAL multi-book). |
| 2026-10-28 | A1 done: holdout reused ≥8×; live CloseLine=finals; DK −110 default; protocol tightened in PLAN. Money-fix rank: settle CLV → holdout peek → thin champs. |
| 2026-10-28 | E3 done: `eval_ml_general.md`. Same #1 distributional OverProb; NBA logistic badly overconfident vs residual σ≈22.8. |
| 2026-10-28 | V1 done: `venue_placement.md`. Top code gaps: fee-adjusted BE, exact strike gate, EU/MX Kalshi series. |
