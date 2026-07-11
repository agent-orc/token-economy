# TE-3 results — Token Economy website + documentation

Task: give the project its own site with proper docs at
**<https://agent-orchestrator.dev/token-economy/>**, plus the CI to deploy it and
an operator note for the hosting side.

## What shipped (in this repo)

| Deliverable | Path | Notes |
| --- | --- | --- |
| Static, self-contained site | [`website/index.html`](../website/index.html) | Plain HTML, no build step (runner-website pattern). Inline CSS + data-URI favicon → **no external requests**. English, light/dark theme-aware, responsive. |
| Site content | ↳ same file | Hero + tagline "token economics for LLM coding agents", what/why, **pricing-history explainer with a worked example**, **cost API** usage, **SuggestModel** preview, **install**, and **family links**. |
| Folder docs | [`website/README.md`](../website/README.md), [`website/DEPLOY.md`](../website/DEPLOY.md) | Excluded from the published site by the deploy rsync. |
| Deploy CI | [`.github/workflows/deploy-website.yml`](../.github/workflows/deploy-website.yml) | Adapted from the runner repo; on push to `main` touching `website/**` it `rsync`s the site into **`/srv/sites/token-economy/`** on the VM over SSH (host-key pinned). Also `workflow_dispatch`. |
| README pointer | [`README.md`](../README.md) | Links the docs/website. |

## Operator step (hosting side — not in this repo)

The Caddy route + `sites.json` entry live in the **private** meta-repo
`agent-orchestrator-website`, and the four deploy secrets come from the VM owner.
That one-time manual step is written up here:

➡ **[`operator-step-hosting.md`](operator-step-hosting.md)**

No credentials, hostnames, or real VM paths are guessed anywhere in this repo —
per the task constraint.

## Verification — the docs describe *real* code

Every code example on the page was run **verbatim** on the shipped API
(`net10.0`, SDK 10.0.301) and its output checked against what the page claims.
Full log: [`verification-output.txt`](verification-output.txt).

- Pricing history: `ComputeCost("claude-sonnet-5", 1M in + 200K out, …)` →
  **$4.00** on the introductory rate, **$6.00** from the `2026-09-01` standard
  rate — exactly the page's table and code comments.
- Cost API: `ComputeCost("claude-opus-4-8", 250K in + 12K out + 40K cache-read)`
  → **1.57 USD**, `HasPrice = true`; unknown/unpriced models return
  `UnknownModel` / `NoPriceForDate` with `Total = null` (never a silent $0).
- SuggestModel: `SuggestModel(Feature, Tight, [Cli.Claude], now)[0]` prints the
  page's quoted line **character-for-character**:
  `claude-sonnet-5 @ Medium — claude-sonnet-5: balanced tier, an ideal match for feature work; standard cost — moderate spend under tight pressure. Suggested effort: medium.`
- Library test suite: **126 passed, 0 failed**.

## Rendered evidence (`--real`)

Screenshots of the **actual committed `website/index.html`** rendered by headless
Chrome — nothing mocked (the site makes no API calls; it is fully static):

| View | Image |
| --- | --- |
| Desktop (1280px, full page) | [`website-desktop--real.png`](website-desktop--real.png) |
| Mobile (390px, full page) | [`website-mobile--real.png`](website-mobile--real.png) |

The mobile shot confirms the responsive layout (pillars stack, tables scroll,
the sticky header stays on one line and its section-nav scrolls internally
instead of overflowing the body).

## Style-guide compliance

Callouts, pillars, and family cards use **full symmetric 1px borders** — no
coloured left accent bars/lines on any card, panel, row, banner, or pill group
(the style hard-rule). The lone deliberate exception is a neutral **dashed**
border on the "this library" family card, used only to mark the current site.
