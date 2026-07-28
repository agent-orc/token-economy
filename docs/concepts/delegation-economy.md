# Delegation economy

Delegation economy assigns each unit of work to the least expensive model that
can complete it reliably. A capable model keeps responsibility for planning,
decomposition, integration, and risk decisions; cheaper models execute bounded
work whose result can be checked cheaply. The goal is not to minimize the price
of every call. It is to minimize the expected cost of a correct result.

The pattern applies at three levels:

1. An operator session can spawn cheaper subagents for bounded work.
2. A CodingAgentRunner run can spawn small agents for simple subtasks.
3. A task card can pin a cheaper model when the whole card is narrow and
   mechanically verifiable.

At every level, the delegating agent or operator remains accountable for
defining the boundary, supplying the required context, and validating the
result.

## Model tiers

| Tier | Use it for | Typical work |
| --- | --- | --- |
| **Orchestrator** | Ambiguous, high-impact, cross-cutting, or hard-to-verify decisions | Decomposition, architecture, risk analysis, conflict resolution, final integration |
| **Implementation** | Well-scoped changes that require local reasoning | A contained feature, bug fix, test design, or documentation that synthesizes several sources |
| **Mechanical** | Deterministic, repetitive work with an objective check | File sweeps, formatting, inventory, simple test execution, link checks, or a first-pass doc draft |

These are capability roles, not permanent model labels. Map currently available
models to the tiers using measured quality, tool support, latency, and price.
When evidence is weak, choose the higher tier.

## When to delegate down

Delegate only when all of the following are true:

- The subtask has a narrow boundary and an explicit deliverable.
- The necessary context can be passed without transferring the entire session.
- Completion can be verified with a test, diff, schema, checklist, or similarly
  inexpensive review.
- A failure cannot silently cause an irreversible or high-impact action.
- Delegation overhead is smaller than the expected saving.

Keep the work at the orchestrator tier when it changes architecture, interprets
unclear intent, handles credentials or destructive actions, spans tightly
coupled components, or depends mainly on judgment. An implementation agent can
split off its own mechanical checks under the same rules.

## Cost rationale

A cheaper call saves money only if retries, review, context transfer, and
integration do not consume the difference:

`expected cost = execution + context transfer + verification + retry risk + integration`

This favors small, independent assignments with objective acceptance checks.
It disfavors delegating tiny tasks whose setup costs more than direct execution,
as well as broad tasks that require repeatedly reconstructing the
orchestrator's context. Parallel delegation can reduce elapsed time, but only
for subtasks that do not contend for the same files or decisions.

## Failure modes and escalation

- **Cheap-agent loop:** repeated retries, repeated clarification, or the same
  failed check indicate that the task was placed too low. Stop the loop,
  preserve the evidence, and escalate one tier.
- **False economy:** review and repair cost more than the model saving. Raise
  the default tier for that task class.
- **Context starvation:** the result is locally plausible but violates a
  constraint the agent never received. Tighten the prompt or keep the task with
  the context holder.
- **Unverifiable delegation:** correctness depends on subjective judgment or
  unavailable evidence. Use a more capable model before execution.
- **Fragmentation:** too many small agents duplicate discovery or produce
  conflicting edits. Increase subtask size or return integration work to one
  owner.
- **Capability mismatch:** the selected model lacks a required tool, modality,
  or context window. Select by capability first and price second.

This is model choice under uncertainty: start with the cheapest tier supported
by evidence, observe the result, and move upward when uncertainty or failure
signals exceed the cheap tier's bounds. Never keep a cheaper agent retrying
merely because its individual calls cost less.

## Reusable prompt rule

Prompts and task cards can include the standardized,
self-contained [`contexts/delegation-economy.md`](../../contexts/delegation-economy.md)
block verbatim. A host may add model names and prices around it, but should not
weaken its verification or escalation rules.
