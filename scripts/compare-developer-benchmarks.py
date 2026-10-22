"""RF-05: fail closed on protocol/ledger/SQL drift; report latency without a speedup gate."""
import json
import math
import statistics
import sys
from pathlib import Path

SHAPES = {"search", "list", "book", "chapters", "content", "invalid-key", "invalid-parameter", "missing", "exceeded"}
FIELDS = {"Shape", "Round", "Index", "Status", "Headers", "Body", "LedgerCount", "LedgerUnits", "SqlCount", "SqlHash", "ElapsedMilliseconds"}


def validated_samples(document):
    assert document["schemaVersion"] == 1
    assert document["concurrency"] == 1 and document["warmups"] == 3
    assert document["rounds"] == 3 and document["samplesPerRound"] == 30
    result = {}
    for sample in document["samples"]:
        assert set(sample) == FIELDS, "sample fields changed"
        key = (sample["Shape"], sample["Round"], sample["Index"])
        assert key not in result, f"duplicate sample {key}"
        assert math.isfinite(sample["ElapsedMilliseconds"]) and sample["ElapsedMilliseconds"] >= 0
        assert sample["SqlCount"] > 0, "missing SQL instrumentation"
        result[key] = sample
    assert set(result) == {(shape, r, i) for shape in SHAPES for r in range(3) for i in range(30)}, "incomplete observations"
    return result


def compare(before, after):
    left, right = validated_samples(before), validated_samples(after)
    assert {k: v for k, v in before.items() if k != "samples"} == {k: v for k, v in after.items() if k != "samples"}, "fixture parameters changed"
    for key in left:
        expected = {k: v for k, v in left[key].items() if k != "ElapsedMilliseconds"}
        actual = {k: v for k, v in right[key].items() if k != "ElapsedMilliseconds"}
        assert expected == actual, f"behavior/ledger/SQL drift at {key}"
    lines = ["PASS: 810 responses, headers, ledger deltas and SQL counts/hashes match.",
             "Latency is diagnostic only; frozen Core SLO and RF-03 gates still apply."]
    for shape in sorted(SHAPES):
        times = [[s["ElapsedMilliseconds"] for s in samples.values() if s["Shape"] == shape] for samples in (left, right)]
        values = [(statistics.median(t), sorted(t)[math.ceil(len(t) * .95) - 1]) for t in times]
        sql = sorted({s["SqlCount"] for s in right.values() if s["Shape"] == shape})
        lines.append(f"{shape}: SQL={sql}, before p50/p95={values[0][0]:.3f}/{values[0][1]:.3f} ms, after={values[1][0]:.3f}/{values[1][1]:.3f} ms")
    return "\n".join(lines)


if __name__ == "__main__":
    print(compare(*(json.loads(Path(path).read_text(encoding="utf-8")) for path in sys.argv[1:])))
