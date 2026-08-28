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
common process-piping code), without claiming conformance to any specific edition — see the
stress-math caveat under "Explicitly OUT of scope". The code Conduit reports for a run
(`CaesarConfig.EffectiveCode`) always prefers `caesar.cfg`'s own `DEFAULT_CODE` when one is
found; only when no `caesar.cfg`/`DEFAULT_CODE` is available does it fall back to a hardcoded
default, **B31.3-2024** (`CaesarConfig.DefaultAssumedCode` — the latest B31.3 edition CAESAR II
15.1 supports, per the list above). This is reporting/context only in v1 — no calculation
actually varies by code edition yet (see "Real load cases vs. v1's simplification" below).

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
  **`iecho.exe` is not under the `C:\ProgramData\Intergraph CAS\CAESAR II\<version>\System`
  data-directory tree** (see "CAESAR II installation layout" below) — per the user's confirmation
  and reference wrapper, it lives in a different branch of the install (the application/program
  directory), so it needs its own, separate discovery logic — expect to search common install
  paths (Intergraph CAS and Hexagon-branded, multiple CAESAR II versions, 15.00 and up per
  Conduit's supported floor) plus a config/environment-variable override, the same pattern as any
  external-tool discovery, but **do not** assume `CaesarInstallationLocator`'s paths apply to it.

**Update (2026-08-27) — the two conversion directions are not equally automatable.** Per a
reference Python wrapper the user shared (their own code from a separate project, shared as
context — see QUESTIONS.md's "Noted for later: `iecho.exe` automation is one-directional only"
entry for the full detail): `.cii` → `.C2` ("silent conversion") is a genuinely headless, blocking
subprocess call with no UI — this is the direction Conduit's own optimize output needs, and stays
fully automatable as originally planned. `.C2` → `.cii` ("interactive export"), however, only
works through `iecho.exe`'s interactive UI — there is no documented headless call for this
direction; the reference wrapper's own workaround is launching the UI non-blocking and polling for
the expected output file to appear. **This means `ToNeutralFile(nativePath) -> ciiPath` cannot be
fully invisible to the user the way `ToNativeFile(ciiPath) -> nativePath` can** — at minimum it
needs a launch-and-watch pattern, and the user still has to be present to click through the export
dialog once. Doesn't change `IechoConverter`'s scope (still a skeleton, still deferred), but does
change what "Conduit's users should never have to run `iecho` by hand" can mean in practice once
it's implemented for real — worth revisiting this note at that point rather than assuming both
directions are symmetric.
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
  properties, including the auxiliary-data pointer array) and `#$ AUX_DATA` → `#$ RESTRANT`
  (supports). Also parses (read-only, exposed on `NeutralFile` for current and future use) the
  `#$ ALLOWBLS` allowable-stress records (linked from each element via its pointer array), the
  `#$ EQUIPMNT` nozzle/equipment load-limit records, and the `#$ MISCEL_1` material-ID (`RRMAT`)
  array — per review direction to make neutral-file data dynamically available rather than
  hardcoding placeholders. These three sections still round-trip byte-for-byte on write (parsed
  into a read-only side-model, not yet part of what Conduit regenerates).
- Span-limit heuristic: given pipe size/schedule/material, compute a maximum allowable
  unsupported span (beam-theory formula, documented in code with its source assumption), using
  the element's own `#$ ALLOWBLS` cold allowable stress when the file provides one (real,
  per-material/code/temperature data CAESAR II already computed) rather than a hardcoded
  placeholder, falling back to a documented default only when the file has no allowable-stress
  record for that element.
- Support-type selection heuristic: for each candidate location, classify per the corrected
  taxonomy — rest, hold-down, guide, line stop, with anchor as their combination — based on
  documented rules (vertical runs favor guides, locations near a run's fixed endpoints or a real
  `#$ EQUIPMNT` nozzle node favor anchors). v1's classifier only ever produces rest, guide, or
  anchor (no signal yet for hold-down/line-stop specifically). Rules are simplified for v1 and
  documented as such. Maps to the real CAESAR II restraint codes: `+Y`/`+Z` for a rest alone,
  `-Y`/`-Z` for a hold-down alone, bidirectional `Y`/`Z` for rest+hold-down together, `GUI` for
  guide, `LIM` for line stop, `ANC` for anchor (see "Neutral file format"). v1 focuses on rigid
  supports only.
- Support-placement algorithm (rewritten 2026-08-28 — see "Known open decisions" for the full
  derivation): walk each pipe run between fixed points (anchors, and — when `#$ EQUIPMNT` is
  populated — real nozzle/equipment node locations), tracking the two horizontal axes' unsupported
  span *separately* (not summed) plus a vertical accumulator checked against 2x the horizontal max
  span, with a universal reset (all three accumulators) at any support — placed or pre-existing.
  Bend corners and tee/branch nodes (node degree > 2 across the whole file) are excluded from
  placement, with clearance matching `ElementSplitter`'s own bend buffer; the placer backs off to
  the nearest eligible same-axis node already passed when the natural overflow point falls in an
  excluded zone. Every eligible plain rest also gets a co-located guide (one multi-DOF
  `#$ RESTRANT` record, via `Restraint.CreateMultiDof`). `SupportPlacer`'s own walk still only
  places at existing nodes; the iterate-and-adjust loop below still splits an element (with the
  same bend clearance) when there's no existing node to use — see "Known open decisions" for why
  reactively-split rests don't yet get the same companion guide the initial pass's do.
- Element-splitting (`ElementSplitter` + `NeutralFile.ReplaceElement`), per direct instruction:
  when the iterate-and-adjust loop hits a single-element span with no existing intermediate node
  (previously reported as an unresolvable failure), it splits that element into evenly-spaced
  chunks — the max allowable span rounded *down* to the nearest 1000 mm — with a new node and
  support at each interior boundary, e.g. a 25550 mm span against a 6446.76 mm max allowable span
  becomes four 6000 mm elements plus a 1550 mm remainder (four new restraints). This is Conduit's
  first production capability that adds/mutates pipe elements, not just restraints —
  `NeutralFile.ReplaceElement` surgically splices the new element records into both `#$ ELEMENTS`
  and `#$ MISCEL_1`'s positional `RRMAT` array (which would otherwise desync from the new element
  count the same way `#$ WIND`/`#$ MISCEL_1`'s trailing block did — see "Neutral file format"),
  leaving every other element's raw lines untouched. A chunk immediately adjacent to an existing
  bend is never left shorter than that bend's own tangent length (radius x tan(45°) for the 90°
  bends Conduit produces) plus a 500 mm shoe-clearance buffer, per direct instruction — a
  too-short remainder there is merged into the previous chunk instead. Only covers a bend at the
  split element's own `ToNode`, not at its `FromNode` (needs the preceding element's bend status,
  which isn't threaded through yet — see "Known open decisions").
- `IStressSolver` interface + `MockStressSolver` (functional) + `CaesarComStressSolver` (skeleton,
  see above).
- `INeutralFileConverter` interface + `IechoConverter` (skeleton only, see "Native file adapter
  (iecho)" above) — not wired up or tested in v1, exists so the seam is in place for later.
- `CaesarConfig`/`CaesarConfigReader` for the directory-level `caesar.cfg` (see "CAESAR II global
  configuration" below) — the CLI looks for one next to the input file and uses it to cross-check
  the axis setting and surface the default piping code/material-database locations.
- `CaesarInstallationLocator` (see "CAESAR II installation layout" below) — finds installed CAESAR
  II versions (15.00+) under the real, confirmed `ProgramData\Intergraph CAS\CAESAR II\<version>`
  data-directory layout, and resolves each version's `System` (material/component database)
  folder. Not wired into the CLI in v1 (nothing yet consumes the database files it points at) —
  exists so the path is known once that becomes necessary.
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
- Interpreting (parsing into a rich model) any `#$ AUX_DATA` subsection other than `NODENAME`,
  `RESTRANT`, `ALLOWBLS`, and `EQUIPMNT` (see "In scope" above) — `BEND`, `RIGID`, `EXPJT`,
  `DISPLMNT`, `FORCMNT`, `UNIFORM`, `WIND`, `OFFSETS`, `SIF&TEES`, `REDUCERS`, `FLANGES`, hanger
  data, and `#$ UNITS` / `#$ COORDS` are round-tripped opaquely in *production* Conduit.Core
  (preserved byte-for-byte on write when read from a real file) but not modeled or reasoned about
  in v1. **One narrow exception**: `Element.AuxiliaryPointers[0]` (the bend pointer) is preserved
  correctly when `ElementSplitter` splits an element whose `ToNode` has a bend — only the final
  chunk keeps the pointer, not every interior one — but this is pointer *bookkeeping* during a
  split, not interpreting `#$ BEND`'s own contents; Conduit.Core still never reads or reasons
  about a bend record itself. `#$ MISCEL_1` is a partial exception — its leading `RRMAT` (material ID) array is now
  parsed and exposed, but the rest of that section's content is still opaque. Interpreting the
  remaining sections is future work as later stages need them (e.g. `UNITS` for cross-unit-system
  correctness) — per review direction, the goal is to have as much of the neutral file's data
  available now as is practical, but this remains a real scope boundary, not something this pass
  closes out entirely. **Separately**, `tests/.../NeutralFileFixtureBuilder` (test-only, not part
  of Conduit.Core) *does* now synthesize structurally-correct `VERSION`/`WIND`/`UNITS`/`COORDS`
  content from scratch, for generating valid test neutral files — see "Generating test neutral
  files" below. That's a from-nothing generation concern, not a from-a-real-file parsing concern,
  so it doesn't change what production Conduit.Core parses or models.
- Using the element's material ID (`RRMAT`, now parsed) to look up allowable stress/density from
  an external material database keyed by piping code and edition year — this needs a concrete
  material-database source, which is an open question (see "Known open decisions"). v1 instead
  reads the allowable stress CAESAR II already computed and stored per-element in `#$ ALLOWBLS`,
  which satisfies "dynamically retrieve from the input, not a hardcoded constant" without needing
  that external database.
- Persisting optimizer iteration history to a database — the file's explicit "Storage: none...no
  database" hard constraint above stands until confirmed otherwise (see "Known open decisions").
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
v15 interface) — public vendor documentation, not proprietary material. The user has supplied
several real `.cii` files over the course of this project. Most were used as demonstration/example
files rather than client project data — reviewed locally to confirm the real-world structure
matches the published spec (it does, closely) but **not committed**, and not used as source data
for anything committed. **Three specific files are the exception**: `fixtures/real-samples/
TESTv15.cii`, `TESTv15_slugged.cii`, and `44002.cii` — the user explicitly said these are safe to
commit, so unlike the rest they *are* committed and available as real, non-synthetic structural
references. Everything else under `fixtures/` (`straight-run.cii`, `run-with-riser.cii`,
`loop-50m-3d.cii`, ...) remains freshly authored synthetic geometry with invented node numbers —
merely *structurally* valid `.cii` files, cross-checked against the real samples' byte layout but
not derived from their content. See "Known open decisions" for the reasoning.

Key structural facts the parser/writer must honor:
- **Line endings are CRLF, always** — confirmed against real CAESAR II-exported `.cii` files.
  `iecho.exe` and CAESAR II itself are Windows/Fortran-heritage tools that reject LF-only input;
  this was an actual bug (`NeutralFileWriter` wrote LF-only, silently downgrading real CRLF input
  on every round-trip) found and fixed after real-world testing showed `iecho.exe` rejecting
  Conduit's output. `NeutralFileReader.Read` tolerates either convention on input; `NeutralFileWriter.Write`
  always emits CRLF on output regardless of platform. See `NeutralFileRoundTripTests.Write_UsesCrlfLineEndings`.
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
  `SIF&TEES`, `REDUCERS`, `FLANGES`, `EQUIPMNT`); a subsection header is always written. **All of
  these, including `#$ WIND`, are header-only (no data lines) when their `#$ CONTROL` count is
  zero** — confirmed against `fixtures/real-samples/*.cii`: `TESTv15.cii`/`TESTv15_slugged.cii`
  (no wind load, `NumWindLoads = 0`) have an empty `#$ WIND`, while `44002.cii` (wind load
  applied, `NumWindLoads = 1`) has one 6-value data row. An earlier version of this doc claimed
  `#$ WIND` was a structural exception that's *always* populated — wrong, from checking real
  samples that all happened to have wind loads; see QUESTIONS.md's "Fixed: WIND section
  unconditionally populated" entry, and `docs/neutral-file/WALKTHROUGH.md` for why a
  count/content mismatch here breaks `iecho.exe`'s parse several sections later, not at `#$ WIND`
  itself. (`#$ UNITS` and `#$ COORDS`, covered separately below, aren't `AUX_DATA` subsections and
  aren't count-gated the same way — a fixed conversion-constants block and a self-describing,
  always-present count line, respectively.)
- `#$ RESTRANT` (the support data v1 reads and writes): one block per restraint, one *degree of
  freedom* sub-block per DOF (up to 6), each DOF as 4 lines — 2 data lines (node, restraint-type
  code 1–62, stiffness, gap, friction, connecting node, direction cosines) then a length-prefixed
  tag line and a length-prefixed GUID line. Restraint type codes cover anchors (`ANC`), single
  and double-acting translational restraints (`X`,`Y`,`Z`,`+X`,`-X`, …), guides (`GUI`), limit
  stops (`LIM`), rod/spring-related codes (`XROD`, `+XROD`, `XSPR`, …) — the full 1–62 table is
  in the vendor doc and is reproduced as the `RestraintType` enum with XML-doc comments in code.
  **Corrected support taxonomy (per review):** a rest is a one-directional restraint that allows
  lift-off (`+Y`/`+Z`); a hold-down is the opposite one-directional restraint (`-Y`/`-Z`); a rest
  and hold-down together are what the bidirectional `Y`/`Z` code represents; a guide is `GUI`; a
  line/limit stop is `LIM`; an anchor is the combination of all of these (equivalently, the single
  `ANC` code, or `Y` + `GUIDE` + `LIM` together). `RestraintTypeMapper` encodes this mapping from
  v1's semantic `SupportType` to the neutral-file `RestraintType`.
- Every `#$` section the parser doesn't specifically model (see "OUT of scope") is still read and
  re-emitted verbatim on write, so a file round-tripped through Conduit without any support
  changes is byte-identical, and a file with only `#$ RESTRANT` changes preserves everything
  else CAESAR II needs to re-import it.
- **A restraint record alone is not enough — the owning element must point to it.** Per
  `NeutralFile-v15.pdf`, an element's 4th auxiliary pointer is a 1-based pointer into
  `#$ RESTRANT`'s records; without it, CAESAR II/`iecho.exe` silently treats the node as having no
  support at all even though the restraint data itself is well-formed. `NeutralFile.AddRestraint`
  wires this up (see its doc comment and `docs/neutral-file/WALKTHROUGH.md`'s `#$ RESTRANT`
  section for the full fix, including the rigid-stiffness-constant and direction-cosine findings
  that came with it — this was a confirmed, real user-reported bug, not a hypothetical).

## Generating test neutral files
Per direct instruction (2026-08-24): Conduit should be able to produce its own valid test neutral
files, so the user isn't required to hand-build them. Two strategies were on the table — patching
a real CAESAR-II-exported seed file (mirrors the user's own Python tooling, which always launches
real `iecho.exe` to export a valid file first and only ever makes narrow edits on top of it,
never hand-constructing one from nothing) vs. synthesizing every section from scratch (fully
self-contained, no seed file needed, but more work and more risk of getting an obscure section
wrong). **Decision: blend — patch a real seed now, keep pushing from-scratch synthesis in
parallel.** Unit-system default for anything synthesized from scratch: CAESAR II's own standard
metric preset (name TBD — the exact preset name wasn't confirmed; see `QUESTIONS.md`), not the
company-specific "AIBEL (mm)" unit-system name found in the real samples used for reference.
Generated test files with no real project data are committed like the existing fixtures.

`tests/.../NeutralFileFixtureBuilder` (test-only) now synthesizes every section correctly,
confirmed byte-for-byte against 4 real samples and `NeutralFile-v15.pdf`:
- **`#$ VERSION`**: 1 info line + exactly 60 fixed 75-char title-page lines (FORTRAN `(2X, A75)`),
  61 lines total — previously just the 1 info line, a real bug: everything after `#$ VERSION`
  would land 60 lines earlier than a real file expects. **This may be the actual root cause of an
  `iecho.exe` "Error processing CONTROL section, line # 62" the user hit** — line 62 is exactly
  where a real file's `#$ CONTROL` header sits, one past its 61-line `VERSION` block — if the file
  that triggered it was a Conduit-synthesized fixture rather than a round-tripped real file. Not
  yet confirmed which case applies.
- **`#$ WIND`**: header-only when `NumWindLoads = 0` (no wind load), one 6-value data line when
  `NumWindLoads = 1` — see "Key structural facts" above; corrected 2026-08-26 after an earlier fix
  here wrongly made it *always* carry a data row regardless of the count (see QUESTIONS.md).
- **`#$ COORDS`**: lists the start coordinate of every *discontinuous* piping segment (a segment
  whose `FromNode` isn't the previous element's `ToNode`) — format `(2X, I13)` for the `NXYZ`
  count, then `(2X, I13, 3F13.4)` per entry. Optional per the vendor doc ("may not exist") but
  real samples always carry at least the count line. The fixture builder's elements always form
  one contiguous chain per run, so this is always just the zero-count line.
- **`#$ UNITS`**: never empty — 4 lines of 22 packed conversion constants (`(2X, 6G13.6)`, order
  `CNVLEN..CNVTHK` per the vendor doc) + 24 unit-label lines (`(2X, A<n>)`, widths per label per
  the vendor doc, order `CCVNAME..CCVTHK`). The constants/labels are confirmed byte-identical
  across all 4 real samples — ordinary physical conversion factors (25.4 mm/in, 4.448 N/lbf, ...)
  and standard engineering unit abbreviations (mm, N, kg, MPa, ...), not project-specific — except
  `CCVNAME` itself, which the real samples set to the company-specific "AIBEL (mm)"; the builder
  uses the generic "Metric (mm)" instead.

**Update (2026-08-26)**: the user supplied three real, explicitly-safe-to-commit `.cii` files —
`fixtures/real-samples/TESTv15.cii`, `TESTv15_slugged.cii`, `44002.cii` — unblocking the patch half
of the blend. These reconfirmed most of the fixes above byte-for-byte (same 61-line `VERSION`,
28-line "AIBEL (mm)" `UNITS` block) and surfaced a new fact: their `#$ ELEMENTS` geometry
(`DeltaX/Y/Z`, OD, wall thickness) is in **millimetres**, confirmed via a 355.6 mm OD element being
exactly a 14" pipe OD in mm. Every `NeutralFileFixtureBuilder`-produced fixture up to this point
used inch-scale numbers instead (e.g. `OutsideDiameter: 6.625`) — harmless for the fixtures'
own unit tests (self-consistent either way), but it means `SpanLimitCalculator`'s heuristic math
(calibrated in psi/lb/inch) produces nonsensical results on real mm-scale geometry — see
`QUESTIONS.md`'s "Blocking: SpanLimitCalculator's unit-blindness" entry; not fixed yet, needs the
user's direction since it's cross-cutting support-placement math, not a single support type.

Built `fixtures/loop-50m-3d.cii` per direct instruction: a straight 50 m leg in X with a 3D
expansion loop (up in Y, out in Z, back down, back in Z) at the midpoint, using millimetre-scale
geometry and metric OD/WT (168.3/7.11 mm) to match the real samples' unit convention.

**Update (2026-08-26, continued)**: `iecho.exe` still rejected the loop file — "Error processing
ELEMENT section, line # 79". Byte-diffed against the real samples at that exact line and found a
second structural bug: the element record's "line color, line visibility" field was written in
real/scientific-notation format, while every element in all 3 real samples writes it as plain
integers (`             -1           -1`) instead — see `docs/neutral-file/WALKTHROUGH.md`'s
`#$ ELEMENTS` section for the full field-by-field layout this was checked against, and
`QUESTIONS.md`'s "Fixed: ELEMENTS color/visibility line format" entry for detail. Fixed, with a
regression test (`ElementSectionFormatTests`) asserting the byte format against the real samples so
it can't regress silently. Also resolved the `SpanLimitCalculator` unit-blindness question from the
previous update: per direct instruction, Conduit's calculations now default to metric (mm/N/MPa/kg)
and convert non-metric file data to match, with every span message printing its unit — see
`QUESTIONS.md`'s "Resolved: SpanLimitCalculator's unit-blindness" entry.

**Update (2026-08-26, retest)**: the ELEMENTS fix confirmed correct — `iecho.exe` no longer errors
on the `#$ ELEMENTS` section. It now errors further along, at `#$ OFFSETS`, on *both* the raw
`loop-50m-3d.cii` and the `optimize`d `out.cii`. Same diagnostic approach: byte-diffed the
transition into that section against the real samples and found `#$ WIND` was the actual problem —
see the corrected "Key structural facts" bullet above and QUESTIONS.md's "Fixed: WIND section
unconditionally populated" entry. Fixed and regenerated all three built fixtures again, with a new
regression test (`SectionCountConsistencyTests`) checking every count-gated section's line count
against its `#$ CONTROL` field, for both the real samples and Conduit's own output — this class of
bug (a section's content not matching its own count) is confirmed to surface as an error several
sections later, not at the section that's actually wrong, so a byte-count check is worth more here
than trusting where `iecho.exe` says the error is.

**Update (2026-08-26, second retest)**: confirmed — no more `#$ WIND`/`#$ OFFSETS` error. The
user then tried converting to `.C2` (CAESAR II's native format) rather than just re-running
`iecho.exe`'s neutral-file check, and hit a new error: "Error processing MISCEL_1 section." Same
diagnosis approach: `#$ MISCEL_1` contains the RRMAT material-ID array *plus* an unconditional
trailing block (hanger-table defaults, execution options — see
`docs/neutral-file/WALKTHROUGH.md`'s `#$ MISCEL_1` section) that isn't gated by any `#$ CONTROL`
count at all; `NeutralFileFixtureBuilder` only ever wrote the RRMAT part. Fixed by reusing the
exact trailing block confirmed byte-identical between two of the three real samples (the third
differs slightly in a few fields — logged as a low-priority open question, not a structural
concern).

**Update (2026-08-26, third retest — success)**: `.C2` conversion now works.
`fixtures/loop-50m-3d.cii` is the first Conduit-generated neutral file confirmed to convert
successfully through `iecho.exe` on a real CAESAR II install. The structural-bug-hunting phase of
this effort is done; `docs/neutral-file/WALKTHROUGH.md` is the confirmed-correct reference going
forward. The user also corrected the loop's geometry: the original shape (two straight legs with
an open zigzag between them) wasn't a real expansion loop — a loop needs to actually return to
(near) its starting line, via a closed U/camelback shape with bends at each corner, per the
attached sketches. Rebuilt with the correct topology (horizontal approach, up, across-and-out in
the 3D direction, down, horizontal departure — 4 bends) and added `#$ BEND` support to
`NeutralFileFixtureBuilder` (see `docs/neutral-file/WALKTHROUGH.md`'s new `#$ BEND` section for
the field layout and the confirmed real-sample conventions it follows — bend radius/angle/fitting
values reused from `44002.cii`'s 13 real bends, tangent-point node numbering following that same
file's convention). Total X span across the loop is exactly 50 m, per direct instruction.

**Update (2026-08-26, fourth round — geometry corrected again, element-splitting added)**: the
`.C2` conversion worked again, confirming the 6-bend geometry was structurally sound, but the
user pointed out the *shape* was still wrong — it collapsed the 3D jog into a single diagonal
element instead of two separate axis-aligned legs. The exact element sequence, per direct
instruction: `+DX` (long), `+DY`, `-DZ`, `+DX`, `+DZ` (opposite of the `-DZ` leg), `-DY` (opposite
of the `+DY` leg), `+DX` (long, to complete) — 7 elements, 6 bends (one at the end of every
element but the last). Rebuilt again to match exactly.

Also per direct instruction: **element-splitting** (see "In scope (v1)" above for the mechanism).
The user noticed the two 24 m straight legs were being reported as unresolvable failures rather
than fixed, and gave the exact algorithm to use: round the max allowable span down to the nearest
1000 mm (e.g. 6446.76 mm → 6000 mm), divide the span by that to get full chunks plus a remainder,
and add a restraint at each new interior boundary. Implemented as `ElementSplitter` (pure
chunking math, unit-tested against the user's own worked example) plus
`NeutralFile.ReplaceElement` (the production element-mutation mechanism, splicing into both
`#$ ELEMENTS` and `#$ MISCEL_1`'s `RRMAT` array). Wired into `OptimizationLoop.Adjust` as the
fallback when no existing node is available. Verified against the corrected loop file: both 24 m
legs now split into 4×6000 mm elements with 3 interior rest supports each, and the file passes in
2 iterations instead of failing after 5.

Per a separate direct instruction, `TESTING.md` now has a "Test this now" section rewritten every
round with the exact current ask, rather than that living only in PR comments — see CLAUDE.md's
new bullet on keeping it dynamic.

**Update (2026-08-26, fifth round — bend radius and minimum chunk length)**: a proactive
follow-up (not from a failing test): bend radius should default to "Long" (confirmed via a CAESAR
II screenshot of its radius-type dropdown: Short/Long/3D/5D), and an element-split must never
leave a chunk next to a bend shorter than that bend's own minimum length plus a 500 mm
shoe-clearance buffer. `NeutralFile-v15.pdf` confirmed there's no separate neutral-file field for
the radius *type* — just the one numeric radius value — so `NeutralFileFixtureBuilder`'s bend
generation now computes "Long" (1.5x outside diameter, approximated from actual OD since Conduit
has no NPS table) per bend instead of reusing 44002.cii's flat 381 mm; the other two
`44002.cii`-derived constants ("angle to node position #1", fitting thickness) are still reused
as-is (see "In scope (v1)" and QUESTIONS.md for what's still unconfirmed there). The
minimum-chunk-near-a-bend constraint is implemented in `ElementSplitter` — see "In scope (v1)".

**Update (2026-08-27, sixth round — restraint pointer wiring, the actual root cause of "no
restraints appear")**: user's fifth retest confirmed the splitting and geometry work, but reported
that after converting the neutral file to a CAESAR input file, **no restraints existed at all** —
"I therefore suspect that the elements which are supposed to have the restraints do not correctly
point to them ... Check for a pointer first." Confirmed exactly right: `NeutralFile.AddRestraint`
had never set an element's 4th auxiliary pointer (the restraint pointer), so every restraint
Conduit ever wrote sat in `#$ RESTRANT` unreferenced by any element — valid data, but invisible to
CAESAR II. Fixed by wiring the pointer on every `AddRestraint` call (`ToNode`-preferred,
`FromNode`-fallback, with collision-avoidance for two restraints that would otherwise both want the
same connecting element), and by extending `ElementSplitter.Split` to preserve (not duplicate or
drop) an existing restraint pointer across a split element's chunks. Found and fixed a second,
independent contributing bug in the same pass: every restraint's `Stiffness` was left at `0` (a
spring with zero resistance, not a rigid support) — CAESAR II's actual rigid-restraint constant
(`1e12 lbf/in`, converted via `#$ UNITS`' CNVTSF constant) is now used instead, and axis-implied
restraint types (`X`/`Y`/`Z` and variants) now get their confirmed direction cosines too (`GUI`'s
is left an open question — see QUESTIONS.md — rather than guessed, per the support-placement-logic
consultation rule). Full details, vendor-doc/real-sample justification, and the known residual gap
in the owner-selection fallback are in `docs/neutral-file/WALKTHROUGH.md`'s `#$ RESTRANT` section.
79/79 tests passing (15 new), `dotnet build`/`test` clean. Regenerated `fixtures/loop-50m-3d.cii`;
`conduit optimize` still passes in 2 iterations with the same output the user originally reported,
now with all 11 final restraints correctly and distinctly wired.

**Update (2026-08-27) — re-verified the bend-radius question from the same PR comment**: the user
was confident there's a proper pointer/preset field for CAESAR's Short/Long/3D/5D bend-radius UI
options, distinct from just writing the resolved number, and asked that this be re-checked rather
than assumed. Did so directly: re-extracted `NeutralFile-v15.pdf`'s own text and re-read its
`#$ BEND` section fresh, plus cross-checked all 3 real samples' actual bend bytes. Conclusion is
unchanged from the earlier round but is now backed by a fresh, direct re-check rather than a
carried-forward summary — see QUESTIONS.md's "Re-verified: no bend-radius-type pointer exists"
entry and `docs/neutral-file/WALKTHROUGH.md`'s `#$ BEND` section for the full evidence. No code
change was needed.

## CAESAR II global configuration (`caesar.cfg`)
Separate from the per-job neutral file, every CAESAR II model directory contains a `caesar.cfg` —
install/directory-wide settings (axis convention, default piping code, material/component
database locations, and many analysis-behavior toggles not relevant to v1) that applied when jobs
in that directory were built and analyzed. There's no vendor documentation for this format (unlike
the neutral file); the user shared one real example, confirmed as a non-proprietary demonstration
case safe to use directly, committed at `fixtures/caesar.cfg`.

- Format (best-effort, inferred from the one example): each recognized line is `KEY = VALUE`
  followed by loosely-aligned numeric column metadata that v1 ignores, e.g.
  `DEFAULT_CODE =                    B31.3_2020        43      43.` parses to key `DEFAULT_CODE`,
  value `B31.3_2020`. Lines without an `=` (e.g. the leading `Ver. 15.010` version line) are
  skipped rather than treated as an error, since the exact grammar beyond this one example is
  unconfirmed. `CaesarConfigReader.Parse`/`Read` implement this.
- Fields v1 reads: `Z_AXIS_UP` (`YES`/`NO`, cross-checked against — never overriding — each file's
  own `#$ CONTROL.Izup`, see "Known open decisions"), `DEFAULT_CODE` (piping code *and edition*,
  e.g. `B31.3_2020`), `SYSTEM_DIRECTORY_NAME` and `User_Material_File_Name` (material database
  locations — surfaced for context; not parsed further, see "Known open decisions"). Everything
  else the parser recognizes is still available on `CaesarConfig.Values` for future use.
- The CLI looks for `caesar.cfg` next to the input `.cii` file (the directory convention above)
  and treats it as optional/supplementary: a missing or unreadable config doesn't fail the run,
  and its fields are used only to cross-check or add context, never to override anything the
  neutral file itself says.

### CAESAR II installation layout
Confirmed by the user, resolving where `caesar.cfg`'s `SYSTEM_DIRECTORY_NAME` actually points:
CAESAR II's data directory (material/component databases, not the application binaries) lives at
`C:\ProgramData\Intergraph CAS\CAESAR II\<version>\System` — one version-numbered subfolder per
installed release (e.g. `15.01`), each with its own `System` folder. **Conduit's supported version
floor is 15.00 and up** — "we will begin the build from 15.00 and up" — older installations are
out of scope, not just untested. `CaesarInstallationLocator` (`src/Conduit.Core/Configuration/`)
implements this: `FindInstallations`/`FindLatest` enumerate version subfolders under an injectable
root (default `DefaultInstallRoot`, the Windows path above), filtering to `>= MinimumSupportedVersion`
(15.0). It's pure `System.IO` directory listing, so — unlike COM automation or invoking
`iecho.exe` — the logic itself is fully unit-testable without Windows; only the *default* root is
Windows-specific.

**`iecho.exe` is not here.** Per the user's explicit correction, the converter binary lives in a
different branch of the install (the application directory), not under this `ProgramData`/`System`
tree — see "Native file adapter (iecho)" above. Don't reuse `CaesarInstallationLocator`'s paths for
it; it needs independent discovery logic when `IechoConverter` is implemented.

Actually parsing the material/component database *files* this locator can now point at is still
deferred — no format documentation for them, same situation `iecho.exe` is in (see "Known open
decisions"). This locator only answers "where," not "how to read what's there."

## Behaviour by example
1. Given a synthetic `.cii` file with two anchors (`#$ RESTRANT` type `ANC`) 18 m apart connected
   by a straight 6" Sch 40 carbon-steel `#$ ELEMENTS` run and no intermediate supports →
   `conduit optimize` proposes N rest supports (one-directional, type `+Y`/`+Z` per `IZUP`)
   spaced at or under the computed max allowable span, writes them into the output file as new
   `#$ RESTRANT` DOF blocks, leaves every other section byte-identical to the input, and the
   summary reports "PASS" from `MockStressSolver`.
2. Given the same run but with a vertical riser segment whose own length is what pushes the
   accumulated span over the max allowable → the support at the riser is classified as a guide
   (restraint type `GUI`), per the support-type heuristic. (A short riser that doesn't itself
   trigger the span-driven placement check isn't guaranteed a guide in v1 — see "Known open
   decisions".)
3. Given a run where the computed span would require more supports than fit before a real `#$
   EQUIPMNT` nozzle connection → the support nearest the nozzle is flagged as an anchor candidate
   per the near-equipment heuristic, and the summary explains why.
4. Given a malformed/unparseable input file (bad section header, a data line that doesn't match
   its section's expected column layout) → `conduit optimize` exits non-zero with a clear
   parse-error message (section/line reference), writes no output file.

## Acceptance criteria (definition of done)
- [x] `dotnet build` succeeds from a clean checkout via `setup.sh`, with no Caesar II/Windows
      dependency.
- [x] `dotnet test` passes, covering parser round-trip, span heuristic, support-type
      classification, placement, and iterate-loop-against-mock scenarios.
- [x] `conduit optimize <in> <out>` runs end-to-end on the synthetic (non-proprietary) `.cii`
      fixture files committed under `fixtures/`, producing a modified neutral file and a printed
      pass/fail summary.
- [x] Neutral file format, span-heuristic table, and support-type rules are documented in code
      (XML doc comments) with their simplifying assumptions stated explicitly.
- [x] `CaesarComStressSolver` and `IechoConverter` exist as skeletons (compile, not implemented)
      and do not block build/test.
- [x] PROGRESS.md and QUESTIONS.md updated per CLAUDE.md as work proceeds.

## Known open decisions (pre-answer what you can)
- Real sample `.cii` files and the official Hexagon format documentation (CAESAR II Users Guide,
  v15 neutral file interface) are now available and were used to write the "Neutral file format"
  section above. The supplied sample files are demonstration/example files (not client project
  data) and were reviewed locally but are **not committed** to this repo — v1 fixtures are
  freshly authored, structurally-valid synthetic `.cii` files instead. See QUESTIONS.md for the
  full reasoning.
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
- **Partially resolved (2026-08-26)**: `SupportPlacer`'s own initial-pass walk still doesn't split
  elements — a support from that first pass can only land at an existing node — but per direct
  instruction, `OptimizationLoop`'s iterate-and-adjust fallback now does (`ElementSplitter` +
  `NeutralFile.ReplaceElement`, see "In scope (v1)"): a single-element span with no existing node
  is split into evenly-spaced chunks rather than reported as an unresolvable failure. The
  consequence flagged in the original review (a vertical riser only gets classified as a guide
  when it happens to be the element whose own length triggers the span-driven overflow check; a
  short riser fully contained within an otherwise-fine span may not get its own guide) still
  applies to `SupportPlacer`'s own walk, unchanged — proactively splitting *during* the initial
  pass (not just reactively, after a failure is detected) is still deferred. A previous fix that
  forced a guide at every vertical segment's start regardless of span was tried and found unsound
  in review (breaks on short verticals) and has been removed; that heuristic stays removed.
- **Open (2026-08-26)**: `ElementSplitter`'s minimum-chunk-near-a-bend constraint (see "In scope
  (v1)") only covers a bend at the split element's own `ToNode`. A bend at the element's `FromNode`
  (the *preceding* element's own corner) isn't visible from a single `Element` — `OptimizationLoop`
  doesn't currently thread neighbor context into the split call. Not yet exercised by any real
  case (our own fixture's two splits are both comfortably clear of their nearest bend either way).
  **Next step if it ever matters**: pass the preceding element's bend status (`file.Elements` is
  already in scope in `OptimizationLoop.TrySplit`) into `ElementSplitter.Split` and apply the same
  minimum to the first chunk too.
- **Resolved (2026-08-21, updated with the confirmed absolute path):** the "material database...
  in the system folder" question above is clarified — every CAESAR II model directory carries a
  `caesar.cfg` global-settings file (the user shared a real example, confirmed as a
  non-proprietary demonstration case and now committed at `fixtures/caesar.cfg`), which names
  `SYSTEM_DIRECTORY_NAME` (typically just `SYSTEM`) and `User_Material_File_Name` (a user-defined
  `.UMD` material file), plus `DEFAULT_CODE` (the piping code *and edition*, e.g. `B31.3_2020` —
  answering the "which standard and year" half of the question directly). The user has since
  confirmed the absolute root this resolves against: `C:\ProgramData\Intergraph CAS\CAESAR
  II\<version>\System` — see "CAESAR II installation layout" above, implemented by
  `CaesarInstallationLocator`. v1 now parses `caesar.cfg` (`CaesarConfig`/`CaesarConfigReader`)
  and locates the version/System directory on disk (`CaesarInstallationLocator`), surfacing both.
  What's still deferred: actually reading the material database *files* at that location
  (`.UMD`/system database) — there's no format documentation for them (same situation as
  `iecho.exe`), and v1 doesn't need to, since `#$ ALLOWBLS` already gives the allowable stress
  CAESAR II computed per-element from whatever that lookup would have produced. Parsing those
  database files becomes necessary only if a future non-mock solver needs to compute allowables
  itself rather than reading what CAESAR II already computed.
- **Resolved (2026-08-28):** the bend-corner support-placement bug that opened this whole design
  discussion (`SupportPlacer` placing supports directly on bend corners — not buildable without a
  trunnion) is fixed, along with everything it turned out to depend on. The final, confirmed model:
  span accumulation is tracked per horizontal axis (not combined), with a universal reset — any
  support, on any axis, resets both horizontal accumulators and the vertical one — since a rest
  resists gravity regardless of which direction the pipe happens to run at that point. Bend corners
  and tee/branch nodes (detected by node degree, not yet by SIF/collinearity) are excluded from
  placement with the same clearance buffer `ElementSplitter` already used for splitting; a vertical
  run's own length is checked against 2x the horizontal max span, not 1x; and every eligible plain
  rest also gets a co-located guide, since "close to a directional change" (the one condition left
  undefined, per direct instruction: "no need to define this right now") falls out of the same
  bend/tee clearance check for free. Implemented in `SupportPlacer`/`PipeAxisClassifier`, with
  `MockStressSolver` updated to the identical per-axis model so the iterate loop's pass/fail check
  doesn't fight the placer's own decisions. Verified against three examples (the existing 3D loop
  fixture, a new self-designed 2D planar-jog fixture at `fixtures/loop-2d.cii`, and a new flattened
  axis-aligned approximation of the textbook's Fig 6.8 example at `fixtures/fig6-8-example.cii` —
  flattened because the real figure turned out to be genuinely sloped/diagonal once re-checked
  against the actual image rather than an earlier paraphrase, and diagonal segments remain out of
  MVP scope). Still open: tee/branch *span* exclusion (only the node itself is kept clear of
  placements so far, not a separate accumulator for the branch arm), applying the SIF at a tee, the
  guide direction-cosine question (still `(0,0,0)`, unresolved from a few rounds back), and a
  reactive-split rest not getting the same companion guide an initial-pass one does. See
  QUESTIONS.md's "Implemented: the `SupportPlacer` rewrite" entry for the full derivation.
- **Resolved (2026-08-28, third round):** a real report (with CAESAR II screenshots) showed the
  bend-corner bug still happening after the rewrite above — traced to `OptimizationLoop.Adjust`'s
  reactive fallback path (`TryPickMidpointNode`/`TrySplit`, used when the initial pass alone can't
  fully resolve a span), which the rewrite hadn't touched and had no bend/tee awareness at all.
  Fixed: `TryPickMidpointNode` now excludes bend/tee nodes with the same clearance
  `SupportPlacer` uses; the split fallback (renamed `TrySplitAtFirstOverflow`) walks a failing
  zone's elements in file order and splits the first one that would push its axis (now a real
  `PipeAxis` field on `StressFinding`, not embedded only in its message) past the *remaining*
  budget, not the pipe's full max span — accounting for however much of that axis's allowance
  earlier elements in the same zone already spent. This is conservative rather than span-optimal
  (can add more supports than a human would place by hand in the same spot) but always converges
  without landing on an excluded node — logged as a known follow-up, not fixed further this round.
- **Resolved (2026-08-21):** the database-for-iteration-tracking question above is answered — not
  needed yet ("the first step of this program is to have a fully functioning support placement
  program"), so SPEC.md's "Storage: none... No database" constraint stands unchanged for v1. It
  is, however, a real planned direction once placement itself is solid: accumulating iteration
  history so later runs can supplement first-principles heuristics with empirical knowledge
  learned from stored outcomes (the user's stated design philosophy, citing leap71/noyron-style
  computational engineering). Captured here as a roadmap item, not built now — no schema, no
  storage code, nothing to reverse if the direction changes before it's built.
- Axis handling: `RestraintTypeMapper`/`SupportPlacer` use each file's own `#$ CONTROL.Izup` as
  the authoritative vertical-axis source (baked in by CAESAR II at generation time, so it should
  already reflect whatever `caesar.cfg` was active for that model). `caesar.cfg`'s `Z_AXIS_UP` is
  used only as a cross-check — the CLI prints a warning if the two disagree, but doesn't override
  `Izup` with it. Decided this way (reversible, logged in QUESTIONS.md) rather than making
  `caesar.cfg` authoritative, since it's an external file located by directory convention with no
  guaranteed correspondence to a given input file, whereas `Izup` is intrinsic to the file itself.
- **Resolved (2026-08-28):** `SpanLimitCalculator`'s fallback constants and formula, both flagged
  above as "best-effort" placeholders, are now real. Per direct instruction, the material fallback
  is ASTM A106 Grade B (UMAT1.umd material #107 from the user's own database printout — chosen
  because it's a real material with every field `SpanLimitCalculator` needs populated; materials
  #1-10, including "LOW CARBON STEEL" previously used implicitly, carry no allowable/yield/UTS data
  at all and don't exist as usable materials in the standard): cold allowable stress 118 MPa, yield
  241 MPa, density 7833.4399 kg/m3, elastic modulus 203,400 MPa. The formula itself is now the
  textbook's own (Pipe Stress Engineering, Ch. 6, Section 6.2, Eqs. 6.1/6.2) rather than Conduit's
  earlier simply-supported-beam derivation: a semi-fixed-beam bending criterion (`L1 =
  sqrt(10*Z*S/w)`, constant 10 not 8, since the pipe continues past each support rather than
  terminating there) and a sag/deflection criterion (`L2 = (128*E*I*delta/w)^(1/4)`, using a 12.5mm
  design sag limit — the lower/more conservative end of the book's Kellogg range for process
  piping), taking the allowable span as `min(L1, L2)` per the book's own stated rule. `#$ ALLOWBLS`
  remains the first-choice source per element when a real file provides it; these are fallback
  defaults only. Also confirmed: the bend-clearance number from the prior round was a typo (500mm,
  not 200mm) — already matches `ElementSplitter`'s existing radius+500mm constant exactly, no code
  change needed. See QUESTIONS.md's "Implemented: real A106 Grade B material + textbook span
  formula" entry for the full derivation and verification detail.
