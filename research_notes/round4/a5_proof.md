# A5 proof: seasonality scan (BH q=0.10)

**Branch:** `r4/a5` · **Worktree:** `../pm-r4-a5`  
**Owns:** `Cli/Backtest/Seasonality*` · candidate/shadow via A1 `VariantLedger`

**Protocol lock:** every phase below is **pre-registered before any scan run**. Windows are calendar rules, not data-mined. No look-once holdout tuning. BH FDR at **q=0.10**; print **N_trials** = number of (sport × phase) cells with settled CLV.

**Signal:** shadow variant `V3` settled picks with non-null `ClvPts` (A1 schema). Date from `GameKey` `yyyymmdd`; optional write-back of `CandidateEntry.SeasonPhase`.

---

## Pre-registered phases (locked 2026-11-07, before first scan)

Priority: more specific windows win when ranges overlap (listed order within each sport).

### NBA

| Phase id | Window (MM-DD inclusive, tip date) | Rationale |
|---|---|---|
| `opening-weeks` | 10-01 … 11-14 | First ~3–4 weeks of regular season |
| `weather-winter` | 12-01 … 02-28/29 | Cold-weather travel months |
| `tanking-stretch` | 03-01 … 04-15 | Final weeks / tanking stretch |
| `playoffs` | 04-16 … 06-30 | Playoffs |

### MLS

| Phase id | Window | Rationale |
|---|---|---|
| `opening-weeks` | 02-15 … 03-31 | Season openers |
| `summer-intl-break` | 06-01 … 07-31 | Summer / intl / All-Star lull |
| `final-weeks` | 09-01 … 10-15 | Regular-season close |
| `playoffs` | 10-16 … 12-10 | MLS Cup playoffs |

### LIGUE1 / SERIEA (shared European calendar)

| Phase id | Window | Rationale |
|---|---|---|
| `intl-break` | 09-01…09-10; 10-07…10-16; 11-11…11-20; 03-17…03-26 | FIFA windows (fixed approx.; highest priority) |
| `opening-weeks` | 08-01 … 09-30 | Season openers (excl. intl-break days) |
| `winter-break` | 12-20 … 01-10 | Mid-season pause |
| `final-weeks` | 04-15 … 05-31 | Run-in |
| `promoted-first` | _(deferred)_ | Needs promoted-club list; **not** calendar-matched / not in BH family until then |

### LIGAMX (Apertura / Clausura)

| Phase id | Window | Rationale |
|---|---|---|
| `apertura-opening` | 07-01 … 08-31 | Apertura openers |
| `apertura-liguilla` | 11-01 … 12-15 | Apertura final + liguilla |
| `clausura-opening` | 01-01 … 02-28/29 | Clausura openers |
| `clausura-liguilla` | 04-15 … 05-31 | Clausura final + liguilla |

**Out of phase:** tip dates that match none of the above are excluded from the BH family (not a trial).

**N_trials definition:** count of (sport, phase_id) groups that have ≥1 settled V3 CLV observation after tagging. Printed on every scan line / footer.

---

## Scan command

```bash
dotnet run -c Release --project PossiblyMaybe.Cli -- seasonality-scan [variants.db]
```

Writes phase tags onto matching `CandidateEntries` when present; always prints the phase table (mean CLV, raw p, BH-adjusted p, BH reject @ q=0.10).

---

## Results (filled after first run — do not edit phase windows)

_Pending first `seasonality-scan` on a backfilled `data/variants.db`._

| Sport | Phase | N | MeanClv | P | P_BH | BH@0.10 |
|---|---|---|---|---|---|---|
| | | | | | | |

`N_trials=` _(pending)_

---

## Tests

```bash
dotnet test --filter "FullyQualifiedName~Seasonality"
```

Covers: phase resolve priority, BH adjusted-p / reject at q=0.10, scan aggregation + `N_trials`.
