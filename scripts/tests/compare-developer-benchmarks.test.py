"""Comparator must reject partial evidence and drift, while accepting timing noise."""
import copy
import runpy
import unittest
from pathlib import Path

module = runpy.run_path(str(Path(__file__).resolve().parents[1] / "compare-developer-benchmarks.py"))


class ComparisonTests(unittest.TestCase):
    def setUp(self):
        self.baseline = dict(schemaVersion=1, concurrency=1, warmups=3, rounds=3, samplesPerRound=30,
                            samples=[dict(Shape=shape, Round=r, Index=i, Status=200, Headers={"Cache-Control": "no-store, private"},
                                          Body="[]", LedgerCount=1, LedgerUnits=1, SqlCount=3, SqlHash="hash", ElapsedMilliseconds=1)
                                     for shape in module["SHAPES"] for r in range(3) for i in range(30)])

    def test_latency_noise_is_diagnostic(self):
        after = copy.deepcopy(self.baseline)
        after["samples"][0]["ElapsedMilliseconds"] = 500
        self.assertIn("PASS: 810", module["compare"](self.baseline, after))

    def test_missing_duplicate_and_contract_drift_fail(self):
        for field, value in {"Status": 503, "Body": "{}", "Headers": {}, "LedgerCount": 0, "LedgerUnits": 5,
                             "SqlCount": 4, "SqlHash": "changed", "Round": 99, "ElapsedMilliseconds": float("nan")}.items():
            with self.subTest(field=field):
                after = copy.deepcopy(self.baseline)
                after["samples"][0][field] = value
                with self.assertRaises(AssertionError):
                    module["compare"](self.baseline, after)
        for samples in [self.baseline["samples"][:-1], self.baseline["samples"] + [self.baseline["samples"][0]]]:
            with self.assertRaises(AssertionError):
                module["compare"](self.baseline, dict(self.baseline, samples=samples))


if __name__ == "__main__":
    unittest.main()
