# Human-friendly language capability

Token Economy records how plainly, warmly, directly, and concretely a model
writes, without habitual assistant phrasing. Voice Lint owns the rubric,
prompts, blind benchmark, judging, and operator spot checks. Token Economy owns
the dated catalogue, validated intake, typed constraints, and publication.

The capability is evidence about a particular model, language, and thinking
level. It does not certify Easy German, accessibility, factual correctness in
all domains, or suitability for a correctness-critical task.

## What the first catalogue establishes

The initial snapshot is dated **2026-09-25**. It contains 22 provisional
research records: German (`de`) and English (`en`) for each of the 11 canonical
models in the shared Voice Lint contract. Five primary publications were
verified on that date. They support the evaluation method, but none supplies
comparable measurements of all six dimensions for those exact models.

Scores, overall quality, and sample costs therefore remain **null**, and the
thinking level is `unspecified`. A null is unknown, not a failing score or a
free sample. We do not transfer GPT-4o findings to GPT-6, convert preference
win rates to readability scores, turn prose claims into decimal estimates, or
assume English results apply to German. These records cannot pass a routing
threshold, even when provisional evidence is allowed. The first measured
rankings will appear when Voice Lint results are imported.

## Six dimensions and aggregation

All measured values are in `[0, 1]`; higher is better. Keep their interpretation
aligned with Voice Lint's retained rubric:

| Dimension | What the assessment should check |
| --- | --- |
| `readability` | Familiar words, manageable sentences, and comprehension for the intended reader. |
| `warmth` | Respectful, natural consideration without flattery or excessive reassurance. |
| `directness` | Answers and actions appear promptly; concrete wording avoids evasive padding. |
| `absenceOfAiIsms` | Few stock openings, canned summaries, forced contrasts, or other assistant mannerisms. |
| `toneAdherence` | Follows the requested voice, audience, formality, and language. |
| `factualRestraint` | Preserves source meaning, limits claims to available evidence, and expresses uncertainty accurately. |

For shared contract version 1, the documented weighted mean is
`overall = sum(score[d] * 1) / 6`: all six weights are 1. This is an intake
convention, not an empirically optimized weighting. The original shared shape
has no weight field; its omission means equal weights. An optional
`method.weights` object may state six equal positive weights explicitly.
Unequal weights require an agreed new contract version rather than silently
making scores incomparable. The importer checks the mean with absolute
rounding tolerance `0.000001`. Missing dimensions cannot produce an overall.
A real score or cost of zero is valid in an observed run.

An overall threshold and every supplied dimension threshold must **all** pass.
A high warmth score cannot compensate for a failed factual-restraint floor.
The catalogue carries the six dimensions, weights, and append-only observations
in [language-capabilities.json](../src/TokenEconomy/catalog/language-capabilities.json),
validated by its [adjacent schema](../src/TokenEconomy/catalog/language-capabilities.schema.json).

## Primary study inventory

All five sources below were read and verified on **2026-09-25**. Their
catalogue status is `provisional` because application to the tracked models
has not been measured. Publication peer review is distinct from local
capability evidence status.

| Study | Primary URL | Finding used in the method |
| --- | --- | --- |
| Guo, August, Leroy, Cohen & Wang (EMNLP 2024), *APPLS* | [ACL Anthology](https://aclanthology.org/2024.emnlp-main.519/) | A single metric did not cover all plain-language criteria; use a suite of checks. |
| Agrawal & Carpuat (TACL 2024), *Do Text Simplification Systems Preserve Meaning?* | [ACL Anthology](https://aclanthology.org/2024.tacl-1.24/) | Simpler outputs can lose information needed for comprehension; assess meaning preservation independently. |
| Dubois, Galambosi, Liang & Hashimoto (COLM 2024), *Length-Controlled AlpacaEval* | [Author paper on arXiv](https://arxiv.org/abs/2404.04475) | Automatic preference judges can reward verbosity; control length when comparing responses. |
| Ibrahim, Hafner & Rocher (Nature 2026), *Training language models to be warm can reduce accuracy and increase sycophancy* | [Nature](https://www.nature.com/articles/s41586-026-10410-0) | Warmth tuning can compromise accuracy; warmth alone is insufficient. |
| Jablotschkin, Teich & Zinsmeister (LTEDI 2024), *DE-Lite* | [ACL Anthology](https://aclanthology.org/2024.ltedi-1.9/) | Genre affects Easy/Standard German differences; retain German and genre-specific evaluation. |

These are methodological takeaways, not model rankings. The proposed intake
method combines blinded comparisons, shuffled presentation order, identical
prompts, Voice Lint rules, and human spot checks. Sample counts, raters, rubric,
and prompt-set references are retained from the producing study. `observed`
means an actual run was supplied; it does not assert statistical significance,
independent replication, or that all model comparisons used identical prompts.

## Import the shared Voice Lint result

The producer writes `benchmarks/human-friendly-language/results.json` in its
repository. The complete structural contract is
[language-evidence-results.schema.json](../src/TokenEconomy/catalog/language-evidence-results.schema.json).
It accepts the schemaVersion 1 shape agreed with Voice Lint, including its
method, ordered six dimensions, result rows, and study inventory.

Each result needs a canonical id, matching CLI (`codex` or `claude-code`),
model-supported concrete thinking level, `de` or `en`, six numeric scores,
consistent overall, nonnegative USD sample cost, and a run reference of the
form `benchmarks/human-friendly-language/runs/<runId>/`. Notes may be empty.
The method requires a positive sample count, rubric reference, unique supported
languages, rater description, and prompt-set reference. Each row's language must
appear in the method. One model/language/effort pair may appear once per file.
Run identifiers may contain letters, digits, dots, underscores, and hyphens,
but must begin with a letter or digit; traversal paths are rejected.

Install the schema-validation dependency in your Python environment, then run:

```bash
python3 -m pip install -r scripts/requirements-language.txt
npm run catalog:import-language-evidence -- \
  /path/to/voice-lint/benchmarks/human-friendly-language/results.json \
  --source-repository https://your-host/your-org/voice-lint --check

# Remove --check to retain the evidence and update the catalogue and text priors.
npm run catalog:import-language-evidence -- \
  /path/to/voice-lint/benchmarks/human-friendly-language/results.json \
  --source-repository https://your-host/your-org/voice-lint

python3 scripts/generate-website-data.py
python3 -m unittest discover -s scripts -p test_language_evidence.py
python3 scripts/generate-website-data.py --check
python3 scripts/check-website-links.py
dotnet test
```

`python3 scripts/import-language-evidence.py` is equivalent to the npm command;
Node is only a task-name convenience. `--source-repository` is required because
the shared file contains no repository URL. Use the actual repository URL;
paths in run references are relative to it, not to Token Economy or its website.
The importer does not fetch or independently re-run the source benchmark.

Validation finishes before any output is changed. The importer then:

1. Retains the source file's **exact bytes** at
   `src/TokenEconomy/catalog/language-evidence/<sha256>.json`. This mirrors all
   method details, study metadata, scores, notes, and run references.
2. Appends `observed` result rows with source repository, fixed source path,
   full SHA-256, mirror path, and original run reference. The source only has a
   date, so `observedAtUtc` is that date at UTC midnight, explicitly day precision.
3. Keeps previous observations and research seeds. The same bytes and origin
   import idempotently; conflicting provenance or a changed score/cost for an
   existing model/language/effort/run reference is rejected. A new run is a new
   observation, not an edit to historical evidence.
4. Copies producer study metadata with `unverifiedClaim` and a null verification
   date until independently reviewed. Study ids are namespaced by import hash.
5. Recomputes the eight text priors (four work kinds × two languages), naming
   qualifying models, evidence status, record id, effort, and measured cost.
   No qualifying evidence produces an explicit empty set.

Each file replacement is atomic. The multi-file update is recoverable, not a
cross-file transaction: the immutable mirror is written first, followed by the
catalogue and priors. Repeating the import repairs an interrupted projection.
Run a single importer at a time. Publication rechecks the mirror hash, validates
its shared contract, checks the projected scores/run/date against the mirror,
and refuses stale priors. It does not reprice a historical measured sample.

## Typed lookup and routing

See [Human-friendly language capability](model-routing-api.md#human-friendly-language-capability)
for complete API examples. `LanguageCapabilityCatalog.Default.Find` accepts a
model id or typed `ModelId`, language, optional thinking level, and optional UTC
cutoff. Model aliases use the existing price catalogue. Inspection can omit
effort; routing always supplies the candidate's actual effort.

For each model/language/effort, lookup prefers `observed` over `provisional`
over `unverifiedClaim`, then the newest observation, then ordinal record id for
a deterministic same-date tie. Future evidence is excluded. Thresholds are
checked **after** selection: an older high score cannot hide a newer low score.
The complete `Records` collection retains all history for inspection.

`HumanFriendlyLanguageRequirement` needs a language and at least one threshold.
It accepts an overall floor, any subset of dimension floors, optional concrete
thinking level, and an explicit `AllowProvisional` switch (default false).
Unverified claims never qualify. Unknown required values fail even a zero
threshold. A requirement without an effort uses the matrix's established
suggestion; specifying an effort requests that exact measured configuration.
If clamping changes it, it fails the requirement rather than borrowing scores.
An explicit `EvaluateModel` effort takes precedence and must still match.

`SuggestModel` filters by the constraint and retains its CLI/status/workflow
checks; underpowered task matches are excluded when language is constrained.
For `Tight` and `Critical`, passing candidates sort by actual evidence-run cost
per sample, known costs first, with the existing compatibility order breaking
cost ties. `Comfortable` preserves the existing compatibility order among
passing models. Calls without the constraint retain all previous behavior.
`EvaluateModel` returns null for a failing constraint. A passing suggestion
includes the exact `LanguageCapability` record and a dated rationale.

Sample cost varies with prompt length, thinking, language, tool use, metering,
and evaluation setup. Comparing unlike runs is an operator judgment; the
shared contract records the method but cannot prove comparability. No
confidence interval, success-adjusted cost, or evaluation overhead is inferred
from its aggregate `costPerSampleUsd` field. Voice Lint should document the
metering scope in its rubric/run notes.

The text priors live in `task-class-recommendations.json` under
`languagePriors`, exposed by `TaskClassRecommendationCatalog.LanguagePriors`
and `RecommendLanguage(TextWorkKind, language)`. Copy, documentation, operator
messages, and replies retain the existing `TaskClass.DocEdit` classification.
Their initial requirement is overall/readability/absence-of-AI-mannerisms
`≥ 0.75`, factual restraint `≥ 0.80`, medium effort, observed evidence. These
thresholds are explicit working requirements, not conclusions from the papers.
They do not mutate the older task recommendation sets or the risk taxonomy.

A language score is a **constraint**, not a capability-tier change. Establish
the concrete task's correctness floor through `ModelRoutingPolicy`/`ModelRouter`
first; a compatibility candidate cannot authorize a downgrade or invent a
provider fallback. For a chosen route, evaluate its exact model/effort with the
constraint. If no policy-qualified route passes, return a visible wait or seek
better evidence. The machine policy and its `policyVersion` are unchanged.

## Publication and maintenance

The [website page](https://agent-orchestrator.dev/token-economy/human-friendly-language/)
reads generated `website/data/language-capabilities.json`. It shows the matrix,
score and cost unknowns, effort/language filters, evidence kinds and dates,
study inventory, text priors, and an interactive cost/quality threshold table.
USD per quality point is `costPerSampleUsd / overall` when both are known and
overall is positive; zero or unknown quality has no ratio.

Shared page tokens and components live in `website/evidence-page.css`, also
used by the task-study page. Source strings render through text nodes rather
than HTML. Tests cover schema rejection, provenance and historical intake,
threshold behavior, cost order, stable ties, and generated-data parity. Browser
checks exercise desktop/mobile, light/dark, filters, evidence expansion, error
states, and a synthetic measured-data fixture without publishing fake scores.
