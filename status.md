# TE-64 status

Result: done
Implementation option: import the TE-62 catalogue from its salvage commit and retain TE-63 already on main; make Sonnet 5.5 selectable for explicit core-task pins, keep Haiku 5.5 limited to bounded pins, and add model-specific Claude Code version gates. Claude Code documents Sonnet 5.5 minimum 2.1.284 and Haiku 5.5 minimum 2.1.293; the operator's 2.1.305 runner probes support both. Default routes, correctness floors, provider fallback, and migrations remain unchanged.
Implementation result SHA: `acaacf4eccf38381c054fe5ada0476b8d036402f` (follow-up on 6344621249; the first delivery's implementation was `8d8670aa6a67ace7ef8e00b4c58b2228efbefb8c`)

Release artifact: `TokenEconomy.0.3.8.nupkg` and `TokenEconomy.0.3.8.snupkg` in the shipped results directory.

Verification:
- `dotnet build --no-restore -c Release` and `dotnet build tests/TokenEconomy.Tests/TokenEconomy.Tests.csproj --no-restore -c Release` passed.
- `dotnet test tests/TokenEconomy.Tests/TokenEconomy.Tests.csproj --no-build -c Release --filter 'FullyQualifiedName~KnownModelsTests|FullyQualifiedName~ModelEfficiencyMatrixTests|FullyQualifiedName~ModelPriceProvenanceTests|FullyQualifiedName~ModelPriceSeedTests|FullyQualifiedName~ModelRouterTests|FullyQualifiedName~ModelRoutingKnowledgeBaseTests|FullyQualifiedName~October2026ModelOnboardingTests|FullyQualifiedName~Sonnet55SupportTests'` passed (152 tests).
- `dotnet test --no-build -c Release --filter 'FullyQualifiedName~.Architecture.|Category=Guard'` completed; no tests matched in the repository's test project.
- `python3 scripts/generate-website-data.py --check` passed.
- `dotnet pack src/TokenEconomy/TokenEconomy.csproj --no-build --no-restore -c Release -o "$JOB_RESULTS_DIR"` passed.
- `dotnet format style --verify-no-changes --severity error` passed.

## Follow-up round 2026-10-21

Review of 6344621249 reported two `ModelReleaseDateTests` failures and two documentation gaps.

- Release dates: every touched model already had a seeded date with a primary source (Haiku 5.5: Anthropic release notes, 2026-10-07; Gemini 4 Argon: Google Gemini API changelog, 2026-10-13; Gemini 3.8 Flash: Google Antigravity announcement, 2026-09-01; Sonnet 5.5: Anthropic model page, 2026-09-28). No date was invented or changed. The failures came from stale test expectations: the source-host allowlist had no Google hosts, and the newest-first order did not include the new models. The test now accepts `ai.google.dev` and `www.antigravity.google`, expects the new order, and checks that the `gemini-4-argon-high` alias and the Gemini 3.8 Flash 2026-09-02 price start leave publication dates unchanged. `docs/model-release-dates.md` now lists Gemini 3.8 Flash in date order.
- Documentation: the routing policy now documents the `google` provider and Antigravity CLI (`agy`) and the test-only `gemini-4-argon` and `gemini-3.8-flash` pins next to the Sonnet/Haiku 5.5 entries. The authority SHA-256, generated knowledge view and website routing data are refreshed. The 2026-10-16 snapshot now says `firstShippedIn` `0.3.8`, matching `Directory.Build.props` and the `[0.3.8]` changelog entry. The changelog compare links now include 0.3.8 and 0.3.7.
- Merged `origin/main` (1207248, already contained; the merge was a no-op). No rebase or squash.

Verification (agent-runner-01):
- `dotnet build --no-restore -c Release` passed.
- Whole TokenEconomy suite, as the operator asked: `dotnet test --no-build -c Release` passed 584/584 with 0 failures.
- `dotnet test --no-build -c Release --filter 'FullyQualifiedName~.Architecture.|Category=Guard'`: no tests match in this repository.
- `dotnet format style --verify-no-changes --severity error` passed; `python3 scripts/generate-website-data.py --check` passed.
- `dotnet pack src/TokenEconomy/TokenEconomy.csproj --no-build --no-restore -c Release -o "$JOB_RESULTS_DIR"` rebuilt `TokenEconomy.0.3.8.nupkg` and `.snupkg`.
