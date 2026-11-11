#!/usr/bin/env python3
"""Fails when backend line coverage is below a threshold.

Each test project writes its own Cobertura report. A line counts as covered
when any report shows it was run, so the unit and integration suites are
measured together. Which files count is decided by coverage.settings.xml
(generated code and migrations are left out).

Usage: coverage-gate.py THRESHOLD REPORT [REPORT ...]
"""
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict


def main() -> int:
    threshold = float(sys.argv[1])
    hits = {}  # (package, file, line) -> covered
    for report in sys.argv[2:]:
        for package in ET.parse(report).getroot().iter("package"):
            for cls in package.iter("class"):
                for line in cls.findall("./lines/line"):
                    key = (package.get("name"), cls.get("filename"), line.get("number"))
                    hits[key] = hits.get(key, False) or int(line.get("hits", "0")) > 0

    if not hits:
        print("::error::No coverage data found.")
        return 1

    per_package = defaultdict(lambda: [0, 0])
    for (package, _, _), covered in hits.items():
        per_package[package][0] += covered
        per_package[package][1] += 1

    for package, (covered, total) in sorted(per_package.items()):
        print(f"{package:<28} {100 * covered / total:6.1f}%  ({covered}/{total} lines)")

    covered = sum(hits.values())
    percent = 100 * covered / len(hits)
    print(f"{'Total':<28} {percent:6.1f}%  ({covered}/{len(hits)} lines), threshold {threshold:.0f}%")

    if percent < threshold:
        print(f"::error::Backend line coverage {percent:.1f}% is below {threshold:.0f}%.")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
