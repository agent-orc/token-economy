---
tags: [token-economy, execution-and-runner, decision, evidence]
taggingStatus: tagged
---

# Effort dial: one slider from frugal to strong (TE-83)

**Owner decision, 17 November 2026.** A single slider steers cost and usage. Its semantics are **frugal to strong**: the slider position directly picks a model and reasoning level for each work class, using that class's ladder. Usage follows from those picks; there is no weekly-budget target. Token Economy owns the ladders, the evidence and the mapping. Agent Studio shows the slider and applies the picks (a separate Studio card).

The same decision also fixed these defaults:

- Normal cards run at **medium**.
- **High** is for hard cards only (concurrency, unclear bugs, architecture).
- **xhigh** is used only where it is targeted.
- Review aspects stay on **Opus 5.5 low** for now.

At the default position (50), every class picks exactly those rungs; tests pin this.

What is new in the library:

- Data: [`effort-dial.json`](../src/TokenEconomy/catalog/effort-dial.json) and its schema, [`effort-dial.schema.json`](../src/TokenEconomy/catalog/effort-dial.schema.json).
- API: `EffortDial`.
- A new evidence class, **F (measured in our fleet)**.
- A creative benchmark catalogue in the TE-82 schemas, [`creative-benchmark-types.json`](../src/TokenEconomy/catalog/creative-benchmark-types.json) and [`creative-benchmark-results.json`](../src/TokenEconomy/catalog/creative-benchmark-results.json). How it was researched is recorded in [creative-benchmarks-2026-11-27.md](creative-benchmarks-2026-11-27.md).

No website page changed in this card. The creative rows live in their own catalogue files, so the generated website projections stay identical (`generate-website-data.py --check` passes).

## API

```csharp
var dial = EffortDial.Default;

// dial position + work class + CLI -> rung
DialPick pick = dial.Pick(position: 50, workClassId: "feature-logic", DialCli.Claude);
// pick.Rung.ModelId == "claude-opus-5-5", pick.Rung.Effort == EffortLevel.Medium
// pick.Cost.ListUsdLow/High, pick.Cost.Unit, pick.Cost.QuotaBasis, pick.Cost.Derivation

// conditional floors
dial.Pick(0, "review-aspect", DialCli.Claude, ["security-relevant"]);   // Opus 5.5 medium

// inverse view: what runs at this position for every class, and its expected cost
IReadOnlyList<DialPick> all = dial.PicksAt(75, DialCli.Codex);
IReadOnlyDictionary<string, DialRungCost> costs = dial.ExpectedCostsAt(75, DialCli.Codex);

// after a card failed at a rung: next stronger rung, including escalation-only rungs
DialRung? next = dial.Escalate("hard", DialCli.Claude, "hard.claude.opus-5-5-xhigh"); // Fable 5.2 high (restricted)
```

`Pick` is a pure, deterministic function of the embedded catalogue and works in these steps:

1. **Band.** Each banded rung has a `fromPosition`. The band selects the last rung whose `fromPosition` is less than or equal to the position. The first rung starts at 0, and bands rise strictly. Positions outside 0..100 throw.
2. **Floors.** A class can declare floors: unconditional ones (`condition: null`) and conditional ones that apply only when the caller passes the condition (`code-change`, `correctness-critical`, `security-relevant`). The pick is raised to the highest applicable floor. `DialPick.AppliedFloor` reports when a floor changed the result. A floor never lowers a stronger band.
3. **Routing restrictions.** A rung whose model is not `selectable` in the routing policy (for example Fable 5.2 and the GPT-6.2 models, which stay restricted until fleet compatibility is observed) is skipped downwards. It never goes below the floor, and the skip is reported in `SkippedRestrictedRungIds`. The status is read from `model-routing-policy.json`, never restated.
4. **Escalation-only rungs** (for example Fable 5.2 after Opus has failed) have no band. `Pick` never returns them; only `Escalate` does.

Raising the position never lowers the rung. Position 0 gives the first rung of each ladder, and position 100 gives the last banded rung. `EffortDial.Parse(json)` validates a variant document against the same rules; the tests use it for negative fixtures.

### Load-time rules (the loader throws when one is broken)

- Every rung's model exists in the price catalogue and in the routing policy on the matching CLI, and uses a reasoning level that model supports.
- Light-tier models (Haiku 5.5, Luna) appear only in classes marked `allowLightTier`, which are mechanical and summary.
- Along a ladder, the capability tier never drops. When two consecutive rungs use the same model, the effort rises.
- Every rung cites at least one resolvable evidence entry. Creative rungs cite only creative or fleet evidence, never coding benchmarks, and state all four rubric dimensions.
- Floor rungs are banded and selectable. The first rung of every ladder is selectable.
- Fleet cohorts carry class F, a window and per-row case counts, consistent counts, and no card keys (anonymisation guard).
- Every cost factor resolves to positive bounds with low ≤ high. A reprice is only allowed when output and cache-write prices are proportional to input price on both models.

## Evidence classes

| Class | Meaning |
| --- | --- |
| M | Third-party measurement whose raw data was pulled |
| V | Vendor statement |
| L | Derived from list prices or rate cards |
| P | Press or operator lead not verified at the source |
| **F** | **Measured in our fleet.** Anonymised (no card keys or project names). A measurement window and a case count are required. |

`EvidenceClasses` holds the shared identifiers. `benchmark-results.schema.json` now accepts `F`. The benchmark loader accepts an F row only with an `ownRun` context, a `taskCount` (case count) and `ownRun` confidence.

### Fleet cohort seeded (F)

The cohort covers Agent Studio runs from 20 Oct to 17 Nov 2026 (28 days), coding cards only, priced at list price. A card counts as *pure* when one model and level carry at least 95% of its cost.

| Model / level | Cards | Accepted | First review passed | Median USD per accepted card | Median tokens per accepted card |
| --- | ---: | ---: | ---: | ---: | ---: |
| Opus 5.5 low | 16 | 16 | 16/16 | 1.58 | 3.0 M |
| Opus 5.5 medium | 51 | 42 | 50/51 | 4.57 | 11.9 M |
| Opus 5.5 high | 77 | 75 | 69/76 | 9.65 | 25.5 M |
| gpt-6-sol medium | 92 | 67 | 74/75 | 8.25 | 36.6 M |
| gpt-6-sol high | 219 | 199 | 175/205 | 10.80 | 44.9 M |
| gpt-6-sol xhigh | 16 | 16 | 12/16 | 14.53 | 59.6 M |

Caveats stored with the cohort:

- Levels were not assigned at random: harder cards tended to get higher levels.
- The retry tax was about 2.6% of spend.
- Total 28-day list-price spend was about 12,500 USD, 98% of it coding runs.

Cache reads over the same window:

- gpt-6-sol high: 17.7 B cached-read tokens, about 74% of its list-price cost.
- Opus 5.5 high: 7.1 B cached-read tokens. Its cost share was not reported, so no Opus share is used anywhere.

## Cost model

Each rung's expected cost is a list-price **range** (`ListUsdLow`, `ListUsdHigh`). It is an anchor multiplied by factors, and every input is referenced by ID and derived in code, not restated.

- **Anchors.** An anchor is either a fleet row (unit: **accepted card**) or a benchmark row's published or derived cost per task (unit: **benchmark task**). The benchmark-task unit is used for creative work, which has no fleet cohort.
- **Reprice (the cache lever).** A reprice keeps the token profile and changes the price card:

  `factor = s × (cache-read price ratio) + (1 − s) × (input price ratio)`

  Here *s* is the cache-read share of the anchor's list-price cost.
  - Where *s* is measured (gpt-6-sol high, 74%), the factor is a point: 0.74 × 0.5 + 0.26 × 1 = **0.63**. So gpt-6.1-sol high costs 6.80 USD per accepted card against gpt-6-sol's 10.80, at the same input and output list price.
  - Where *s* is not measured (other gpt-6-sol levels), the range assumes 65 to 85%.
- **Ratios.** A ratio is a cost-per-task ratio between two benchmark rows, or a ratio between two fleet medians. Ratios supply effort steps (for example fleet gpt-6-sol xhigh/high as the lower bound and ARC-AGI-2 Opus xhigh/high as the upper bound) and model steps (for example Terminal-Bench 4.1 Fable/Opus at about 3.5 times).
- **Constants** are allowed only as stated bounds, with a reason. One example: high costs at most the max measurement.
- **Codex** costs are also given in Standard credits: list USD × 25, taken from the official credit rate card in the 17 Nov refresh. The loader checks that the card is uniform across components.
- **Claude** has no vendor-published per-model weight for the weekly limit. List price is used as a **flagged proxy** (`DialQuotaBasis.ClaudeListPriceProxy`), and every Claude rung has a `measuredQuotaWeight` slot for quota percentage points per accepted card, with window, case count and plan. The Studio will report that weight later; when it is set, the basis becomes `ClaudeMeasuredWeight`.
- **Unknown** costs (summary, orchestrator chat) have `Status = Unknown`, null totals and a reason. They are never zero.

Base (short-context) tariffs are used. Requests above a model's long-context threshold cost more, so the ranges understate such sessions.

## Work classes and ladders

The work classes are:

- (a) mechanical rounds
- (b1) feature logic
- (b2) UI work judged on visual quality
- (c) hard bugs, concurrency and architecture
- (d) review aspects
- (e) review grade and decision
- (f) research and reports
- (g) summaries, metadata and classification
- (h) orchestrator chat
- (i) creative work: visual design, UI aesthetics, interactive 3D scenes and games

Class (b) of the brief is split into (b1) and (b2), as the second owner note asked.

### What runs at each position

Generated from `EffortDial.Default.Pick`.

Claude:

| Class | 0 | 25 | 50 (default) | 75 | 100 |
| --- | --- | --- | --- | --- | --- |
| (a) mechanical | Haiku 5.5 medium | Opus 5.5 low | Opus 5.5 low | Opus 5.5 medium | Opus 5.5 medium |
| (b1) feature-logic | Opus 5.5 medium | Opus 5.5 medium | Opus 5.5 medium | Opus 5.5 high | Opus 5.5 high |
| (b2) ui-visual | Opus 5.5 medium | Opus 5.5 medium | Opus 5.5 medium | Opus 5.5 high | Opus 5.5 max |
| (c) hard | Opus 5.5 high | Opus 5.5 high | Opus 5.5 high | Opus 5.5 xhigh | Opus 5.5 xhigh |
| (d) review-aspect | Opus 5.5 low | Opus 5.5 low | Opus 5.5 low | Opus 5.5 low | Opus 5.5 medium |
| (e) review-decision | Opus 5.5 low | Opus 5.5 low | Opus 5.5 low | Opus 5.5 medium | Opus 5.5 medium |
| (f) research | Opus 5.5 medium | Opus 5.5 medium | Opus 5.5 medium | Opus 5.5 high | Opus 5.5 high |
| (g) summary | Haiku 5.5 low | Haiku 5.5 low | Haiku 5.5 medium | Haiku 5.5 medium | Haiku 5.5 medium |
| (h) orchestrator-chat | Opus 5.5 medium | Opus 5.5 medium | Opus 5.5 medium | Opus 5.5 medium | Opus 5.5 medium |
| (i) creative | Sonnet 5.5 high | Sonnet 5.5 high | Sonnet 5.5 high | Sonnet 5.5 max | Opus 5.5 max |

Codex:

| Class | 0 | 25 | 50 (default) | 75 | 100 |
| --- | --- | --- | --- | --- | --- |
| (a) mechanical | 6.1 Terra low | 6.1 Terra medium | 6.1 Sol low | 6.1 Sol medium | 6.1 Sol medium |
| (b1) feature-logic | 6.1 Sol medium | 6.1 Sol medium | 6.1 Sol medium | 6.1 Sol high | 6.1 Sol high |
| (b2) ui-visual | 6.1 Sol medium | 6.1 Sol medium | 6.1 Sol medium | 6.1 Sol high | 6.1 Sol max |
| (c) hard | 6.1 Sol high | 6.1 Sol high | 6.1 Sol high | 6.1 Sol xhigh | 6.1 Sol xhigh |
| (d) review-aspect | 6.1 Sol low | 6.1 Sol low | 6.1 Sol low | 6.1 Sol low | 6.1 Sol medium |
| (e) review-decision | 6.1 Sol low | 6.1 Sol low | 6.1 Sol low | 6.1 Sol medium | 6.1 Sol medium |
| (f) research | 6.1 Sol medium | 6.1 Sol medium | 6.1 Sol medium | 6.1 Sol high | 6.1 Sol high |
| (g) summary | 6 Luna high | 6 Luna high | 6 Luna high | 6 Luna high | 6 Luna high |
| (h) orchestrator-chat | 6.1 Sol low | 6.1 Sol low | 6.1 Sol medium | 6.1 Sol medium | 6.1 Sol medium |
| (i) creative | 6.1 Sol max | 6.1 Sol max | 6.1 Sol max | 6.1 Sol max | 6.1 Sol max |

### Floors

| Class | Condition | Claude floor | Codex floor |
| --- | --- | --- | --- |
| (a) mechanical | `code-change` | Opus 5.5 low (Haiku only for pure docs/config) | none |
| (b1) feature-logic | `correctness-critical` | Opus 5.5 high | GPT-6.1 Sol high |
| (c) hard | always | Opus 5.5 high | GPT-6.1 Sol high |
| (d) review-aspect | `security-relevant` | Opus 5.5 medium | GPT-6.1 Sol medium |
| (e) review-decision | `security-relevant` | Opus 5.5 medium | GPT-6.1 Sol medium |

### Rungs, bands and expected cost

The cost column is list-price USD per accepted card. Rows marked "/task" are per AA-3D task (one agent build of up to 3 h) and are a proxy. Credits are Codex Standard credits.

Claude:

| Class | Rung | Band | Expected cost | Confidence | Main evidence |
| --- | --- | --- | --- | --- | --- |
| (a) | Haiku 5.5 medium (pure docs/config) | 0–19 | 0.10–0.32 | low | ARC-AGI-2, TB 4.1 cost ratios |
| (a) | Opus 5.5 low | 20–74 | 1.58 | high | F: 16/16 accepted, 16/16 first review |
| (a) | Opus 5.5 medium | 75–100 | 4.57 | high | F |
| (b1) | Opus 5.5 medium | 0–74 | 4.57 | high | F: best measured value, 50/51 first review |
| (b1) | Opus 5.5 high | 75–100 | 9.65 | high | F |
| (b2) | Opus 5.5 medium | 0–59 | 4.57 | medium | F (no visual evidence at this effort) |
| (b2) | Opus 5.5 high | 60–84 | 9.65 | medium | F, Design Arena web |
| (b2) | Opus 5.5 max | 85–100 | 12.98–43.81 | medium | WebDev 1820, Design Arena web |
| (c) | Opus 5.5 high | 0–59 | 9.65 | high | F |
| (c) | Opus 5.5 xhigh | 60–100 | 12.98–15.87 | medium | effort-step ratios |
| (c) | Fable 5.2 high (restricted) | escalation only | 21.47–33.73 | low | TB 4.1 (about 3.5x Opus per trial) |
| (d) | Opus 5.5 low | 0–89 | 1.58 | low | owner decision; F as cost anchor |
| (d) | Opus 5.5 medium | 90–100 | 4.57 | low | F |
| (e) | Opus 5.5 low | 0–74 | 1.58 | low | F as cost anchor |
| (e) | Opus 5.5 medium | 75–100 | 4.57 | low | F |
| (f) | Opus 5.5 medium | 0–74 | 4.57 | medium | F |
| (f) | Opus 5.5 high | 75–100 | 9.65 | medium | F |
| (g) | Haiku 5.5 low | 0–39 | unknown | low | ARC-AGI-2 shape |
| (g) | Haiku 5.5 medium | 40–100 | unknown | low | ARC-AGI-2 shape |
| (h) | Opus 5.5 medium | 0–100 | unknown | low | F (no chat cohort) |
| (i) | Sonnet 5.5 high | 0–54 | 2.37–12.91 /task | low | Arena 3D 1817 ± 16 |
| (i) | Sonnet 5.5 xhigh | 55–74 | 4.68–12.91 /task | low | Arena 3D 1868 ± 21, WebDev 1773 |
| (i) | Sonnet 5.5 max | 75–89 | 12.91 /task | medium | AA-3D 1204 (1176–1232) |
| (i) | Opus 5.5 max | 90–100 | 19.23 /task | medium | AA-3D 1202, Arena 3D 1915, WebDev 1820 |
| (i) | Fable 5.2 max (restricted) | escalation only | 25.41 /task | medium | AA-3D 1271 (1246–1297) |

Codex:

| Class | Rung | Band | Expected cost | Credits | Confidence |
| --- | --- | --- | --- | --- | --- |
| (a) | 6.1 Terra low | 0–19 | 0.82–4.24 | 21–106 | low |
| (a) | 6.1 Terra medium | 20–39 | 2.37–4.97 | 59–124 | low |
| (a) | 6.1 Sol low | 40–74 | 1.64–4.75 | 41–119 | medium |
| (a) | 6.1 Sol medium | 75–100 | 4.74–5.57 | 119–139 | medium |
| (b1) | 6.1 Sol medium | 0–74 | 4.74–5.57 | 119–139 | medium |
| (b1) | 6.1 Sol high | 75–100 | 6.80 | 170 | medium |
| (b2) | 6.1 Sol medium | 0–59 | 4.74–5.57 | 119–139 | low |
| (b2) | 6.1 Sol high | 60–84 | 6.80 | 170 | low |
| (b2) | 6.1 Sol max | 85–100 | 11.90–27.07 | 298–677 | medium |
| (c) | 6.1 Sol high | 0–59 | 6.80 | 170 | medium |
| (c) | 6.1 Sol xhigh | 60–100 | 8.35–9.81 | 209–245 | medium |
| (c) | 6.2 Astra high (restricted) | escalation only | 10.49–27.22 | 262–680 | low |
| (d) | 6.1 Sol low | 0–89 | 1.64–4.75 | 41–119 | low |
| (d) | 6.1 Sol medium | 90–100 | 4.74–5.57 | 119–139 | low |
| (e) | 6.1 Sol low | 0–74 | 1.64–4.75 | 41–119 | low |
| (e) | 6.1 Sol medium | 75–100 | 4.74–5.57 | 119–139 | low |
| (f) | 6.1 Sol medium | 0–74 | 4.74–5.57 | 119–139 | medium |
| (f) | 6.1 Sol high | 75–100 | 6.80 | 170 | medium |
| (g) | 6 Luna high | 0–100 | unknown | – | medium |
| (h) | 6.1 Sol low | 0–39 | unknown | – | low |
| (h) | 6.1 Sol medium | 40–100 | unknown | – | low |
| (i) | 6.1 Sol max | 0–100 | 3.25 /task | 81 | low |
| (i) | 6.2 Sol max (restricted) | escalation only | 3.43 /task | 86 | low |
| (i) | 6.2 Astra max (restricted) | escalation only | 13.91 /task | 348 | low |

### Where the ladders differ from the research leads

The research leads were adjusted to the verified evidence:

- **(a) Sonnet 5.5 medium is a candidate, not a rung.** The coding cost evidence conflicts: Terminal-Bench 4.1 puts Sonnet above Opus per trial, while ARC-AGI-2 puts it below. Meanwhile Opus 5.5 low is already measured at 1.58 USD with 16/16 accepted.
- **(b1) Sonnet 5.5 high is a candidate only**, as briefed.
- **(b2) has no visual-quality evidence below max.** WebDev Arena has no Opus 5.5 row below max and no GPT-6.1 Sol row below max. The default rungs therefore rest on fleet coding value, and the class records this as a gap. Sonnet 5.5 xhigh and high, Fable 5.2 xhigh and GPT-6.2 Sol max are recorded as candidates with their WebDev scores.
- **(c) Fable 5.2 and GPT-6.2 Astra are escalation-only.** They are reached through `Escalate` after Opus or 6.1 Sol has failed, and both are restricted in routing for now.
- **(g) codex** uses Luna high, following OpenAI's [model guide](https://learn.chatgpt.com/docs/models) [V, retrieved 2026-11-27]: "Choose Luna for specific, high-volume tasks … such as extraction, classification, transformation, and structured summaries."
- **(i)** is built from creative evidence only (next section).

## Class (i): creative work

Coding benchmarks (Terminal-Bench, SWE-bench, ARC, the AA coding index) are **not** used as quality evidence here; the loader rejects them. ARC-AGI-2 appears in one place only: as a *cost shape*, bounding Sonnet's high/xhigh cost below its max measurement. It is never a quality signal.

The owner's rubric:

| Dimension | Example for a 3D game |
| --- | --- |
| Originality | Does the game have an interesting idea or mechanic? |
| Design | Do colours, lighting, shapes, animations and user interface fit together? |
| Game feel | Are controls, camera, pace and feedback pleasant? |
| Technical execution | Do collisions, game states, restart and performance work? |

What each source measures (from `creativeRubric.benchmarks`):

| Benchmark | Method | Originality | Design | Game feel | Technical execution | Broken builds before vote | Cost published |
| --- | --- | --- | --- | --- | --- | --- | --- |
| AA-3D v1.0 | human preference, recruited panel | partial | measured | measured | **excluded** (filtered before vote) | excluded | tokens per task |
| WebDev Arena (Code Arena) | human preference, requester votes | none | measured | partial | partial ("both bad" vote) | kept | none |
| Arena 3D | human preference, 3D-labelled battles | partial | measured | partial | partial | kept | none |
| Design Arena 3D Design (HTML) | human preference | partial | measured | none | none | not stated | per generation |
| Design Arena Game Dev (HTML) | human preference | partial | measured | measured | partial | not stated | per generation |
| Design Arena Agentic Game Dev (Three.js) | human preference | partial | measured | measured | partial | not stated | per build |
| Design Arena Website (HTML) | human preference | partial | measured | none | none | not stated | per generation |
| Design Arena Agentic Web Apps | human preference | none | measured | none | partial | not stated | per build |

Each creative rung states what the evidence says per dimension (`DialRung.Dimensions`, status `evidence`, `partial` or `none`):

- **Sonnet 5.5 high (default).**
  - Design: evidence. Arena 3D 1817 ± 16, clearly above what its overall Code Arena level predicts and strongest on games and simulations.
  - Originality and game feel: partial, mixed into preference.
  - Technical execution: **none**.
  - This is the cheapest configuration with direct creative evidence.
- **Sonnet 5.5 xhigh.**
  - Design: Arena 3D 1868 ± 21 and WebDev 1773.
  - Technical execution: none.
- **Sonnet 5.5 max.**
  - Design and game feel: AA-3D 1204 (1176–1232), level with Opus 5.5 max, at about two thirds of the derived cost per task.
  - Technical execution: partial. 46 of 54 tasks have a scene in voting; this is a coverage proxy, not a playable rate.
- **Opus 5.5 max.**
  - Design: AA-3D 1202; Arena 3D 1915 ± 28 and WebDev 1820, ahead of every Sonnet configuration.
  - Game feel: Design Arena's agentic Three.js game board ranks Opus 5.5 high first (1408).
  - Technical execution: partial, 49 of 54.
- **Fable 5.2 max (escalation only, restricted).**
  - Design: AA-3D 1271 (1246–1297), ahead of both. Coverage 54 of 54.

The OpenAI side has these rungs:

- **GPT-6.1 Sol max** is the only selectable configuration with creative evidence: AA-3D 1121, Arena 3D 1802.
- **GPT-6.2 Sol max** is restricted: AA-3D 1259 at about the same derived cost (3.43 against 3.25 USD per task). It is the first promotion candidate for this class.
- **GPT-6.2 Astra max** leads AA-3D at 1318.

Gaps recorded in the data:

- **Sonnet 5.5 high against Opus 5.5 low is thin evidence.** No source has an Opus 5.5 row below max on creative work. The only effort-named Opus row on Design Arena is high, and AA-3D has max rows only. The pair stays unranked and is not inferred.
- **No source publishes a playable rate.** AA-3D removes non-running scenes before voting and does not publish the excluded share, so its Elo is preference among valid results. Technical execution therefore has to come from our own creative cards: the Studio should report a playable rate (runs, restart, collisions, performance) per rung. Arena's Interaction-as-a-Judge method and its `ArenaWebdevCorrectness` data show one way to check this automatically.
- There is no fleet cohort of creative cards, so expected cost is per AA-3D task, not per accepted card.

## Which classes a conclusion applies to

Conclusions are data (`conclusions`), each with `appliesTo`, `provisionalFor` and `doesNotApplyTo`. `ConclusionsFor(classId)` returns the ones that hold for a class.

| Conclusion | Applies to | Provisional for | Does not apply to |
| --- | --- | --- | --- |
| Sonnet 5.5 is not a cheap Opus 5.5 on coding (TB 4.1: 22.22 vs 14.30 USD per trial) | a, b1, c | d, e, f, h | **b2, i** |
| Sonnet and Opus are level on 3D at max (AA-3D 1204 vs 1202), Sonnet about two thirds the cost | i | – | all coding, review, text classes and b2 |
| Opus max ahead of Sonnet on WebDev (about 48 over xhigh, about 105 over high) | b2 | i | coding, review, text classes |
| The cache-read price is the main cost lever (6.1 Sol dominates 6 Sol) | a, b1, b2, c, d, e, f | h, g | i |
| Haiku and Luna collapse at low and medium on hard reasoning | all (enforced by the loader) | – | – |
| Preference arenas do not measure playability | i, b2 | – | – |

## For the Studio card

- Show `Positions.Default` (50) as the neutral point. 0 means frugal and 100 means strong.
- Apply `Pick(position, class, cli, conditions)`. Pass `code-change`, `correctness-critical` and `security-relevant` when they hold.
- Use `Escalate` only after a failed attempt.
- Display `DialPick.Cost` with its `Unit` and `QuotaBasis`. Label Claude list-price costs as a proxy until `measuredQuotaWeight` is filled in.
- Report back for each rung, with window and case count, anonymised (class F):
  - quota percentage points per accepted card for Claude;
  - a creative-card playable rate;
  - fleet cohorts for GPT-6.1 Sol and Terra, Sonnet 5.5 and UI cards with a visual verdict.
