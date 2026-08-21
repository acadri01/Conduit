# Project: Conduit — Stage 1 (Support Optimisation MVP)

## Goal (1–2 sentences)
Given a Caesar II neutral file (`.cii`) describing a predetermined piping layout, Conduit parses
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
  production), with real project files handed to it in CAESAR II's native `.C2`/`._A` format, not
  `.cii` — see "Native file adapter (iecho)" below. The build/test loop itself must also work
  headless on Linux (this dev container has no Windows/Caesar II/COM/`iecho.exe` available), so
  all Caesar II access (stress solving *and* native file conversion) is isolated behind
  interfaces — see "Caesar II abstraction" and "Native file adapter (iecho)" below.
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

**How `CaesarComStressSolver` should eventually get results.** CAESAR II 15.1's results live
behind two interactive GUI reviewers — the Classic "Static Output Processor" and the modern "New
Analysis Reviewer" (per the vendor's "Output Tab", "New Analysis Reviewer Help", and "Static
Analysis Output Help" documentation) — there is no headless/CLI batch report generator outside
COM automation, so *triggering* an analysis and its reports always requires driving CAESAR II
through COM (or its GUI). *Reading back* results, though, does not require deep interactive COM
calls: both reviewers can save standard reports (Code Compliance, Restraints/Restraint Summary,
Displacements, Stresses, …) to plain ASCII text files ("Send to Text (ASCII) File" / Output
Processor "Save"), and the Report Template Editor lets you define a **custom report template**
with an exact, fixed column layout, order, and precision per field — built once and reused, so
its output is stable to parse. The revised plan for `CaesarComStressSolver`:
1. Drive CAESAR II via COM: load the neutral file, error-check, run static analysis (the
   "Batch Run" action — error check + analyze + generate results in one step).
2. Have it emit a Code Compliance Report (stress ratios) and a Restraints/Restraint Summary
   Report (support loads) — ideally via a custom Report Template authored for this purpose — to
   ASCII text files.
3. Parse those text files for `StressResult`, rather than pulling values through interactive COM
   calls one field at a time.

The Code Compliance Report's real shape (per the vendor docs) is richer than v1's simplified
boolean pass/fail: per load case, per element (From node → To node), it reports Code Stress,
Allowable Stress, and Ratio % — plus job-level "Highest Stresses" (worst ratio, axial/bending/
torsion/hoop stress, each with its node). `MockStressSolver`'s span/utilisation proxy is a
deliberate v1 simplification of this; a future non-mock, code-compliant solver should target
this ratio-based shape rather than a bare pass/fail. Nothing here changes v1's `.cii` input
parsing — these docs cover results/output only.

The New Analysis Reviewer (and so CAESAR II 15.1 generally) supports these piping codes: ASME
B31.1 (1967, 2018, 2020, 2022, 2024), ASME B31.3 (2018, 2020, 2022, 2024), ASME B31.3-IX (2018,
2020, 2022, 2024), ASME NC (2009), ASME ND (2009), EN 13480 (2017, 2017/A5:2022). v1's
simplified span/stress heuristics should be understood as approximating ASME B31.3 (the most
common process-piping code), latest edition, without claiming conformance to any specific
edition — see the stress-math caveat under "Explicitly OUT of scope".

**Real load cases vs. v1's simplification.** A real CAESAR II analysis doesn't produce one
pass/fail — it runs a set of *load cases*, each tagged with a stress type: `OPE` (operating,
hot displacements/loads, not itself a code-compliance case for B31.1/B31.3), `SUS` (sustained —
weight + pressure, the primary code-compliance case), `EXP` (expansion — the range between
operating and sustained, a combination case), `OCC` (occasional, user-defined), `FAT` (fatigue,
needs a load-cycle count), plus special types (`HGR` for hanger design, `HYD` for hydrotest,
`CRP` for creep). Combination cases (e.g. `L4 = L1-L3 (EXP)`) are built from basic cases via a
combination method — `Algebraic` (default), `Scalar`, `SRSS`, `Abs`, `Max`, `Min`, `SignMax`,
`SignMin` — each combining displacements/forces/stresses differently. B31.3's recommended set is
just `L1=W+T1+P1 (OPE)`, `L2=W+P1 (SUS)`, `L3=W+P1 (SUS, alternate)`, `L4=L1-L3 (EXP, Algebraic)`.
v1's `MockStressSolver` collapses all of this to a single span/utilisation pass/fail — a
deliberate simplification, not an oversight — but any future work implementing real B31.3
stress checks (out of v1 scope, see below) needs this load-case/stress-type framework, not just
a bigger span table.

### Native file adapter (iecho)
The `.cii` neutral file is an interchange format, not what a piping engineer actually has on
disk day to day — their working files are CAESAR II's native format, `.C2` (current) or `._A`
(legacy). CAESAR II ships a converter, `iecho.exe`, that translates between the two; it is the
*only* documented way to get a `.C2`/`._A` file into `.cii` (or back). The user shared (for
context only, not to copy — internal, project-specific code, not committed here) a Python
wrapper they use elsewhere that shells out to `iecho.exe`, which clarifies the real-world
requirement precisely: **Conduit's users should never have to run `iecho` by hand.** The CLI's
job is to accept whatever file CAESAR II actually produces (`.C2`/`._A`) and hand back the same,
converting through `.cii` transparently in between.

This implies a second small abstraction, alongside `IStressSolver`, with the same shape of
problem (a Windows-only external tool this container can't exercise):
- `INeutralFileConverter` (or similar) with two operations — `ToNeutralFile(nativePath) ->
  ciiPath` and `ToNativeFile(ciiPath) -> nativePath` — mirroring `iecho.exe`'s two conversion
  directions.
- v1 implementation: a compiled skeleton only (`IechoConverter`, `NotImplementedException`
  bodies, XML-doc notes on the `iecho.exe` invocation), exactly like `CaesarComStressSolver` —
  not wired up or tested here, completed later on Windows with a licensed CAESAR II install.
  `iecho.exe`'s location isn't fixed; expect to search common install paths (Intergraph CAS and
  Hexagon-branded, multiple CAESAR II versions) plus a config/environment-variable override, the
  same pattern as any external-tool discovery.
- One asymmetry worth flagging for whoever implements this: in the reference wrapper, `.cii` →
  `.C2` (writing Conduit's changes back to the native format) ran as a plain silent subprocess
  call, but `.C2` → `.cii` (reading a native file in) was done by launching `iecho.exe`
  interactively and polling for the output file to appear, then terminating it. That may be a
  hard `iecho.exe` limitation on the export direction, or just a conservative design choice in
  that tool — worth verifying directly against `iecho.exe` on Windows rather than assuming
  either way. Design `IechoConverter`'s interface to tolerate either (synchronous return, or an
  async/pollable variant) so the real implementation isn't forced to fake synchronicity.
- v1's CLI (`conduit optimize <input.cii> <output.cii>`) still speaks `.cii` directly, since
  that's what's parseable/testable in this container. `INeutralFileConverter` is the seam a
  later, Windows-side CLI wraps around it (`conduit optimize <input.C2> <output.C2>`) so the
  `.cii` round-trip becomes an internal implementation detail the user never sees.

## In scope (v1)
- Neutral file model + parser/writer for the **real, official CAESAR II neutral file format**
  (see "Neutral file format" below) — round-trips the whole file byte-for-byte except for the
  sections Conduit actively edits, and fully models `#$ ELEMENTS` (node list, pipe segment
  properties) and `#$ AUX_DATA` → `#$ RESTRANT` (supports).
- Span-limit heuristic: given pipe size/schedule/material, compute a maximum allowable
  unsupported span (simplified B31.3-style table, documented in code with its source assumption).
- Support-type selection heuristic: for each candidate location, classify as rest, guide, anchor,
  or spring based on documented rules (e.g., vertical runs favor guides, direction changes near
  equipment nozzles favor anchors, spans exceeding thermal-growth thresholds flag spring
  candidates). Rules are simplified for v1 and documented as such. Maps to CAESAR II restraint
  type codes (`ANC`, `X`/`Y`/`Z`, `GUI`, `LIM`, etc. — see "Neutral file format").
- Support-placement algorithm: walk each pipe run between fixed points (anchors/equipment), place
  candidate supports at/under the max allowable span, assign a type via the heuristic above, and
  write them as new `#$ RESTRANT` records.
- `IStressSolver` interface + `MockStressSolver` (functional) + `CaesarComStressSolver` (skeleton,
  see above).
- `INeutralFileConverter` interface + `IechoConverter` (skeleton only, see "Native file adapter
  (iecho)" above) — not wired up or tested in v1, exists so the seam is in place for later.
- Iterate-and-adjust loop: run placement → `IStressSolver.Evaluate` → if any check fails, adjust
  (tighten span / add support / change type) → re-evaluate, up to a bounded iteration count, then
  report pass/fail with reasons.
- CLI: `conduit optimize <input.cii> <output.cii>` reads input, runs the loop against
  `MockStressSolver` by default, writes the modified neutral file, prints a summary report. Only
  `.cii` is accepted/produced in v1 — `.C2`/`._A` support is future work once `IechoConverter` is
  implemented (see "Explicitly OUT of scope").
- Unit tests covering: parser round-trip (including sections Conduit doesn't interpret), span
  heuristic table lookups, support-type classification, placement algorithm on synthetic
  fixtures, and the iterate loop against `MockStressSolver`.

## Explicitly OUT of scope (do not build)
- Any real Caesar II COM automation that actually runs (only the `CaesarComStressSolver`
  skeleton/interface — no working implementation, no COM calls executed).
- Any real `iecho.exe` invocation, and any direct handling of `.C2`/`._A` files (only the
  `IechoConverter` skeleton/interface — no working implementation, no subprocess/COM calls
  executed). CLI-level `.C2`/`._A` support is future work built on top of it.
- Interpreting (parsing into a rich model) any `#$ AUX_DATA` subsection other than `NODENAME`
  and `RESTRANT` — `BEND`, `RIGID`, `EXPJT`, `DISPLMNT`, `FORCMNT`, `UNIFORM`, `WIND`, `OFFSETS`,
  `ALLOWBLS`, `SIF&TEES`, `REDUCERS`, `FLANGES`, `EQUIPMNT`, hanger data, and `#$ MISCEL_1` /
  `#$ UNITS` / `#$ COORDS` are round-tripped opaquely (preserved byte-for-byte on write) but not
  modeled or reasoned about in v1. Interpreting them is future work as later stages need them
  (e.g. hangers for spring-support sizing, EQUIPMNT for nozzle load checks).
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
Conduit targets the real, official CAESAR II neutral file format (`.cii`, ASCII, one CAESAR II
"jobname" per file), as published in Hexagon's CAESAR II Users Guide ("CAESAR II Neutral File",
v15 interface) — public vendor documentation, not proprietary material. The user also supplied
several real `.cii` files from their own projects; those were reviewed locally to confirm the
real-world structure matches the published spec (it does, closely) but **are not committed to
this repo** and are not used as source data for anything committed — per the clean-room
constraint above, v1's fixtures are freshly authored synthetic files with invented node numbers
and geometry that are merely *structurally* valid `.cii` files, not derived from anyone's real
project. See "Known open decisions" for the reasoning.

Key structural facts the parser/writer must honor:
- The file is organized into sections marked by a `#$ SECTIONNAME` header in columns 1–2 (`#$ `
  literally, then the section name).
- Each data line is **fixed-width columnar** (FORTRAN `G13.6`/`I13` formats), not
  whitespace-delimited: real values are `2X` (2 leading spaces) then repeating 13-character
  fields; a negative number's `-` sign occupies the column where a separating space would
  otherwise be, so two adjacent fields can appear to run together (e.g.
  `1.300000E+02-5.238750E+01` is two 13-char fields, not one token). The parser must slice by
  fixed column width, never split on whitespace.
- Top-level sections relevant to v1, in file order: `#$ VERSION` (interface/CAESAR II version,
  code page, 60-line title block, generator stamp), `#$ CONTROL` (element/aux-data-type counts,
  including `IZUP` — 0 = -Y vertical, 1 = -Z vertical), `#$ ELEMENTS` (per-element real block of
  53 used values — FROM/TO node, delta X/Y/Z, OD, wall thickness, etc. — an integer pointer
  block indexing into the auxiliary arrays, name/line-number/color strings), `#$ AUX_DATA`
  (container for the subsections below), `#$ MISCEL_1`, `#$ UNITS`, `#$ COORDS`.
- Within `#$ AUX_DATA`, the subsections appear in a fixed order (`NODENAME`, `BEND`, `RIGID`,
  `EXPJT`, `RESTRANT`, `DISPLMNT`, `FORCMNT`, `UNIFORM`, `WIND`, `OFFSETS`, `ALLOWBLS`,
  `SIF&TEES`, `REDUCERS`, `FLANGES`, `EQUIPMNT`); a subsection header is always written even when
  its count in `#$ CONTROL` is zero (header only, no data lines).
- `#$ RESTRANT` (the support data v1 reads and writes): one block per restraint, one *degree of
  freedom* sub-block per DOF (up to 6), each DOF as 4 lines — 2 data lines (node, restraint-type
  code 1–62, stiffness, gap, friction, connecting node, direction cosines) then a length-prefixed
  tag line and a length-prefixed GUID line. Restraint type codes cover anchors (`ANC`), single
  and double-acting translational restraints (`X`,`Y`,`Z`,`+X`,`-X`, …), guides (`GUI`), limit
  stops (`LIM`), rod/spring-related codes (`XROD`, `+XROD`, `XSPR`, …) — the full 1–62 table is
  in the vendor doc and will be reproduced as an enum with XML-doc comments in code.
- Every `#$` section the parser doesn't specifically model (see "OUT of scope") is still read and
  re-emitted verbatim on write, so a file round-tripped through Conduit without any support
  changes is byte-identical, and a file with only `#$ RESTRANT` changes preserves everything
  else CAESAR II needs to re-import it.

## Behaviour by example
1. Given a synthetic `.cii` file with two anchors (`#$ RESTRANT` type `ANC`) 18 m apart connected
   by a straight 6" Sch 40 carbon-steel `#$ ELEMENTS` run and no intermediate supports →
   `conduit optimize` proposes N rest supports (type `Y`, vertical-only restraint) spaced at or
   under the computed max allowable span, writes them into the output file as new `#$ RESTRANT`
   DOF blocks, leaves every other section byte-identical to the input, and the summary reports
   "PASS" from `MockStressSolver`.
2. Given the same run but with a vertical riser segment → the support at the riser is classified
   as a guide (restraint type `GUI`), per the support-type heuristic.
3. Given a run where the computed span would require more supports than fit before a nozzle
   connection → the support nearest the nozzle is flagged as a spring/anchor candidate per the
   thermal-growth heuristic, and the summary explains why.
4. Given a malformed/unparseable input file (bad section header, a data line that doesn't match
   its section's expected column layout) → `conduit optimize` exits non-zero with a clear
   parse-error message (section/line reference), writes no output file.

## Acceptance criteria (definition of done)
- [ ] `dotnet build` succeeds from a clean checkout via `setup.sh`, with no Caesar II/Windows
      dependency.
- [ ] `dotnet test` passes, covering parser round-trip, span heuristic, support-type
      classification, placement, and iterate-loop-against-mock scenarios.
- [ ] `conduit optimize <in> <out>` runs end-to-end on the synthetic (non-proprietary) `.cii`
      fixture files committed under `fixtures/`, producing a modified neutral file and a printed
      pass/fail summary.
- [ ] Neutral file format, span-heuristic table, and support-type rules are documented in code
      (XML doc comments) with their simplifying assumptions stated explicitly.
- [ ] `CaesarComStressSolver` and `IechoConverter` exist as skeletons (compile, not implemented)
      and do not block build/test.
- [ ] PROGRESS.md and QUESTIONS.md updated per CLAUDE.md as work proceeds.

## Known open decisions (pre-answer what you can)
- Real sample `.cii` files and the official Hexagon format documentation (CAESAR II Users Guide,
  v15 neutral file interface) are now available and were used to write the "Neutral file format"
  section above. The user's own sample files were reviewed locally but are **not committed** to
  this repo (clean-room constraint) — v1 fixtures are freshly authored, structurally-valid
  synthetic `.cii` files instead. See QUESTIONS.md for the full reasoning.
- Simplified span-limit table and support-type rules are Claude's best-effort encoding of common
  piping-support heuristics, explicitly not a substitute for a real B31.3 span calculation —
  documented inline as simplifying assumptions.
- `CaesarComStressSolver`'s exact COM call sequence is deferred until it can be developed/tested
  against a real licensed Caesar II install (Windows, out of this container's reach). Triggering
  an analysis and its reports requires COM/GUI automation (no headless/CLI report generator
  exists), but *reading* results doesn't need deep interactive COM calls — CAESAR II can save
  standard reports (Code Compliance, Restraints, …), or a custom Report Template with a stable
  column layout, to plain ASCII text files; the plan is COM to drive analysis + emit a report to
  text, then parse that file. See "Caesar II abstraction" for the full reasoning.
- `IechoConverter`'s exact `iecho.exe` invocation (arguments, working directory, whether export
  is truly silent or needs the interactive-launch-and-poll pattern) is deferred the same way —
  developed/tested later on Windows against a real licensed CAESAR II install. See "Native file
  adapter (iecho)" for what's known so far from the user's reference implementation.
