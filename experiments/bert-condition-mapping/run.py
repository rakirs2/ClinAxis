#!/usr/bin/env python3
"""
BERT Condition Mapping Pipeline

Usage:
  python run.py all          # Full pipeline: download → review → train → normalize → evaluate → export
  python run.py download     # Step 1: Download training data from API
  python run.py review       # Step 2: Manual review (flag conditions as valid/invalid)
  python run.py train        # Step 3: Train BERT classifier
  python run.py normalize    # Step 4: Normalize conditions with Sentence-BERT
  python run.py evaluate     # Step 5: Evaluate models vs rule-based approach
  python run.py export       # Step 6: Export mapping table + ONNX model
"""

import subprocess
import sys
import os

SCRIPTS = {
    "download": "download_data.py",
    "review": "review.py",
    "train": "train.py",
    "normalize": "normalize.py",
    "evaluate": "evaluate.py",
    "export": "export.py",
}

PIPELINE = ["download", "review", "train", "normalize", "evaluate", "export"]


def run_script(name: str):
    script = SCRIPTS.get(name)
    if not script:
        print(f"Unknown step: {name}")
        print(f"Available: {', '.join(SCRIPTS.keys())}")
        return False
    print(f"\n{'='*60}")
    print(f"Step: {name} ({script})")
    print(f"{'='*60}\n")
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
        print("\n" + "=" * 60)
        print("Pipeline complete!")
        print(f"{'='*60}")
    elif command in SCRIPTS:
        if not run_script(command):
            sys.exit(1)
    else:
        print(f"Unknown command: {command}")
        print(__doc__)
        sys.exit(1)
