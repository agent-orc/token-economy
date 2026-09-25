"""Contract/schema, importer transaction and publication regression tests; all scores are synthetic."""
import copy
import hashlib
import importlib.util
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from language_evidence import (CATALOG_DIR, DIMENSIONS, ROOT, load, merge_results,
                               validate_catalog, validate_results, validate_schema, latest_records, refresh_priors)


def fixture():
    return dict(schemaVersion=1, asOfDate='2026-09-26',
                method=dict(rubricRef='docs/human-friendly-language/rubric.md', samples=40,
                            languages=['de', 'en'], raters='Blind panel with operator spot check',
                            promptSet='benchmarks/human-friendly-language/prompts/'),
                dimensions=DIMENSIONS.copy(), results=[
                    dict(modelId='gpt-6-luna', cliId='codex', thinkingLevel='medium', language='de',
                         scores={d: .8 for d in DIMENSIONS}, overall=.8, costPerSampleUsd=.0001,
                         evidenceRef='benchmarks/human-friendly-language/runs/test-run/', notes='Synthetic test fixture.')], studies=[])


class LanguageEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.catalog = load(CATALOG_DIR / 'language-capabilities.json')

    def merge(self, result):
        return merge_results(self.catalog, result, json.dumps(result).encode(), 'https://example.org/voice-lint')

    def test_seed_schema_and_missing_evidence_rejected(self):
        validate_catalog(self.catalog, check_mirrors=True)
        for value in ([], None):
            bad = copy.deepcopy(self.catalog)
            bad['records'][0]['evidence'] = value
            with self.assertRaises(ValueError):
                validate_schema(bad, 'language-capabilities.schema.json')
        bad = copy.deepcopy(self.catalog)
        del bad['records'][0]['evidence']
        with self.assertRaises(ValueError):
            validate_schema(bad, 'language-capabilities.schema.json')

    def test_contract_accepts_shared_shape_and_zero_is_a_real_score(self):
        data = fixture()
        validate_results(data)
        data['results'][0].update(scores={d: 0 for d in DIMENSIONS}, overall=0, costPerSampleUsd=0)
        validate_results(data)
        data['method']['weights'] = {d: 1 for d in DIMENSIONS}
        validate_results(data)

    def test_contract_rejects_malformed_rows_and_method(self):
        cases = [
            ('schemaVersion', 2), ('asOfDate', '2026-02-30'), ('dimensions', DIMENSIONS[:-1]),
            ('results', []), ('studies', [dict(id='bad')]), ('method.samples', 0), ('method.raters', ''),
            ('method.languages', ['fr']), ('row.modelId', 'luna'), ('row.modelId', 'invented'),
            ('row.cliId', 'claude-code'), ('row.thinkingLevel', 'unspecified'), ('row.thinkingLevel', 'ultra'),
            ('row.language', 'fr'), ('row.overall', .7), ('row.overall', None), ('row.overall', True),
            ('row.costPerSampleUsd', -1), ('row.costPerSampleUsd', None), ('row.evidenceRef', ''),
            ('row.evidenceRef', 'benchmarks/human-friendly-language/runs/../'),
            ('row.scores', {d: 1.1 for d in DIMENSIONS}), ('row.scores', {d: .8 for d in DIMENSIONS[:-1]}),
            ('row.scores', {d: None for d in DIMENSIONS}),
            ('method.weights', {d: i + 1 for i, d in enumerate(DIMENSIONS)}),
        ]
        for field, value in cases:
            with self.subTest(field=field, value=value):
                bad = fixture()
                scope, _, name = field.partition('.')
                if scope == 'row': bad['results'][0][name] = value
                elif scope == 'method' and name: bad['method'][name] = value
                else: bad[field] = value
                with self.assertRaises(ValueError): validate_results(bad)
        bad = fixture(); bad['method']['languages'] = ['en']
        with self.assertRaises(ValueError): validate_results(bad)
        bad = fixture(); bad['results'].append(copy.deepcopy(bad['results'][0]))
        with self.assertRaises(ValueError): validate_results(bad)

    def test_merge_is_append_only_idempotent_and_retains_method_hash_and_provenance(self):
        result = fixture()
        result['studies'] = [dict(id='S1', title='Test study', authors='Test author', year=2026,
                                 url='https://example.org/study', kind='preprint', claim='Test claim', relevance='Test relevance')]
        original = copy.deepcopy(self.catalog)
        merged, mirror = self.merge(result)
        self.assertEqual(self.catalog, original)
        self.assertEqual(merged['records'][:-1], self.catalog['records'])
        row = merged['records'][-1]
        self.assertEqual(row['provenance']['sourceSha256'], hashlib.sha256(json.dumps(result).encode()).hexdigest())
        self.assertEqual(row['evidence'][0]['observedAtUtc'], '2026-09-26T00:00:00Z')
        self.assertEqual(row['provenance']['mirrorPath'], mirror)
        self.assertEqual(merged['studies'][-1]['status'], 'unverifiedClaim')
        self.assertIsNone(merged['studies'][-1]['verifiedAtUtc'])
        again, again_path = merge_results(merged, result, json.dumps(result).encode(), 'https://example.org/voice-lint')
        self.assertEqual(again, merged); self.assertEqual(again_path, mirror)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); path = root / mirror; path.parent.mkdir(parents=True)
            path.write_bytes(json.dumps(result).encode())
            validate_catalog(merged, check_mirrors=True, root=root)
            path.write_text('{}')
            with self.assertRaises(ValueError): validate_catalog(merged, check_mirrors=True, root=root)

    def test_new_runs_keep_history_and_latest_failure_is_not_replaced_by_an_older_pass(self):
        result = fixture(); first, _ = self.merge(result)
        result['asOfDate'] = '2026-09-27'; row = result['results'][0]
        row.update(overall=.2, scores={d: .2 for d in DIMENSIONS}, evidenceRef='benchmarks/human-friendly-language/runs/test-2/')
        second, _ = merge_results(first, result, json.dumps(result).encode(), 'https://example.org/voice-lint')
        self.assertEqual(len(second['records']), len(first['records']) + 1)
        selected = [r for r in latest_records(second) if r['status'] == 'observed']
        self.assertEqual(selected[0]['overall'], .2)
        before = [r for r in latest_records(second, '2026-09-26T23:59:59Z') if r['status'] == 'observed']
        self.assertEqual(before[0]['overall'], .8)

    def test_same_run_cannot_be_rewritten(self):
        result = fixture(); first, _ = self.merge(result)
        result['results'][0]['costPerSampleUsd'] = .9
        with self.assertRaises(ValueError):
            merge_results(first, result, json.dumps(result).encode(), 'https://example.org/voice-lint')

    def test_priors_name_cheapest_qualifying_models_and_evidence_status(self):
        result = fixture()
        result['results'].append(dict(result['results'][0], modelId='gpt-5.6-luna', costPerSampleUsd=.02))
        merged, _ = self.merge(result)
        priors = refresh_priors(merged, load(CATALOG_DIR / 'task-class-recommendations.json'))
        for prior in priors['languagePriors']:
            expected = ['gpt-6-luna', 'gpt-5.6-luna'] if prior['language'] == 'de' else []
            self.assertEqual([c['modelId'] for c in prior['candidates']], expected)
            self.assertTrue(all(c['evidenceStatus'] == 'observed' for c in prior['candidates']))
        validate_schema(priors, 'task-class-recommendations.schema.json')

    def test_import_command_validates_before_writing_and_repeated_import_is_byte_stable(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            shutil.copytree(ROOT / 'scripts', root / 'scripts', ignore=shutil.ignore_patterns('__pycache__'))
            shutil.copytree(CATALOG_DIR, root / 'src/TokenEconomy/catalog')
            path = root / 'results.json'; path.write_text(json.dumps(fixture()))
            command = [sys.executable, str(root / 'scripts/import-language-evidence.py'), str(path),
                       '--source-repository', 'https://example.org/voice-lint']
            catalog = root / 'src/TokenEconomy/catalog/language-capabilities.json'
            initial = catalog.read_bytes()
            result = subprocess.run(command + ['--check'], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(initial, catalog.read_bytes())
            for _ in range(2):
                result = subprocess.run(command, capture_output=True, text=True)
                self.assertEqual(result.returncode, 0, result.stderr)
                if _ == 0: first = catalog.read_bytes()
                else: self.assertEqual(first, catalog.read_bytes())
            mirrored = list((catalog.parent / 'language-evidence').glob('*.json'))
            self.assertEqual(len(mirrored), 1)
            self.assertEqual(mirrored[0].read_bytes(), path.read_bytes())
            self.assertEqual(load(mirrored[0])['method'], fixture()['method'])
            bad = fixture(); del bad['results'][0]['evidenceRef']; path.write_text(json.dumps(bad))
            result = subprocess.run(command, capture_output=True, text=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertEqual(first, catalog.read_bytes())

    def test_json_reader_rejects_duplicate_properties_and_non_finite_numbers(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'bad.json'
            for raw in ['{"x":1,"x":2}', '{"x":NaN}', '{"x":Infinity}', '{"x":1e999}']:
                path.write_text(raw)
                with self.assertRaises(ValueError): load(path)

    def test_website_projection_is_current_and_cost_ratios_handle_unknown_and_zero(self):
        spec = importlib.util.spec_from_file_location('website_data', ROOT / 'scripts/generate-website-data.py')
        module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
        actual = load(ROOT / 'website/data/language-capabilities.json')
        self.assertEqual(actual, module.create_language_payload())
        self.assertTrue(all(r['costPerQualityPointUsd'] is None for r in actual['costQuality']))
        data = fixture()
        merged, _ = self.merge(data)
        measured = module.project_language_payload(merged, {})
        observed = next(r for r in measured['costQuality'] if r['status'] == 'observed')
        self.assertAlmostEqual(observed['costPerQualityPointUsd'], .000125)
        data['results'][0].update(overall=0, scores={d: 0 for d in DIMENSIONS})
        merged, _ = self.merge(data)
        zero = module.project_language_payload(merged, {})
        observed = next(r for r in zero['costQuality'] if r['status'] == 'observed')
        self.assertIsNone(observed['costPerQualityPointUsd'])


if __name__ == '__main__':
    unittest.main()
