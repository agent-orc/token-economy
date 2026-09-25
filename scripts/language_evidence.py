"""Shared deterministic language-catalog validation, selection and import logic."""
from __future__ import annotations

import copy
import hashlib
import json
import math
from datetime import date, datetime, timezone
from pathlib import Path
from urllib.parse import urlsplit

ROOT = Path(__file__).resolve().parents[1]
CATALOG_DIR = ROOT / 'src/TokenEconomy/catalog'
DIMENSIONS = ['readability', 'warmth', 'directness', 'absenceOfAiIsms', 'toneAdherence', 'factualRestraint']
SOURCE_PATH = 'benchmarks/human-friendly-language/results.json'
STATUS = {'unverifiedClaim': 0, 'provisional': 1, 'observed': 2}


def load(path):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f'Duplicate JSON property: {key}')
            result[key] = value
        return result
    def finite(value):
        parsed = float(value)
        if not math.isfinite(parsed):
            raise ValueError(f'Non-finite number: {value}')
        return parsed
    return json.loads(Path(path).read_bytes(), object_pairs_hook=unique, parse_float=finite,
                      parse_constant=lambda value: (_ for _ in ()).throw(ValueError(f'Non-finite number: {value}')))


def validate_schema(document, name):
    try:
        from jsonschema import Draft202012Validator, FormatChecker
    except ImportError as error:
        raise ValueError('Install validation tools: python3 -m pip install -r scripts/requirements-language.txt') from error
    schema = load(CATALOG_DIR / name)
    Draft202012Validator.check_schema(schema)
    validator = Draft202012Validator(schema, format_checker=FormatChecker())
    errors = sorted(validator.iter_errors(document), key=lambda e: str(list(e.path)))
    if errors:
        error = errors[0]
        raise ValueError(f'{name}: {list(error.path)}: {error.message}')


def policy_models():
    return {m['canonicalId']: m for m in load(CATALOG_DIR / 'model-routing-policy.json')['models']}


def validate_identity(row, models, unspecified=False):
    model = models.get(row['modelId'])
    if model is None:
        raise ValueError(f'Unknown canonical model: {row["modelId"]}')
    expected_cli = 'codex' if model['providerId'] == 'openai' else 'claude-code'
    if row['cliId'] != expected_cli:
        raise ValueError(f'CLI does not run {row["modelId"]}')
    if row['thinkingLevel'] not in model['supportedThinkingLevels']:
        if not (unspecified and row['thinkingLevel'] == 'unspecified'):
            raise ValueError(f'Unsupported thinking level for {row["modelId"]}')


def validate_mean(row):
    if row['overall'] is not None:
        scores = list(row['scores'].values())
        if any(s is None for s in scores) or abs(sum(scores) / 6 - row['overall']) > 0.000001:
            raise ValueError('overall must be the equally weighted six-dimension mean (tolerance 0.000001)')


def validate_results(document):
    validate_schema(document, 'language-evidence-results.schema.json')
    models = policy_models()
    seen = set()
    weights = document['method'].get('weights', {d: 1 for d in DIMENSIONS})
    if len(set(weights.values())) != 1:
        raise ValueError('v1 uses equal dimension weights; unequal rubric weights need a new contract version')
    for row in document['results']:
        validate_identity(row, models)
        validate_mean(row)
        if row['language'] not in document['method']['languages']:
            raise ValueError('Result language is absent from method.languages')
        key = (row['modelId'], row['language'], row['thinkingLevel'])
        if key in seen:
            raise ValueError(f'Duplicate model/language/thinking-level row: {key}')
        seen.add(key)
    study_ids = [study['id'] for study in document['studies']]
    if len(set(study_ids)) != len(study_ids):
        raise ValueError('Duplicate study id')


def validate_catalog(document, *, check_mirrors=False, root=ROOT):
    validate_schema(document, 'language-capabilities.schema.json')
    models = policy_models()
    ids = set()
    studies = set()
    for study in document['studies']:
        if study['id'] in studies:
            raise ValueError('Duplicate study id')
        studies.add(study['id'])
        if study['verifiedAtUtc'] and study['verifiedAtUtc'][:10] > document['asOfDate']:
            raise ValueError('Study verification is later than catalog date')
    for row in document['records']:
        if row['id'] in ids:
            raise ValueError(f'Duplicate record id: {row["id"]}')
        ids.add(row['id'])
        validate_identity(row, models, unspecified=True)
        validate_mean(row)
        if any(e['observedAtUtc'][:10] > document['asOfDate'] for e in row['evidence']):
            raise ValueError('Evidence is later than catalog date')
        if row['status'] == 'observed' and check_mirrors:
            provenance = row['provenance']
            if provenance['mirrorPath'] != f'src/TokenEconomy/catalog/language-evidence/{provenance["sourceSha256"]}.json':
                raise ValueError('Evidence mirror filename must match its hash')
            mirror = root / provenance['mirrorPath']
            if not mirror.is_file() or hashlib.sha256(mirror.read_bytes()).hexdigest() != provenance['sourceSha256']:
                raise ValueError(f'Missing or changed mirrored evidence: {mirror}')
            source = load(mirror)
            validate_results(source)
            matches = [r for r in source['results'] if all(r[k] == row[k] for k in
                       ('modelId', 'cliId', 'language', 'thinkingLevel', 'scores', 'overall', 'costPerSampleUsd'))]
            if len(matches) != 1:
                raise ValueError('Catalog observation disagrees with mirrored source')
            benchmark = [e for e in row['evidence'] if e['kind'] == 'localBenchmark']
            if not any(e['reference'] == matches[0]['evidenceRef']
                       and e['observedAtUtc'] == source['asOfDate'] + 'T00:00:00Z' for e in benchmark):
                raise ValueError('Catalog provenance disagrees with source run or date')


def latest_records(document, at_utc=None):
    """Match LanguageCapabilityCatalog.Find, preserving separate languages and efforts."""
    selected = {}
    for row in sorted(document['records'], key=lambda r: r['id']):
        observed = max(e['observedAtUtc'] for e in row['evidence'])
        if at_utc and observed > at_utc:
            continue
        key = (row['modelId'], row['language'], row['thinkingLevel'])
        rank = (STATUS[row['status']], observed)
        if key not in selected or rank > selected[key][0]:
            selected[key] = (rank, row)
    return [value[1] for value in selected.values()]


def satisfies(row, requirement):
    if row['status'] != 'observed' and not (requirement['allowProvisional'] and row['status'] == 'provisional'):
        return False
    if row['language'] != requirement['language'] or row['thinkingLevel'] != requirement['thinkingLevel']:
        return False
    if row['overall'] is None or row['overall'] < requirement['minimumOverall']:
        return False
    return all(row['scores'][d] is not None and row['scores'][d] >= threshold
               for d, threshold in requirement['minimumScores'].items())


def refresh_priors(document, recommendations):
    """Text variants stay on DocEdit; these candidates are language constraints, never risk routes."""
    result = copy.deepcopy(recommendations)
    models = policy_models()
    # For equal sample costs, the default DocEdit compatibility order at tight budget.
    prices = load(CATALOG_DIR / 'model-prices.json')
    def tie(row):
        model = models[row['modelId']]
        listing = next(p for p in prices if p['modelId'] == row['modelId'])
        at = document['asOfDate'] + 'T23:59:59Z'
        history = [p for p in listing['history'] if p['validFrom'] <= at and (not p.get('validTo') or at <= p['validTo'])]
        price = max(history, key=lambda p: p['validFrom']) if history else None
        amount = price['inputPerMTok'] + .2 * price['outputPerMTok'] if price else None
        cost_class = 3 if amount is None else 0 if amount < 4 else 1 if amount < 8 else 2
        suitability, base = {'light': (3, 100), 'balanced': (2, 60), 'frontier': (1, 30)}[model['capabilityTier']]
        score = base + [20, 0, -20, -20][cost_class]
        return (-score, -suitability, cost_class, list(models).index(row['modelId']))
    for prior in result.get('languagePriors', []):
        passing = [r for r in latest_records(document) if satisfies(r, prior['requirement'])
                   and models[r['modelId']]['routingStatus'] == 'selectable'
                   and 'coreTask' in models[r['modelId']]['workflowRoles']]
        passing.sort(key=lambda r: (r['costPerSampleUsd'] is None, r['costPerSampleUsd'] or 0, *tie(r)))
        prior['asOfDate'] = document['asOfDate']
        prior['candidates'] = [dict(modelId=r['modelId'], thinkingLevel=r['thinkingLevel'], evidenceStatus=r['status'],
                                    recordId=r['id'], costPerSampleUsd=r['costPerSampleUsd']) for r in passing]
        prior['note'] = ('Cheapest measured sample first; apply concrete-task correctness floors before admission.' if passing else
                         'No retained measured row meets the requirement yet. Import Voice Lint evidence; an empty set means wait, not an invented cheapest model.')
    return result


def merge_results(catalog, document, source_bytes, source_repository):
    validate_results(document)
    validate_catalog(catalog)
    if json.loads(source_bytes) != document:
        raise ValueError('Parsed result does not match the bytes retained as evidence')
    url = urlsplit(source_repository)
    if url.scheme != 'https' or not url.netloc or url.query or url.fragment:
        raise ValueError('source-repository must be an HTTPS repository URL without query or fragment')
    result = copy.deepcopy(catalog)
    digest = hashlib.sha256(source_bytes).hexdigest()
    mirror_path = f'src/TokenEconomy/catalog/language-evidence/{digest}.json'
    existing = {r['id']: r for r in result['records']}
    for row in document['results']:
        for previous in catalog['records']:
            if previous['status'] == 'observed' and all(previous[k] == row[k] for k in ('modelId', 'language', 'thinkingLevel')):
                if any(e['kind'] == 'localBenchmark' and e['reference'] == row['evidenceRef'] for e in previous['evidence']):
                    if any(previous[k] != row[k] for k in ('scores', 'overall', 'costPerSampleUsd', 'cliId')):
                        raise ValueError('An existing run observation cannot be rewritten; use a new run reference')
        record = {k: copy.deepcopy(v) for k, v in row.items() if k not in ('evidenceRef', 'notes')}
        record.update(id=f'voice-lint-{digest[:16]}-{row["modelId"]}-{row["language"]}-{row["thinkingLevel"]}',
                      status='observed', notes=row['notes'] or 'Imported Voice Lint result; see mirrored method and run evidence.',
                      evidence=[dict(kind='localBenchmark', reference=row['evidenceRef'],
                                     observedAtUtc=document['asOfDate'] + 'T00:00:00Z',
                                     note='Voice Lint result date at UTC midnight (day precision); observed is not a statistical validation claim.')],
                      provenance=dict(sourceRepository=source_repository.rstrip('/'), sourcePath=SOURCE_PATH,
                                      sourceSha256=digest, mirrorPath=mirror_path))
        if record['id'] in existing:
            if existing[record['id']] != record:
                raise ValueError('Conflicting provenance for an already imported result')
        else:
            result['records'].append(record)
    for study in document['studies']:
        imported = dict(study, id=f'voice-lint-{digest[:16]}-{study["id"]}', status='unverifiedClaim', verifiedAtUtc=None)
        if not any(s['id'] == imported['id'] for s in result['studies']):
            result['studies'].append(imported)
    result['asOfDate'] = max(result['asOfDate'], document['asOfDate'])
    validate_catalog(result)
    return result, mirror_path
