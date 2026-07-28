# Security policy

## Supported versions

TokenEconomy is pre-1.0. Security fixes are provided for the latest released
minor version. Older minor versions are not supported.

| Version | Supported |
| --- | --- |
| 0.2.x | Yes |
| < 0.2 | No |

## Reporting a vulnerability

Do not open a public issue for a suspected vulnerability. Use
[GitHub's private vulnerability reporting flow](https://github.com/agent-orc/token-economy/security/advisories/new)
to send the maintainers a report.

Include the affected version, impact, reproduction steps or a proof of concept,
and any known mitigations. The maintainers will acknowledge the report through
the advisory and use that private thread for follow-up and disclosure
coordination.

## Scope notes

The library core is dependency-free and performs no network, filesystem, or
process access on its pricing and selection paths, so its exposure is mostly
about the data it returns being trusted downstream. Reports in these areas are
in scope:

- **Cost or price data that is silently wrong** — a resolved price applying to
  the wrong `ValidFrom` window, or an unknown/unpriced model yielding a total
  instead of an explicit `null` status. A silent `$0` is a security-relevant
  defect here, because callers use these totals for budget admission.
- **Selection results that leak restricted models** — `SuggestModel` returning a
  restricted or deprecated model, or a model whose CLI was not in
  `availableClis`.
- **Untrusted input handling** in the parts that do read external data: the
  Agent Studio `task.json` importer, the benchmark and document-to-text corpus
  readers, and the embedded catalog JSON deserialization.
- **Anything that would cause the published package to ship unintended
  content**, including the release workflow and its Trusted Publishing setup.

Model prices in the seeded catalog are published list prices and are not
secrets. A price that is merely out of date is a normal bug — open a regular
issue for it.
