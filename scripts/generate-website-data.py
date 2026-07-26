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
    capability_studies = []
    for raw_path in sorted(RESULTS.rglob("*.json")):
        if raw_path.name.endswith((".report.json", ".capabilities.json")):
            continue
        raw = load(raw_path)
        # Other append-only evidence can live beside benchmark runs (for
        # example dashboard fixtures). It is intentionally outside this
        # public benchmark projection.
        if "setupId" not in raw and "corpusId" not in raw:
            continue
        report_path = raw_path.with_name(raw_path.stem + ".report.json")
        capabilities_path = raw_path.with_name(raw_path.stem + ".capabilities.json")
        if report_path.exists():
            report = load(report_path)
            if raw["setupId"] != report["setupId"] or raw["runId"] != report["runId"]:
                raise ValueError(f"Raw/report identity mismatch for {raw_path.relative_to(ROOT)}")
            raw_cases_by_variant = {}
            for case in raw["cases"]:
                usage = case["usage"]
                aggregate = raw_cases_by_variant.setdefault(case["variantId"], {
                    "model": case["model"],
                    "input": 0,
                    "output": 0,
                    "cacheRead": 0,
                    "cacheWrite": 0,
                })
                if aggregate["model"] != case["model"]:
                    raise ValueError(f"Variant maps to multiple models in {raw_path.relative_to(ROOT)}")
                for component in ("input", "output", "cacheRead", "cacheWrite"):
                    aggregate[component] += usage[component]

            variants = []
            for variant in report["variants"]:
                raw_variant = raw_cases_by_variant.get(variant["variantId"])
                if raw_variant is None:
                    raise ValueError(f"Report variant missing raw cases in {raw_path.relative_to(ROOT)}")
                projected = dict(variant)
                projected["model"] = raw_variant.pop("model")
                projected["usage"] = raw_variant
                variants.append(projected)
            studies.append({
                "setupId": raw["setupId"], "runId": raw["runId"],
                "startedAtUtc": raw["startedAtUtc"], "completedAtUtc": raw["completedAtUtc"],
                "winner": report["winner"], "winnerReason": report["winnerReason"],
                "qualityDelta": report["qualityDelta"], "costDeltaUsd": report["costDeltaUsd"],
                "variants": variants,
            })
        elif capabilities_path.exists():
            capabilities = load(capabilities_path)
            if raw["runId"] != capabilities["runId"] or raw["corpusId"] != capabilities["corpusId"]:
                raise ValueError(f"Raw/capability identity mismatch for {raw_path.relative_to(ROOT)}")
            capability_studies.append({
                "corpusId": raw["corpusId"], "runId": raw["runId"],
                "startedAtUtc": raw["startedAtUtc"], "completedAtUtc": raw["completedAtUtc"],
                "capabilities": capabilities["capabilities"],
            })
        else:
            raise ValueError(f"Missing derived result for {raw_path.relative_to(ROOT)}")
    return {"schemaVersion": 2, "generatedAtUtc": datetime.now(timezone.utc).isoformat(), "studies": studies, "capabilityStudies": capability_studies}


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
    # Keep the generated artifact byte-stable across Windows and Unix runners.
    OUTPUT.write_bytes((json.dumps(payload, indent=2) + "\n").encode("utf-8"))
    print(f"Wrote {OUTPUT.relative_to(ROOT)} with {len(payload['studies'])} study/studies.")


if __name__ == "__main__":
    main()
