"""Fail the workflow step if any finding meets or exceeds MIN_SEVERITY.

Mirrors JIT-Optimization-Engine/scripts/severity_gate.py.
"""

from __future__ import annotations

import json
import os
import sys

ORDER = {"LOW": 0, "MEDIUM": 1, "HIGH": 2, "CRITICAL": 3}


def main() -> int:
    report_path = os.environ["REPORT_JSON"]
    min_severity = os.environ["MIN_SEVERITY"].strip().upper()

    if min_severity not in ORDER:
        print(f"::error::fail-on-severity must be one of {list(ORDER)}, got '{min_severity}'.")
        return 2

    with open(report_path, encoding="utf-8") as f:
        predictions = json.load(f)["predictions"]

    threshold = ORDER[min_severity]
    blocking = [
        (p["systemName"], f)
        for p in predictions
        for f in p["findings"]
        if ORDER.get(f["severity"], 0) >= threshold
    ]

    if blocking:
        print(f"::error::{len(blocking)} finding(s) at or above {min_severity}:")
        for system_name, finding in blocking:
            print(f"::error::  {system_name} — {finding['severity']} — {finding['title']}")
        return 1

    print(f"No findings at or above {min_severity}.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
