# Handoff: MaybeMoney (NBA betting app) design discussion

Follow-up to Claude Code session `92e90ef2-a3a6-4d50-8a9f-716b4678642f` (the MaybeMoney planning session: .NET stack, betting all NBA games on DraftKings, EV + fractional Kelly, walk-forward backtests).

## Decisions so far

- News: use only to extract structured facts (player status, minutes limit, lineups) into features; re-check before each bet; store raw items with `first_seen_at` for later testing.
- No separate decision framework; bet logic stays plain C# behind `IBetDecider` with thresholds in config tuned by the backtest.
- Jev (TypeSafe) and Mercury (Inception): not needed; only relevant to news extraction.
- News extraction: **`INewsExtractor` via `claude` CLI subprocess** (prompt → JSON stdout). **No Anthropic C# SDK / API key** — superseded earlier Haiku-API plan; see `PLAN.md`.
- Next step (post-edge): CLI-backed extractor + ~100-item hand-labeled test set.

## Full conversation

### User

Look at the session 92e90ef2-a3a6-4d50-8a9f-716b4678642f

Should it also read from news sources before each game to help inform of the outcome? Is that white noise or helpful?

---

### Claude

Yes, have it read news before each game, but only to pull out a few specific facts. Reading articles to form a general opinion of who wins is noise.

## Why most news adds nothing

- **The odds already include it.** By the time an article is published, DraftKings has usually priced it in. Your model is also blended with the sharp market line (the w ≈ 0.2–0.4 weight from that session). So a news story only helps if it reaches you before the line moves, or if it tells you something the line doesn't.
- **You can't backtest it.** That session found there's no free archive showing what the news said before each past game. A feature you can't backtest can't earn a place in the model. If you add "the article sounded bullish on the home team" anyway, you've added a parameter with no evidence behind it. That's the overfitting you said you wanted to avoid.

## Signal vs. noise

| Helpful (structured facts) | White noise |
|---|---|
| Player status: out, doubtful, questionable, probable | Previews, predictions, power rankings |
| Minutes restriction or return from injury | Tone or sentiment of an article |
| Confirmed starting lineup, late scratches | "Revenge game" and "must-win" stories |
| Rest days for healthy players (load management) | Quotes from coaches and players |
| Trades and signings that change the rotation | Fan forum opinions, unless used as an early heads-up to check official sources |
| Referee assignments (already planned as a feature) | Recaps of past games (box scores cover these) |

## How the news should be used

1. **Update the inputs, not the prediction.** An LLM reads each news item and fills in fields such as `player`, `status`, `minutes_limit`, `source` and `first_seen_at`. Those fields feed the injury and player-impact features the model already uses. The news never changes the probability directly.
2. **Re-check right before betting.** If any of those fields changed since the model last ran, run it again before placing the bet. This is the part that matters most.
3. **Pass on uncertain games.** If a key player is listed as questionable and the decision comes late, don't bet yet. Wait for the status to be confirmed. The line will move once it is, and you only bet if an edge is still there afterward.
4. **Watch for slow lines.** When news arrives and DraftKings hasn't moved, that's the speed edge from your reaction-time experiment. Alert on it, but check the gap against the sharp line before betting.

## Store everything, decide later

Save the raw text of every article with its `first_seen_at` time, even items you don't use yet. After a season you'll have your own point-in-time archive. Then you can test whether something like "a beat reporter hints at limited minutes" adds anything beyond the structured fields. Promote it to a feature only if a walk-forward backtest says it helps.

**In short:** news that changes who plays and for how many minutes is signal, and it's worth checking right before every bet. Everything else is noise until your own data shows otherwise.

---

### User

Does using Jev or another decision model provide any value into this stack? I don't want to use something arbitrarily but only if it provides excellent value at a free or cheap price point.

---

### Claude

I don't recognize "Jev" as a decision model or library, so I can't judge it directly. Is it the name of a product, or maybe shorthand for something like DMN (Decision Model and Notation)? Send me a link or a longer name and I'll evaluate it. Until then, here's how decision tools in general fit this stack.

## The decision step is already a model, and it's small

From that session, the whole decision layer is:

1. Blend the model's probability with the sharp market's.
2. Calculate expected value (EV) at DraftKings' price.
3. Apply the pass rules: below the EV threshold, a key player still questionable, or a stale input.
4. Size the bet with quarter Kelly.
5. Apply the caps: per bet, and per day.

That's roughly 100–200 lines of C# in `MaybeMoney.Betting`. Kelly is the decision theory, and it's the right one for growing a bankroll without risking ruin. A separate decision framework wouldn't make better choices than this. It would only express the same rules in a different format.

## Options and whether they're worth it

| Option | What it is | Cost | Value here |
|---|---|---|---|
| **Plain C# + config file** | Thresholds (blend weight, minimum EV, Kelly fraction, caps) kept in `appsettings.json` | Free | **Best.** The backtest harness can sweep these values, which is where the real gains are |
| **Rules engines** (NRules, Microsoft RulesEngine, DMN tools) | Rules written as data instead of code | Free | **Low.** They're built for hundreds of business rules edited by non-programmers. You'll have about 5 rules, and you write the code yourself |
| **Optimization solvers** (Google OR-Tools, MathNet) | Choose stakes when several bets are open at once | Free | **Situational.** Only needed if you bet correlated markets on the same game, like moneyline plus spread, or several props. A simpler rule avoids it: one bet per game |
| **Bayesian decision analysis** (CmdStan) | Kelly sized on the uncertainty in your probability, not a single estimate | Free | **Medium, later.** It shrinks stakes when the model is unsure. Quarter Kelly already approximates this cheaply |
| **Reinforcement learning / learned bet sizing** | A model that learns when and how much to bet | Free, but costly in time | **Negative.** Your data is too small and too noisy, so it would overfit. That's the exact risk you're trying to avoid |

## Recommendation

Don't add a decision framework. Put the decision logic behind an `IBetDecider` interface, keep every threshold in config, and let the walk-forward backtest choose the values. The tuning is where the value is, not the tool that runs the rules.

The one exception worth planning for: when you start betting player props, several correlated bets on one game need joint sizing. At that point a small OR-Tools solver or a correlation-adjusted Kelly is worth adding. Both are free.

---

### User

Here is jev: https://typesafe.ai/blog/introducing-system-one-models-and-jev

---

### Claude

Jev isn't a decision model in the sense I described last time. It's a fast, cheap model that reads text and returns typed answers, with a probability attached to each answer. In your stack it could do one job: turning news into structured fields. It should not predict games or decide bets.

## What it is

From the TypeSafe post:

- **Input and output:** you send text and define the possible answers in advance. It returns one of those typed values with a confidence probability, never free-form text.
- **Claims:** responses in 70–500ms, 40–200x faster than frontier LLMs, about $0.04 per million input tokens, and output is free.
- **Status:** early access with a waitlist. The page mentions only a Python wrapper, nothing for .NET. It doesn't say where the training data comes from or what the license is.

## Where it fits and where it doesn't

| Stack step | Use Jev? | Why |
|---|---|---|
| **Pulling facts out of news** | **Yes, it's a good fit** | This is the step from my earlier answer: an article becomes `player`, `status`, `minutes_limit` and so on. That's a fixed-answer extraction task, which is exactly what Jev is built for. The confidence score helps too: a low one can send the item to you for review instead of updating the features automatically. |
| **Predicting who wins** | **No** | Its probabilities measure how sure it is of its answer, not real NBA knowledge. You can't backtest it either: its training data may contain past results, which would leak outcomes into the test. |
| **Deciding whether and how much to bet** | **No** | EV, Kelly and the caps are exact math. A model can only add errors there. |
| **Checking LLM output** | Maybe | Only needed if a larger LLM ends up in the pipeline. |

## Is the value "excellent"? Only modest for you

Its two selling points are speed and price, and neither matters much at your volume:

- **Price:** league-wide news might be several hundred items per game day, around 0.5–0.75M tokens. Any small model, such as Claude Haiku, costs a few dollars a month at that volume. Jev being cheaper still saves you almost nothing.
- **Speed:** your poller checks every ~30 seconds. Whether extraction takes 0.2 seconds or 2 seconds is noise next to that, so it won't change your speed edge.
- **Risks:** it's early access, the benchmarks are the company's own (the post admits its reference answers may be biased), the post says it can't prove the low price isn't subsidized, and there's no .NET SDK. You'd call it with `HttpClient`, assuming it has a plain REST API. I haven't checked the docs to confirm that.

## Recommendation

Don't build around it, but leave room for it. Put news extraction behind an `INewsExtractor` interface and start with whatever model you can use today. If you get Jev access, hand-label about 100 real NBA news items. Then compare Jev with your current extractor on accuracy, and on whether its confidence scores are honest (its 90% answers should be right about 90% of the time). Switch only if it wins. It would be a reasonable optional part, but you don't need it.

---

### User

Or does using a diffusion model like inception provide any value?

https://www.inceptionlabs.ai/

---

### Claude

Mostly no, for the same reasons as Jev. "Diffusion" here describes how Inception's Mercury models produce text: many tokens at once instead of one at a time. That makes them fast, but it doesn't make them better at predicting games or sizing bets. Like Jev, they could only do the news extraction step.

## What's relevant on the site

- **Mercury Decide** is a direct competitor to Jev: typed questions with structured choices. It's listed at $0.02 per million input tokens with free output, during a 50%-off promotion.
- **Mercury 3** is a general reasoning model at $0.10 per million input and $0.35 per million output tokens.
- **The API works like OpenAI's.** That's the real practical advantage over Jev: you can call it from .NET with the official OpenAI .NET SDK by changing the base URL. No custom wrapper needed.
- **It seems usable now** through their API platform, and also through AWS Bedrock and Azure Foundry. Jev is waitlist-only.
- The site mentions no free tier and no open-source models.

## Fit in your stack

| Stack step | Mercury? |
|---|---|
| Pulling facts out of news | **Yes, a reasonable option.** Mercury Decide fits the fixed-answer extraction format, and Mercury 3 can handle messier articles |
| Predicting games | No. Tabular game data needs gradient boosting or logistic regression, not a language model |
| Deciding bets | No. EV and Kelly are exact formulas |

## Does it add excellent value? Not at your volume

The cost argument is the same as for Jev. Several hundred news items per game day costs a few dollars a month with almost any model, and a 30-second poller hides any speed difference. Diffusion's speed matters for voice agents and code autocomplete. It doesn't matter for checking injury reports.

## Recommendation

Of the two, Mercury is easier to adopt: it's available now, and the OpenAI-style API works from .NET with no custom code. Use the same plan as before. Keep extraction behind `INewsExtractor`, and since many providers use OpenAI-style APIs, one implementation with a configurable base URL lets you swap Mercury, Claude Haiku or others in and out. Test them on your 100 hand-labeled NBA news items and keep whichever is most accurate. At your volume, the price difference between them is too small to matter.

---

### User

Could Claude Haiku do as good of a job as Mercury for extracting text from news articles?

---

### Claude

Yes, very likely. Pulling injury status out of a news item is an easy task for any current small model. I haven't seen a head-to-head benchmark of Haiku against Mercury, so your 100 labeled NBA items are still the real test. But nothing about this job needs more than Haiku offers.

## Comparison for this job

| | Claude Haiku 5.5 | Mercury Decide / Mercury 3 |
|---|---|---|
| Price per 1M tokens (input / output) | $0.10 / $0.50 | $0.02 / $0 (Decide), $0.10 / $0.35 (Mercury 3), both at a 50%-off promo |
| Your cost per month (~0.5–0.75M input tokens per game day) | **About $1–3** | Under $1 |
| Guaranteed valid output | **Yes.** Structured outputs force the response to match your JSON schema exactly | Decide answers fixed-choice questions; the site doesn't mention a JSON mode |
| .NET support | **Official Anthropic C# SDK** | OpenAI .NET SDK pointed at their URL |
| Speed | A second or two | Faster, but your 30-second poller hides the difference |

The cost difference is a couple of dollars a month. Choose on accuracy and ease of use, not price.

## What actually decides accuracy

Both models will read "[Player] (ankle) is questionable" correctly. Errors come from the harder cases, so put these in your 100 labeled test items:

- **Hedged language:** "game-time decision," "expected to play through it," "will warm up and see." Map each one to a fixed status, and add an `uncertain` option.
- **Which game:** "out the next two weeks" against "out tonight." The output needs a `games_affected` or `return_estimate` field.
- **Recycled news:** a blog repeating yesterday's injury report. Compare against the stored status, so a repeat isn't treated as a new event.
- **Wrong player or team:** the other team's injuries mentioned in an article about one team, players who share a last name, and nicknames.
- **Minutes restrictions** buried mid-article.

A precise schema with fixed choices for `status`, plus a short system prompt with examples of these cases, matters more than which model you pick.

## Recommendation

Start with Haiku. It has an official C# SDK, schema-guaranteed output and negligible cost. Run it at `low` effort, since this is simple extraction (Haiku 5.5 defaults to `medium`). Keep the `INewsExtractor` interface so you can swap in Mercury later. Switch only if it scores clearly better on your labeled set.

---

### User

I already have an anthropic account so it seems cheaper for me to stuck with Claude for now.

---

### Claude

Agreed. At your volume every option costs a few dollars a month at most, so staying with the provider you already have is the right call.

One thing to check: a Claude subscription (Pro or Max on claude.ai) doesn't cover API usage. The API is billed separately through the Claude Console (console.anthropic.com) with prepaid credits. If you've only used claude.ai so far, you'll need to add a small amount of API credit there and create an API key. A $5 deposit covers a couple of months at roughly $1–3 a month.

When you're ready to build the extractor, I can write the `INewsExtractor` implementation with the Anthropic C# SDK, including the JSON schema for status, minutes limit and games affected.

---

