import contextlib
import copy
import importlib.util
import io
from pathlib import Path
import sys
import unittest

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("budgets", Path(__file__).parents[1] / "compare-consistency-benchmarks.py")
budgets = importlib.util.module_from_spec(spec)
spec.loader.exec_module(budgets)


class BudgetTests(unittest.TestCase):
    def setUp(self):
        self.before = dict(mode="rules-only", framework="test", os="test", cpuCount=1,
                           warmups=3, samplesPerRound=30, rounds=3, concurrency=1)
        self.before["measurements"] = [dict(size=size, corrupt=corrupt, round=round_,
            completeHash="complete", reportHash="report", TotalIssueCount=2000 if corrupt else 0,
            p50=100, p95=120, samples=[dict(allocated=1000000) for _ in range(30)])
            for size in (0, 1000, 10000, 100000) for corrupt in (False, True) for round_ in (1, 2, 3)]
        self.after = copy.deepcopy(self.before)
        for row in self.after["measurements"]:
            row.update(p50=70, p95=80)
            for sample in row["samples"]:
                sample["allocated"] = 700000

    def compare(self):
        with contextlib.redirect_stdout(io.StringIO()):
            return budgets.compare(self.before, self.after)

    def test_repeatable_gain_passes(self):
        self.assertEqual([], self.compare())

    def test_unchanged_baseline_does_not_satisfy_improvement(self):
        self.after = copy.deepcopy(self.before)
        self.assertTrue(self.compare())

    def test_truncated_or_changed_result_fails(self):
        self.after["measurements"][0]["completeHash"] = "different"
        self.assertTrue(self.compare())

    def test_missing_round_fails(self):
        self.after["measurements"].pop()
        self.assertTrue(self.compare())

    def test_other_shape_latency_regression_fails(self):
        for row in self.after["measurements"]:
            if row["size"] == 10000:
                row["p95"] = 200
        self.assertTrue(self.compare())

    def test_environment_change_requires_rebaseline(self):
        self.after["framework"] = "different"
        self.assertTrue(self.compare())


if __name__ == "__main__":
    unittest.main()
