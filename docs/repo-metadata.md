# Repository metadata

Recommended settings for the `agent-orc/token-economy` GitHub repository. An
operator applies these in the GitHub repository settings — they cannot be set
from the repository contents.

## Description

> Token economics for LLM coding agents: model pricing with history, run-cost
> computation, and token-efficiency model selection (.NET).

Fits GitHub's 350-character limit and matches the `<Description>` in
`src/TokenEconomy/TokenEconomy.csproj`, so the nuget.org page and the GitHub
page say the same thing. If one changes, change both.

## Website

<https://agent-orchestrator.dev/token-economy/>

This is the repository's own static site, built from `website/` and deployed by
[`deploy-website.yml`](../.github/workflows/deploy-website.yml).

## Topics

- `dotnet`
- `csharp`
- `llm`
- `tokens`
- `pricing`
- `cost-management`
- `ai-agents`
- `coding-agents`
- `benchmarks`
- `nuget`

## Repository settings

- **Visibility:** public.
- **Private vulnerability reporting:** enabled (Settings → Code security). The
  [SECURITY.md](../SECURITY.md) flow links to the advisory form and depends on
  this being switched on.
- **Issues:** enabled — the issue templates in
  [`.github/ISSUE_TEMPLATE/`](../.github/ISSUE_TEMPLATE/) assume it.
- **Releases:** created by the tag-triggered
  [`release.yml`](../.github/workflows/release.yml); no manual step.

## Notes

Keep the description factual and free of superlatives. The website field points
to the token-economy page; the README links out to the wider Agent Orchestrator
ecosystem, so the topics do not need to carry the family relationship.
