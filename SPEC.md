# Project: Conduit — Stage 1 (Support Optimisation MVP)

## Goal (1–2 sentences)
Given a Caesar II neutral file (`.c2`) describing a predetermined piping layout, Conduit parses
it, proposes support positions and types using encoded engineering heuristics, writes the
result back to a neutral file, and iterates against a stress-solver feedback loop (Caesar II in
production; a mock solver for v1 development/testing) until sustained-stress and span targets
are satisfied.

## Users
Piping stress engineers who already model systems in Caesar II and want a first-pass, defensible
support layout generated automatically instead of placed by hand, before fine-tuning in Caesar II
itself.

## Stack / constraints
- Language/framework: C#, .NET 8 (LTS). Class library (`Conduit.Core`) + console app
  (`Conduit.Cli`) + xUnit test project (`Conduit.Tests`), in one solution.
- Storage: none — the neutral file (and its Conduit-generated output copy) is the only
  persistent artifact. No database.
- Deploy target: local CLI tool run by the engineer next to their Caesar II install (Windows, in
  production). The build/test loop itself must also work headless on Linux (this dev container
  has no Windows/Caesar II/COM available), so all Caesar II access is isolated behind an
  interface — see "Caesar II abstraction" below.
- Hard constraints (must / must-not):
  - MUST be a clean-room implementation: no proprietary project files, employer data, or
    Caesar II-licensed material (docs, DLLs, sample files) may be copied into the repo.
  - MUST NOT depend on Caesar II being installed/licensed to build, run unit tests, or use the
    parser/heuristics in this environment.
  - MUST NOT implement Stage 2 (routing) or Stage 3 (full system generation) logic.

### Caesar II abstraction
Caesar II's real interface is Windows COM automation over a neutral file. This container can't
exercise that. v1 defines an `IStressSolver` interface (`Evaluate(NeutralFile) -> StressResult`)
with two implementations:
- `MockStressSolver` — deterministic, span-rule-based pass/fail used for all v1 dev/testing.
- `CaesarComStressSolver` — skeleton only (constructor + method signatures + `NotImplementedException`
  bodies, with XML-doc notes on the COM calls it needs). It is not wired up or tested here; it's
  intended to be completed and validated later on a Windows machine with a licensed Caesar II
  install. Building/testing the rest of the project must not require this class to be functional.

## In scope (v1)
- Neutral file model + parser/writer for a documented **synthetic subset** of the Caesar II
  neutral file format (see "Neutral file format" below) — round-trips node list, pipe segment
  properties (OD, wall/schedule, material), and support records.
- Span-limit heuristic: given pipe size/schedule/material, compute a maximum allowable
  unsupported span (simplified B31.3-style table, documented in code with its source assumption).
- Support-type selection heuristic: for each candidate location, classify as rest, guide, anchor,
  or spring based on documented rules (e.g., vertical runs favor guides, direction changes near
  equipment nozzles favor anchors, spans exceeding thermal-growth thresholds flag spring
  candidates). Rules are simplified for v1 and documented as such.
- Support-placement algorithm: walk each pipe run between fixed points (anchors/equipment), place
  candidate supports at/under the max allowable span, assign a type via the heuristic above.
- `IStressSolver` interface + `MockStressSolver` (functional) + `CaesarComStressSolver` (skeleton,
  see above).
- Iterate-and-adjust loop: run placement → `IStressSolver.Evaluate` → if any check fails, adjust
  (tighten span / add support / change type) → re-evaluate, up to a bounded iteration count, then
  report pass/fail with reasons.
- CLI: `conduit optimize <input.c2> <output.c2>` reads input, runs the loop against
  `MockStressSolver` by default, writes the modified neutral file, prints a summary report.
- Unit tests covering: parser round-trip, span heuristic table lookups, support-type
  classification, placement algorithm on synthetic fixtures, and the iterate loop against
  `MockStressSolver`.

## Explicitly OUT of scope (do not build)
- Any real Caesar II COM automation that actually runs (only the `CaesarComStressSolver`
  skeleton/interface — no working implementation, no COM calls executed).
- Parsing the full/official Caesar II neutral file format — v1 uses a documented synthetic
  subset (see below) sufficient to exercise the pipeline; full-format fidelity is future work
  once real sample files are available (user has samples but has not provided them yet).
- Stage 2 (routing automation) and Stage 3 (full system generation) — no routing/pathfinding, no
  spatial-envelope logic.
- WRC 297/537 nozzle load checks, flange leakage checks, code-compliant (real B31.3 Appendix)
  sustained/occasional/expansion stress calculations — v1's "stress check" in `MockStressSolver`
  is a simplified span/utilisation proxy, not a certified code calculation. Real code-compliant
  stress math is future work.
- GUI, web service, database, multi-user/project features.
- Non-stress routing constraints (access, constructability, operability, aesthetics).
- Cross-discipline coordination.

## Neutral file format
No real `.c2` samples or format docs are available yet (user will provide later — see Known open
decisions). v1 defines and documents a small, clearly-labeled **synthetic neutral file format**
(plain text, line-record based, loosely inspired by Caesar II's public documentation of neutral
file structure — node/element/support records) good enough to build and test the pipeline against.
When real samples arrive, the parser will be revised to match them; this is tracked as an open
follow-up, not a v1 blocker.

## Behaviour by example
1. Given a synthetic neutral file with two anchors 18 m apart connected by a straight 6" Sch 40
   carbon-steel run and no existing supports → `conduit optimize` proposes N rest supports spaced
   at or under the computed max allowable span, writes them into the output neutral file as new
   SUPPORT records, and the summary reports "PASS" from `MockStressSolver`.
2. Given the same run but with a vertical riser segment → the support at the riser is classified
   as a guide (not rest), per the support-type heuristic.
3. Given a run where the computed span would require more supports than fit before a nozzle
   connection → the support nearest the nozzle is flagged as a spring/anchor candidate per the
   thermal-growth heuristic, and the summary explains why.
4. Given a malformed/unparseable input file → `conduit optimize` exits non-zero with a clear
   parse-error message (node/line reference), writes no output file.

## Acceptance criteria (definition of done)
- [ ] `dotnet build` succeeds from a clean checkout via `setup.sh`, with no Caesar II/Windows
      dependency.
- [ ] `dotnet test` passes, covering parser round-trip, span heuristic, support-type
      classification, placement, and iterate-loop-against-mock scenarios.
- [ ] `conduit optimize <in> <out>` runs end-to-end on the synthetic fixture files committed
      under `fixtures/`, producing a modified neutral file and a printed pass/fail summary.
- [ ] Neutral file format, span-heuristic table, and support-type rules are documented in code
      (XML doc comments) with their simplifying assumptions stated explicitly.
- [ ] `CaesarComStressSolver` exists as a skeleton (compiles, not implemented) and does not block
      build/test.
- [ ] PROGRESS.md and QUESTIONS.md updated per CLAUDE.md as work proceeds.

## Known open decisions (pre-answer what you can)
- Real `.c2` sample files / official format docs: user has them but hasn't provided them yet.
  Claude proceeds with the synthetic format documented above; format will be revised when real
  samples arrive (logged as a follow-up in QUESTIONS.md, not blocking).
- Simplified span-limit table and support-type rules are Claude's best-effort encoding of common
  piping-support heuristics, explicitly not a substitute for a real B31.3 span calculation —
  documented inline as simplifying assumptions.
- `CaesarComStressSolver`'s exact COM call sequence is deferred until it can be developed/tested
  against a real licensed Caesar II install (Windows, out of this container's reach).
