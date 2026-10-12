#!/usr/bin/env python3
"""Fail CI if any required project result is missing, failed or skipped."""
import argparse
from pathlib import Path
import xml.etree.ElementTree as ET

PROJECTS = {f"LinerNotes.{name}.Tests" for name in
            ("Domain", "Application", "DataAccess", "Infrastructure", "Presentation")}
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def validate(directory):
    projects = set()
    total = 0
    files = sorted(Path(directory).glob("*.trx"))
    if not files:
        raise ValueError("no TRX results")
    for file in files:
        root = ET.parse(file).getroot()
        counters = root.find("t:ResultSummary/t:Counters", NS)
        if counters is None:
            raise ValueError("missing TRX counters")
        count = int(counters.get("total", "0"))
        if count <= 0 or int(counters.get("passed", "0")) != count:
            raise ValueError("failed, skipped or missing tests")
        results = root.findall("t:Results/t:UnitTestResult", NS)
        if len(results) != count:
            raise ValueError("result count differs from counters")
        for result in results:
            if result.get("outcome") != "Passed":
                raise ValueError("non-passing result")
        file_projects = set()
        for method in root.findall("t:TestDefinitions/t:UnitTest/t:TestMethod", NS):
            codebase = method.get("codeBase", "").replace("\\", "/")
            name = Path(codebase).stem
            if name in PROJECTS:
                file_projects.add(name)
        if len(file_projects) != 1 or projects & file_projects:
            raise ValueError("unexpected or duplicate project result file")
        projects.update(file_projects)
        total += count
    if projects != PROJECTS:
        raise ValueError("required test project results missing")
    return total


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    args = parser.parse_args()
    try:
        total = validate(args.directory)
    except (ValueError, ET.ParseError) as error:
        parser.exit(1, f"FAIL test results: {error}\n")
    print(f"PASS test results: five projects, {total} passed, zero failures/skips")
