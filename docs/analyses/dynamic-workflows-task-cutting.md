# Dynamic Workflows as a Task-Cutting Strategy

**Analysis date:** 2026-07-28

**Question:** Is one large card that orchestrates a dynamic multi-agent
workflow better than several small cards chained with `dependsOn`?

## Decision

Use **small, dependency-linked cards as the default**. Use one large
workflow-driven card only when the selected run is Claude-capable, the work
contains genuinely independent and mostly disjoint slices, one end-to-end gate
can validate the combined result, and the operator does not need decisions
between slices.

This is primarily an effectiveness decision, not a reliable token-saving
trick. In Agent Studio's measured pipeline corpus, coding runs carried 98.1% of
recorded tokens and review aspects carried about 1.9%; deterministic test, git,
and lint steps carried none. Collapsing several cards therefore removes real
outer-pipeline overhead, but the maximum visible token saving from removing
repeated outer reviews is small compared with one extra coding run or an
over-wide workflow fan-out. The large-card strategy wins on tokens only when it
also avoids repeated coding context and retries.

For current Agent Studio routing:

- **Claude Code:** a large card may opt into a scripted dynamic workflow after
  the headless permission path has been validated for that project.
- **Codex:** use small cards with `dependsOn` today. Codex has native subagents,
  but those are parallel agent threads, not Claude's scripted Workflow runtime,
  and the current `codex exec` documentation does not establish the exact
  non-interactive spawn contract used by CodingAgentRunner (CAR).

## 1. Capability Boundary

### Claude Code: scripted workflows plus subagents

Claude Code dynamic workflows are JavaScript programs generated for a task.
The runtime, rather than the parent model turn, owns loops, branches,
intermediate results, and subagent fan-out. Anthropic documents workflows in
Claude Code v2.1.154 or later; the `ultracode` effort setting, which combines
`xhigh` effort with automatic workflow orchestration, requires v2.1.203 or
later. `ultracode` is a setting, not a subscription plan. A direct request such
as “use a workflow” can opt one task in without enabling it for the session.
See the [Claude Code workflow reference](https://code.claude.com/docs/en/workflows).

Ordinary Claude subagents do not require `ultracode`. They remain useful for
cheap exploration, testing, or review even when a card is too small for a
scripted workflow. Model choice is a separate cost decision from task cutting.

Important headless qualification:

- Workflows are available through non-interactive `claude -p`, but the literal
  `ultracode` keyword no longer triggers from a `-p` prompt. CAR should invoke
  a saved or bundled workflow command, or another activation path it has
  smoke-tested, rather than relying on the keyword.
- A workflow launched from `claude -p` starts without the interactive workflow
  approval prompt.
- Workflow child agents always run in `acceptEdits` mode and inherit the tool
  allowlist; they do **not** simply inherit a parent's
  `bypassPermissions`/“yolo” mode. Unallowlisted shell, web, or MCP calls cannot
  obtain a fresh interactive approval in headless execution.

The last point answers the open verification item: filesystem edits are
headless-friendly, but clean inheritance of unrestricted parent permissions is
not the contract. CAR must provide and smoke-test the needed allowlist before a
workflow-sized coding card is considered supported. Anthropic's
[workflow approval rules](https://code.claude.com/docs/en/workflows#approve-the-plan-before-it-runs)
and [subagent permission rules](https://code.claude.com/docs/en/sub-agents#permission-modes)
are the controlling sources.

### Codex: subagents and Ultra, not Claude Workflow scripts

Current Codex releases enable subagents by default. A prompt, applicable
`AGENTS.md`, or a skill can request delegation; custom agents live in
`~/.codex/agents/` or project `.codex/agents/` TOML files and can select their
own `model` and `model_reasoning_effort`. OpenAI recommends
`gpt-5.6-terra` for lighter, read-heavy workers, and
`agents.max_concurrent_threads_per_session` caps open spawned threads. Codex
Ultra can delegate proactively. These features are documented in
[Codex Subagents](https://learn.chatgpt.com/docs/agent-configuration/subagents).

This is model-driven parallel delegation. It is not the same primitive as a
Claude workflow script whose runtime owns control flow and intermediate
variables.

The non-interactive gap is narrower than “Codex has no subagents,” but remains
material for Agent Studio:

- the subagent page says actions requiring a new approval fail in
  non-interactive flows;
- the [`codex exec` reference](https://learn.chatgpt.com/docs/non-interactive-mode)
  documents non-interactive sandboxing and event output, but contains no
  subagent invocation or lifecycle contract; and
- no CAR `codex exec` spawn-mode evidence is attached to this analysis.

Therefore this analysis does not claim that native Codex subagents fail under
`codex exec`; it records the integration as **undocumented and unverified**.
Until a CAR smoke test proves spawn, permission, token attribution, and terminal
result behavior, Codex work that needs task orchestration must be cut into
small `dependsOn` cards.

### Headless verification matrix

| Open item | Documentation result | Agent Studio decision |
|---|---|---|
| Do Claude workflow children inherit a headless parent's yolo mode cleanly? | **No.** The workflow launch itself skips approval under `claude -p`, but workflow children always use `acceptEdits` and inherit the tool allowlist. With no interactive user, unallowlisted tools follow configured permission rules and cannot obtain a fresh approval. | Require a project-specific CAR allowlist smoke test before routing a workflow-sized card to Claude. |
| Do Codex subagents work under CAR's non-interactive `codex exec` spawn mode? | **Not established.** The subagent page discusses failure of approval-requiring actions in non-interactive flows, but its CLI trigger instructions name an interactive session. The `codex exec` page does not define subagent spawn events, lifecycle, attribution, or final-result behavior. | Treat the CAR integration as unverified, not unsupported. Use `dependsOn` cards until a recorded smoke test closes the gap. |

These are documentation-verification results as of the analysis date, not
claims from absence alone: both conclusions name what the current vendor pages
do and do not guarantee. A CAR smoke test can supersede the conservative Codex
policy without changing the distinction between Codex subagent parallelism and
Claude script-owned workflows.

## 2. Evidence and Cost Model

### Measured Agent Studio baseline

The primary source is Agent Studio's 2026-07-25
[pipeline-time-economy evidence](https://github.com/agent-orc/agent-studio/blob/2e5b52cf06eb11db7ef053a5ddc45d0a26414dfa/docs/quality/pipeline-time-economy/brief.md).
It aggregated per-step ledgers from 847 tasks and 2,396 attempts. The companion
[analysis page](https://github.com/agent-orc/agent-studio/blob/2e5b52cf06eb11db7ef053a5ddc45d0a26414dfa/docs/quality/pipeline-time-economy/index.html)
reports:

| Observed quantity | Evidence |
|---|---:|
| Coding (`core-agent-run`) tokens | 1.89 billion, 98.1% |
| Four review aspects | about 36 million, about 1.9% |
| Deterministic test, git, and lint tokens | 0, a true zero |
| Total recorded compute | 29,808 minutes |
| Coding share of compute | 19,911 minutes, 66.8% |
| Test-gate share of compute | 4,949 minutes, 16.6% |
| Review aspects plus grade | 2,450 minutes, 8.3% |
| Test-gate repetition | 1,954 runs across 535 gated tasks, 3.65 runs/task |
| Test-gate time occurring on retries | 84% |

The source estimates the 82 CPU-hours of tests at about $4 on a throwaway cloud
vCPU, or around 0.05% of an order-of-magnitude $8,400 token cost. The dollar
comparison assumes blended token prices and is not a subscription bill. The
hard conclusion is the scale separation: **about 98% of tokens sit in coding
runs, while test CPU is negligible as a money cost**. Gate time still matters
for latency and throughput.

The measured review bucket is the four review-aspect LLM steps. The corpus does
not isolate the newer, separately fenced review executor as its own universal
per-card token constant. Treat 1.9% as the observed review-pipeline share, not
as a guaranteed cost for every future review attempt; retain the review CLI's
own usage in new workflow-versus-card experiments.

Retries amplify every stage. The same corpus observed 978 adapted reissue
prompts across 437 tasks, while the checked-in
[30-card Agent Studio backtest](../../results/complexity-backtest/agent-studio-30-card-backtest.md)
contains 98 token entries and a raw total of 300,740,433 tokens. Seven
single-entry cards had a median raw total of 886,074 tokens; 23 multi-entry
cards had a median card total of 6,676,326. Those raw Codex totals overcount
cached input and must not be converted to dollars, but they do show that
multi-attempt histories dominate fixed one-shot overhead. See the accounting
qualification in
[Token Cost Dynamics](long-vs-short-session-cost.md#53-codex-one-shot-and-card-aggregates).

### What repeats

| Cost component | `k` small cards | One workflow-sized card | Meter |
|---|---|---|---|
| Claim and worktree materialization | `k` times | once externally | Operations/latency; no retained token estimate |
| Coding-agent setup and task context | at least once per card and again on retries | parent setup plus setup for every workflow worker | Tokens; part of the dominant 98.1% coding bucket |
| Review CLI / LLM review | at least once per delivered card and again when the pipeline reopens it | one outer review, plus any internal workflow reviewers | Tokens; measured aspect-review share about 1.9%, with the coverage caveat above |
| Deterministic gate | per card attempt | per outer big-card attempt, usually over a larger tree | CPU/time; measured token count zero |
| Merge and attribution | `k` integration events and `k` task ledgers | one outer integration event and one task ledger | Operations and evidence quality |
| Orchestration | dependency scheduling and durable hand-offs | script planning, worker fan-out, intermediate results, and synthesis | Workflow-only token overhead |

### Variables

For a proposed body of work cut into `k` slices, let:

- `F` be fixed non-model lifecycle work per outer card: claim, worktree
  materialization, review dispatch, gate dispatch, merge, and attribution;
- `S_i` be repeated model context/setup tokens for card `i`;
- `C_i,a` be coding tokens for slice `i` on attempt `a`;
- `R_i,a` be outer review tokens for that attempt;
- `G_i,a` be deterministic gate CPU/time for that attempt;
- `A_i` be the number of attempts for card `i`;
- `W_plan`, `W_workers`, and `W_synth` be workflow planning, worker, and
  synthesis/verification tokens; and
- `A_B` be outer retries of one big card.

For small cards:

```text
Tokens_small
  = sum over cards i and attempts a of (S_i + C_i,a + R_i,a)

Operations_small
  = kF + sum(G_i,a)
```

For one workflow-sized card:

```text
Tokens_big
  = sum over big-card attempts of
      (S_B + C_parent + W_plan + W_workers + W_synth + R_B)

Operations_big
  = F + sum(G_B,a)
```

The big card is token-cheaper only when:

```text
avoided repeated setup + avoided outer reviews + avoided coding retries
  >
workflow planning + duplicated worker context + synthesis
  + extra tokens caused by big-card retries
```

Equivalently, the fixed lifecycle saving is `(k - 1)F`, but much of `F` is
operations rather than tokens. The evidence bounds the visible token part:
outer review-like work was only about 1.9% of the measured corpus, whereas one
additional coding attempt touches the 98.1% bucket. Dynamic workflows can also
use meaningfully more tokens than a single conversation because every worker
does its own model and tool work; Anthropic explicitly recommends trying a
small slice before a large fan-out.

Do not plug the corpus averages into a card estimate as if they were constants.
They are observational ratios from one Agent Studio population. Measure
`W_plan + W_workers + W_synth`, outer review tokens, and retry outcomes on the
first workflow cohort, then update the break-even estimate.

## 3. Effectiveness Comparison

| Dimension | One large Claude workflow card | Small cards with `dependsOn` |
|---|---|---|
| Review granularity | Agent reviewers can inspect files or slices internally, but Agent Studio receives one outer subject and one large combined diff. Cross-slice omissions are easier to hide. | Each acceptance contract and diff is reviewed in isolation. Findings point to one bounded delivery. |
| Attribution | Studio attributes the outer run, review, gate, and merge to one card. Per-worker usage visible inside Claude's workflow view is not automatically a per-card Studio ledger. | Task key, run history, outcome, review, gate, and merge all identify the responsible slice. |
| Merge-conflict surface | One outer integration merge. Internal agents can still collide when they write overlapping files; workflows do not make shared writes conflict-free. | `k` integration merges and more opportunities for a later card to conflict with already-merged work. Sequential dependencies reduce concurrent collisions but add queueing. |
| Failure isolation | A failed outer gate or cross-slice regression can reopen the whole card. A workflow can reuse completed agent results when resumed in the same Claude session, but exiting Claude starts the workflow fresh. | Only the failed slice is retried. Already accepted dependencies remain durable and reviewable. |
| Steering points | No mid-run operator input other than permission prompts. Stop/resume is possible; sign-off between phases requires separate workflows. | Every card boundary is a natural operator decision, reprioritization, or cancellation point. |
| Context and coordination | The script keeps intermediate results out of the parent context and can encode loops and cross-checks. Worker startup and duplicated context cost tokens. | Each card starts from a durable brief and repository state. Hand-offs cost context, but ownership and contracts stay explicit. |
| Parallelism | Strong when slices are independent, bounded, and path-disjoint. Poor when agents need a changing shared interface. | `dependsOn` expresses necessary serialization; independent leaf cards can still run in parallel as separate cards. |

The central trade is therefore:

- a big workflow reduces outer lifecycle repetitions and integration merges;
- small cards reduce review size, failure blast radius, and attribution
  ambiguity.

## 4. Decision Rubric

### Cut one big card with a dynamic workflow

Choose this only for a **Claude-capable** run when all or nearly all are true:

1. The task contains at least three repeatable, independent slices such as
   audits, file-local migrations, or parallel research.
2. Agents can own disjoint paths or read-only questions; they do not need to
   negotiate a changing shared interface.
3. A single deterministic end-to-end gate validates the combined result.
4. The expected combined diff is still reviewable, or the workflow produces
   per-slice evidence and performs an explicit final integration review.
5. No product or architecture decision needs operator sign-off mid-run.
6. The project's `claude -p` workflow and tool allowlist have passed a
   headless smoke test.
7. The expected saving in repeated coding context and retries is larger than
   worker fan-out and synthesis cost. Avoided claim/merge mechanics alone are
   not a sufficient token argument.

Start with a `small` workflow-size guideline or one directory. Record agent
count, per-agent tokens, outer attempts, final diff size, and review findings
before widening the pattern.

### Cut small cards with `dependsOn`

Choose small cards when any of these apply:

- the route may select Codex today;
- one slice defines an API, schema, migration, or decision needed by the next;
- acceptance can be proved independently per slice;
- the operator needs steering or go/no-go points;
- a failed slice should not reopen completed work;
- attribution, auditability, or review precision matters more than one fewer
  merge;
- paths overlap heavily or integration behavior is uncertain; or
- the combined diff would exceed a reviewer's practical attention budget.

Do not make cards so small that each contains only mechanical ceremony. A good
small card owns one coherent outcome with its own acceptance test and creates a
durable dependency for the next card.

### Hybrid patterns

1. **Workflow planning, small implementation:** one Claude planning card fans
   out exploration and competing designs, then produces a cut plan and
   creates/spawns small implementation cards with explicit dependencies.
2. **Small leaves, explicit integration:** parallel leaf cards own disjoint
   components; one final integration card depends on all leaves and owns the
   end-to-end gate.
3. **One implementation owner, cheap supporting agents:** keep one card and one
   writing agent, while cheaper subagents perform read-only discovery, tests,
   or review. This gains context isolation without concurrent write conflicts.

The first pattern is the safest default for ambiguous large work: use dynamic
workflows to improve the cut, not to erase card-level control.

## 5. Routing Hook for TE-8

TE-8 must treat workflow capability as a routing constraint, not as a prompt
decoration.

Add or derive a capability equivalent to
`supportsScriptedDynamicWorkflow`. A workflow-sized card may route only to a
Claude Code run with:

- a supported version and workflows enabled;
- a headless activation path that does not rely on the interactive-only
  `ultracode` keyword behavior;
- a tested tool allowlist/permission configuration; and
- workflow telemetry retained for cost review.

If the chosen or available provider is Codex, the cutter must replace the large
workflow requirement with small `dependsOn` cards or fail closed and request a
recut. It must not silently run the whole large card as one Codex agent.

Keep two routing decisions separate:

1. **Can this run execute scripted workflow orchestration?** Currently Claude
   only for this policy.
2. **Which model should each worker use?** Both Claude and Codex can route
   lighter subagent work to cheaper models without `ultracode`.

Record the requested cutting mode, actual provider/CLI, workflow activation,
agent count, worker models, per-agent tokens, outer attempts, and fallback
reason. Without those fields, TE-8 cannot learn whether workflow-sized cards
saved cost or merely hid it inside one task total.

## 6. Feed-Forward: AI Patterns Candidate

> **Pattern candidate: Workflow-sized tasks**
>
> Use one workflow-sized card when a Claude-capable run can split a coherent
> goal into independent, path-disjoint work, keep intermediate results in a
> scripted workflow, and prove the combined result with one end-to-end gate.
> Prefer small dependency-linked cards when the work is sequential, the
> provider is Codex, the operator needs steering points, or failure and review
> must be isolated by slice. Do not justify a large card by pipeline ceremony
> alone: Agent Studio evidence places about 98% of tokens in coding runs and
> about 2% in review, with deterministic test CPU negligible in money terms.
> The economic break-even is reached only when avoided repeated coding context
> and retries exceed workflow planning, worker fan-out, synthesis, and the
> larger retry blast radius. A safe hybrid uses a Claude workflow to produce
> the cut, then executes and reviews small implementation cards.

## Sources and Limitations

- [Agent Studio pipeline-time-economy brief](https://github.com/agent-orc/agent-studio/blob/2e5b52cf06eb11db7ef053a5ddc45d0a26414dfa/docs/quality/pipeline-time-economy/brief.md),
  snapshot 2026-07-25 at source revision `2e5b52c`: 847 tasks, 2,396
  attempts, token and compute shares.
- [Agent Studio pipeline-time-economy analysis](https://github.com/agent-orc/agent-studio/blob/2e5b52cf06eb11db7ef053a5ddc45d0a26414dfa/docs/quality/pipeline-time-economy/index.html):
  per-step totals, retry amplification, and the qualified CPU/token dollar
  comparison.
- [Agent Studio 30-card complexity backtest](../../results/complexity-backtest/agent-studio-30-card-backtest.md),
  generated 2026-07-25: attempt proxy and raw card-token distribution.
- [Token Cost Dynamics](long-vs-short-session-cost.md): component accounting,
  caching qualification, and why raw Agent Studio Codex aggregates must not be
  priced.
- [Claude Code dynamic workflows](https://code.claude.com/docs/en/workflows)
  and [Claude subagents](https://code.claude.com/docs/en/sub-agents): current
  workflow, headless, cost, resume, and permission behavior.
- [Codex Subagents](https://learn.chatgpt.com/docs/agent-configuration/subagents)
  and [`codex exec`](https://learn.chatgpt.com/docs/non-interactive-mode):
  current native subagent configuration and the documented non-interactive
  boundary.

No controlled Agent Studio experiment has run the same task both ways.
Accordingly, the formulas are a decision model, not a causal claim that one
cutting strategy saves a fixed percentage.
