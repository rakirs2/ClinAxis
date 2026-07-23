#!/usr/bin/env python3
"""
MeSH Condition Mapping Experiment

Usage:
  python run.py fetch        # Step 1: Fetch 50 studies from CT.gov API v2
  python run.py mesh          # Step 2: Download MeSH → match all conditions + keywords → generate report
  python run.py report        # Step 3: Print the MeSH mapping report
  python run.py all           # Full pipeline: fetch → mesh → report
"""

import subprocess
import sys
import os

SCRIPTS = {
    "fetch": "fetch_ctgov.py",
    "mesh": "mesh_matcher.py",
    "report": "mesh_report.py",
    "ab-test": "ab_test.py",
}

PIPELINE = ["fetch", "mesh", "report"]


def run_script(name: str):
    script = SCRIPTS.get(name)
    if not script:
        print(f"Unknown step: {name}")
        print(f"Available: {', '.join(SCRIPTS.keys())}")
        return False
    print(f"\n{'='*60}")
    print(f"Step: {name} ({script})")
    print(f"{'='*60}")
    result = subprocess.run([sys.executable, script], capture_output=False)
    return result.returncode == 0


if __name__ == "__main__":
    os.chdir(os.path.dirname(os.path.abspath(__file__)))

    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)

    command = sys.argv[1]

    if command == "all":
        for step in PIPELINE:
            if not run_script(step):
                print(f"Step '{step}' failed. Aborting.")
                sys.exit(1)
        print(f"\n{'='*60}")
        print("Pipeline complete!")
        print(f"{'='*60}")
    elif command in SCRIPTS:
        if not run_script(command):
            sys.exit(1)
    else:
        print(f"Unknown command: {command}")
        print(__doc__)
        sys.exit(1)
