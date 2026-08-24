#!/usr/bin/env bash
# Runs Conduit's build, test suite, and CLI, capturing everything printed to the console into a
# single timestamped log file — so you can hand that file back to Claude for review instead of
# re-describing what happened. macOS/Linux/WSL twin of scripts/run-and-log.ps1 (Windows).
#
# Usage:
#   ./scripts/run-and-log.sh                       # runs against fixtures/
#   ./scripts/run-and-log.sh /path/to/your/files    # runs against every .cii there (a caesar.cfg
#                                                    # in that same folder is picked up automatically)
#
# When it's done: git add the printed log file (and any *-output.cii files you want kept) and
# commit + push, then tell Claude which run to look at.
set -uo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

input_path="${1:-fixtures}"
log_dir="test-logs"
mkdir -p "$log_dir"

timestamp="$(date +%Y-%m-%d_%H%M%S)"
log_path="$log_dir/${timestamp}-run.log"

section() {
  {
    echo
    printf '=%.0s' {1..80}
    echo
    echo "$1"
    printf '=%.0s' {1..80}
    echo
  } | tee -a "$log_path"
}

{
  echo "Conduit test run — $timestamp"
  echo "Machine: $(uname -a)"
  echo "dotnet: $(dotnet --version 2>&1)"
} | tee "$log_path"

section "dotnet build"
dotnet build 2>&1 | tee -a "$log_path"

section "dotnet test"
dotnet test 2>&1 | tee -a "$log_path"

if [ -d "$input_path" ]; then
  targets=("$input_path"/*.cii)
else
  targets=("$input_path")
fi

for target in "${targets[@]}"; do
  [ -f "$target" ] || continue
  base="$(basename "${target%.cii}")"
  out_file="$log_dir/${timestamp}-${base}-output.cii"
  section "conduit optimize $target -> $out_file"
  dotnet run --project src/Conduit.Cli -- optimize "$target" "$out_file" 2>&1 | tee -a "$log_path"
  echo "Exit code: ${PIPESTATUS[0]}" | tee -a "$log_path"
done

section "Done"
echo "Log written to: $log_path" | tee -a "$log_path"
echo
echo "Log file: $log_path"
echo "Commit it (git add \"$log_path\") and push, then point Claude at it."
