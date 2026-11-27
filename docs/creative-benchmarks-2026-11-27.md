---
tags: [token-economy, evidence, research]
taggingStatus: tagged
---

# Creative benchmarks for design, UI, interactive 3D and games (TE-83, 27 November 2026)

This is the research record behind work class (i) and the UI class (b2) of the [effort dial](effort-dial.md). All sources were retrieved on **2026-11-27**. Evidence classes are as in the [17 Nov refresh](model-catalogue-refresh-2026-11-17.md): [M] third-party measurement with raw data pulled, [V] vendor, [L] list-price derivation, [P] unverified.

The catalogue rows live in [`creative-benchmark-types.json`](../src/TokenEconomy/catalog/creative-benchmark-types.json) and [`creative-benchmark-results.json`](../src/TokenEconomy/catalog/creative-benchmark-results.json). They use the TE-82 schemas with the new capability class `creativeBuild` and are loaded as `BenchmarkEvidenceCatalog.Creative`. They are kept out of `benchmark-results.json`, so coding projections and website pages are unchanged.

Catalogue rows exist only for models in the price catalogue (76 rows over 8 boards). The full raw extract, with every model on each board, is in [`analyses/creative-benchmarks-2026-11-27.json`](analyses/creative-benchmarks-2026-11-27.json). The source downloads and the extraction scripts are in the task result bundle.

**Scope of conclusions.** Nothing here is coding evidence, and no coding-benchmark conclusion is carried over. The "Sonnet 5.5 is not a cheap Opus 5.5" finding comes from Terminal-Bench 4.1 and applies to coding classes only. On creative work, the evidence below says something different. See the [conclusion scope table](effort-dial.md#which-classes-a-conclusion-applies-to).

## Benchmarks catalogued

| Benchmark | What is judged | Method | Date | Candidates and effort levels covered | Link |
| --- | --- | --- | --- | --- | --- |
| AA-3D v1.0 (Artificial Analysis) | Interactive three.js scenes and small games from a written brief | Human preference: recruited panel, blind pairwise, Bradley-Terry Elo with 95% sandwich interval | first observed 2026-11-27 | Claude and OpenAI at **max** only; Gemini at high or xhigh | [leaderboard](https://artificialanalysis.ai/evaluations/aa-3d), [methodology](https://artificialanalysis.ai/methodology/aa-3d), [Explorer](https://artificialanalysis.ai/3d/explorer) |
| WebDev Arena (Arena Code Arena: WebDev) | Web apps built from the requester's own description | Human preference: the requester votes A, B, tie or both bad; Bradley-Terry | snapshot 2026-11-23 | Opus 5.5 max; Sonnet 5.5 xhigh and high; Haiku 5.5 high; Fable 5.2 xhigh; 6.1 Sol, 6.1 Terra, 6.2 Sol and 6.2 Astra at max; Gemini 4 | [leaderboard](https://arena.ai/leaderboard/code/webdev), [dataset](https://huggingface.co/datasets/lmarena-ai/leaderboard-dataset) |
| Arena 3D (Code Arena, 3D-labelled battles) | Battles whose request explicitly asks for a 3D result | As WebDev, restricted by an LLM request classifier (about 96% agreement with a manual check) | launched 2026-11-24 (snapshot 2026-11-23) | Same as WebDev, including **Sonnet 5.5 high** | [post](https://arena.ai/blog/introducing-the-arena-3d-leaderboard), [leaderboard](https://arena.ai/leaderboard/code/webdev/3d) |
| Design Arena: 3D Design, Game Dev (HTML), Website (HTML) | Single-shot scenes, games and websites from user prompts | Human preference, blind head-to-head, Bradley-Terry | updated 2026-11-27 | Base model ids at the provider's **unpublished default effort** | [leaderboard](https://designarena.ai/leaderboard), [methodology](https://designarena.ai/methodology) |
| Design Arena: Agentic Game Dev (Three.js), Agentic Web Apps | Multi-file builds by coding agents | Human preference | updated 2026-11-27 | Opus 5.5 **high** and 6.1 Sol **max** (game dev); default effort (web apps) | same |

Checked but not catalogued, because they have no rows for our candidates:

- [GameCraft-Bench](https://tongxuluo.github.io/gamecraft-bench-website/): Godot projects scored by rubric. Its latest entries are July 2026, with Opus 4.7 and GPT-5.5 era models.
- [ArtifactsBench](https://artifactsbenchmark.github.io/): MLLM judge.
- [UI-Bench](https://uibench.ai/): compares text-to-app tools, not models.
- WebGameBench: the aggregator page returned 403, and the self-reported entries we saw predate the 5.5 models.

### Rubric mapping

The full mapping is in `creativeRubric.benchmarks`.

- **AA-3D** measures **design** and **game feel**: panelists must watch each scene for at least 30 s and can interact with it. It measures **originality** only in part, because the idea is mixed into the preference vote. **Technical execution is excluded by construction**: scenes that do not run are removed before the vote.
- **WebDev Arena** mixes design, requirement fit and interactivity. Broken builds are not filtered out and can get a "both bad" vote, so it measures technical execution in part. It does not measure originality.
- **Arena 3D**: according to Arena's own analysis, builds first have to run and move (a build that drew a 3D scene won 73% against one that did not), and then win on visual fidelity and art direction. Games are won mostly on UI, HUD and onboarding.
- **Design Arena**: design on every board, and game feel only on the game boards. No broken-build rule is published.

## AA-3D: verification of the owner's notes

Everything in operator note 3 was checked against the AA-3D page, the methodology page and the Explorer.

- **Method** [M]:
  - 55 tasks across 7 domains, for example a spinning black hole, a playable pinball table and the Colosseum.
  - Each model runs once per task in AA's open-source Stirrup harness, in an offline sandbox, for up to **3 h or 250 turns**.
  - Tools: three.js 0.186.0, Rapier, Blender 4.5, Python, Node, a headless Chromium and a CC0 asset library. Software WebGL rendering runs on 4 CPU cores and 8 GB.
  - Models run code and look at their own screenshots.
  - A recruited panel votes blind. Each scene must be viewed for **at least 30 seconds** before a vote, and panelists interact with both scenes.
  - Quality-check sessions are rejected and public votes are not counted. The board is based on 23K+ panel votes.
  - Ratings are Bradley-Terry with a 95% sandwich interval, anchored at GPT-6 Luna (Max) = 1000.
- **Claude rows** (all at max; AA labels them "Max, Default Fallback"). These match the owner's figures exactly:

  | Model | Elo | 95% interval |
  | --- | ---: | --- |
  | Fable 5.2 | 1271 | 1246–1297 |
  | Sonnet 5.5 | 1204 | 1176–1232 |
  | Opus 5.5 | 1202 | 1174–1231 |

  Sonnet and Opus are level within the interval, and Fable is ahead. **No rows exist for Sonnet 5.5 high or Opus 5.5 low.** AA's release lists name lower Claude levels, but AA-3D has only max rows. The source therefore gives no ranking for that pair; this is stored as a gap and not inferred.
- **Main limitation, confirmed.** Before voting, each scene must load and draw its first frame within 60 s in a software-rendered headless browser, keep rendering, show a non-blank image and run offline without errors. It then passes a manual review. Scenes that fail are excluded. **AA publishes no share of excluded scenes per model**, and that gap is recorded.

  As a proxy, TE-83 counted the scenes each model has *in blind voting* using AA's per-task coverage data ("N of M models have a scene for this task in blind voting"). The Explorer lists 54 of the 55 tasks. This is a valid-scene count *after* AA's check and manual review, not a playable rate. Our fleet still has to measure technical execution on its own creative cards.
- **Cost.** AA publishes per-model token counts for AA-3D (input, cacheable input, answer and reasoning) and list prices identical to our catalogue. It displays a cost-per-task chart, but only the cheapest three values appear as text: GPT-6 Luna $0.20, MiMo-V2.6-Pro $0.44, Claude Haiku 5.5 $0.49.

  The table derives cost per task [L] from the published token counts. Cache writes are not published per model, so the derivation is a **lower bound**; AA's own figures for Luna and Haiku are about 6–8% higher. This is the basis for "cheapest sufficient" in class (i).

  AA-3D cache-read shares (22–48% for the candidates) are much lower than the 74% measured in our coding fleet. That is why the cache-lever conclusion is not applied to class (i).

| AA-3D row | Elo | ± (95%) | Derived USD per task | Cache-read share of cost | Scenes in voting (of 54) | Median turns |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| GPT-6.2 Astra (Max) | 1318 | 31 | 13.91 | 41% | 54 | 100 |
| Claude Fable 5.2 (Max) | 1271 | 25 | 25.41 | 26% | 54 | 105 |
| GPT-6.2 Sol (Max) | 1259 | 28 | 3.43 | 39% | 54 | 92 |
| Gemini 4 Neon (Xhigh) | 1246 | 31 | 5.19 | 27% | 51 | 58.5 |
| Claude Sonnet 5.5 (Max) | 1204 | 28 | 12.91 | 47% | 46 | 160 |
| Claude Opus 5.5 (Max) | 1202 | 28 | 19.23 | 48% | 49 | 137 |
| Gemini 4 Carbon (High) | 1200 | 23 | 6.23 | 28% | 54 | 56 |
| Gemini 4 Hydrogen (Xhigh) | 1196 | 29 | 3.22 | 37% | 52 | 75 |
| GPT-6 Astra (Max) | 1168 | 28 | 17.00 | 41% | 53 | 60 |
| Gemini 4 Argon (High) | 1147 | 30 | 4.98 | 26% | 52 | 46 |
| Claude Fable 5.1 (Max) | 1139 | 28 | 28.00 | 33% | 50 | 138.5 |
| GPT-6 Sol (Max) | 1131 | 27 | 3.24 | 55% | 54 | 73 |
| GPT-6.1 Sol (Max) | 1121 | 28 | 3.25 | 22% | 52 | 56 |
| GPT-6 Luna (Max) | 1000 | anchor | 0.19 | 51% | 53 | 67 |
| Claude Haiku 5.5 (Max) | 998 | 27 | 0.46 | 43% | 50 | 57 |

Gemini 4 Neon is not in the price catalogue, so it is listed here and in the raw extract but has no catalogue row.

## WebDev Arena: verification of the owner's excerpt

The web leaderboard at arena.ai blocks automated clients (HTTP 403 from Cloudflare, also in a headless browser). The rows were therefore taken from **Arena's own published dataset**, [`lmarena-ai/leaderboard-dataset`](https://huggingface.co/datasets/lmarena-ai/leaderboard-dataset) (config `webdev`), which carries `leaderboard_publish_date` and both interval bounds [M].

- **Owner excerpt of 13 Nov.** The dataset has no 13 Nov snapshot: published dates are 11, 18, 19, 20 and 23 Nov. Every owner value lies between the bracketing 11 Nov and 18 Nov snapshots:

  | Model configuration | 11 Nov | Owner, 13 Nov | 18 Nov |
  | --- | --- | --- | --- |
  | Fable 5.2 xhigh | 1837.8 ± 20.5 | 1826 ± 19 | 1823.8 ± 16.6 |
  | Opus 5.5 max | 1814.7 ± 11.7 | 1816 ± 12 | 1818.4 ± 11.1 |
  | Sonnet 5.5 xhigh | 1772.3 ± 9.9 | 1772 ± 10 | 1773.0 ± 9.4 |
  | Sonnet 5.5 high | 1715.2 ± 8.4 | 1716 ± 8 | 1716.3 ± 8.1 |

  The excerpt is consistent with the source but not reproducible as a dated snapshot. The catalogue uses the **23 Nov** snapshot.
- **Candidate rows, 23 Nov** (category `webdev`, 95% interval):

  | Configuration | Score | ± | Votes |
  | --- | ---: | ---: | ---: |
  | Claude Fable 5.2 xhigh | 1821.7 | 15.3 | 2159 |
  | Claude Opus 5.5 max | 1820.4 | 10.7 | 5121 |
  | Claude Sonnet 5.5 xhigh | 1772.6 | 9.1 | 6973 |
  | Gemini 4 Carbon high | 1769.0 | 15.4 | 2091 |
  | GPT-6 Astra max | 1763.6 | 8.0 | 9541 |
  | GPT-6.2 Astra max | 1760.7 | 14.4 | 2170 |
  | GPT-6.2 Sol max | 1754.8 | 13.8 | 2336 |
  | GPT-6.1 Sol max | 1744.7 | 9.0 | 6737 |
  | Claude Sonnet 5.5 high | 1715.4 | 7.8 | 9129 |
  | Gemini 4 Hydrogen xhigh | 1701.4 | 14.3 | 2012 |
  | GPT-6.1 Terra max | 1686.3 | 9.3 | 5462 |
  | Gemini 4 Argon high | 1682.1 | 8.8 | 6227 |
  | GPT-6 Sol max | 1674.5 | 7.7 | 8574 |
  | Claude Haiku 5.5 high | 1594.7 | 7.7 | 8776 |
  | GPT-6 Luna max | 1578.7 | 5.8 | 32623 |

  Missing for our candidates: **Opus 5.5 below max, any Sonnet 5.5 below high, and GPT-6.1 Sol or Terra below max**. Gemini 4 Neon xhigh (1765.3) is not priced in our catalogue.

  The sub-boards differ. On the HTML sub-board GPT-6.2 Sol (1851) and 6.2 Astra (1849) lead, and Opus 5.5 max (1765) and Sonnet 5.5 xhigh (1761) are level. On the React sub-board Fable 5.2 (1884) and Opus 5.5 (1874) lead.
- **Difference to AA-3D, confirmed.** On WebDev, Sonnet 5.5 xhigh is about 48 points below Opus 5.5 max, outside both intervals, and Sonnet high is about 105 below. On AA-3D, the two are level at max.

  The Arena 3D board sits between the two: Opus max 1915 ± 28, Sonnet xhigh 1868 ± 21, Sonnet high 1817 ± 16. Arena names Sonnet 5.5 high and xhigh among the models that score clearly higher on 3D than their overall level predicts, with Sonnet high's edge coming from games and simulations.

  On Design Arena (default effort), Opus and Sonnet are level on HTML game dev (1438 vs 1427), but Opus leads on 3D design (1449 vs 1395).
- **Broken builds.** WebDev does **not** exclude broken builds before voting; voters can choose "both bad". On 3D battles that happened 13% of the time, against 10% elsewhere. No per-model failure share is published.
- **Cost.** WebDev reports no cost or tokens per battle. Design Arena does publish an average cost per generation, for example agentic web apps:

  | Model (default effort) | USD per build |
  | --- | ---: |
  | Opus 5.5 | 3.53 |
  | Sonnet 5.5 | 1.59 |
  | GPT-6.1 Sol | 0.63 |

## Gaps carried into the ladder

1. No source ranks Sonnet 5.5 high against Opus 5.5 low (thin evidence). On creative work, the only effort-named Opus row below max is Design Arena's agentic game dev entry at **high**.
2. No published playable rate. Our creative cards should report one per rung (anonymised, class F): runs, restart, collisions and performance. Arena's [Interaction-as-a-Judge](https://arena.ai/blog/interaction-as-a-judge) method and its `ArenaWebdevCorrectness` set (189 game versions labelled working or broken) show how such checks can be automated.
3. Effort below max is missing for every OpenAI candidate on every creative board.
4. Cost per accepted creative card is unknown. AA-3D's derived cost per task is the proxy.
