How to test Conduit. The first section is a step-by-step tutorial for testing the program on your
own computer, from GitHub all the way to seeing it run. The sections after that are a developer
reference (what each automated test covers, how to add fixtures) — useful once you're past the
tutorial, not required reading to get started.

Kept up to date per CLAUDE.md: update this file whenever what/how to test changes (a new project,
a new fixture convention, a new manual check that matters), and consult it whenever testing is
relevant to the task at hand.

# Step-by-step: test Conduit on your own machine

This walks through everything from "I have nothing installed" to "I ran Conduit and can see what
it did." Commands are shown for **Windows PowerShell** first (since that's where CAESAR II lives),
with the macOS/Linux equivalent noted where it differs. Nothing here requires CAESAR II to be
installed — this whole tutorial runs standalone.

## 1. Install prerequisites

You need two things: **Git** (to get the code) and the **.NET 8 SDK** (to build and run it).

**Windows (PowerShell):**
```powershell
winget install --id Git.Git -e
winget install --id Microsoft.DotNet.SDK.8
```
Close and reopen PowerShell after installing so both are on your `PATH`, then check:
```powershell
git --version
dotnet --version   # should print something like 8.0.x
```
If you don't have `winget`, install Git from https://git-scm.com/downloads and the .NET 8 SDK
from https://dotnet.microsoft.com/download/dotnet/8.0 instead, then reopen your terminal.

**macOS/Linux:** install Git via your usual package manager, and the .NET 8 SDK from
https://dotnet.microsoft.com/download/dotnet/8.0 (or `brew install dotnet-sdk` on macOS). Same
`git --version` / `dotnet --version` check applies.

## 2. Get the code

Pick a folder for it and clone the repository:
```powershell
cd C:\Users\<you>\source          # or wherever you keep code; create the folder first if needed
git clone https://github.com/acadri01/Conduit.git
cd Conduit
```

**This work is currently on a branch that hasn't been merged into `main` yet**, so you need to
check that branch out explicitly (once it's merged, `git checkout main` + `git pull` is enough —
check the PR on GitHub to see if it says "Merged"):
```powershell
git fetch origin claude/project-setup-phase-1-5zonpw
git checkout claude/project-setup-phase-1-5zonpw
```

## 3. Build it

From the repository root:
```powershell
dotnet build
```
First run downloads NuGet packages, so it'll take a little longer. You want to see
`Build succeeded.` with `0 Error(s)` at the end. If you see errors here, stop and report them —
nothing past this point will work.

## 4. Run the automated test suite (sanity check)

```powershell
dotnet test
```
Expect `Passed!` with every test passing (30 tests as of this writing — the exact count will grow
over time, that's fine). This confirms your machine's setup is fine and the code itself is
healthy, independent of anything you do manually next.

## 5. Run Conduit on the example file

Conduit's CLI takes an input neutral file (`.cii`) and an output path:
```powershell
dotnet run --project src\Conduit.Cli -- optimize fixtures\straight-run.cii out.cii
```
(macOS/Linux: same command, but paths use `/` — `src/Conduit.Cli` and `fixtures/straight-run.cii`.)

You should see output like:
```
Conduit optimize: fixtures\straight-run.cii -> out.cii

  Piping code assumed: B31.3_2020 (from caesar.cfg)
  Material database (caesar.cfg): system directory 'SYSTEM', user material file 'UMAT1.UMD'

  - Placed 3 initial support(s): node 60 (Rest), node 110 (Rest), node 160 (Rest)

Iterations: 1

PASS
```
What this means:
- It read `fixtures\straight-run.cii` (a small, synthetic — not real-project — example file
  committed in this repo for exactly this purpose).
- It also picked up `fixtures\caesar.cfg` — a real (non-proprietary, example) CAESAR II settings
  file that happens to already sit right next to that fixture in this repo, which is why you see
  the "Piping code assumed" and "Material database" lines (more on this file in step 6).
- It proposed 3 new pipe supports and wrote the modified file to `out.cii` (check your folder —
  it's there now).
- `PASS` means the placement satisfies Conduit's span checks. (See "What PASS/FAIL/exit codes
  mean" below for the other outcomes.)

Open `out.cii` in a text editor and compare it to `fixtures\straight-run.cii` if you're curious —
the only differences should be the restraint count near the top and the new support records
appended near the end (`#$ RESTRANT` section); everything else is byte-for-byte identical to the
input, which is intentional (Conduit only touches what it's actually changing).

Try the other two committed examples too:
```powershell
dotnet run --project src\Conduit.Cli -- optimize fixtures\run-with-riser.cii out-riser.cii
dotnet run --project src\Conduit.Cli -- optimize fixtures\malformed.cii out-bad.cii
```
The last one is deliberately broken (to test error handling) — it should print a clear parse
error and exit without writing `out-bad.cii` at all. That's expected, not a bug.

## 6. See what changes without a `caesar.cfg` present

CAESAR II keeps a `caesar.cfg` settings file alongside your model files, which Conduit can read
for extra context (the piping code/edition in use, where material databases live) — that's the
file step 5 picked up automatically, since `fixtures\caesar.cfg` sits right next to
`fixtures\straight-run.cii` in this repo. Conduit looks for `caesar.cfg` **in the same folder as
the input file**, not as a separate argument, so to see the *other* case — no config file
available — copy just the `.cii` on its own, without `caesar.cfg`, somewhere else first:
```powershell
mkdir conduit-check
copy fixtures\straight-run.cii conduit-check\
dotnet run --project src\Conduit.Cli -- optimize conduit-check\straight-run.cii conduit-check\out.cii
```
(macOS/Linux: `mkdir -p conduit-check && cp fixtures/straight-run.cii conduit-check/`)

Compare the printed "Piping code assumed" line to step 5's — this time, with no `caesar.cfg`
around, it should fall back to `B31.3_2024 (default — no caesar.cfg DEFAULT_CODE found)`, and the
material database line disappears entirely. That confirms Conduit only uses `caesar.cfg` when
one is actually present next to your input file — it never invents one.

## 7. Trying it on your own files

A few things to know before pointing Conduit at a real model:
- **Only `.cii` (CAESAR II's neutral/interchange format) is accepted right now — not `.C2`/`._A`**
  (CAESAR II's native working format). If your working files are `.C2`, you'd need to export to
  `.cii` yourself first (CAESAR II's own `iecho.exe` converter, or File → Export in CAESAR II) —
  Conduit doing this conversion automatically is planned but not built yet (see SPEC.md's "Native
  file adapter (iecho)").
- **Always point Conduit at a copy, not your only copy of a real file**, until you're comfortable
  with what it changes. It never overwrites the input (it writes to whatever output path you give
  it), but good habit regardless.
- Run it the same way as step 5, just with your own paths:
  ```powershell
  dotnet run --project src\Conduit.Cli -- optimize C:\path\to\yourfile.cii C:\path\to\output.cii
  ```

## What PASS/FAIL/exit codes mean

- **Exit code 0, prints `PASS`**: ran successfully and every span check passed.
- **Exit code 1**: usage error or the input file couldn't be parsed — no output file is written.
  The error message names what went wrong (e.g. a missing section).
- **Exit code 2, prints `FAIL`**: it ran and wrote an output file, but couldn't fully satisfy the
  span checks within its iteration limit — it prints the remaining failing spans so you can see
  what's still an issue. This is a legitimate outcome for a harder layout, not necessarily a bug.

If you're running this from a script and want to check the outcome automatically, check
`$LASTEXITCODE` in PowerShell (or `$?` in Bash) right after the `dotnet run` command.

## 8. When something looks wrong: run the log script and send it back

Rather than describing what happened, run everything (build, tests, and the CLI against a file or
folder of files) and capture the full console output to a file you commit back to the repo — so
Claude sees exactly what your machine saw.

**Windows PowerShell:**
```powershell
.\scripts\run-and-log.ps1                              # against fixtures\
.\scripts\run-and-log.ps1 -InputPath C:\path\to\files   # against your own .cii file(s)/folder
```
**macOS/Linux (or WSL):**
```bash
./scripts/run-and-log.sh                    # against fixtures/
./scripts/run-and-log.sh /path/to/files      # against your own .cii file(s)/folder
```
Both write a timestamped log under `test-logs\` (Windows) / `test-logs/` (macOS/Linux) — e.g.
`test-logs/2026-08-24_112417-run.log` — plus a copy of Conduit's output file for each input it
ran against. When it finishes it prints the exact log path and the `git add`/commit/push you need;
follow that, then tell Claude which run to look at. `test-logs/` isn't gitignored on purpose —
these are meant to be committed when you want a review, not silently discarded — but it's your
call which runs are worth keeping; delete the ones that aren't before committing.

---

# Reference (for making changes to Conduit itself)

Everything below is for anyone modifying Conduit's code, not needed just to try it out.

## Quick check (do this after almost any code change)

```bash
./setup.sh                 # bootstraps the .NET SDK headlessly if it's missing, then builds+tests
# or, once the SDK is present:
dotnet build
dotnet test
```

`dotnet test` runs the full xUnit suite (`tests/Conduit.Tests`) — currently 30 tests, all
expected to pass on every commit. A failing test blocks the change; there are no known-flaky or
skipped tests in this project.

## Automated tests: what's covered where

- `tests/Conduit.Tests/NeutralFiles/NeutralFileRoundTripTests.cs` — the `.cii` parser/writer:
  byte-identical round-trip for untouched sections, `#$ RESTRANT`/`#$ CONTROL` regeneration when a
  restraint is added, and the malformed-file parse-error path. Uses the committed fixtures under
  `fixtures/*.cii`.
- `tests/Conduit.Tests/Heuristics/SpanLimitCalculatorTests.cs` — the beam-theory max-span formula,
  including the real-`#$ ALLOWBLS`-vs-default-constant fallback behavior.
- `tests/Conduit.Tests/Heuristics/SupportTypeClassifierTests.cs` — rest/guide/anchor
  classification rules in isolation.
- `tests/Conduit.Tests/Heuristics/SupportPlacerTests.cs` — the run-walking placement algorithm:
  spacing under max span, the corrected restraint-code mapping (`+Y` not bidirectional `Y`), and
  the riser-guide trigger condition (a guide is placed when the riser element itself causes the
  span overflow — not "every vertical segment always gets one", see SPEC.md's "Known open
  decisions" for why).
- `tests/Conduit.Tests/Optimization/OptimizationLoopTests.cs` — the iterate-and-adjust loop
  against `MockStressSolver`: adding intermediate rest supports, and reporting (not escalating —
  no spring logic in the MVP) a span that has no room left to add one.
- `tests/Conduit.Tests/Configuration/CaesarConfigReaderTests.cs` and `CaesarConfigTests.cs` — the
  `caesar.cfg` parser (against the real example at `fixtures/caesar.cfg`) and the
  config-vs-default piping-code fallback (`CaesarConfig.EffectiveCode`).
- `tests/Conduit.Tests/Configuration/CaesarInstallationLocatorTests.cs` — the
  `C:\ProgramData\Intergraph CAS\CAESAR II\<version>\System` install-tree locator: version
  filtering (15.00 floor), newest-first ordering, `System`-subfolder resolution, and graceful
  handling of a missing root. Runs against a temp directory (injectable root), not the real
  Windows path — this logic is pure `System.IO`, so it's fully testable on Linux.
- `tests/Conduit.Tests/TestHelpers/NeutralFileFixtureBuilder.cs` is not a test file itself — it's
  the shared builder both the unit tests and the committed `fixtures/*.cii` files are generated
  from. If you change what a valid minimal neutral file needs to contain (e.g. a newly-parsed
  section becomes required), update the builder here first, then regenerate/update the committed
  fixtures to match (see "Adding or changing a fixture" below) — otherwise the committed fixtures
  and the in-memory test fixtures will silently drift apart.

## Manual end-to-end check (do this for anything touching the CLI, placement output, or `caesar.cfg`)

Run the CLI directly against a fixture and read the summary:

```bash
dotnet run --project src/Conduit.Cli -- optimize fixtures/straight-run.cii /tmp/out.cii
dotnet run --project src/Conduit.Cli -- optimize fixtures/run-with-riser.cii /tmp/out-riser.cii
dotnet run --project src/Conduit.Cli -- optimize fixtures/malformed.cii /tmp/out-bad.cii   # expect exit 1, no output file
```

Check:
- Exit code: `0` = PASS, `1` = usage/parse error (no output file written), `2` = ran but didn't
  converge within the iteration cap.
- The printed "Piping code assumed" line and the placed-support list look right for the input.
- `diff fixtures/straight-run.cii /tmp/out.cii` — the diff should be confined to `#$ CONTROL`'s
  restraint count and the appended `#$ RESTRANT` records; everything else must be byte-identical.

**To check `caesar.cfg` handling specifically** (it's read from the *input file's directory*, not
a CLI argument, so it won't be picked up from `fixtures/` unless you run from there or copy both
files together):

```bash
mkdir -p /tmp/conduit-check && cp fixtures/straight-run.cii fixtures/caesar.cfg /tmp/conduit-check/
dotnet run --project src/Conduit.Cli -- optimize /tmp/conduit-check/straight-run.cii /tmp/conduit-check/out.cii
# expect: "Piping code assumed: B31.3_2020 (from caesar.cfg)" and the material-database line

dotnet run --project src/Conduit.Cli -- optimize fixtures/straight-run.cii /tmp/out-noconfig.cii
# expect: "Piping code assumed: B31.3_2024 (default — no caesar.cfg DEFAULT_CODE found)"
```

## Adding or changing a fixture

Fixtures are generated (not hand-written) via `NeutralFileFixtureBuilder` +
`NeutralFileWriter.ToLines` — see the existing `fixtures/*.cii` files for the pattern, and any
test in `NeutralFileRoundTripTests.cs`/`SupportPlacerTests.cs` for how the builder is called. If
you add a section the reader now requires (like `#$ MISCEL_1`'s `RRMAT` array), update the
builder first so both the in-memory tests and any freshly-generated committed fixtures stay
consistent — a builder change alone does **not** retroactively fix already-committed fixture
files; those need their own edit (or regeneration) too, and `dotnet test` will fail loudly if
they're out of sync (see `NeutralFileRoundTripTests` for the symptom: a
`NeutralFileParseException` reading a section the builder didn't populate).

## What isn't tested here (by design, v1 scope)

- `CaesarComStressSolver` (real Caesar II COM automation) and `IechoConverter` (real `iecho.exe`
  invocation) are compiled skeletons only — see SPEC.md's "Caesar II abstraction" and "Native file
  adapter (iecho)". There is nothing to test until these are implemented on a Windows machine with
  a licensed CAESAR II install; don't add tests that assume they work.
- No code-compliance stress math (real B31.3 Appendix calculations, WRC 297/537 nozzle checks) —
  `MockStressSolver`'s span/utilisation proxy is deliberately simplified; see SPEC.md's
  "Explicitly OUT of scope".
- Actually reading the material-database files `caesar.cfg` points to (`SYSTEM_DIRECTORY_NAME`,
  `User_Material_File_Name`) — only the config file itself is parsed; see SPEC.md's "Known open
  decisions".
