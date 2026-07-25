#!/usr/bin/env python3
"""Build the deployable website data from append-only benchmark evidence.

The public site is static, so it cannot read files outside website/ after it is
deployed.  This command is the narrow bridge: it copies only the published
fields from benchmark JSON and validates that each raw run has its report.
"""
from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
RESULTS = ROOT / "benchmarks" / "results"
OUTPUT = ROOT / "website" / "data" / "benchmarks.json"


def load(path: Path) -> dict:
    with path.open(encoding="utf-8") as source:
        return json.load(source)


def create_payload() -> dict:
    studies = []
    for raw_path in sorted(RESULTS.glob("*/*.json")):
        if raw_path.name.endswith(".report.json"):
            continue
        report_path = raw_path.with_name(raw_path.stem + ".report.json")
        if not report_path.exists():
            raise ValueError(f"Missing derived report for {raw_path.relative_to(ROOT)}")
        raw, report = load(raw_path), load(report_path)
        if raw["setupId"] != report["setupId"] or raw["runId"] != report["runId"]:
            raise ValueError(f"Raw/report identity mismatch for {raw_path.relative_to(ROOT)}")
        studies.append({
            "setupId": raw["setupId"], "runId": raw["runId"],
            "startedAtUtc": raw["startedAtUtc"], "completedAtUtc": raw["completedAtUtc"],
            "winner": report["winner"], "winnerReason": report["winnerReason"],
            "qualityDelta": report["qualityDelta"], "costDeltaUsd": report["costDeltaUsd"],
            "variants": report["variants"],
        })
    return {"schemaVersion": 1, "generatedAtUtc": datetime.now(timezone.utc).isoformat(), "studies": studies}


def canonical(value: dict) -> str:
    # generatedAt changes by design; checking compares the evidence-derived body.
    value = dict(value)
    value.pop("generatedAtUtc", None)
    return json.dumps(value, sort_keys=True, separators=(",", ":"))


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="fail when committed site data is stale")
    args = parser.parse_args()
    payload = create_payload()
    if args.check:
        if not OUTPUT.exists() or canonical(load(OUTPUT)) != canonical(payload):
            raise SystemExit("website/data/benchmarks.json is stale; run scripts/generate-website-data.py")
        print("Website benchmark data is current.")
        return
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {OUTPUT.relative_to(ROOT)} with {len(payload['studies'])} study/studies.")


if __name__ == "__main__":
    main()
