#!/usr/bin/env python3
"""Score a source-review response against hidden seeded defects.

The scenario manifest lives here rather than in the copied subject workspace so
the reviewed model cannot read the answer key through ordinary workspace discovery.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path


SCENARIOS = {
    "tenant-transfer": [
        ("TransferService.cs", 26, 29, 4, ("tenant", "authorization", "account")),
        ("TransferService.cs", 31, 32, 3, ("negative", "amount", "zero")),
        ("TransferService.cs", 34, 40, 4, ("atomic", "rollback", "audit", "balance")),
        ("TransferService.cs", 40, 40, 2, ("cancellation", "token", "audit")),
        ("TransferService.cs", 42, 42, 2, ("secret", "idempotency", "log")),
    ],
    "tenant-cache": [
        ("TenantCache.cs", 23, 23, 4, ("tenant", "key", "collision")),
        ("TenantCache.cs", 24, 29, 2, ("utc", "local", "datetime", "expiry")),
        ("TenantCache.cs", 28, 37, 3, ("exception", "fault", "task", "poison")),
        ("TenantCache.cs", 49, 52, 3, ("tenant", "clear", "all", "isolation")),
        ("TenantCache.cs", 34, 35, 2, ("cancellation", "catch", "swallow")),
    ],
}


def fail(message: str) -> int:
    print(message, file=sys.stderr)
    return 2


def main() -> int:
    if len(sys.argv) != 3 or sys.argv[2] not in SCENARIOS:
        return fail("usage: evaluate_source_review.py <workspace> <scenario>")
    workspace = Path(sys.argv[1])
    response_path = workspace / "review.json"
    metrics_path = workspace / "benchmark-metrics.json"
    try:
        response = json.loads(response_path.read_text(encoding="utf-8"))
        findings = response["findings"]
        if not isinstance(findings, list):
            raise ValueError("findings is not an array")
    except (OSError, ValueError, KeyError, json.JSONDecodeError) as error:
        metrics_path.write_text(json.dumps({
            "seededDefectRecall": 0,
            "severityWeightedRecall": 0,
            "findingPrecision": 0,
            "falsePositiveCount": 0,
        }), encoding="utf-8")
        return fail(f"invalid review response: {error}")

    seeds = SCENARIOS[sys.argv[2]]
    matched: set[int] = set()
    valid_findings = 0
    for finding in findings:
        if not isinstance(finding, dict):
            continue
        file_name = str(finding.get("file", "")).strip()
        summary = f"{finding.get('title', '')} {finding.get('summary', '')} {finding.get('evidence', '')}".lower()
        try:
            line = int(finding.get("line", 0))
        except (TypeError, ValueError):
            line = 0
        valid_findings += 1
        candidates = []
        for index, (expected_file, first_line, last_line, _weight, words) in enumerate(seeds):
            if index in matched or Path(file_name).name != expected_file:
                continue
            line_match = first_line - 2 <= line <= last_line + 2
            keyword_match = sum(1 for word in words if word in summary) >= 2
            if line_match and keyword_match:
                candidates.append(index)
        if candidates:
            matched.add(candidates[0])

    detected_weight = sum(seeds[index][3] for index in matched)
    total_weight = sum(seed[3] for seed in seeds)
    recall = len(matched) / len(seeds)
    weighted_recall = detected_weight / total_weight
    precision = len(matched) / valid_findings if valid_findings else 0
    false_positives = max(0, valid_findings - len(matched))
    metrics = {
        "seededDefectRecall": round(recall, 6),
        "severityWeightedRecall": round(weighted_recall, 6),
        "findingPrecision": round(precision, 6),
        "falsePositiveCount": false_positives,
    }
    metrics_path.write_text(json.dumps(metrics, indent=2) + "\n", encoding="utf-8")
    critical = {0, 2} if sys.argv[2] == "tenant-transfer" else {0}
    return 0 if recall >= 0.6 and precision >= 0.5 and critical.issubset(matched) else 1


if __name__ == "__main__":
    raise SystemExit(main())
