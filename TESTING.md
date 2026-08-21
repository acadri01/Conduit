How to test Conduit — read this whenever you need to verify a change, not just when writing new
tests. Kept up to date per CLAUDE.md; update it whenever what/how to test changes (a new project,
a new fixture convention, a new manual-check that matters).

## Quick check (do this after almost any change)

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
- `tests/Conduit.Tests/Optimization/OptimizationLoopTests.cs` — the iterate-and-adjust loop against
  `MockStressSolver`, including the spring-candidate escalation path.
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
