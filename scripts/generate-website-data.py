#!/usr/bin/env python3
"""Build the deployable website data from append-only benchmark evidence.

The public site is static, so it cannot read files outside website/ after it is
deployed.  This command is the narrow bridge: it copies only the published
fields from benchmark JSON and validates that each raw run has its report.

Two artifacts are produced:

* ``website/data/benchmarks.json`` — the published A/B and capability studies.
* ``website/data/token-usage.json`` — the aggregates the token-usage charts
  render (per model, per task class, per measured reissue count, and one
  measured session over time), each carrying the evidence path it came from.
  Dollar figures are list prices resolved from the dated repository price
  catalog; a model without a published price stays explicitly unpriced.

Every number here is derived from checked-in evidence. Nothing is hand-authored,
so ``--check`` fails loudly when the committed site data no longer matches the
evidence.
"""
from __future__ import annotations

import argparse
import json
import re
import statistics
from datetime import datetime, timezone
from decimal import Decimal, ROUND_HALF_EVEN
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
RESULTS = ROOT / "benchmarks" / "results"
OUTPUT = ROOT / "website" / "data" / "benchmarks.json"
USAGE_OUTPUT = ROOT / "website" / "data" / "token-usage.json"

PRICE_CATALOG = ROOT / "src" / "TokenEconomy" / "catalog" / "model-prices.json"
ROUTING_POLICY = ROOT / "src" / "TokenEconomy" / "catalog" / "model-routing-policy.json"
DOCUMENT_RESULTS = RESULTS / "document-to-text" / "curated-hard-cases-v1"
CARD_BACKTEST = ROOT / "results" / "complexity-backtest" / "agent-studio-30-card-backtest.json"
SESSION_ANALYSIS = ROOT / "docs" / "analyses" / "long-vs-short-session-cost.md"

COMPONENTS = ("input", "output", "cacheRead", "cacheWrite")
CENT_MICRO = Decimal("0.000001")


def load(path: Path) -> dict:
    with path.open(encoding="utf-8") as source:
        return json.load(source)


def create_payload() -> dict:
    price_index = load_price_index()
    routing_index = load_routing_index()
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
                    "tokenSamples": [],
                    "durationSamplesMs": [],
                })
                if aggregate["model"] != case["model"]:
                    raise ValueError(f"Variant maps to multiple models in {raw_path.relative_to(ROOT)}")
                for component in ("input", "output", "cacheRead", "cacheWrite"):
                    aggregate[component] += usage[component]
                aggregate["tokenSamples"].append(total_tokens(usage))
                aggregate["durationSamplesMs"].append(case["durationMs"])

            variants = []
            for variant in report["variants"]:
                raw_variant = raw_cases_by_variant.get(variant["variantId"])
                if raw_variant is None:
                    raise ValueError(f"Report variant missing raw cases in {raw_path.relative_to(ROOT)}")
                projected = dict(variant)
                projected["model"] = raw_variant["model"]
                projected["usage"] = {
                    component: raw_variant[component] for component in COMPONENTS
                }
                projected["tokenRange"] = {
                    "minimum": min(raw_variant["tokenSamples"]),
                    "maximum": max(raw_variant["tokenSamples"]),
                }
                projected["durationRangeMs"] = {
                    "minimum": min(raw_variant["durationSamplesMs"]),
                    "maximum": max(raw_variant["durationSamplesMs"]),
                }
                projected.update(model_annotation(projected["model"], routing_index))
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
                # Raw benchmark evidence is append-only, but the public projection
                # must not present models absent from the current catalog as real
                # capability measurements.
                "capabilities": project_capabilities(
                    raw, capabilities, price_index, routing_index),
            })
        else:
            raise ValueError(f"Missing derived result for {raw_path.relative_to(ROOT)}")
    studies.sort(key=lambda study: study["startedAtUtc"], reverse=True)
    capability_studies.sort(key=lambda study: study["startedAtUtc"], reverse=True)
    return {"schemaVersion": 3, "generatedAtUtc": datetime.now(timezone.utc).isoformat(), "studies": studies, "capabilityStudies": capability_studies}


# ---------------------------------------------------------------------------
# Token-usage aggregates for the website charts.
#
# The price arithmetic below mirrors ModelPriceCatalog.ComputeCost: greatest
# ValidFrom not after the instant, inclusive ValidTo, cache rates falling back
# to the input rate, and an explicit status instead of a silent zero.
# WebsiteTokenUsageDataTests re-costs the committed artifact through the real
# library, so the two implementations cannot drift apart unnoticed.
# ---------------------------------------------------------------------------


def parse_utc(text: str) -> datetime:
    """Parse a catalog/evidence UTC stamp, tolerating .NET's 7-digit fractions."""
    normalized = re.sub(r"(\.\d{6})\d+", r"\1", text.strip().replace("Z", "+00:00"))
    parsed = datetime.fromisoformat(normalized)
    return parsed if parsed.tzinfo else parsed.replace(tzinfo=timezone.utc)


def normalize_model_key(model: str) -> str:
    return model.strip().lower().replace(".", "-")


def load_price_index() -> dict[str, dict]:
    index: dict[str, dict] = {}
    for listing in load(PRICE_CATALOG):
        for key in [listing["modelId"], *listing.get("aliases", [])]:
            index[normalize_model_key(key)] = listing
    return index


def load_routing_index() -> dict[str, dict]:
    """Current policy status by canonical model id and alias.

    Benchmark evidence remains dated and append-only. Lifecycle annotations are
    deliberately current so a historical success cannot make a retired model
    appear selectable on the public site.
    """
    index: dict[str, dict] = {}
    today = datetime.now(timezone.utc).date()
    for model in load(ROUTING_POLICY)["models"]:
        status = model["routingStatus"]
        lifecycle = None
        retirement = re.search(r"\bRetires\s+(\d{4}-\d{2}-\d{2})\b", model.get("note", ""), re.I)
        if status == "deprecated":
            lifecycle = "retired" if retirement and datetime.fromisoformat(retirement.group(1)).date() <= today \
                else "deprecated"
        annotation = {
            "canonicalModel": model["canonicalId"],
            "routingStatus": status,
            "lifecycleStatus": lifecycle,
            "lifecycleNote": model.get("note") if lifecycle else None,
        }
        for key in [model["canonicalId"], *model.get("aliases", [])]:
            index[normalize_model_key(key)] = annotation
    return index


def model_annotation(model: str, routing_index: dict[str, dict]) -> dict:
    annotation = routing_index.get(normalize_model_key(model))
    if annotation is None:
        raise ValueError(f"Published model '{model}' is missing from the routing policy")
    return dict(annotation)


def infrastructure_failure(case: dict) -> bool:
    outcome = case.get("outcome")
    return outcome == "InfrastructureFailure" or (outcome is None and case["exitCode"] != 0)


def project_capabilities(
        raw: dict, capabilities: dict, price_index: dict[str, dict],
        routing_index: dict[str, dict]) -> list[dict]:
    """Re-derive public levels so legacy CLI crashes cannot read as misses."""
    raw_groups: dict[tuple[str, str], list[dict]] = {}
    for case in raw["cases"]:
        raw_groups.setdefault((case["model"], case["documentType"]), []).append(case)

    projected = []
    for row in capabilities["capabilities"]:
        if normalize_model_key(row["model"]) not in price_index:
            continue
        cases = raw_groups.get((row["model"], row["documentType"]), [])
        if not cases:
            raise ValueError(
                f"Capability row has no raw cases: {row['model']} / {row['documentType']}")
        infrastructure = sum(1 for case in cases if infrastructure_failure(case))
        attempted_cases = [case for case in cases if not infrastructure_failure(case)]
        passed = sum(1 for case in attempted_cases if case["succeeded"])
        attempted = len(attempted_cases)
        if attempted == 0:
            level = "NotAttempted"
            success_rate = None
        elif passed == 0:
            level = "NotDemonstrated"
            success_rate = 0
        elif passed == attempted and infrastructure == 0:
            level = "Demonstrated"
            success_rate = 1
        else:
            level = "Partial"
            success_rate = passed / attempted

        item = dict(row)
        item.update({
            "level": level,
            "casesAttempted": attempted,
            "casesPassed": passed,
            "infrastructureFailures": infrastructure,
            "successRate": success_rate,
        })
        item.update(model_annotation(item["model"], routing_index))
        projected.append(item)
    return projected


def money(value: Decimal) -> float:
    """Round like Math.Round(value, 6) so the C# cross-check compares equal."""
    return float(value.quantize(CENT_MICRO, rounding=ROUND_HALF_EVEN))


def compute_cost(index: dict[str, dict], model: str, usage: dict[str, int], at_utc: datetime) -> dict:
    """Cost one usage tuple at list price, or report why it has no price."""
    listing = index.get(normalize_model_key(model))
    if listing is None:
        return {"status": "UnknownModel", "totalUsd": None}
    price = None
    for entry in listing.get("history", []):
        valid_from = parse_utc(entry["validFrom"])
        valid_to = parse_utc(entry["validTo"]) if entry.get("validTo") else None
        if valid_from <= at_utc and (valid_to is None or at_utc <= valid_to) \
                and (price is None or valid_from > parse_utc(price["validFrom"])):
            price = entry
    if price is None:
        return {"status": "NoPriceForDate", "totalUsd": None}

    rates = {
        "input": Decimal(str(price["inputPerMTok"])),
        "output": Decimal(str(price["outputPerMTok"])),
        "cacheRead": Decimal(str(price.get("cacheReadPerMTok", price["inputPerMTok"]))),
        "cacheWrite": Decimal(str(price.get("cacheWritePerMTok", price["inputPerMTok"]))),
    }
    costs = {
        component: Decimal(max(usage[component], 0)) / Decimal(1_000_000) * rates[component]
        for component in COMPONENTS
    }
    return {
        "status": "Resolved",
        "currency": price.get("currency", "USD"),
        "unconfirmed": bool(price.get("unconfirmed", False)),
        "totalUsd": money(sum(costs.values(), Decimal(0))),
        "components": {component: money(costs[component]) for component in COMPONENTS},
    }


def empty_usage() -> dict[str, int]:
    return {component: 0 for component in COMPONENTS}


def add_usage(target: dict[str, int], usage: dict[str, int]) -> None:
    for component in COMPONENTS:
        target[component] += usage[component]


def total_tokens(usage: dict[str, int]) -> int:
    return sum(usage[component] for component in COMPONENTS)


def latest_document_run() -> Path:
    candidates = [
        path for path in DOCUMENT_RESULTS.glob("*.json")
        if not path.name.endswith((".capabilities.json", ".report.json"))
    ]
    if not candidates:
        raise ValueError(f"No document benchmark run found below {DOCUMENT_RESULTS.relative_to(ROOT)}")
    return max(candidates, key=lambda path: path.stem)


def create_document_usage(index: dict[str, dict]) -> tuple[dict, dict]:
    """Per-model and per-document-type usage from the capability corpus run.

    This is the widest measured slice in the repository: every model attempts
    every document class once, so model and task class are directly comparable.
    """
    document_run = latest_document_run()
    run = load(document_run)
    cases = [
        case for case in run["cases"]
        if normalize_model_key(case["model"]) in index
    ]
    priced_at = parse_utc(run["startedAtUtc"])
    evidence = str(document_run.relative_to(ROOT)).replace("\\", "/")

    by_model: dict[str, dict] = {}
    by_type: dict[str, dict] = {}
    failed_usage = empty_usage()
    failed_with_usage = 0
    without_usage = 0
    for case in cases:
        recorded = total_tokens(case["usage"]) > 0
        without_usage += 0 if recorded else 1
        failed_infrastructure = infrastructure_failure(case)
        for group, key in ((by_model, case["model"]), (by_type, case["documentType"])):
            bucket = group.setdefault(key, {
                "cases": 0, "casesPassed": 0, "casesWithUsage": 0,
                "infrastructureFailures": 0, "usage": empty_usage(),
            })
            bucket["cases"] += 1
            bucket["casesPassed"] += 1 if case["succeeded"] else 0
            bucket["casesWithUsage"] += 1 if recorded else 0
            bucket["infrastructureFailures"] += 1 if failed_infrastructure else 0
            add_usage(bucket["usage"], case["usage"])
        if not case["succeeded"] and recorded:
            failed_with_usage += 1
            add_usage(failed_usage, case["usage"])

    models = sorted(
        (
            {
                "model": model,
                **bucket,
                "tokens": total_tokens(bucket["usage"]),
                "cost": compute_cost(index, model, bucket["usage"], priced_at),
            }
            for model, bucket in by_model.items()
        ),
        key=lambda row: (-row["tokens"], row["model"]),
    )
    # Corpus order, not token order: the classes are a fixed, comparable set.
    types = [
        {"documentType": name, **bucket, "tokens": total_tokens(bucket["usage"])}
        for name, bucket in by_type.items()
    ]

    run_usage = empty_usage()
    for bucket in by_model.values():
        add_usage(run_usage, bucket["usage"])

    source = {
        "corpusId": run["corpusId"], "runId": run["runId"],
        "startedAtUtc": run["startedAtUtc"], "completedAtUtc": run["completedAtUtc"],
        "evidencePath": evidence, "pricedAtUtc": run["startedAtUtc"],
        "cases": len(cases),
        "casesPassed": sum(1 for case in cases if case["succeeded"]),
        # Cases whose CLI never started record no tokens at all; they carry no
        # token or cost information and must not read as an efficient model.
        "casesWithUsage": len(cases) - without_usage,
        "casesWithoutUsage": without_usage,
        "usage": run_usage, "tokens": total_tokens(run_usage),
        "failedCasesWithUsage": failed_with_usage,
        "failedCaseTokens": total_tokens(failed_usage),
        "infrastructureFailures": sum(1 for case in cases if infrastructure_failure(case)),
        "pricedModels": sum(
            1 for row in models if row["cost"]["status"] == "Resolved" and row["tokens"] > 0),
    }
    return {"source": source, "models": models}, {"source": source, "documentTypes": types}


def create_card_usage() -> dict:
    """Observed token totals per card task class and per measured reissue count.

    Reissues are the backtest's attempt proxy (measured entry count minus one),
    not a semantic retry classification, and the raw card totals over-count
    cached Codex input — so these rows stay in tokens and are never costed.
    """
    backtest = load(CARD_BACKTEST)
    rows = backtest["rows"]

    by_type: dict[str, dict] = {}
    for row in rows:
        bucket = by_type.setdefault(row["TaskType"], {"cards": 0, "tokens": 0, "reissues": 0, "samples": []})
        bucket["cards"] += 1
        bucket["tokens"] += row["ActualTokens"]
        bucket["reissues"] += row["ActualReissues"]
        bucket["samples"].append(row["ActualTokens"])

    task_types = sorted(
        (
            {
                "taskType": name, "cards": bucket["cards"], "tokens": bucket["tokens"],
                "medianTokens": int(statistics.median(bucket["samples"])),
                "reissues": bucket["reissues"],
            }
            for name, bucket in by_type.items()
        ),
        key=lambda row: (-row["tokens"], row["taskType"]),
    )

    labels = ["0", "1", "2", "3+"]
    buckets = {label: {"reissues": label, "cards": 0, "tokens": 0} for label in labels}
    for row in rows:
        label = labels[min(row["ActualReissues"], 3)]
        buckets[label]["cards"] += 1
        buckets[label]["tokens"] += row["ActualTokens"]

    tokens = sum(row["ActualTokens"] for row in rows)
    reissued_tokens = sum(row["ActualTokens"] for row in rows if row["ActualReissues"] > 0)
    return {
        "source": {
            "evidencePath": str(CARD_BACKTEST.relative_to(ROOT)).replace("\\", "/"),
            "generatedAtUtc": backtest["generatedAtUtc"],
            "selection": backtest["source"]["selection"],
            "reissueMeasurement": backtest["measurement"]["reissues"],
            "cards": len(rows), "tokens": tokens,
            "reissuedCards": sum(1 for row in rows if row["ActualReissues"] > 0),
            "reissuedTokens": reissued_tokens,
        },
        "taskTypes": task_types,
        "reissueBuckets": [buckets[label] for label in labels],
    }


def create_session_usage(index: dict[str, dict]) -> dict:
    """The one measured multi-turn session in the repository, turn by turn.

    Parsed out of the analysis document that owns the query contract, so the
    chart cannot drift from the prose. The parser is deliberately strict: an
    edit that changes the table shape fails the data build instead of silently
    publishing stale turns.
    """
    text = SESSION_ANALYSIS.read_text(encoding="utf-8")
    section = re.search(r"### 5\.1 (.+?)\n(.*?)\n### ", text, re.S)
    if section is None:
        raise ValueError(f"Section 5.1 not found in {SESSION_ANALYSIS.relative_to(ROOT)}")
    body = section.group(2)

    model = re.search(r"real `([a-z0-9.\-]+)` session\s*`?([0-9a-f-]{36})`?", body, re.S)
    if model is None:
        raise ValueError("Section 5.1 no longer states its model and session id")

    turns = []
    for row in re.finditer(
        r"^\|\s*(\d+)\s*\|\s*([\d]{4}-[\d]{2}-[\d]{2} [\d:]{8})\s*\|[^|]*\|"
        r"\s*([\d,]+)\s*\|\s*([\d,]+)\s*\|\s*([\d,]+)\s*\|\s*([\d,]+)\s*\|\s*\$([\d.]+)\s*\|",
        body, re.M,
    ):
        turn, completed, fresh, cache_read, cache_write, output, documented = row.groups()
        usage = {
            "input": int(fresh.replace(",", "")), "output": int(output.replace(",", "")),
            "cacheRead": int(cache_read.replace(",", "")), "cacheWrite": int(cache_write.replace(",", "")),
        }
        at_utc = parse_utc(completed.replace(" ", "T") + "Z")
        cost = compute_cost(index, model.group(1), usage, at_utc)
        if cost["status"] != "Resolved":
            raise ValueError(f"Session model '{model.group(1)}' has no catalog price at {at_utc:%Y-%m-%d}")
        # The document publishes its own list-price column. Recompute it from the
        # catalog and refuse to publish a chart that disagrees with the prose.
        if abs(Decimal(str(cost["totalUsd"])) - Decimal(documented)) > CENT_MICRO / 2:
            raise ValueError(
                f"Turn {turn} cost {cost['totalUsd']} disagrees with the documented ${documented}")
        turns.append({
            "turn": int(turn), "completedAtUtc": at_utc.isoformat().replace("+00:00", "Z"),
            "usage": usage, "tokens": total_tokens(usage),
            "costUsd": cost["totalUsd"], "costComponents": cost["components"],
        })
    if len(turns) < 2:
        raise ValueError("Section 5.1 no longer contains a multi-turn table")

    totals = empty_usage()
    for turn in turns:
        add_usage(totals, turn["usage"])
    cost_total = sum(Decimal(str(turn["costUsd"])) for turn in turns)
    cache_write_cost = sum(Decimal(str(turn["costComponents"]["cacheWrite"])) for turn in turns)
    return {
        "source": {
            "evidencePath": str(SESSION_ANALYSIS.relative_to(ROOT)).replace("\\", "/"),
            "title": section.group(1).strip(),
            "model": model.group(1), "sessionId": model.group(2),
            "reproducibility": "Read-only Agent Studio bus query; the raw responses are not checked in.",
        },
        "turns": turns,
        "totals": {
            "usage": totals, "tokens": total_tokens(totals),
            "costUsd": money(cost_total),
            "cacheWriteCostUsd": money(cache_write_cost),
            "cacheWriteCostShare": float((cache_write_cost / cost_total).quantize(Decimal("0.0001"))),
            "spanHours": round(
                (parse_utc(turns[-1]["completedAtUtc"]) - parse_utc(turns[0]["completedAtUtc"])).total_seconds() / 3600,
                2),
        },
    }


def create_usage_payload() -> dict:
    index = load_price_index()
    by_model, by_document_type = create_document_usage(index)
    return {
        "schemaVersion": 1,
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
        "priceCatalogPath": str(PRICE_CATALOG.relative_to(ROOT)).replace("\\", "/"),
        "byModel": by_model,
        "byDocumentType": by_document_type,
        "byCard": create_card_usage(),
        "session": create_session_usage(index),
    }


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
    usage_payload = create_usage_payload()
    artifacts = ((OUTPUT, payload), (USAGE_OUTPUT, usage_payload))
    if args.check:
        for path, expected in artifacts:
            name = path.relative_to(ROOT).as_posix()
            if not path.exists() or canonical(load(path)) != canonical(expected):
                raise SystemExit(f"{name} is stale; run scripts/generate-website-data.py")
        print("Website benchmark and token-usage data are current.")
        return
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    for path, value in artifacts:
        # Keep the generated artifacts byte-stable across Windows and Unix runners.
        path.write_bytes((json.dumps(value, indent=2) + "\n").encode("utf-8"))
    print(f"Wrote {OUTPUT.relative_to(ROOT).as_posix()} with {len(payload['studies'])} study/studies.")
    print(
        f"Wrote {USAGE_OUTPUT.relative_to(ROOT).as_posix()} with "
        f"{len(usage_payload['byModel']['models'])} models, "
        f"{len(usage_payload['byCard']['taskTypes'])} card task classes, and "
        f"{len(usage_payload['session']['turns'])} session turns.")


if __name__ == "__main__":
    main()
