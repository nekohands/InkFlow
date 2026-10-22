"""RF-03 frozen budgets; compare raw RF-02-format measurements without dependencies."""
import json
import statistics
import sys


def compare(before, after):
    errors = []
    for key in ("mode", "framework", "os", "cpuCount", "warmups", "samplesPerRound", "rounds", "concurrency"):
        if before[key] != after[key]:
            errors.append(f"Environment mismatch: {key}")
    postgres = before["mode"] == "postgres-end-to-end"
    if postgres and (before["database"], before["cache"]) != (after["database"], after["cache"]):
        errors.append("Database/cache mismatch")
    index = lambda data: {(m["size"], m["corrupt"], m["round"]): m for m in data["measurements"]}
    old, new = index(before), index(after)
    expected = {(size, corrupt, round_) for size in (0, 1000, 10000, 100000)
                for corrupt in (False, True) for round_ in (1, 2, 3)}
    if set(old) != expected or set(new) != expected or len(before["measurements"]) != 24 or len(after["measurements"]) != 24:
        return ["Expected exactly 24 fixture/round records"]
    for key in expected:
        for field in ("completeHash", "reportHash", "TotalIssueCount") + (("rows", "queries") if postgres else ()):
            if old[key][field] != new[key][field]:
                errors.append(f"Semantic mismatch {key}: {field}")
        for record in (old[key], new[key]):
            if len(record["samples"]) != 30:
                errors.append(f"Expected 30 samples: {key}")
            if postgres and any(s["sqlCount"] != 13 for s in record["samples"]):
                errors.append(f"SQL count changed: {key}")
    for size in (0, 1000, 10000, 100000):
        for corrupt in (False, True):
            rows = [[data[size, corrupt, r] for r in (1, 2, 3)] for data in (old, new)]
            allocations = [statistics.median(s["allocated"] for row in group for s in row["samples"]) for group in rows]
            if allocations[1] > allocations[0] * 1.05 + 65536:
                errors.append(f"Allocation regression: {size}/{corrupt}")
            if size == 100000 and allocations[1] > allocations[0] * (0.9 if postgres else 0.85):
                errors.append(f"Allocation improvement target missed: {size}/{corrupt}")
            timings = {}
            for metric in ("p50", "p95"):
                values = [statistics.median(row[metric] for row in group) for group in rows]
                timings[metric] = values
                factor, slack = (1.5, 5) if postgres and size <= 1000 else (1.2, 1)
                if values[1] > values[0] * factor + slack:
                    errors.append(f"{metric} regression: {size}/{corrupt}")
                if not postgres and size == 100000 and corrupt and values[1] > values[0] * 0.8:
                    errors.append(f"{metric} improvement target missed: {size}/{corrupt}")
            print(f"{size}/{corrupt}: p50={timings['p50']}, p95={timings['p95']}, allocated={allocations}")
    return errors


if __name__ == "__main__":
    with open(sys.argv[1], encoding="utf-8-sig") as source:
        before = json.load(source)
    with open(sys.argv[2], encoding="utf-8-sig") as source:
        after = json.load(source)
    failures = compare(before, after)
    for failure in failures:
        print("FAIL:", failure)
    print("FAIL" if failures else "PASS: frozen RF-03 budgets and report equivalence")
    sys.exit(bool(failures))
