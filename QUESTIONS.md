where Claude parks non-blocking questions + logs assumptions

## Assumptions made (Phase 1 interview, 2026-08-20)
- Starting the C# project from scratch in this repo — README's "Hello World"/parser status note
  is treated as aspirational/stale; no prior code exists to pull in.
- Caesar II feedback loop is abstracted behind `IStressSolver`. `MockStressSolver` is the only
  functional implementation for v1; `CaesarComStressSolver` is a compiled skeleton only (no COM
  calls), to be completed later on a Windows machine with a licensed Caesar II install.
- (Superseded 2026-08-21, see below) No real `.c2` sample files or format docs available yet.
- v1 scope is the "broader" option: span heuristic + support-type selection (rest/guide/anchor/
  spring) + a stubbed iterate-until-pass loop against `MockStressSolver`.
- Deploy target: local CLI (`Conduit.Cli`), not a service — inferred from README (local engineer
  tool paired with a desktop Caesar II install); no storage/DB needed beyond the neutral file
  itself.
- Target framework: .NET 8 (LTS) — chosen for a stable, cross-platform (Linux-buildable) C# stack
  matching README's "C#" stack constraint; Visual Studio Community remains usable for local dev
  on Windows against the same solution.
- Simplified span-limit table and support-type classification rules are Claude's best-effort
  encoding of common heuristics, explicitly documented in code as non-code-compliant
  approximations — not a substitute for a real B31.3 calculation.

## Follow-ups (non-blocking, revisit later)
- Implement and validate `CaesarComStressSolver` against a real licensed Caesar II install
  (Windows COM automation) — cannot be done in this headless container.
- Replace the simplified span/support-type heuristics with code-compliant B31.3 calculations and
  WRC 297/537 nozzle load checks (currently explicitly out of v1 scope per SPEC.md).
- `#$ AUX_DATA` subsections other than `NODENAME`/`RESTRANT` are currently round-tripped opaquely
  (preserved on write, not modeled). Interpret `BEND`, hanger data, `EQUIPMNT`, etc. as later
  stages need them.

## Real neutral file format adopted (2026-08-21)
The user provided the official Hexagon "CAESAR II Neutral File" documentation (CAESAR II Users
Guide, v15 interface — public vendor docs) plus four real `.cii` files, supplied as demonstration
examples (not client project data — corrected 2026-08-21 per review, see below). Decision, per
SPEC.md's existing clean-room hard constraint (set by the user's own README "IP considerations"
section, i.e. not a new rule Claude is inventing here):
- **Format documentation (the PDF)**: used directly and cited in SPEC.md. It's Hexagon's public
  product documentation, not the user's proprietary material — no IP concern.
- **The four real `.cii` sample files**: reviewed locally in this session to confirm the
  published format spec matches real-world files (it does — same section structure, same
  fixed-width columnar layout, same `#$ RESTRANT` DOF-block structure). **Not copied into the
  repo, not committed, not used as the literal content of any fixture** — kept out pending
  explicit confirmation that committing them is wanted, independent of the provenance correction
  below.
- v1's `fixtures/` directory will instead contain freshly authored, structurally-valid `.cii`
  files with invented node numbers/geometry/tags — real CAESAR II syntax, fictitious project.
- Flagged explicitly to the user in the Phase 1 chat response (not just buried here) given the
  IP/legal stakes — if the user did intend for the real files to be committed (e.g. they already
  have clearance to do so), they can say so and this decision is easy to reverse.

**Correction (2026-08-21, PR review):** the sample files were mischaracterized above and in
SPEC.md as "real client project files" — the user has clarified they are demonstration/example
files, not client project data. Wording fixed in both documents. This corrects the provenance
claim only; it doesn't by itself change the not-committed decision above, since that's a
separate, easily-reversed call the user can override any time.

## Results/output workflow documented (2026-08-21)
User supplied three more vendor PDFs: CAESAR II 15.1 "Output Tab", "New Analysis Reviewer Help"
(17 pages), "Static Analysis Help" (69 pages), and "Static Analysis Output Help" (76 pages, read
40 of 76 — Standard Reports section covered in full; stopped after Report Template Editor/
Available Commands since remaining pages are GUI menu reference not needed for the spec). All
public vendor docs, no IP concern. Key correction to the earlier (2026-08-21, same day) COM note
in SPEC.md: it is NOT true that no batch/parseable results format exists — CAESAR II can save
standard reports (Code Compliance, Restraints, Displacements, Stresses, …) to plain ASCII text
files, and a custom Report Template (Report Template Editor) gives a stable, fixed column layout
per field. Revised `CaesarComStressSolver` plan in SPEC.md: drive analysis via COM (still
required — no headless/CLI report generator exists outside COM/GUI), then have it emit a Code
Compliance + Restraints report to text files via a custom template, then parse those text files
for `StressResult`, instead of pulling values through interactive COM calls one at a time. Also
documented the real load-case/stress-type model (OPE/SUS/EXP/OCC/FAT/HGR/HYD/CRP + combination
methods) as context for future non-mock stress-check work — v1's `MockStressSolver` stays a
deliberate simplification, unchanged in scope.

## Native format (.C2/._A) adapter requirement identified (2026-08-21)
User shared two Python files (`iecho.py`, `lift_case_builder.py`) from a different internal
project, for context/requirements only — explicitly not to copy the logic, and neither file is
committed here. They wrap `iecho.exe` (CAESAR II's own `.C2`↔`.cii` converter) to let that other
tool patch neutral files without the user manually running iecho by hand each time.
- Real production files are `.C2`/`._A` (CAESAR II's native format), never `.cii` directly.
  `.cii` is purely an interchange format Conduit (and this other tool) work with internally.
- Added a new `INeutralFileConverter` interface (skeleton `IechoConverter`, same treatment as
  `CaesarComStressSolver` — not implemented/tested in this container, deferred to Windows) so
  Conduit's architecture has the seam for this, even though v1's CLI still only accepts `.cii`
  directly. Logged as an assumption per CLAUDE.md, not a blocking question — the interface shape
  is a routine, reversible engineering call.
- Noted an asymmetry visible in the reference implementation worth validating later, not
  guessing at now: `.cii` → `.C2` ran as a silent scripted subprocess call; `.C2` → `.cii` was
  done via an interactive `iecho.exe` launch + poll-for-output-file, which may be a real
  `iecho.exe` limitation on the export direction or just that tool's design choice. Flagged as
  an open decision in SPEC.md rather than asserting either way.
- This did not change the `.cii` format documentation itself (still the real, official CAESAR II
  neutral file format from the Hexagon PDF) — only added the layer above it that converts to/from
  what users actually have on disk.

## Phase 2 build — assumptions made (2026-08-21)
Implemented the C# solution per SPEC.md. Decide-and-proceed calls made along the way, all
reversible/internal:
- **Span-limit "table" implemented as a computed formula, not a literal lookup table.**
  `SpanLimitCalculator` derives max allowable span from first-principles beam theory
  (simply-supported, uniform load: `L = sqrt(8·σ_allow·Z/w)`), with clearly-labeled placeholder
  constants (`DefaultAllowableBendingStress = 1500 psi`, `DefaultSteelDensity`), rather than
  reciting specific-looking numbers from a real span table from memory that I could not verify
  and might present as more authoritative than warranted. This still satisfies the acceptance
  criterion ("compute a maximum allowable span... documented... simplifying assumptions") — it's
  a formula instead of a table, which SPEC.md's "table" wording didn't strictly require given the
  bullet itself says "compute".
- **`SupportType.SpringCandidate` is an iterate-loop escalation, not an initial-placement rule.**
  A literal reading of Behaviour example 3 ("spans exceeding thermal-growth thresholds flag
  spring candidates") as an initial classification rule is self-defeating: placement already
  spaces rest supports at/under the max span, so "span is near the max" would fire on nearly
  every placed support, making the rule meaningless. Instead, `OptimizationLoop` escalates an
  already-placed (non-anchor) support to a spring candidate only when a failing span has no room
  for an intermediate support — matching the loop's own documented "change type" adjustment.
- **Vertical risers always get a mandatory guide at their start node**, independent of span
  accumulation — found via a failing test: the span-driven overflow check can trigger on a later
  *horizontal* element after passing a short riser, missing the riser itself. `SupportPlacer` now
  places a guide the moment it enters a vertical segment, resetting the span accumulator there.
- **NODENAME is parsed (read-only) but never written back** — resolves a minor inconsistency
  between SPEC.md's "In scope" bullet (only mentions ELEMENTS + RESTRANT) and its "OUT of scope"
  bullet (implies NODENAME is also interpreted). Harmless either way since its raw lines are
  preserved verbatim regardless.
- **Node positions assume each disconnected element chain's first node is the origin** — `#$
  COORDS` isn't parsed in v1, and only relative geometry along a run matters for span/length math.
- **`setup.sh` installs the .NET SDK via `apt` first**, falling back to the `dot.net` install
  script — this sandbox's egress proxy blocks `dot.net` outright; `apt`'s `dotnet-sdk-8.0`
  package worked and is likely more reliable in similar restricted environments generally.
- **CLI exit codes**: `0` = passed, `1` = usage/parse error (no output file written), `2` = ran
  successfully but didn't converge to a full pass within the iteration cap (output *is* written,
  with the remaining failures printed) — not specified in SPEC.md, a conventional choice.
- **Fixture count**: two CLI-exercised fixtures (`straight-run.cii`, `run-with-riser.cii`) plus
  `malformed.cii` for the parse-error case, rather than one fixture per Behaviour-by-example
  scenario. Example 3 (spring escalation near a nozzle) is covered by targeted in-memory tests
  using the shared `NeutralFileFixtureBuilder` test helper instead of a fourth committed file,
  to keep the fixture set lean while still meeting the acceptance criterion.

## Blocking questions answered; caesar.cfg support added (2026-08-21)
The user answered both items previously logged as blocking (per CLAUDE.md's stop-and-ask rules)
in a PR #4 review comment, and shared a real `caesar.cfg` example — confirmed a pure/non-client
demonstration case, safe to commit directly (now at `fixtures/caesar.cfg`).

- **Material database question, answered.** Every CAESAR II model directory carries a
  `caesar.cfg` naming the material-database locations (`SYSTEM_DIRECTORY_NAME`,
  `User_Material_File_Name`) and the default piping code *and edition* (`DEFAULT_CODE`, e.g.
  `B31.3_2020`) — directly answering "which standard and year". Added `CaesarConfig`/
  `CaesarConfigReader` (`src/Conduit.Core/Configuration/`) to parse this file (best-effort format,
  no vendor doc — inferred from the one example, same treatment as `iecho.exe`) and wired it into
  the CLI: it looks for `caesar.cfg` next to the input `.cii`, and if present, surfaces
  `DefaultCode`/`SystemDirectoryName`/`UserMaterialFileName` in the run summary. Actually reading
  the referenced material-database *files* stays deferred — no format spec for them either, and
  v1 doesn't need to since `#$ ALLOWBLS` already has the allowable stress CAESAR II computed from
  whatever that lookup produced.
- **Decide-and-proceed (reversible, logged per CLAUDE.md): `caesar.cfg`'s `Z_AXIS_UP` cross-checks
  rather than overrides each file's own `#$ CONTROL.Izup`.** `Izup` is baked into the neutral file
  itself at generation time by CAESAR II, so it should already be correct for that specific job;
  `caesar.cfg` is found by directory convention next to the input file, with no guaranteed
  correspondence to it (wrong directory, stale config, etc.). `RestraintTypeMapper`/
  `SupportPlacer` keep using `Izup` unchanged; the CLI just prints a warning if the two disagree.
  Chose this over making `caesar.cfg` authoritative because overriding intrinsic per-file data
  with an externally-located file on a naming convention is the riskier, less-reversible-feeling
  direction of the two — easy to revisit if the user wants the override behavior instead.
- **Storage/database question, answered.** Not needed yet — "the first step of this program is to
  have a fully functioning support placement program" — so SPEC.md's "Storage: none... No
  database" constraint is unchanged for v1. Confirmed as a real future direction once placement
  itself is solid, though: accumulating iteration history so later runs can supplement
  first-principles heuristics with empirical knowledge from stored outcomes (the user's stated
  design philosophy, citing leap71/noyron-style computational engineering). Logged as a roadmap
  note in SPEC.md's "Known open decisions", not built now.
