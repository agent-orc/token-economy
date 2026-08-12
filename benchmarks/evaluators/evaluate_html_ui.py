#!/usr/bin/env python3
"""Render an HTML/UI artifact and score it with deterministic checks plus a fixed jury model."""
from __future__ import annotations

import json
import re
import subprocess
import sys
from html.parser import HTMLParser
from pathlib import Path


SCENARIOS = {
    "incident-console": {
        "required": ("Northstar", "Incident console", "Active incidents", "API latency", "Assign owner", "Recent activity"),
        "description": "An incident-operations console with a strong primary incident, useful status hierarchy, owner action, and recent activity.",
    },
    "billing-onboarding": {
        "required": ("Lumen", "Set up billing", "Company details", "Payment method", "Review", "Continue"),
        "description": "A calm SaaS billing-onboarding step with clear progress, form hierarchy, trust cues, and a primary Continue action.",
    },
}


class Markup(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.tags: list[tuple[str, dict[str, str]]] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        self.tags.append((tag, {key: value or "" for key, value in attrs}))


def run(command: list[str], cwd: Path, timeout: int = 180) -> subprocess.CompletedProcess[str]:
    return subprocess.run(command, cwd=cwd, text=True, capture_output=True, timeout=timeout, check=False)


def usage(json_lines: str) -> tuple[int, int, int]:
    input_total = output = cached = 0
    for line in json_lines.splitlines():
        try:
            item = json.loads(line)
        except json.JSONDecodeError:
            continue
        candidates = [item.get("usage"), (item.get("item") or {}).get("usage")]
        for value in (candidate for candidate in candidates if isinstance(candidate, dict)):
            input_total = max(input_total, int(value.get("input_tokens", value.get("inputTokens", 0))))
            output = max(output, int(value.get("output_tokens", value.get("outputTokens", 0))))
            cached = max(cached, int(value.get("cached_input_tokens", value.get("cacheReadTokens", 0))))
    return max(0, input_total - cached), output, cached


def strip_fence(text: str) -> str:
    value = text.strip()
    if not value.startswith("```"):
        return value
    first = value.find("\n")
    last = value.rfind("```")
    return value[first + 1:last].strip() if first >= 0 and last > first else value


def main() -> int:
    if len(sys.argv) != 3 or sys.argv[2] not in SCENARIOS:
        print("usage: evaluate_html_ui.py <workspace> <scenario>", file=sys.stderr)
        return 2
    workspace = Path(sys.argv[1])
    scenario = SCENARIOS[sys.argv[2]]
    page = workspace / "index.html"
    metrics_path = workspace / "benchmark-metrics.json"
    try:
        html = page.read_text(encoding="utf-8")
    except OSError as error:
        print(error, file=sys.stderr)
        return 2

    parser = Markup()
    parser.feed(html)
    lower = html.lower()
    checks = {
        "validDocument": "<!doctype html" in lower and any(tag == "html" and attrs.get("lang") for tag, attrs in parser.tags),
        "responsiveViewport": any(tag == "meta" and attrs.get("name", "").lower() == "viewport" for tag, attrs in parser.tags),
        "semanticMain": any(tag == "main" for tag, _ in parser.tags),
        "interactiveControl": any(tag in {"button", "input", "select", "textarea"} for tag, _ in parser.tags),
        "responsiveRule": "@media" in lower,
        "noExternalAssets": re.search(r"(?:src|href)=[\"']https?://", html, re.I) is None,
        "requiredContent": all(text.lower() in lower for text in scenario["required"]),
    }
    checklist_pass_rate = sum(checks.values()) / len(checks)

    desktop = workspace / "desktop.png"
    mobile = workspace / "mobile.png"
    url = page.resolve().as_uri()
    for viewport, target in (("1440,1000", desktop), ("390,844", mobile)):
        rendered = run([
            "npx", "--yes", "playwright@1.55.0", "screenshot",
            "--viewport-size", viewport, "--wait-for-timeout", "500", url, str(target),
        ], workspace)
        if rendered.returncode != 0:
            print(rendered.stderr[-2000:], file=sys.stderr)
            metrics_path.write_text(json.dumps({
                "qualityScore": 0,
                "juryScore": 0,
                "checklistPassRate": round(checklist_pass_rate, 6),
                "criticalDefectCount": 1,
                "majorDefectCount": 0,
                "verificationTokens": 0,
                "juryInputTokens": 0,
                "juryOutputTokens": 0,
                "juryCacheReadTokens": 0,
            }, indent=2) + "\n", encoding="utf-8")
            return 1

    schema = workspace / "jury-schema.json"
    schema.write_text(json.dumps({
        "type": "object",
        "additionalProperties": False,
        "required": ["juryScore", "criticalDefects", "majorDefects", "minorDefects", "summary"],
        "properties": {
            "juryScore": {"type": "integer", "minimum": 0, "maximum": 100},
            "criticalDefects": {"type": "array", "items": {"type": "string"}},
            "majorDefects": {"type": "array", "items": {"type": "string"}},
            "minorDefects": {"type": "array", "items": {"type": "string"}},
            "summary": {"type": "string"},
        },
    }), encoding="utf-8")
    jury_output = workspace / "jury-verdict.json"
    prompt = (
        "You are the fixed, blinded visual jury for a controlled HTML/UI benchmark. "
        f"The intended result is: {scenario['description']} "
        "Judge only the two attached screenshots. Apply this defect checklist: content completeness; "
        "clear hierarchy and primary action; spacing/alignment consistency; desktop and mobile fit without "
        "overlap, clipping, or horizontal overflow; legibility/contrast; recognizable interactive controls; "
        "and visible focus/label affordances where observable. Critical means unusable, missing primary content, "
        "or severe clipping; major means a conspicuous defect that materially harms quality; minor is polish. "
        "Return the schema JSON only. Do not guess the producing model or reward visual complexity."
    )
    jury = run([
        "codex", "--ask-for-approval", "never", "exec", "--json", "--ephemeral",
        "--skip-git-repo-check", "--sandbox", "read-only", "-C", str(workspace),
        "-m", "gpt-5.6-sol", "-c", 'model_reasoning_effort="xhigh"',
        "--image", str(desktop), "--image", str(mobile),
        "--output-schema", str(schema), "--output-last-message", str(jury_output), prompt,
    ], workspace, 300)
    if jury.returncode != 0 or not jury_output.exists():
        print((jury.stderr or jury.stdout)[-2000:], file=sys.stderr)
        return 2
    try:
        verdict = json.loads(strip_fence(jury_output.read_text(encoding="utf-8")))
        jury_score = int(verdict["juryScore"])
        critical = len(verdict["criticalDefects"])
        major = len(verdict["majorDefects"])
    except (OSError, ValueError, TypeError, KeyError, json.JSONDecodeError) as error:
        print(f"invalid jury response: {error}", file=sys.stderr)
        return 2

    jury_input, jury_output_tokens, jury_cached = usage(jury.stdout)
    quality = (jury_score / 100 * 0.75) + (checklist_pass_rate * 0.25)
    metrics = {
        "qualityScore": round(quality, 6),
        "juryScore": jury_score,
        "checklistPassRate": round(checklist_pass_rate, 6),
        "criticalDefectCount": critical + (0 if all(checks.values()) else 1),
        "majorDefectCount": major,
        "verificationTokens": jury_input + jury_output_tokens + jury_cached,
        "juryInputTokens": jury_input,
        "juryOutputTokens": jury_output_tokens,
        "juryCacheReadTokens": jury_cached,
    }
    metrics_path.write_text(json.dumps(metrics, indent=2) + "\n", encoding="utf-8")
    return 0 if quality >= 0.75 and critical == 0 and all(checks.values()) else 1


if __name__ == "__main__":
    raise SystemExit(main())
