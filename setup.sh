#!/usr/bin/env bash
set -euo pipefail
set -x   # echo commands so a failure is easy to spot in logs

# Fast, parallel installs — keep total under ~5 minutes
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt &      # background heavy installs
# other independent setup here &
wait                                    # block until backgrounded jobs finish

# Non-critical steps shouldn't block session start:
# some_optional_tool --warm-cache || true
