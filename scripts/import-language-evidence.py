#!/usr/bin/env python3
"""Validate and append a Voice Lint results file, retaining its exact bytes and provenance."""
import argparse
import json
import os
import tempfile
from pathlib import Path
from language_evidence import CATALOG_DIR, ROOT, load, merge_results, refresh_priors, validate_catalog, validate_schema


def atomic_write(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(dir=path.parent, delete=False) as stream:
        temporary = Path(stream.name)
        stream.write(content)
    try:
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def encoded(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode('utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('results', type=Path)
    parser.add_argument('--source-repository', required=True, help='HTTPS URL of the producing Voice Lint repository')
    parser.add_argument('--check', action='store_true', help='validate the complete merge without writing')
    args = parser.parse_args()
    try:
        path = CATALOG_DIR / 'language-capabilities.json'
        priors_path = CATALOG_DIR / 'task-class-recommendations.json'
        catalog = load(path)
        validate_catalog(catalog, check_mirrors=True)
        raw = args.results.read_bytes()
        merged, mirror_path = merge_results(catalog, load(args.results), raw, args.source_repository)
        priors = refresh_priors(merged, load(priors_path))
        validate_schema(priors, 'task-class-recommendations.schema.json')
        mirror = ROOT / mirror_path
        if mirror.exists() and mirror.read_bytes() != raw:
            raise ValueError('Existing evidence mirror has different bytes')
        added = len(merged['records']) - len(catalog['records'])
        if not args.check:
            # Validate everything first. Evidence precedes projections; retries repair interrupted projections.
            if not mirror.exists():
                atomic_write(mirror, raw)
            atomic_write(path, encoded(merged))
            atomic_write(priors_path, encoded(priors))
        print(f'{"Validated" if args.check else "Imported"} {added} new language observations; mirror {mirror_path}')
        print('Next: python3 scripts/generate-website-data.py and dotnet test')
    except (OSError, ValueError, KeyError) as error:
        parser.exit(1, f'Language evidence rejected: {error}\n')


if __name__ == '__main__':
    main()
