# Delegation economy

Assign each subtask to the least expensive model tier that can complete it
reliably. Optimize for the expected cost of a correct result, including context
transfer, verification, retries, and integration—not the price of one call.

| Tier | Responsibility | Examples |
| --- | --- | --- |
| Orchestrator | Ambiguous, high-impact, cross-cutting, or hard-to-verify work | Plan, decompose, resolve conflicts, integrate, make risk decisions |
| Implementation | Bounded work requiring local reasoning | Contained features, bug fixes, test design, multi-source documentation |
| Mechanical | Deterministic, repetitive work with objective checks | File sweeps, formatting, inventories, test runs, link checks, first drafts |

Delegate down only when the boundary and deliverable are explicit, the required
context is small enough to pass, the result is cheap to verify, and failure
cannot silently cause an irreversible or high-impact action. The delegating
agent retains responsibility for validation and integration.

Use capability before price. If evidence for a lower tier is weak, choose the
higher tier. Keep architecture, unclear intent, destructive operations,
security-sensitive decisions, and tightly coupled cross-component work with the
orchestrator.

If a cheaper agent repeats a failure, asks for the same clarification, loops,
or produces an unverifiable result, stop retrying and escalate one tier with
the task context and failure evidence. Repeated repair or review cost means the
task class should default to a higher tier in the future.
