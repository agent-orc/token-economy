#!/usr/bin/env python3
"""Regenerate the dated example through the .NET importer and decision API."""
import argparse
import json
import os
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
FIXTURE = "data/agent-studio-cohorts/2026-09-18/"
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--results-dir", default=os.environ.get("JOB_RESULTS_DIR"))
args = parser.parse_args()
if not args.results_dir:
    parser.error("--results-dir or JOB_RESULTS_DIR is required for collected artifacts")
results = Path(args.results_dir).resolve()
results.mkdir(parents=True, exist_ok=True)
environment = dict(os.environ, JOB_RESULTS_DIR=str(results))
result = subprocess.run([
    "dotnet", "run", "--no-build", "-c", "Release", "--project", "src/TokenEconomy.Benchmarks",
    "--", "decide", FIXTURE + "query.json", FIXTURE + "run-records.json",
], cwd=ROOT, env=environment, text=True, capture_output=True, check=True)
report = json.loads(result.stdout)
evidence = report["cohortEvidence"]

def money(value):
    return "Unknown" if value is None else f"${value:.2f}"

lines = ["", "Generated from the retained export through the dated catalogue; all reasoning levels are unknown.", ""]
for label, key in [("Full exported histories (14–18 September)", "fullHistoryModels"),
                   ("Runs on/after 18 September 00:00 UTC", "windowModels")]:
    lines += [f"**{label}**", "",
              "| Model | Recorded runs | Total USD | Mean USD/run | Completed histories | Mean USD/completed history | Weekly %/run | Weekly %/completed card |",
              "|---|---:|---:|---:|---:|---:|---|---|"]
    for model in evidence[key]:
        lines.append(f"| {model['model']} | {model['recordedRuns']} | {money(model['totalRunUsd'])} | "
                     f"{money(model['meanUsdPerRun'])} | {model['completedCards']} | "
                     f"{money(model['usdPerCompletedCard'])} | Unknown | Unknown |")
    lines += [""]
checks = [p for p in evidence["priceChecks"] if p["recordedUsd"] is not None]
recorded = sum(p["recordedUsd"] for p in checks)
corrected = sum(p["repricedUsd"] for p in checks)
lines += [f"Codex recorded USD: **{money(recorded)}**; repriced USD: **{money(corrected)}** "
          f"({recorded / corrected:.2f}× overstatement).", "",
          "| Account probe interval (+02:00) | Codex weekly window | Claude weekly window |",
          "|---|---:|---:|"]
for interval in evidence["quotaIntervals"]:
    start, end = interval["from"][11:16], interval["through"][11:16]
    def delta(value):
        return "Unknown" if value is None else f"+{value:g} percentage points"
    lines.append(f"| {start}–{end} | {delta(interval['codexPercentagePoints'])} | {delta(interval['claudePercentagePoints'])} |")
lines += ["", "Across 10:15–19:15, Codex moved from 30% to 43% (+13 points) and Claude from 84% to 93% (+9 points).", ""]
path = ROOT / "docs/card-economics.md"
text = path.read_text()
begin, end = "<!-- BEGIN GENERATED COHORT MEASUREMENTS -->", "<!-- END GENERATED COHORT MEASUREMENTS -->"
prefix, tail = text.split(begin, 1)
_, suffix = tail.split(end, 1)
path.write_text(prefix + begin + "\n" + "\n".join(lines) + end + suffix)
print(f"Wrote {results / 'card-economics.json'} and {results / 'card-economics.html'}; updated {path}")
