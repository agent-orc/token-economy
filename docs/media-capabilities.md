# Native media capabilities

As of 2026-07-24, Token Economy carries a versioned, evidence-linked inventory
of media abilities that are native to each coding CLI. "Native" includes a
built-in model or tool invoked by the CLI, but excludes code the CLI can write
to call Veo, Lyria, a speech API, or another external provider API. SVG,
Mermaid, and HTML created as code are not raster-image generation.

## Capability matrix

| CLI | Image generation | Image edit | Reference images | Image understanding | Video | Music | TTS | Voice dictation |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Codex CLI | Yes, `$imagegen` or natural language | Yes, built-in edit flow | Yes, `-i`/`--image` plus image role | Yes, `-i`/`--image` | No | No | No | No in CLI |
| Antigravity CLI | Yes, built-in image tool | Yes, Gemini image edit | Yes, local/multiple image inputs | Yes, local multimodal files | No native output | No native output | No native output | No documented CLI mode |
| Claude Code | No raster model | No raster model | No image-output reference flow | Yes, paste/drop/path; screenshots and PDFs | No | No | No | Yes, `/voice`, hold/tap Space |

The Claude Code voice result corrects the initial research input: current
official documentation says CLI voice dictation is available from v2.1.69
(tap mode from v2.1.116), requires Claude.ai authentication and a local
microphone, and does not consume Claude messages or tokens. It is speech-to-text,
not TTS.

Google's current material confirms Antigravity's out-of-box image generation
and multimodal image processing. It does not identify the exact backing model
of that built-in CLI tool, so the more specific "Nano Banana 2-backed" claim is
not promoted to catalog fact. The separate Gemini image guide establishes the
generation/edit/reference abilities of that model family, not the CLI tool's
undisclosed routing choice.

## Pulling the catalog

The source of truth is
[`src/TokenEconomy/catalog/media-capabilities.json`](../src/TokenEconomy/catalog/media-capabilities.json).
It is embedded in the NuGet assembly beside the existing pricing catalog, so
consumers pull package data and make a pure local query; no new HTTP API or
service was introduced.

```csharp
var imageGeneration = MediaCapabilityCatalog.Default.Find(
    cliId: "codex",
    modelId: "gpt-5.6-sol",
    capability: MediaCapability.ImageGeneration);

var completeMatrix = MediaCapabilityCatalog.Default.Pull(
    cliId: "claude-code",
    modelId: "claude-opus-4-8");
```

Every row has `supported`, `invocationPath`, a structured `costFactor`, and one
or more dated `evidence` entries. `modelId: "*"` means the ability belongs to
the CLI host or built-in media tool and is independent of the selected coding
model. `Find` prefers an exact future model override, then falls back to that
host row. Unknown CLI/model combinations remain unknown rather than silently
becoming unsupported. The adjacent
[`media-capabilities.schema.json`](../src/TokenEconomy/catalog/media-capabilities.schema.json)
defines the exchange shape.

## Image-generation benchmark

The v1 catalog fixes four small prompts: icon, desktop UI mockup, architecture
diagram, and photo-style scene. N is the number of independent generation
attempts. On Codex CLI 0.144.1, the built-in tool completed N=4 total
(N=1 per case) and retained all four PNGs:

| Case | Result | Duration | Artifact |
| --- | --- | ---: | --- |
| Icon | pass | 29.0 s | [`icon.png`](../benchmarks/results/media/20260724-codex-built-in/images/icon.png) |
| UI mockup | pass | 64.8 s | [`ui-mockup.png`](../benchmarks/results/media/20260724-codex-built-in/images/ui-mockup.png) |
| Architecture diagram | pass | 52.8 s | [`architecture-diagram.png`](../benchmarks/results/media/20260724-codex-built-in/images/architecture-diagram.png) |
| Photo style | pass | 120.0 s | [`photo-style.png`](../benchmarks/results/media/20260724-codex-built-in/images/photo-style.png) |

Prompts and deterministic checks are in
[`image-generation-catalog.json`](../benchmarks/media/image-generation-catalog.json);
timestamps, hashes, dimensions, review notes, environment, and cost caveats are
in the immutable
[`results.json`](../benchmarks/results/media/20260724-codex-built-in/results.json).
The active ChatGPT-authenticated built-in path required no
`OPENAI_API_KEY`.

Antigravity is capability-eligible. The official v1.1.6 binary was downloaded
to an isolated temporary directory and its SHA-512 matched Google's manifest,
but `agy models` required an interactive Google sign-in. With no authenticated
Antigravity account in the runner, its benchmark is recorded as `not-run`,
N=0, rather than represented by substituted Gemini API output.

### Cost-factor result

The operator-supplied Codex estimate of **3–5× a normal Codex turn remains an
unverified claim**. A control turn returned token usage and took 11.91 seconds;
the four image calls returned files and latency but no token, credit, or USD
usage. Latency is not cost. The data therefore records the range with
`status: unverifiedClaim` and does not manufacture an empirical multiplier.
A future run can promote it to `measured` only when the same subscription meter
is captured immediately before and after both control and image operations.

## Evidence reviewed

- Codex: [CLI command reference](https://learn.chatgpt.com/docs/developer-commands?surface=cli),
  [authentication](https://learn.chatgpt.com/docs/auth), and the installed
  `imagegen` system skill/tool inventory.
- Antigravity: Google's [out-of-box image generation codelab](https://codelabs.developers.google.com/companion-adk-beginner/instructions),
  [multimodal CLI codelab](https://codelabs.developers.google.com/antigravity-cli-hands-on),
  and [Gemini image generation/editing guide](https://ai.google.dev/gemini-api/docs/image-generation).
- Claude Code: [image workflows](https://code.claude.com/docs/en/common-workflows#work-with-images),
  [Read-tool image/PDF behavior](https://code.claude.com/docs/en/tools-reference#read-tool-behavior),
  [interactive mode](https://code.claude.com/docs/en/interactive-mode), and
  [voice dictation](https://code.claude.com/docs/en/voice-dictation).

Unsupported rows mean no native path was established in the reviewed CLI
inventory on the stated date. They do not claim that the vendor lacks a
separate API or product.
