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
  **Next step if you confirm they may be committed:** add them under `fixtures/` alongside (not
  replacing) the synthetic ones, add a `NeutralFileRoundTripTests` case per file (byte-identical
  round-trip + expected element/restraint counts, same pattern as the existing fixtures), and
  register each under the test project's `<None Include="..\..\fixtures\*.cii">` glob — no code
  changes needed, since the reader/writer already handle real files (that's how they were
  validated originally).
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

## Default piping code, TESTING.md, and process conventions (2026-08-21)
Per direct user instruction (not a PR review comment this time):
- **Default piping code is now `CaesarConfig.DefaultAssumedCode = "B31.3_2024"`** (was previously
  no hardcoded default at all — the CLI only ever printed a code when `caesar.cfg` had one).
  `CaesarConfig.EffectiveCode(config)` always prefers `config.DefaultCode` when present, falling
  back to `DefaultAssumedCode` only when there's no `caesar.cfg`/no `DEFAULT_CODE` in it. The CLI
  now always prints the effective code (previously conditional on `caesar.cfg` existing). No
  calculation actually varies by code edition yet in v1 (`#$ ALLOWBLS` already carries the real
  allowable regardless) — this is reporting/context, matching the user's "always take from the
  config" instruction.
- **Added TESTING.md** — instructions for testing Conduit (automated + manual), kept up to date
  per a new CLAUDE.md instruction; consult/update it whenever testing is relevant, not just when
  writing new tests.
- **Process convention, now in CLAUDE.md**: every blocking-question entry logged here must also
  state the concrete next implementation step to take once the user decides, so an answer alone
  is enough to unblock work without another round-trip. Retrofitted the one still-open item above
  (the real `.cii` sample files) with this framing; everything else in this file is already either
  resolved or a non-blocking decide-and-proceed assumption, so nothing else needed retrofitting.

## CAESAR II install-tree layout confirmed; iecho.exe location corrected (2026-08-21)
Per direct user instruction:
- **Confirmed the absolute install path**: `C:\ProgramData\Intergraph CAS\CAESAR II\<version>\System`
  (e.g. `...\15.01\System`) is where the material/component databases actually live, resolving
  what `caesar.cfg`'s `SYSTEM_DIRECTORY_NAME` is relative to. Added `CaesarInstallationLocator`
  (`src/Conduit.Core/Configuration/`) to enumerate installed versions and resolve each one's
  `System` directory — pure `System.IO`, fully unit-tested against an injectable root even though
  the *default* root is Windows-specific.
- **Version floor: 15.00 and up** — "we will begin the build from 15.00 and up." Encoded as
  `CaesarInstallationLocator.MinimumSupportedVersion`; older installations aren't discovered.
- **`iecho.exe` is in a different install branch** than the `ProgramData`/`System` tree above —
  confirmed by the user, matching the ambiguity already flagged in SPEC.md's "Native file adapter
  (iecho)" section. Corrected that section and added an explicit warning against reusing
  `CaesarInstallationLocator`'s paths for `iecho.exe` discovery — it needs independent logic,
  still deferred (no confirmed path yet, just confirmation that it's elsewhere).
- Not wired into the CLI/allowable-stress logic yet — nothing in v1 actually reads the database
  files this locator points at (same "no format spec yet" situation as before); this only answers
  "where," matching the scope of what was asked.

## Resolved: "hold off on committing the example files" does NOT mean reverting `fixtures/caesar.cfg`
The same message that gave the install-path info above also said "Hold off on comitting the
example files." This is genuinely ambiguous between two materially different actions:
1. **Forward-looking only**: keep not committing the four real `.cii` sample files (already the
   status quo — see the entry above), and don't fabricate/commit any new install-tree example
   files (e.g. a fake material-database file) as part of this round's work — which I wasn't
   planning to anyway, since no database *content* format is known yet, only the *path*.
2. **Also retroactive**: revert the already-committed, already-merged `fixtures/caesar.cfg` (PR
   #4, merged into `main`) — i.e. the user has reconsidered whether that example was actually
   fine to commit.

I did not guess — reading (1) requires no action (already the plan); reading (2) means removing
content from `main`'s history, which isn't cleanly undoable (it's already public/merged) and is
exactly the kind of "irreversible-feeling" content decision CLAUDE.md says to confirm rather than
assume. Asked the user directly via `AskUserQuestion`.

**Answered (2026-08-21): reading (1) — keep `fixtures/caesar.cfg` as-is.** The instruction was
forward-looking only: don't commit the real `.cii` sample files or fabricate new install-tree
example files going forward (already the plan either way). No repo change needed.

## Real-world iecho rejection found and fixed: CRLF line endings (2026-08-24)
The user reported the neutral file converter "does not work as it should... the iecho does not
accept it" — a real, concrete formatting bug, not a hypothetical. Root-caused it directly:
- `NeutralFileWriter.Write` hardcoded `\n` (LF-only) line joins, on every platform.
- `NeutralFileReader.Read` uses `File.ReadAllLines`, which is EOL-agnostic on input (accepts
  CRLF, LF, or CR transparently).
- Checked the real `.cii` sample files shared earlier in this session (still present locally,
  not committed, permitted to *read* for analysis per the clean-room constraint): every one uses
  CRLF. `iecho.exe`/CAESAR II are Windows/Fortran-heritage tools; LF-only is the classic failure
  mode for that kind of legacy sequential-file reader.
- **Fixed**: `NeutralFileWriter.Write` now joins with `\r\n`. Added `.gitattributes` pinning
  `*.cii` to `eol=crlf` so no local `core.autocrlf` setting or editor silently reverts this.
  Converted the committed fixtures to CRLF to match the real convention. Added
  `NeutralFileRoundTripTests.Write_UsesCrlfLineEndings` asserting the actual on-disk bytes (the
  existing round-trip tests compare in-memory string-list content, which is EOL-agnostic and
  couldn't have caught this — that's *why* it shipped unnoticed).
- **Not yet fully confirmed as the complete fix** — this container has no `iecho.exe` to test
  against, so I can't independently verify CRLF alone resolves the rejection versus being one of
  possibly several issues. Asked the user (in this round's summary) whether they still have the
  exact iecho error/rejection message, to either confirm this closes it out or point at what
  else is wrong.
- Added `reference/` (see below) specifically so future format-correctness work is checked
  against the primary vendor documentation directly, not a paraphrase — this bug is exactly the
  kind of drift-from-source error that invites.

## reference/ folder added; CLAUDE.md now requires consulting it (2026-08-24)
Per direct instruction: committed the 5 public vendor PDFs (already established as
non-proprietary Hexagon documentation, safe to commit — see the "Real neutral file format
adopted" entry above) to a new `reference/` folder, with a README indexing what each covers and
when to read it. CLAUDE.md now instructs always consulting these before touching neutral-file
format or CAESAR II I/O behavior. The real `.cii` sample files and any Python
neutral-file-generator programs the user shares stay out of the repo, per the existing
clean-room constraint — `reference/`'s README says so explicitly to avoid future confusion.

## Spring logic fully removed from the MVP (2026-08-24)
Per direct, explicit instruction: "I do not want to see a mention of it now for the mvp." This
supersedes every earlier entry in this file that described `SupportType.SpringCandidate` as an
iterate-loop escalation (those entries are left as historical record, not deleted, since they're
an accurate account of what was actually built at the time — but they no longer describe current
behavior). Removed from code: `SupportType.SpringCandidate`, its `RestraintTypeMapper` mapping,
and `OptimizationLoop`'s escalate-to-spring path (an unresolvable span now just gets reported).
Kept `RestraintType.Xspr` in the enum — that's the real CAESAR II restraint code, needed so a
real file that already has a spring restraint round-trips correctly; only *Conduit's own logic*
producing/assigning it is what's gone. Updated SPEC.md, TESTING.md, and the `OptimizationLoop`
tests to match; left README.md (the user's own original product-vision document, spanning all
stages, not just this MVP) untouched.

## Process change: support-type logic defined one type at a time, with consultation (2026-08-24)
Per direct instruction: "There is a lot of logic to implement, so I think we will have to take it
one step at a time for each support type. I want to be consulted on this logic definition." Added
to CLAUDE.md as a standing instruction — it overrides the general decide-and-proceed bucket for
this specific class of decision. **Next step**: before extending or changing rest/hold-down/
guide/line-stop/anchor placement logic further, bring the proposed logic to the user first, one
support type at a time, rather than implementing and presenting the result.

## Open observation, not yet actioned: SupportPlacer may be over-placing supports (2026-08-24)
The user flagged, as an initial/unconfirmed observation: "The span calculator implements support
everywhere it seems." Logged here rather than acted on, because there isn't enough to diagnose
yet — no specific input file, output, or expected-vs-actual comparison to work from, and the
user has said they'll supply correctly-formatted real neutral files for analysis (the current
committed fixtures are synthetic and, per the user, not correctly formatted — see the CRLF entry
above for one confirmed instance of that). **Next step once real fixture files are provided**:
run Conduit against them, compare the placed-support output against what the user expects for
that layout, and determine whether this is a real over-placement bug in `SupportPlacer`/
`SpanLimitCalculator`, a misreading of intentionally-conservative spacing, or a symptom of the
same file-format issues that caused the CRLF bug (e.g. if a real file's data is being misparsed
because of a formatting mismatch, span calculations downstream would be wrong in ways that could
look like "everywhere").

## Diagnosis: "Error processing CONTROL section, line # 62" (2026-08-24)
User shared a CAESAR II "Neutral File Generator" error screenshot: "Error processing CONTROL
section, line # 62" during a "Convert Neutral File to CAESAR II Input File" run. Two things about
this are worth recording:
- The error names the `CONTROL` section, but a byte-level comparison of `ControlSection.cs`'s
  output structure against a real sample file's actual `CONTROL` bytes (line count, line lengths,
  field widths, right-justification) found an exact match — so the *content and formatting* of
  that section itself isn't the likely fault. `iecho.exe` is a Fortran-heritage fixed-record
  reader; my working hypothesis is that this is a symptom, not the cause: an LF-only file (the
  bug fixed earlier this same session, before this screenshot arrived) causes a cumulative
  byte-offset drift as the reader consumes fixed-width records, which can surface as a parse
  failure partway into the file (e.g. "line # 62") rather than immediately at line 1, depending on
  exactly where the drift crosses a record boundary the reader chokes on.
- **Not yet confirmed**: whether this screenshot was taken against a build from *before* the CRLF
  fix (in which case it's very likely already resolved) or *after* it (in which case there's a
  second bug still to find). **Next step**: ask the user to retest against the current build (this
  branch, past the CRLF fix) and report whether "line # 62" still reproduces; if it does, get the
  actual `.cii` file that triggers it (or as much of it as can be shared) for a byte-level look,
  the same way the CRLF bug itself was diagnosed.

## Noted for later: CNODES are not anchor supports (2026-08-26)
Per direct instruction (not to act on yet): CAESAR II has a "CNODE" concept — a connection point
set up to see forces/moments between elements at a shared location, carrying its own CNODE number.
These are **not** anchor/support restraints even though they connect two points rigidly-ish; a
node with a CNODE assigned must not be treated as a support candidate by future placement logic.
**Next step**: once `#$` CNODE data is understood well enough (needs its own primary-source dig,
not done yet) and it's this support type's turn under the one-at-a-time consultation rule, exclude
CNODE-bearing nodes from `SupportPlacer`'s candidate set.

## Real test files supplied and committed; new synthetic loop test case built (2026-08-26)
Per direct instruction, the user supplied three real `.cii` files (renamed `.txt` for GitHub
upload, since GitHub blocks `.cii`) and explicitly said they're safe to commit — a change from
the earlier "real files stay local-only" stance for these three specifically:
- `fixtures/real-samples/TESTv15.cii`
- `fixtures/real-samples/TESTv15_slugged.cii` (differs from `TESTv15.cii` only in `#$ FORCMNT`'s
  force magnitudes — a "slug force" load case; structurally identical otherwise)
- `fixtures/real-samples/44002.cii` (per the user: equipment modeled as rigid elements with no
  weight — "it is important to ignore such elements for support considerations." Not yet acted on;
  logged here as a future support-placement input, needs the one-support-type-at-a-time
  consultation before any logic change.)

Confirmed these files exactly match this branch's current section-structure fixes (61-line
`VERSION`, 1-line `WIND`, 28-line `UNITS` byte-identical to the earlier "AIBEL (mm)" sample) —
good independent confirmation the earlier fixes are correct. **New finding**: element geometry
(`DeltaX/Y/Z`, OD, wall thickness) in these real files is in **millimetres**, confirmed via a
355.6 mm OD element that's exactly a 14" pipe OD in mm — not inches, which is what every fixture
`NeutralFileFixtureBuilder` has produced so far (e.g. `OutsideDiameter: 6.625` is 6.625 *inches*).
This hadn't been checked before since no committed fixture's absolute scale had been verified
against a real file's units until now.

Per direct instruction, built `fixtures/loop-50m-3d.cii`: a straight 50 m leg in the X direction
with a 3D expansion loop (up 3 m in +Y, out 3 m in +Z, back down, back in Z) inserted at the
midpoint — using millimetre-scale geometry (25000/3000 mm) and metric OD/WT (168.3/7.11 mm, a 6"
Sch 40 pipe's real metric dimensions) to match the real samples' unit convention. Structurally
verified against the real samples: `dotnet build`/`test` clean (37/37), section byte-layout
matches exactly (`VERSION` 1→63, `WIND` 1 line, `UNITS` 28 lines, etc.).

**Next step**: ask the user to run `iecho.exe` directly against `fixtures/loop-50m-3d.cii` (the
raw geometry, no supports added yet) as the cleanest first test — this isolates neutral-file
*structural* correctness from the separate, already-known `SpanLimitCalculator` unit-scale issue
below, which currently makes `conduit optimize`'s output on this file not meaningful yet. Report
back whether iecho accepts it; if not, share the exact error and as much of the file as needed to
diagnose it, the same way the CRLF and VERSION-length bugs were found.

## Blocking: SpanLimitCalculator's unit-blindness now empirically confirmed on real mm-scale data (2026-08-26)
This is a pre-existing, already-documented assumption (`SpanLimitCalculator`'s own XML doc:
"All neutral-file dimensions and densities are assumed to be in a single consistent unit system
(v1 doesn't parse `#$ UNITS`)") — not a new bug — but running `conduit optimize` against
`fixtures/loop-50m-3d.cii` just confirmed empirically how badly it breaks down: the calculator's
constants (`DefaultAllowableBendingStress = 1500` psi, `DefaultSteelDensity = 0.2836` lb/in³) are
calibrated for inch/psi/lb units, matching every fixture built so far. Fed real millimetre-scale
OD/WT (168.3/7.11 mm) instead, the formula mixes psi/lb-calibrated constants with mm-scale
geometry and computes a nonsensical max allowable span (1279 "mm", i.e. ~1.3 m, for a 6" pipe) —
every single span in the file "fails," including 3 m loop legs that would clearly be fine on a
real 6" line. This makes `conduit optimize`'s PASS/FAIL and support placement meaningless on any
metric-unit file right now, even though the *neutral file itself* is still structurally valid
(iecho-acceptance doesn't depend on this calculator at all).

This affects every support type's underlying math, not just one, so per CLAUDE.md's
one-support-type-at-a-time consultation rule this needs the user's direction before implementing,
not a unilateral fix. **Next step, once the user weighs in**: options include (a) parse `#$ UNITS`
and convert all geometry/stress/density inputs to one consistent internal unit before running any
heuristic math, (b) require `caesar.cfg` or a CLI flag to declare the model's unit system
up front, or (c) something else the user prefers — batched here as the concrete question rather
than guessed at.

## Investigation: generating our own valid test neutral files (2026-08-24)
Per direct instruction: "I think perhaps our next focus should be to make sure you are able to
create functioning neutral files for us to use for testing... If we are able to do this, I am not
required to create the test cases." Investigated using the real `.cii` samples and the user's
Python tooling (still local from earlier in the session, not committed) as reference, plus
`reference/NeutralFile-v15.pdf`. Findings, batched into a blocking decision put to the user (see
chat) rather than acted on unilaterally, since the branches are materially different amounts of
work with different risk profiles:

- **Conduit's existing fixture builder already gets the section skeleton right.** All 4 real
  sample files have the identical set of 20 `#$` sections in the identical order, every time —
  and `NeutralFileFixtureBuilder` (used for the committed `fixtures/*.cii`) already produces that
  same 20-section skeleton. Several sections (`AUX_DATA`, `EXPJT`, `DISPLMNT`, `FORCMNT`,
  `UNIFORM`, `OFFSETS`, `FLANGES`) are confirmed legitimately empty (zero body lines) when there's
  no data of that kind, matching what the builder already does.
- **Three sections the builder gets wrong, found by comparing byte-for-byte against the real
  samples:**
  - `#$ WIND` is never truly empty — even with no wind load, real files carry exactly one
    6-value real-number line. The builder currently emits zero lines for it.
  - `#$ UNITS` is never empty either — it's a fixed 28-line block (4 lines of conversion
    constants + 24 lines of unit labels, per `NeutralFile-v15.pdf`'s exact `#$ UNITS` field spec).
    Byte-identical across all 4 real samples (same "AIBEL (mm)" custom unit-system name and
    label set in every one — plausibly this user's/company's own CAESAR II unit-system
    configuration, not universal). The builder currently emits it completely empty.
  - `#$ VERSION` is a fixed-format title-block (PROJECT/CLIENT/etc. labeled text fields) that's
    ~61 lines in every real sample, not the single line the builder currently emits. **This may
    itself explain part or all of the "line # 62" error** if the file that triggered it was a
    Conduit-generated/synthetic file rather than a round-tripped real one — a wrong VERSION
    length would shift every following section's absolute line position. Not yet confirmed which
    case applies; needs the user to say which file triggered the error.
  - `#$ COORDS` structure/population rule isn't pinned down yet (present in real files, but which
    nodes get listed and why needs more primary-source digging before it can be generated).
- **The user's own Python tooling doesn't synthesize files from scratch either.** Read
  `iecho.py`/`lift_case_builder.py` for context (not copied, not committed, per the standing
  clean-room rule). Its actual strategy: launch real CAESAR II's `iecho.exe` interactively to
  export an existing, real `.C2` model to `.CII` (so CAESAR II itself produces a guaranteed-valid
  VERSION/UNITS/WIND/COORDS/etc.), then make narrow, targeted edits to *just* the restraint data
  on top of that already-valid file, then convert back with `iecho.exe` again. It never
  hand-constructs a neutral file from nothing.

**Decision (2026-08-24, via AskUserQuestion)**: blend — patch a real seed file now, keep pushing
from-scratch synthesis in parallel; unit-system default for anything synthesized is CAESAR II's
own standard metric preset (user: "I think Caesar has a metric default, not sure what it's called
atm" — name not yet confirmed, see the follow-up entry below), not the company-specific
"AIBEL (mm)" name found in the real samples; generated test files with no real project data are
committed like the existing fixtures.

**Acted on immediately** (the "synthesize from scratch" half, since it turned out to be far more
tractable than expected once checked against the primary source — see the new entry below for
what was fixed). **Still open** (the "patch a real seed" half): need the user to export one or
more small, throwaway, non-proprietary test models directly from their own CAESAR II as `.cii`
seeds. **Next step once supplied**: build a thin "patch" helper on top of the existing
read/write round-trip that only ever varies `#$ ELEMENTS`/`#$ RESTRANT` on top of a seed, proven
by `OtherSections_StayByteIdentical_WhenOnlyRestraintsChange` to leave every other section
untouched.

## Fixed: NeutralFileFixtureBuilder's VERSION/WIND/UNITS/COORDS sections were structurally wrong (2026-08-24)
Direct follow-up to the investigation above — checking `NeutralFileFixtureBuilder` (used for both
in-memory tests and the committed `fixtures/*.cii` files) against `reference/NeutralFile-v15.pdf`
and 4 real samples' actual bytes found it was generating structurally invalid files in three
places, now fixed and verified against both the primary source and the real samples (see
SPEC.md's new "Generating test neutral files" section for the full detail):
- `#$ VERSION` was 1 line instead of the required 61 (1 info line + 60 title-page lines) — **very
  likely explains the "Error processing CONTROL section, line # 62" `iecho.exe` error** if the
  file that triggered it was a Conduit-synthesized fixture (line 62 in a real file is exactly
  where `#$ CONTROL` starts, right after a correctly-sized `VERSION` block). Not yet confirmed
  whether that was the case — **next step**: ask the user which file triggered that error (a
  Conduit output, or a round-tripped real file) and have them retest against this fix.
  Regenerated the committed `fixtures/straight-run.cii`/`run-with-riser.cii` with the fix (same
  geometry/restraints as before, only the previously-broken sections changed) and reran the full
  suite (37/37 pass) plus a manual CLI run to confirm nothing else broke.
- `#$ WIND` was header-only (0 lines) instead of always carrying its 1-line default row.
- `#$ UNITS` was empty instead of its fixed 28-line conversion-constants-and-labels block.
- `#$ COORDS` was already effectively correct in outcome (empty, since the builder never
  introduces discontinuous segments) but its 1-line "count = 0" wasn't being written; now it is,
  matching the vendor doc's requirement that the count line always be present.
Assumption made (decide-and-proceed, reversible/cosmetic): reused the real samples' exact
numeric `#$ UNITS` conversion constants and unit labels (confirmed to be ordinary universal
physical conversion factors — 25.4 mm/in, 4.448 N/lbf, etc. — and standard engineering unit
abbreviations, not company-specific), but replaced the company-specific `CCVNAME` value
"AIBEL (mm)" with the generic "Metric (mm)". **Next step**: if the user can confirm CAESAR II's
exact standard metric preset name (see the open question below), swap that in instead.

## Open: what is CAESAR II's exact "standard metric" unit-system preset name? (2026-08-24)
User's answer to the unit-system question: "I think Caesar has a metric default, not sure what
it's called atm." `NeutralFileFixtureBuilder`'s `#$ UNITS` block currently uses the generic label
"Metric (mm)" as a placeholder (see the entry above) — the numeric conversion constants underneath
it are already correct/universal regardless of the preset name. **Next step**: if/when the user
can check their own CAESAR II installation's unit-system configuration (or a fresh, non-project
CAESAR II model) and confirm the exact preset name CAESAR II itself uses for a standard mm-based
metric system, update `BuildUnitsLines()`'s `CCVNAME` value to match. Low priority — cosmetic,
doesn't affect whether a generated file parses correctly.

## Checked for public CAESAR II sample files online: none found downloadable (2026-08-24)
Per direct suggestion ("check if there are available c2 files online that may be used... public so
shouldn't be an issue") — searched for downloadable public `.c2`/`.cii` sample/tutorial files
before asking the user to export one themselves. No direct downloadable sample-file repository or
archive turned up (checked vendor/training sites, forums, GitHub). **Better lead found instead**:
CAESAR II's own installer — including the free trial/demo — ships with Hexagon's own official
tutorial/example jobs (referenced across their training materials), which the user likely already
has locally without downloading anything new. Exporting one of those through `iecho.exe` would
give a genuinely official, Hexagon-authored, explicitly-for-training seed file — a cleaner
provenance than an unverified forum attachment would have been anyway. **Next step**: ask the
user to check their own CAESAR II install for its bundled tutorial job(s) as the seed source,
rather than building one from scratch, before falling back to a from-scratch throwaway model.

## Future Python neutral-file-generator programs: reference-only, not committed (2026-08-24)
The user said they have Python programs that correctly create neutral files, may share them for
context, and confirmed upfront they should not be included in the repo — same treatment already
established for `iecho.py`/`lift_case_builder.py`. Logged here so the pattern is applied
automatically whenever those are shared: read/understand for context, do not copy logic
verbatim, do not commit the files themselves.

## Fixed: ELEMENTS color/visibility line format — confirmed root cause of iecho.exe's "line # 79" rejection (2026-08-26)
Direct follow-up to the "line # 79" error on `fixtures/loop-50m-3d.cii`'s ELEMENTS section
(screenshot attached to the PR). Byte-diffed the file against `fixtures/real-samples/*.cii` at the
exact failing line and found `NeutralFileFixtureBuilder`'s element record writer wrote the "line
color, line visibility" field (item 3 in `NeutralFile-v15.pdf`'s element-name-block list) as
real/scientific-notation values (`0.000000E+00-1.000000E+00`) via `FixedWidth.FormatRealLines`,
while **every element in all 3 real samples** (49 elements checked) writes this field as plain
13-char-wide integers instead: `             -1           -1`, byte-identical every time. This
contradicts `NeutralFile-v15.pdf`'s own stated format for that field ("(2X, 6G13.6)" — real-value
format) — a case of the vendor doc and the real files actively disagreeing, not just the doc being
silent. Per CLAUDE.md's "the PDFs can't [drift], but a paraphrase can" — extended here to also mean
the real files, being CAESAR II's own actual output, are the higher-trust source when the two
disagree. Fixed `NeutralFileFixtureBuilder.BuildElementLines` to use
`FixedWidth.FormatIntLines([-1, -1])`; regenerated `fixtures/loop-50m-3d.cii`,
`fixtures/straight-run.cii`, `fixtures/run-with-riser.cii` with the fix. Added
`ElementSectionFormatTests` (byte-for-byte assertions against both Conduit's own output and all 3
real samples) so this can't silently regress. Documented in the new
`docs/neutral-file/WALKTHROUGH.md` (the "own folder of instructions" the user asked for) as a
named gotcha. **Not yet confirmed against a fresh `iecho.exe` run** — this was the only structural
difference found between the rejected file and the real samples at every level checked (VERSION
line count, WIND/UNITS/COORDS presence, ELEMENTS record byte layout field-by-field), so confidence
is high, but only an actual `iecho.exe` retest closes this out. **Next step**: ask the user to
retest `fixtures/loop-50m-3d.cii` against `iecho.exe` and report the result.

## Resolved: SpanLimitCalculator's unit-blindness — mm/metric is now Conduit's default, per direct instruction (2026-08-26)
Direct answer to the blocking question logged above ("SpanLimitCalculator's unit-blindness"): "I
would also like you to use mm as the default... the conduit calculations should consider metric
formulations and convert to this for all computations if there are different inputs. I would like
the output to include units." Implemented:
- New `UnitsSection` (`src/Conduit.Core/NeutralFiles/UnitsSection.cs`), parsed from `#$ UNITS`'s
  first conversion constant (CNVLEN — confirmed `25.4` in all 3 real samples, meaning "native
  length units per inch"). `LengthToMillimetres = 25.4 / CNVLEN`; a missing/unparseable `#$ UNITS`
  block defaults to metric (`UnitsSection.Metric`), per direct instruction — mm is the default,
  not a fallback-of-last-resort. Wired into `NeutralFile.Units`, populated by both
  `NeutralFileReader` and `NeutralFileFixtureBuilder`.
- `SpanLimitCalculator` now always computes and returns span in millimetres. For a metric file, it
  computes directly in mm/N/MPa/kg. For a (still-supported, in case a real English-unit file shows
  up later) English file, it converts geometry/stress/density to metric first, then computes —
  matching "convert to this for all computations if there are different inputs" literally. Density
  handling required care: a metric file's pipe/insulation/fluid density fields are *mass* density
  (kg/m³, confirmed against `#$ UNITS`'s CNVPDN=27680 constant, which numerically round-trips
  exactly through g=9.80665 m/s² to the equivalent English weight-density constant), so they need
  an explicit gravity conversion to weight-density that an English file's lbf/in³ fields don't.
  New default constants: `DefaultAllowableBendingStressMpa`/`DefaultSteelDensityKgPerM3` (derived
  from the existing psi/lb-in³ defaults via the same conversion factors, not independently chosen).
- `SupportPlacer`, `MockStressSolver`, and `OptimizationLoop`'s span-report messages now all work
  in millimetres and print " mm" after every span/distance value — directly addressing "I do not
  know what span ... would exceed ... at the node... I am fairly sure the system should manage
  more than 300 mm".
- Switched `NeutralFileFixtureBuilder.Schedule40Run`/`Schedule40Riser` (and every test/fixture that
  calls them) from inch-scale geometry (OD 6.625 in) to the real metric equivalent (OD 168.3 mm) —
  Conduit's own test fixtures should reflect its own new default, not the old assumption.
  Regenerated `fixtures/straight-run.cii`/`run-with-riser.cii` accordingly (physically identical
  pipe, just expressed in mm and with metric UNITS labels, which they already had).
- Verified against `fixtures/real-samples/TESTv15.cii`: before this fix, `conduit optimize`
  reported nonsense ("10834.11 > 12.60", failing after 5 iterations); after, it reports
  "10834.11 mm > 7035.44 mm" and passes in 2 — a physically sane ~7 m allowable span for that
  pipe, not a ~13 mm one. This is the exact symptom the user reported.
37/37 pre-existing tests updated and passing, 9 new tests added (46/46 total), `dotnet build`
clean.

## Fixed: WIND section unconditionally populated — corrects an earlier wrong assumption (2026-08-26)
Direct follow-up to the user's `iecho.exe` retest: the ELEMENTS fix above was confirmed correct
(no more "ELEMENT section" error), but a *new* error appeared further along — "Error processing
OFFSETS section" (line # 287 for the `optimize`d `out.cii`, line # 215 for the raw
`loop-50m-3d.cii` — different absolute line, same section, on both). Byte-diffed the WIND→OFFSETS
transition against `fixtures/real-samples/*.cii` and found: **`TESTv15.cii` and
`TESTv15_slugged.cii` both have a completely empty `#$ WIND` (header only, `NumWindLoads = 0`)** —
directly contradicting this project's own earlier claim (2026-08-24 entry, "Fixed:
NeutralFileFixtureBuilder's VERSION/WIND/UNITS/COORDS sections were structurally wrong") that
`#$ WIND` "is never truly empty" and "always carries a default row." That claim was made from
checking real samples that all happened to have a wind load applied (`44002.cii` does: 1 data
line, `NumWindLoads = 1`) — a sampling error, not a doc/real-file disagreement this time.
`NeutralFileFixtureBuilder.BuildWindLines()` unconditionally wrote that 1-line default row while
`Control.NumWindLoads` stayed hardcoded at `0` — a section-content-vs-count-field mismatch that
desyncs `iecho.exe`'s fixed-record reader: told to skip 0 WIND lines, it instead lands mid-way
through Conduit's phantom data line, and every subsequent read is off by one field until it
surfaces as a parse error at whatever section it happens to land on (here, `#$ OFFSETS`, several
sections later) — not at `#$ WIND` itself, which is exactly why this wasn't obvious from the error
message alone. Fixed: `#$ WIND` is now empty by default (matching `NumWindLoads = 0`, since none
of Conduit's synthetic fixtures model wind loads); removed `BuildWindLines()`. Regenerated
`fixtures/loop-50m-3d.cii`/`straight-run.cii`/`run-with-riser.cii` again. Added
`SectionCountConsistencyTests`, checking every count-gated section's line count against its own
`#$ CONTROL` field for both the real samples and Conduit's own fixture output, so this exact class
of bug (not just this one field) can't regress silently. Corrected the now-wrong "WIND is always
populated" claims in SPEC.md and `docs/neutral-file/WALKTHROUGH.md`. 50/50 tests passing (4 new).
**Next step**: get the user's `iecho.exe` retest result against the regenerated
`fixtures/loop-50m-3d.cii`.
