#!/usr/bin/env python3
"""Fetch stable benchmark sources and write review candidates, never the catalog.

The output is deliberately a review queue: extraction from live benchmark pages
is not authoritative until an operator checks the quoted context, effort label,
version, and date. Unmatched sources are printed and retained as gaps.
"""
from __future__ import annotations

import argparse
import html
import json
import os
import re
import sys
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path


SOURCES = [
    {"id": "openai-astra-price", "url": "https://developers.openai.com/api/docs/models/gpt-6-astra", "extractor": "astra_price"},
    {"id": "openai-sol-price", "url": "https://developers.openai.com/api/docs/models/gpt-5.6-sol", "extractor": "sol_price"},
    {"id": "aa-astra-article", "url": "https://artificialanalysis.ai/articles/benchmarking-gpt-6-astra", "extractor": "aa_article"},
    {"id": "aa-astra-low-sol-high", "url": "https://artificialanalysis.ai/models/comparisons/gpt-6-astra-low-vs-gpt-5-6-sol-high", "extractor": "aa_comparison"},
    {"id": "deepswe", "url": "https://deepswe.datacurve.ai/", "extractor": "deepswe"},
    {"id": "openai-astra-launch", "url": "https://openai.com/index/gpt-6-astra/", "extractor": "openai_launch"},
]


def plain(markup: str) -> str:
    text = re.sub(r"<script\b[^>]*>.*?</script>|<style\b[^>]*>.*?</style>", " ", markup, flags=re.I | re.S)
    text = re.sub(r"<[^>]+>", " ", text)
    return re.sub(r"\s+", " ", html.unescape(text)).strip()


def fetch(url: str) -> str:
    request = urllib.request.Request(url, headers={"User-Agent": "TokenEconomy benchmark refresh/1.0"})
    with urllib.request.urlopen(request, timeout=30) as response:
        return response.read().decode(response.headers.get_content_charset() or "utf-8", errors="replace")


def match_numbers(text: str, patterns: dict[str, str]) -> tuple[dict, list[str]]:
    facts, gaps = {}, []
    for field, pattern in patterns.items():
        matched = re.search(pattern, text, re.I | re.S)
        if matched:
            facts[field] = float(matched.group(1).replace(",", ""))
        else:
            gaps.append(field)
    return facts, gaps


def extract(source: dict, text: str) -> tuple[list[dict], list[dict], list[str]]:
    kind = source["extractor"]
    rows: list[dict] = []
    prices: list[dict] = []
    gaps: list[str] = []
    if kind in {"astra_price", "sol_price"}:
        model = "gpt-6-astra" if kind == "astra_price" else "gpt-5.6-sol"
        expected = {"inputPerMTok": r"Input\s*\$([0-9.]+)", "cachedInputPerMTok": r"Cached input\s*\$([0-9.]+)", "outputPerMTok": r"Output\s*\$([0-9.]+)"}
        facts, missing = match_numbers(text, expected)
        if facts:
            prices.append({"modelId": model, **facts, "sourceUrl": source["url"]})
        gaps.extend(f"{model}.{field}" for field in missing)
    elif kind == "aa_article":
        facts, missing = match_numbers(text, {
            "astraCodingScore": r"Astra scores\s+([0-9.]+)\s+in the Index",
            "astraCodingCost": r"costs \$([0-9.]+) per task",
        })
        if "astraCodingScore" in facts:
            rows.append({"benchmarkTypeId": "artificial-analysis-coding-agent-index-<review-version>", "modelId": "gpt-6-astra", "reasoningEffort": "max", "score": facts["astraCodingScore"], "sourceUrl": source["url"]})
        gaps.append("aa_article.solCodingScore requires manual effort/score disambiguation")
        gaps.extend(missing)
    elif kind == "aa_comparison":
        table = re.search(r"Intelligence Index\s*\|?\s*([0-9.]+)\s*\|?\s*([0-9.]+).*?Cost per Task\s*\|?\s*\$([0-9.]+)\s*\|?\s*\$([0-9.]+)", text, re.I | re.S)
        if table:
            for model, effort, score, cost in (("gpt-6-astra", "low", table.group(1), table.group(3)), ("gpt-5.6-sol", "high", table.group(2), table.group(4))):
                rows.append({"benchmarkTypeId": "artificial-analysis-intelligence-index-<review-version>", "modelId": model, "reasoningEffort": effort, "score": float(score), "secondaryMetrics": {"costPerTaskUsd": float(cost)}, "sourceUrl": source["url"]})
        else:
            gaps.append("AA comparison score/cost table")
    elif kind == "deepswe":
        for model, effort in (("gpt-6-astra", "xHigh"), ("gpt-5.6-sol", "max")):
            pattern = rf"{re.escape(model)}\s*\[{effort}\].*?([0-9.]+)%.*?Avg cost \$([0-9.]+).*?Out tok ([0-9]+)k"
            found = re.search(pattern, text, re.I | re.S)
            if found:
                rows.append({"benchmarkTypeId": "deepswe-v1.1", "modelId": model, "reasoningEffort": effort, "score": float(found.group(1)), "secondaryMetrics": {"costPerTaskUsd": float(found.group(2)), "outputTokensPerTask": int(found.group(3)) * 1000}, "sourceUrl": source["url"]})
            else:
                gaps.append(f"DeepSWE row {model}/{effort}")
    elif kind == "openai_launch":
        for type_id, label in (("deepswe-v1.1", "DeepSWE v1.1"), ("terminal-bench-v4.0", "Terminal-Bench 4.0")):
            found = re.search(rf"{re.escape(label)}\s*\|?\s*([0-9.]+)%\s*\|?\s*([0-9.]+)%", text, re.I)
            if found:
                for model, score in (("gpt-6-astra", found.group(1)), ("gpt-5.6-sol", found.group(2))):
                    rows.append({"benchmarkTypeId": type_id, "modelId": model, "reasoningEffort": "unspecified", "score": float(score), "sourceUrl": source["url"]})
            else:
                gaps.append(f"OpenAI launch row {label}")
    return rows, prices, gaps


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", action="append", help="fetch only a named source id; may be repeated")
    default_root = Path(os.environ.get("JOB_RESULTS_DIR", Path(__file__).resolve().parents[1] / "results"))
    parser.add_argument("--output", type=Path, default=default_root / "benchmark-refresh-review.json")
    args = parser.parse_args()
    selected = [source for source in SOURCES if not args.source or source["id"] in args.source]
    unknown = set(args.source or []) - {source["id"] for source in SOURCES}
    if unknown:
        parser.error(f"unknown source id(s): {', '.join(sorted(unknown))}")

    review = {"schemaVersion": 1, "generatedAtUtc": datetime.now(timezone.utc).isoformat(), "sources": [], "candidateRows": [], "priceFacts": [], "gaps": []}
    for source in selected:
        try:
            markup = fetch(source["url"])
            rows, prices, gaps = extract(source, plain(markup))
            review["sources"].append({"id": source["id"], "url": source["url"], "status": "fetched"})
            review["candidateRows"].extend(rows)
            review["priceFacts"].extend(prices)
            review["gaps"].extend({"sourceId": source["id"], "field": gap} for gap in gaps)
        except (urllib.error.URLError, TimeoutError, ValueError) as error:
            review["sources"].append({"id": source["id"], "url": source["url"], "status": "failed"})
            review["gaps"].append({"sourceId": source["id"], "field": "fetch", "error": str(error)})

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(review, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote review file: {args.output}")
    print(f"Fetched {sum(source['status'] == 'fetched' for source in review['sources'])}/{len(selected)} sources; extracted {len(review['candidateRows'])} benchmark rows and {len(review['priceFacts'])} price facts.")
    if review["gaps"]:
        print("Manual gaps:")
        for gap in review["gaps"]:
            print(f"- {gap['sourceId']}: {gap['field']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
