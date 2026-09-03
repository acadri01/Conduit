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

## Fixed: MISCEL_1 section missing its unconditional trailing block (2026-08-26)
Direct follow-up to the user's second retest, this time attempting the `.C2` conversion directly:
confirmed the `#$ WIND` fix (no more OFFSETS error), but a new one appeared — "Error processing
MISCEL_1 section, line # 295" (`out.cii`) / "# 223" (`loop-50m-3d.cii`). Checked
`NeutralFile-v15.pdf`'s `#$ MISCEL_1` description in full this time (not just the RRMAT part
covered by the existing material-ID parser): the section contains RRMAT (material IDs) *plus*
nozzle data, hanger data, and execution options — and unlike every other section checked so far,
**the hanger-table-defaults and execution-options portion is present unconditionally, not gated
by any `#$ CONTROL` count** (confirmed: `TESTv15.cii`/`TESTv15_slugged.cii`/`44002.cii` all have
zero hangers and zero nozzles, yet all three still carry this trailing 4-line block after RRMAT).
`NeutralFileFixtureBuilder` was only ever writing the RRMAT array — the same "content missing
where the reader expects it unconditionally" failure mode as the `#$ WIND` bug, just without an
actual count field to have mismatched.

Fixed: `BuildMiscel1Lines` now appends the exact trailing block, confirmed byte-identical between
`TESTv15.cii` and `TESTv15_slugged.cii`. **Open, low-priority question**: `44002.cii`'s trailing
block differs slightly in a few fields (e.g. `4.001740E+00`/`2.159830E+01` vs. `3.999999E+00`/
`2.159827E+01`, and a few 0/1 int flags) — all three samples have zero hangers/nozzles, so this
isn't obviously tied to actual hanger/nozzle data; more likely an installation/config-level default
(similar to `#$ UNITS`'s per-install `CCVNAME`) than a universal constant, but unconfirmed. Not
blocking — reusing the 2-of-3-agreeing values is a reasonable default either way, and any
discrepancy here is a values question, not a structural one. **Next step if it ever matters**: ask
the user whether they know what these hanger-table-default/execution-option fields correspond to
in CAESAR II's install-level settings (Configuration Editor's "Database Definitions"/hanger-related
tabs are the likely place), and whether it's worth exposing as a `caesar.cfg`-driven setting the
way `DEFAULT_CODE` already is — otherwise leave as-is indefinitely, since it doesn't block anything.
Added `Miscel1FormatTests` guarding the trailing block's exact byte layout. Regenerated
`fixtures/loop-50m-3d.cii`/`straight-run.cii`/`run-with-riser.cii` again. 51/51 tests passing (1
new). **Next step**: get the user's retest result against the regenerated
`fixtures/loop-50m-3d.cii`.

## Milestone: iecho.exe conversion confirmed working; loop geometry corrected to a real expansion loop, with bends (2026-08-26)
User's third retest: the `.C2` conversion **now works** — `fixtures/loop-50m-3d.cii` is the first
Conduit-generated neutral file confirmed to convert successfully on a real CAESAR II install. All
three structural bugs found across this back-and-forth (ELEMENTS color/visibility, WIND, MISCEL_1)
are confirmed fixed together. Per direct instruction, brought `docs/neutral-file/WALKTHROUGH.md`
fully up to date as the confirmed-correct reference (added a status note plus the new `#$ BEND`
section below).

Also per direct instruction, a substantive correction to the loop's geometry: the original shape
(two straight 25 m legs joined by an open up-out-down-in zigzag with no return path) wasn't
actually a piping "expansion loop" — a real one is a closed U/camelback shape that returns to (near)
the main line, adding flexibility via bends, not an open detour. Two reference sketches were
provided (a 2D "goal-post" loop with 4 bends between two anchors, and an isometric 3D version).
Rebuilt `fixtures/loop-50m-3d.cii` to match that topology: horizontal approach (24 m) → bend → riser
up (2 m, Y) → bend → top segment (2 m X, 2 m Z — the 3D component, "elements rising in the plane of
view" per the isometric sketch) → bend → riser down (2 m, Y) → bend → horizontal departure (24 m).
Total X span across all 5 legs is exactly 50 m, per "the total length should be 50 metres, the loop
must fit within."

**Added `#$ BEND` support to `NeutralFileFixtureBuilder`** (new — Conduit had never written this
section before). Researched via `NeutralFile-v15.pdf` plus `44002.cii`'s 13 real bends (the only
real sample with any):
- Format: 3 lines / 14 values per bend (13 documented items + an always-zero 14th, "Overlay
  Thickness"). The corner element (whose `ToNode` is the bend) gets a 1-based pointer to its
  `#$ BEND` record in `AuxiliaryPointers[0]`.
- **The bend record's "node position #1/#2" are *not* the corner node** — they're CAESAR II's own
  auto-generated near/far tangent-point node numbers. Confirmed: in `44002.cii`, these never appear
  as a real `FromNode`/`ToNode` anywhere in `#$ ELEMENTS`, and are consistently (corner − 1,
  corner − 2) for that file's numbering. Reused that exact convention.
- **Radius (381 mm), "angle to node position #1" (-2.0202), and fitting thickness (4.191 mm) are
  confirmed constant across all 13 of that file's bends**, despite differing bend orientations (some
  turn X→Y, others X→Z) — since radius and turn angle (all 90°) are also constant across them, this
  is consistent with "angle to node position" being a function of (radius, turn angle) rather than
  compass direction, but the exact formula isn't confirmed. Reused the real values verbatim (same
  treatment as `#$ UNITS`'s constants and `#$ MISCEL_1`'s trailing block) rather than deriving a
  formula from one data point. **Next step if it ever matters**: if a future test file needs a
  non-90° bend or a different radius, this "angle to node position" value would need to actually be
  derived (or a second real sample with varying bend geometry would need to be found) rather than
  reused as a constant — flag this if it comes up rather than guessing further.

Added `BendFormatTests` (pointer wiring, record byte layout, `NumBends` consistency, and the
no-bends case). 55/55 tests passing (4 new), `dotnet build`/`test` clean. Ran the CLI against the
new geometry — `optimize` correctly reports the two 24 m straight legs as failures (no intermediate
node to place a support at, same known v1 limitation as before — not a new bug).

## Milestone confirmed again; loop geometry corrected a second time; element-splitting implemented (2026-08-26)
User's fourth retest: the `.C2` conversion **worked again** (confirming the 6-bend geometry from
the last round was structurally sound), but flagged the *shape* was still wrong — the 3D jog had
been collapsed into a single diagonal element instead of two separate legs. Gave the exact element
sequence to use: `+DX` (long), `+DY`, `-DZ`, `+DX`, `+DZ` (opposite of the `-DZ` leg), `-DY`
(opposite of the `+DY` leg), `+DX` (long, to complete) — 7 elements, 6 bends. Rebuilt
`fixtures/loop-50m-3d.cii` to match exactly (same 2 m loop dimensions, same 50 m total X span).

**Also per direct instruction: implemented element-splitting**, closing a long-standing documented
v1 limitation ("Conduit never splits an element to introduce a new node mid-span"). The user's
own worked example: a 25550 mm span against a 6446.76 mm max allowable span — round the max span
down to the nearest 1000 mm (6000 mm), divide with remainder (4×6000 + 1550), one restraint per
new interior boundary (4 restraints). Implemented as:
- `ElementSplitter` (`src/Conduit.Core/Heuristics/`) — pure chunking math, no file I/O. Unit
  tested directly against the user's own worked example, plus the exact-multiple case, the
  already-fits no-op case, and the "max span rounds down to 0" edge case (nothing meaningful to
  split into — stays a genuine failure).
- `NeutralFile.ReplaceElement` — the actual production element-mutation mechanism (new capability;
  previously Element.cs's doc comment claimed "Conduit never adds or modifies pipe elements").
  Splices the new element records into `#$ ELEMENTS` at the correct raw-line position, **and** into
  `#$ MISCEL_1`'s positional `RRMAT` array — without this second part, the element count and
  RRMAT's line count would desync exactly the way `#$ WIND`/`#$ MISCEL_1`'s trailing block did in
  earlier rounds, so this was caught and fixed proactively rather than needing another `iecho.exe`
  round-trip to discover it.
- `Element.ToRawLines()` — the inverse of `Element.ParseMany`'s per-element format, now shared by
  both `NeutralFile.ReplaceElement` (production) and `NeutralFileFixtureBuilder.BuildElementLines`
  (test fixtures), so the two can never format-drift apart the way the color/visibility line once
  did. `BuildElementLines` was refactored to call it instead of duplicating the format logic.
- Wired into `OptimizationLoop.Adjust` as the fallback when `TryPickMidpointNode` finds no
  existing node (i.e. the segment is a single element) — the loop already iterates and re-checks,
  so a resolved split converges to PASS on the very next iteration.

**Bug caught before shipping** (not from a real-world test this time — caught by re-checking the
CLI's own output before regenerating fixtures): the first version of `ElementSplitter` copied the
original element's full `AuxiliaryPointers` array to *every* new chunk, including the bend pointer
— so all 3 interior chunks of a split element whose original `ToNode` was a bend corner falsely
claimed to be that same bend, not just the 1 chunk that actually still ends there. Fixed: the bend
pointer (index 0) is now zeroed on every chunk except the final one. Added
`BendPointer_OnlySurvivesOnTheFinalChunk_NotEveryInteriorOne` to guard it. Logged here rather than
silently fixed, since it's a good illustration of why "verify against the real build before
committing" (CLAUDE.md's TESTING.md rule) matters even for changes that never touch documented
neutral-file format gotchas — this one was a plain logic bug, not a format one.

Verified against the corrected loop file: both 24 m legs now split into 4×6000 mm elements with 3
interior rest supports each (matching the user's own "4 elements and a restraint between each"
description exactly), and `conduit optimize` passes in 2 iterations instead of failing after 5.

**Also per direct instruction**: `TESTING.md` now has a "Test this now" section, rewritten every
round with the exact current ask rather than that living only in PR comments — see CLAUDE.md's
new bullet on keeping it dynamic, and TESTING.md's new top section.

62/62 tests passing (7 new: `ElementSplitterTests` × 6, plus one more `OptimizationLoopTests`
case for the genuinely-irreducible failure), `dotnet build`/`test` clean. **Next step**: get the
user's `iecho.exe` retest result against the regenerated `fixtures/loop-50m-3d.cii` (see
TESTING.md's "Test this now").

## Bend radius defaults to "Long"; minimum chunk length near a bend, per direct instruction (2026-08-26)
A proactive follow-up (not a bug report — the user flagged this ahead of hitting it in practice):
"bends have required minimum lengths depending on the pipe size... an element break should never
cause an element with a bend to be shorter than this length. There should also be a buffer of
500 mm between the bend weld at that length and the restraint placement to accommodate shoe
lengths," plus a screenshot of CAESAR II's bend-radius dropdown (Short/Long/3D/5D), confirming
"Long" should be Conduit's default.

- **Confirmed via `NeutralFile-v15.pdf`**: `#$ BEND`'s "Bend radius" item is a single numeric
  field — there's no separate "type" field for Short/Long/3D/5D anywhere in the neutral file, so
  that dropdown is purely a CAESAR II input-UI convenience that resolves to a plain radius number
  on write. Nothing else to look for; just compute the right number.
- **"Long radius" is 1.5x the pipe's diameter** (ASME B16.9's standard long-radius elbow
  definition, using nominal pipe size). Conduit has no NPS lookup table, so this is approximated
  from the element's actual outside diameter instead (off by only a percent or two for standard
  schedules) — `NeutralFileFixtureBuilder.BuildBendLines` now computes radius per-bend from its
  own element's OD, replacing the flat 381 mm reused from `44002.cii` in the previous round.
  `44002.cii`'s other two constant values ("angle to node position #1" = -2.0202, fitting
  thickness = 4.191 mm) are still reused verbatim — they were confirmed constant across that
  file's 13 bends, but all 13 shared one radius (381 mm); whether they still hold at a different
  radius is unconfirmed. **Next step if it ever matters**: no action needed unless an `iecho.exe`
  test surfaces a problem specifically tied to these two fields — nothing to proactively fix
  without more real data at a different radius.
- **Minimum chunk length near a bend, implemented in `ElementSplitter`**: when the element being
  split has a bend at its own `ToNode`, the final chunk (the one that still ends there) is never
  left shorter than the bend's tangent length (radius x tan(45°) = radius, for the 90° bends
  Conduit only ever produces) plus a 500 mm shoe-clearance buffer — a remainder chunk that would
  be too short gets merged into the previous chunk instead of standing alone. Unit tested directly
  (a 500 mm remainder next to a 168.3 mm-OD pipe's bend — minimum 752.45 mm — gets absorbed into
  the previous 6000 mm chunk; the same 500 mm remainder next to a *non*-bend node is left alone,
  confirming the merge is specifically bend-gated).
- **Known gap, logged rather than guessed at**: this only covers a bend at the split element's own
  `ToNode`. A bend at the element's `FromNode` (i.e. the *preceding* element's own corner) isn't
  visible from a single `Element` and isn't handled — `OptimizationLoop.Adjust` only ever passes
  `ElementSplitter` the one element being split, not its neighbors. Our own loop fixture's two
  splits happen to be safe either way (both start comfortably far from their nearest bend — legs
  are 24 m, minimum clearance is well under 1 m), so this hasn't been exercised in practice yet.
  **Next step if it ever matters**: thread the preceding element's bend status into
  `OptimizationLoop.TrySplit`'s call to `ElementSplitter.Split` (it already has `file.Elements` in
  scope to look the neighbor up) and apply the same minimum to the *first* chunk too.

64/64 tests passing (2 new), `dotnet build`/`test` clean. Regenerated `fixtures/loop-50m-3d.cii`
with the new Long-radius bends (252.45 mm for the 168.3 mm-OD pipe, was 381 mm) — structurally
unaffected otherwise, verified `conduit optimize` still passes in 2 iterations. **Next step**: get
the user's `iecho.exe` retest result (see TESTING.md's "Test this now").

## Fixed: restraints never actually applied — root cause was a missing pointer (2026-08-27)
User's fifth retest reported the splitting and geometry both work, but after converting the
neutral file, **no restraints existed in the CAESAR input file at all**, and correctly guessed why:
"I therefore suspect that the elements which are supposed to have the restraints do not correctly
point to them, or that the restraints are not correctly formatted. Check for a pointer first."

Confirmed against `NeutralFile-v15.pdf` and `fixtures/real-samples/44002.cii`: exactly right.
`NeutralFile.AddRestraint` wrote a well-formed `#$ RESTRANT` record but never set the owning
element's 4th auxiliary pointer — the actual mechanism CAESAR II uses to associate a restraint
record with a node (same convention as the bend pointer at index 0, confirmed earlier this
project). An unreferenced restraint record is invisible to CAESAR II/`iecho.exe` even though it
parses fine on its own. Fixed — see `docs/neutral-file/WALKTHROUGH.md`'s `#$ RESTRANT` section and
SPEC.md's 2026-08-27 update for the full owner-selection convention (`ToNode`-preferred,
`FromNode`-fallback, collision-avoidance) and `ElementSplitter.Split`'s matching pointer-
preservation logic.

A second, independent bug found in the same pass while cross-checking real restraint bytes: every
restraint's `Stiffness` field was left at its type default of `0` — a spring with zero resistance,
not a rigid support, so even a *correctly pointed-to* restraint wouldn't have actually restrained
anything. CAESAR II's real rigid-restraint constant (`1e12 lbf/in`, confirmed byte-exact against
`44002.cii` once converted through `#$ UNITS`' CNVTSF constant) is now used for every restraint
Conduit creates.

**Open question, deliberately not guessed at (per CLAUDE.md's support-placement-logic consultation
rule — this borders on it)**: `GUI` restraint direction cosine. Every axis-implied restraint type
(`X`/`Y`/`Z` and their `+`/`-`/snubber/rod/"2" variants) matches its restrained axis in every real
sample checked, and `ANC`'s is confirmed `(0,0,0)` — but the one real `GUI` example available
(`44002.cii`) is on a vertical (Y-axis) run with direction cosine `(1,0,0)`, not matching the run's
own axis. That's not enough data to tell whether `(1,0,0)` is the general rule for a plain full
guide, or specific to some "directional guide" variant CAESAR II also supports. Left at `(0,0,0)`
(the same as `ANC`'s confirmed default) rather than guessed. **Next step**: if/when a second real
`GUI` example becomes available (ideally on a non-Y-axis run), compare direction cosines directly;
otherwise this needs a direct answer from the user about what CAESAR II's UI actually does for a
plain guide before `Restraint.CreateSingleDof` can set it with confidence.

**Known residual gap, logged rather than fixed** (assessed as very unlikely to matter given
Conduit's actual placement patterns): `NeutralFile.AddRestraint`'s owner-selection fallback still
has a theoretical failure mode if a *third* restraint converges on the same one-or-two-element
neighborhood (both its `ToNode` and `FromNode` candidates already claimed by other restraints) — it
would silently overwrite the `ToNode` match rather than erroring. Verified this doesn't occur in
the actual `loop-50m-3d.cii` optimize run (11 restraints, all correctly and distinctly wired) but
isn't proven impossible in general.

79/79 tests passing (15 new — `RestraintFormatTests` plus new `ElementSplitterTests`/
`NeutralFileRoundTripTests` cases), `dotnet build`/`test` clean. Regenerated
`fixtures/loop-50m-3d.cii`; `conduit optimize` output is unchanged (same console output the user
originally reported), but the restraint records are now correctly wired and rigid. **Next step**:
get the user's `iecho.exe` retest result confirming restraints now actually appear in the converted
CAESAR file (see TESTING.md's "Test this now"). Also still pending from the same PR comment (not
yet investigated): the user's separate bend-radius question — whether there's a proper
pointer/preset field for CAESAR's Short/Long/3D/5D bend-radius UI options, as opposed to just
writing the computed radius number (prior conclusion in this project, from `NeutralFile-v15.pdf`,
was that no such field exists — the user's comment suggests re-verifying this rather than assuming
it still holds).

## Re-verified: no bend-radius-type pointer exists in the neutral file (2026-08-27)
Per the same PR comment: "I am sure there are pointers in the bend settings that set the radii
according to the short, long, 3d and 5d settings available in the UI. You will have to check the
example files and the relevant documentation for this." Per CLAUDE.md's instruction to re-verify
against `reference/`'s primary sources rather than trust an earlier summary — did exactly that,
fresh, rather than assuming the prior conclusion (SPEC.md's 2026-08-26 entry) still held.

Re-extracted `NeutralFile-v15.pdf`'s actual text (`pdftotext -layout`, not relying on a paraphrase
or memory) and re-read its `#$ BEND` section directly: the `BND` array's item 1 is documented as
plain "Bend radius" with no unit qualifier or "type" language; item 2 ("Type") is the weld type
(single-flange/double-flange/welded), not a radius preset. A search of the entire document for
"short"/"3d"/"5d" near "radius" found nothing else relevant. Cross-checked against all 3 real
samples' actual `#$ BEND` bytes: every bend within one file shares one constant radius value
(381 mm in `44002.cii`; 533.4 mm in `TESTv15.cii`/`TESTv15_slugged.cii`) — a physical distance,
not a small integer code that would suggest an enum/pointer.

**Conclusion: unchanged from before, now backed by a fresh, direct re-check rather than a carried-
forward summary.** The neutral file format has no separate field for the Short/Long/3D/5D radius
preset — CAESAR II's UI dropdown is a convenience for computing one plain radius number, and only
that resolved number survives into `#$ BEND`. If CAESAR II's own native database (`.c2`) keeps the
dropdown's selection for later re-editing, that's internal to `.c2` and doesn't round-trip through
the interchange/neutral-file format Conduit reads and writes — out of reach regardless. No code
change needed; `NeutralFileFixtureBuilder.BuildBendLines`'s existing approach (compute "Long" =
1.5x OD, write the resolved number) is already correct per this re-verification. Replied on the PR
with this finding and the supporting evidence rather than silently re-asserting the old answer.

## BLOCKING: SupportPlacer places supports directly on bend corners (2026-08-27)
User's sixth retest, with a screenshot: restraints now correctly show up (fix confirmed working),
but flagged a real, more fundamental bug, plus asked to pause and realign on the overall plan
before any more support-placement work — both handled here per CLAUDE.md's rule that
support-placement logic is defined one type at a time with direct consultation, and per the
explicit request: "You may ask any questions you have here before proceeding. I would also like us
to rediscuss the vision... to make sure we are aligned."

**Confirmed bug (not waiting on discussion — this part is a plain fact, not a judgment call)**: in
the `loop-50m-3d.cii` run the user attached, **all three** of `SupportPlacer`'s initial placements
(nodes 20, 50, 70 — every single one) landed exactly on a bend corner node. Root cause read
directly from the code: `SupportPlacer.PlaceSupportsForRun` picks `element.FromNode` the moment
accumulated span would exceed the max allowable span, with **zero awareness of whether that node
carries a bend pointer** — `OptimizationLoop.TrySplit`'s interior-node placement has the same gap.
Per the user: a support can't physically sit on a bend corner without a trunnion fitting (out of
scope for now, per direct instruction — "let's leave that type of support for now").

**Why this wasn't caught already**: `ElementSplitter` already has bend-awareness (the
minimum-chunk-length-near-a-bend rule from the fifth round), but that logic only fires *when
splitting an element* — it was never applied to `SupportPlacer`'s ordinary node-selection walk,
which is a separate code path with no bend-corner check at all.

**Also flagged, needs direct answers before implementing (genuine design questions, not just a bug fix)**:
1. **GUI's direction cosine, revisited**: "It is also possible to determine the required direction
   of support by setting the perpendicular unit vector of the pipe." This bears directly on the
   open `GUI` direction-cosine question logged two rounds ago (currently left at `(0,0,0)`, which
   defaults to CAESAR's "all-round guide" behavior — restrains both directions perpendicular to
   the run, which the user's comment confirms is what's happening here on the vertical segment).
   **Question**: for a given run axis, there are two independent perpendicular directions (e.g. a
   horizontal X-run has both Y and Z available) — how should Conduit choose which one to set as
   the guide's direction cosine? Is it always "the axis the adjacent bend turns into" (i.e. derived
   from the bend geometry immediately before/after the guide), or something else?
2. **Minimum clearance from a bend**: "placing a guide with only one bend between it and a long
   piping section will not work due to the stresses." Is this a minimum *straight-line distance*
   from any bend (like the existing shoe-clearance buffer in `ElementSplitter`, just applied to
   `SupportPlacer` too), a minimum *number of bends* of separation, or something else? A concrete
   number/rule to encode would help.
3. **Loop-specific placement rule**: "The rest on the loop should be centred on the dx segment of
   the bend. It is not possible to subsequently have a rest at the next bend." Read literally:
   within a short chain of bends (like the loop's up/out/down/in jog), only the loop's one
   dominant/long straight segment (the "dx segment") should ever be a rest candidate — none of the
   short legs between two consecutive bends should get their own support. **Question**: is that
   the intended general rule (i.e. "never place a rest on a segment that's short relative to the
   run, or bounded by bends on both ends"), or specific to this loop's particular geometry?
4. **Vision realignment**: the user asked to step back and confirm current understanding and next
   priorities before continuing implementation. See PROGRESS.md's matching entry for a state-of-
   the-project recap posted on the PR — no code response needed here beyond logging that the
   conversation is happening there.

**Next step once answered**: implement the confirmed part first (exclude bend-corner nodes as
`SupportPlacer`/`OptimizationLoop.TrySplit` candidates entirely — straightforward, no ambiguity),
then encode whatever specific clearance/centering/direction-cosine rules come out of the
discussion into `SupportTypeClassifier`/`SupportPlacer`/`RestraintTypeMapper` as appropriate, with
tests against the exact `loop-50m-3d.cii` scenario that surfaced this. **No support-placement code
changes will be pushed until this is resolved** — per CLAUDE.md's reservation of this class of
decision, and the user's own direct request to discuss first this round.

## Answers received: deterministic loop-placement rule (2026-08-27) — restated, not yet implemented
The user answered all three questions from the entry above, in detail, and explicitly asked for a
restatement to confirm before implementing ("You may give a summary of your understanding of this
for me to confirm"). Per that request and CLAUDE.md's consult-before-implementing rule, **nothing
below is implemented yet** — this is the restatement, logged here as the record, with a couple of
narrow follow-up questions where the given rules don't fully specify an edge case.

**1. GUI's direction cosine — CAESAR's own behavior, not a Conduit choice, for the ordinary case.**
With `Izup=0` (Y vertical): a `GUI` restraint on a *horizontal* run auto-resolves to a single
restraint perpendicular to the pipe's own axis when its direction cosine is left `(0,0,0)` — this
is CAESAR's own resolution, confirmed working as-is, nothing to change. A `GUI` on a *vertical* run
with `(0,0,0)` becomes an "all-round guide" — CAESAR restrains **both** horizontal directions (X
and Z) automatically. That's the actual problem: on a vertical segment near a bend into a long
run with limited flexibility (as in this loop), an all-round guide restrains the very direction the
loop exists to let the pipe expand into, driving up bend stress — "this is a poor design." The fix
in this specific case: restrain **only** the transverse direction (Z here), leaving the run's own
expansion axis (X here) free — which requires an explicit, non-zero direction cosine rather than
relying on the all-round default. Understood as: **Conduit doesn't need general logic to pick a
guide's perpendicular direction for the ordinary case** (CAESAR already does that correctly) — the
only place an explicit direction cosine is needed is where the default all-round behavior on a
vertical segment would over-restrain a designed expansion path, which (per point 3 below) is
exactly the case a loop-placement rule replaces anyway, since no guide ends up on the loop's
vertical/transverse legs at all under the new rule.

**2. No fixed minimum-clearance constant — this is inherently a stress question, not a geometry
one.** Confirmed: there's no set distance. The underlying principle is thermal expansion: the
closer a restraint sits to a segment that's expanding in the restrained direction, the higher the
stress in the pipe between the restraint and that expansion. Getting this precisely right needs
real stress analysis (`CaesarComStressSolver`, still a skeleton) — "restraining a pipe in the
direction of its expansion is poor design," full stop, rather than a distance-threshold problem.
For MVP purposes, the loop rule in point 3 is the concrete, implementable stand-in: it avoids the
close-to-a-bend problem structurally (by choosing where a support *can* go) rather than by
measuring a clearance distance.

**3. Deterministic loop-detection and placement rule — the concrete algorithm, restated in full:**
- **Loop detection**: a short bend-connected chain of segments where **two of the three axes each
  appear exactly twice with opposite sign** (a there-and-back pair — e.g. `+DY` then later `-DY`;
  `-DZ` then later `+DZ`), and the **third axis has one segment matching the direction of the long
  run either side of the loop** (e.g. `+DX`, same direction as the run's own long legs before and
  after). That third segment is the loop's "extending" segment.
  - Worked against `loop-50m-3d.cii`'s actual geometry to confirm the reading: segments (in order)
    are `+DX` (long), `+DY`, `-DZ`, `+DX`, `+DZ`, `-DY`, `+DX` (long). Y appears at `+DY`/`-DY`
    (symmetric pair), Z appears at `-DZ`/`+DZ` (symmetric pair), and the middle `+DX` segment
    matches the two long legs' own direction — exactly the pattern described. This matches "any of
    these combinations where there is symmetry in two of the dimensions."
- **Placement trigger**: if the loop's transverse segment length (the `DZ` legs in the 3D case,
  evaluated presumably via the same `SpanLimitCalculator.ComputeMaxSpan` already used everywhere
  else) exceeds the max allowable span for that segment, **the loop's extending segment (`DX`
  here) gets a single rest, centered** — "at the centre of the extending segment." No support goes
  on the rise (`DY`) or transverse (`DZ`) legs themselves, and if the transverse leg does *not*
  exceed max span, the loop gets no support at all.
- **2D case, same notion**: a loop entirely in the horizontal plane (e.g. transverse in `DZ`,
  extend in `DX`, transverse back in `DZ` — only one axis pair symmetric, no rise) follows the same
  rule: a rest at the center of the extending segment if the transverse leg exceeds max span.
- **Sanity check against the actual fixture**: `loop-50m-3d.cii`'s transverse (`DZ`) legs are
  2000 mm each — well under the 6446.76 mm max allowable span the CLI has been computing for this
  pipe. Under this rule, **the loop itself would get zero supports** — the bug-triggering
  placements at nodes 20/50/70 wouldn't happen at all once bend-corner exclusion and this
  loop-awareness are both in place, since none of them were actually load-bearing decisions to
  begin with once the loop is recognized as one unit. (The two long `+DX` legs either side still
  get their own ordinary span-driven rests/splits, unaffected by this rule.)

**Narrow follow-up questions** (the rest is confirmed enough to implement against):
- Does "symmetry" require the two paired segments' lengths to match exactly (e.g. both `DY` legs
  exactly 2000 mm), or just same-axis-opposite-sign regardless of magnitude? Read as the latter
  (topological symmetry — an out-and-back on the same axis) but flagging since "symmetry" could
  mean exact equal magnitude.
- For a genuinely standalone vertical riser that is **not** part of a detected loop (continues on
  to more pipe rather than doubling back) — should Conduit keep the current all-round default
  (`GUI` at `(0,0,0)`) as an interim placeholder, since a general (non-loop) direction-cosine
  heuristic is explicitly future work per point 1 above? Assumed yes unless told otherwise.
- Confirming the transverse-leg-vs-max-span comparison uses the same `SpanLimitCalculator` already
  used for ordinary span-driven placement (no new formula) — read as yes.

**Also noted (per direct instruction) for later, beyond MVP**: the user explicitly deferred the
*general* "heuristic for placing loops and determining the type of loop" (arbitrary loop shapes,
not just the symmetric 2D/3D case above) as future work beyond MVP scope, while asking that
whatever's derivable now be recorded so it doesn't need re-deriving later. This entry is that
record for the symmetric-loop case; the general case (asymmetric loops, other loop topologies,
automatically deciding *where* to route a loop rather than just how to support an existing one)
remains open and unscoped.

**Next step**: post the restatement above on the PR for confirmation (already drafted to mirror
this entry) alongside the narrow follow-ups. Once confirmed, implement in this order: (1) bend-
corner exclusion in `SupportPlacer`/`OptimizationLoop.TrySplit` (already-agreed, no ambiguity —
see the entry above), (2) loop detection as a new step in `SupportPlacer` that identifies a
symmetric-axis-pair segment chain and treats it as one unit rather than walking its individual
elements for span purposes, (3) the centered-rest-on-transverse-trigger rule from point 3, with a
new node introduced at the extending segment's midpoint via `ElementSplitter` if it doesn't already
have one there. Test directly against `loop-50m-3d.cii`: expect zero supports inside the loop
itself (per the sanity check above) given its current 2000 mm transverse legs, plus a second test
fixture with a longer transverse leg (e.g. 8000 mm, above the 6446.76 mm max span) to actually
exercise the "loop gets a centered rest" branch.

## Noted for later: `iecho.exe` automation is one-directional only, not two (2026-08-27)
Per the same round: the user shared reference material from a previous, separate Python project's
`iecho.py` wrapper (their own code, shared as context, not proprietary CAESAR II material) that
clarifies a constraint relevant to `IechoConverter`'s eventual implementation (`SPEC.md`'s "Native
file adapter (iecho)", currently a skeleton with both directions assumed equally automatable):
- **`.CII` → `.C2` ("silent conversion")**: fully scriptable — blocking subprocess call, no UI,
  raises on non-zero exit/timeout/missing output file. This is the direction Conduit's own
  optimize output actually needs (turning a modified neutral file back into something CAESAR II
  can open directly), and it's the easy direction. The reference wrapper resolves `iecho.exe`'s
  path via a config-file override first, then an `IECHO_PATH` environment variable, then a
  hardcoded list of common install paths (both Intergraph CAS and Hexagon branding) — consistent
  with the discovery-logic pattern already planned for `IechoConverter` in SPEC.md.
- **`.C2` → `.CII` ("interactive export")**: **not fully scriptable** — `iecho.exe` only exposes
  this direction through its interactive UI (`launch_for_export`, non-blocking, opens the UI for a
  person to click through). The reference wrapper's workaround is a watchdog that polls for the
  expected output file to appear (or the process to exit) rather than a true headless call.
- **Implication for `IechoConverter`**: `ToNativeFile(ciiPath) -> nativePath` (Conduit's own
  optimized output back into `.C2`) can eventually be a real, fully headless implementation.
  `ToNeutralFile(nativePath) -> ciiPath` (getting a user's existing `.C2` file into `.cii` in the
  first place) cannot be fully headless with `iecho.exe` alone — it needs either the same
  UI-launch-plus-watchdog pattern (semi-automated, still requires the user to be present to click
  through the export dialog once) or a different conversion path entirely. This changes what
  "Conduit's users should never have to run `iecho` by hand" (SPEC.md's existing framing) can
  actually mean in practice: the write-back direction can be invisible; the read direction can only
  be made low-friction, not eliminated, without some other resolution. Logged here rather than
  silently assumed away — worth flagging back if/when `IechoConverter` gets implemented for real.
**Next step**: fold this into SPEC.md's "Native file adapter (iecho)" section (done, this round) —
no code change yet since `IechoConverter` is still out of MVP scope; this is purely so the
constraint isn't rediscovered from scratch when that work starts.

## Corrections to the loop-placement restatement, plus S-loops and a viewer/research request (2026-08-27)
The user's next reply corrected part of the restatement above, confirmed the rest, and added three
separate items. Recorded here in full before any implementation, per the same consult-first rule.

**Correction — transverse and extending legs are NOT immune from ordinary span rules.** My prior
restatement said "nothing goes on the rise or transverse legs" inside a loop. That's wrong: "if a
horizontal transverse section exceeds the limit it should receive a rest like all other pipes.
This goes for the extending loop segment which is parallel to the longer piping segments as well."
So every segment in a loop is still subject to the *ordinary* per-segment span check — the
loop-specific rule is narrower than I first stated:
- If the transverse leg's own length exceeds max span, but the extending segment's own length does
  **not** independently need a support → the extending segment still gets exactly one rest, and it
  must be centered ("It does not have to be in the centre if the traverse segment is longer than
  the allowable, but the extending segment is too" — implying centering is specifically the
  behavior for *this* case, not a universal rule).
- If the extending segment's own length **also** independently exceeds max span (i.e. it needs
  more than one support on its own merits) → place the (multiple) supports "symmetrical on the
  extending segment" rather than a single center point, and rather than the ordinary greedy
  left-to-right walk `SupportPlacer` uses elsewhere (which would produce an off-center remainder at
  one end) — read as: distribute evenly and symmetrically about the segment's midpoint instead.
- **Guides can legitimately appear on transverse (or other loop-internal) legs** — "I have seen
  loops with guides on the horizontal transverse sections - especially when the loops are large."
  So the loop rule is *not* "never place anything inside a loop except the one centered rest" — it
  only kicks in for the specific "transverse triggers, extending doesn't" case above; otherwise
  ordinary per-segment classification (vertical → guide, horizontal → rest, subject to the
  still-confirmed bend-corner exclusion) applies normally inside a loop too.

**Confirmed — loop taxonomy, precisely, with an added third pattern:**
- **2D loop**: 4 bends, 3 segments between them. Of the 3, two have equal magnitude and opposite
  direction (a single out-and-back pair on one axis); the straight segment before the loop and the
  one after it have collinear (same-direction) unit vectors.
- **3D loop**: 6 bends, 5 segments between them, with *two* out-and-back pairs on two different
  axes (matches `loop-50m-3d.cii` exactly, and matches my prior restatement — unchanged).
- **S-loop (new)**: also a planar loop with 6 bends and 5 segments, but *not* the same
  double-symmetric-pair structure — described as "more topological" and harder to detect
  deterministically. The user's own worked example (all in the X/Z plane): `dx=25000, dz=3000,
  dx=2000, dz=-7000, dx=3000, dz=-4000, dx=25000` — three `dz` segments (not a simple ± pair) and
  two `dx` segments both positive (continuing the outer legs' own direction, not opposing each
  other). What's confirmed as always true: the segment before and after the loop still have
  collinear unit vectors (both `dx=25000` here). **Detection algorithm not fully specified** —
  explicitly the harder case. Per the user's own framing, the *general* loop-type-detection
  heuristic (arbitrary shapes, S-loops included) is future work beyond MVP; this entry exists so
  the S-loop pattern and its one worked example aren't lost before that work starts.

**Point 2, restated — this is now a research task, not an implementation one.** "I definitely think
we should find a heuristic for placing guides/all-round guides for vertical risers with
deterministic spacing methods. You may do some research on this and get back to me on what you
find before implementing it." See the separate "Researched: vertical-riser guide spacing" entry
below for the findings — **not yet implemented**, reporting only, per the explicit instruction.

**Point 3**: confirmed correct as restated ("no fixed minimum clearance — a stress question, not a
distance one"). Nothing further needed here.

**GUI-logic test fixture**: explicitly deferred — "we can also create a file that test[s] the GUI
logic we have agreed... once we agree on a GUI placement logic." Not building this yet; it depends
on the still-open vertical-riser guide-spacing research above.

**Next step once the loop rule and guide-spacing research are both confirmed**: implement in this
order — (1) bend-corner exclusion (agreed, unambiguous), (2) loop detection (2D/3D symmetric case
only — S-loops explicitly deferred), (3) the corrected trigger/placement rule above (centered when
only the transverse leg needs it; symmetric multi-support when the extending segment also
independently needs it; ordinary per-segment classification otherwise, including guides on
loop-internal legs when warranted), (4) the vertical-riser guide-spacing multiplier once confirmed.
Test against `loop-50m-3d.cii` (current geometry: transverse legs under max span, so the loop
itself should still need zero supports) plus new fixtures that exercise the "transverse only" and
"both transverse and extending" trigger cases.

## Researched: vertical-riser guide spacing heuristic (2026-08-27) — findings only, not implemented
Per the user's explicit request to research and report back before implementing anything. Checked
`reference/`'s vendor PDFs first (per CLAUDE.md's standing instruction to consult primary sources) —
they contain nothing on this: all 5 are CAESAR II *software/UI* documentation (how to drive the
program), not piping engineering *design-practice* references, and a full-text search of all five
for "guide"/"spacing"/"riser" near each other returned zero hits. This is expected, not a gap in
the docs — support-spacing rules are an engineering-practice topic, not something a stress-analysis
program's help file would prescribe (CAESAR lets you place whatever you specify; it doesn't dictate
where).

Web search turned up a well-established, consistently-cited industry rule of thumb, corroborated
across multiple independent sources (SPED — Society of Piping Engineers and Designers; Eng-Tips;
wermac.org; hardhatengineer.com; Industrial Monitor Direct's piping knowledge base):

**Guide spacing on a vertical riser ≈ 2× the pipe's ordinary horizontal max allowable span for the
same size/schedule.** Rationale given consistently across sources: horizontal span tables limit
*bending stress* between supports under gravity load — a vertical riser has no such gravity-induced
bending moment along its own axis, so the governing concerns are different (lateral sway/buckling
resistance, not sag), and the accepted practice is to allow roughly double the horizontal interval
before requiring another guide.

A related but distinct finding, about the riser's primary *rigid* support location rather than
guide interval spacing: some sources describe locating the main rigid/anchor support at
approximately 2/3 of the total riser height (not at the base), so thermal expansion can occur in
both directions away from it rather than forcing all growth one way and risking compressive
buckling at the base. This is a different question (where the *one* rigid restraint goes on a
riser) from guide *spacing* (how far apart repeated guides go along its length) — noted since it's
adjacent and may matter once loads/anchoring are revisited, but not folded into the guide-spacing
answer above.

**Implementability**: directly usable with what Conduit already has — `SpanLimitCalculator.
ComputeMaxSpan` already computes the ordinary (horizontal) max allowable span per element; the
proposed rule is simply to apply a 2× multiplier to that value specifically when walking a vertical
segment for guide-interval purposes, rather than reusing the plain horizontal value as
`SupportPlacer` currently does (confirmed by re-reading `SupportPlacer.PlaceSupportsForRun`: it
uses the same `maxSpan` for vertical and horizontal segments today, with no distinction).

Sources:
- https://www.spedweb.com/technical-information/articles/supporting-vertical-piping
- https://www.eng-tips.com/threads/vertical-supporting-of-pipes.234556/
- https://www.eng-tips.com/threads/vertical-spacing-for-pipe-supports.216801/
- https://www.wermac.org/steel/pipesupports_spacing.html
- https://hardhatengineer.com/pipe-support-span-chart/
- https://industrialmonitordirect.com/blogs/knowledgebase/vertical-piping-support-span-design-criteria-vs-horizontal
- https://industrialmonitordirect.com/blogs/knowledgebase/vertical-pipe-riser-support-design-with-expansion-accommodation

**Next step**: reported back on the PR per the explicit instruction; awaiting go-ahead before
implementing the 2× multiplier in `SupportPlacer`/`SpanLimitCalculator`, and before building the
GUI-logic test fixture that depends on this.

## Noted: neutral-file viewer request + CAESAR dependency file list (2026-08-27)
Two new, separate requests in the same round, neither support-placement logic:

**1. A lightweight neutral-file viewer**, so the user doesn't have to open a licensed CAESAR II
install just to look at what Conduit produced/modified: "It should show all elements of the model
in the neutral file." Scope is genuinely ambiguous between a cheap option (a text/table dump of
nodes, elements, coordinates, and existing restraints — everything `NeutralFile.ComputeNodePositions`
and the parsed model already have in memory) and a much larger one (an actual 2D/3D geometric
rendering). Per CLAUDE.md's decide-and-proceed default (pick the most reversible option, log it,
keep moving): proposing to start with the cheap option — a new `conduit inspect <file.cii>` CLI
command printing a plain-text table (node positions, element connectivity/OD/material, existing
restraints with type and node) — since it's low-cost, immediately useful for exactly the "check
what Conduit changed without opening CAESAR" use case, and doesn't foreclose a later graphical
viewer built on the same data. Asked on the PR whether that's sufficient for now or whether an
actual visual/graphical rendering (e.g. a simple 2D plan/elevation SVG) is wanted as the real goal —
that's a big enough scope difference to confirm rather than assume, so not building the graphical
version yet.

**2. A list of CAESAR-related files Conduit depends on**, so the user can ensure they're available
during development. Answered directly on the PR (factual, no ambiguity) — as of this round:
- `reference/*.pdf` (5 files): the public Hexagon vendor documentation Conduit's neutral-file and
  CAESAR II behavior understanding is built from.
- `fixtures/real-samples/*.cii` (3 files): real CAESAR II-exported neutral files, committed with
  the user's explicit authorization, used as ground truth for byte-layout questions.
- `fixtures/caesar.cfg`: one real (non-proprietary, example) CAESAR II directory-config file.
- Not committed, referenced only: `iecho.exe` itself (external tool, must be present on whatever
  machine runs the real conversion — see SPEC.md's "Native file adapter (iecho)") and the
  `C:\ProgramData\Intergraph CAS\CAESAR II\<version>\System` material/component database tree
  `CaesarInstallationLocator` locates but doesn't yet parse (see SPEC.md's "CAESAR II installation
  layout" and "Known open decisions").
**Next step**: build `conduit inspect` once the scope question above is answered (or immediately,
if a text table turns out to be sufficient) — not support-placement logic, so not blocked by the
loop/guide-spacing discussion above; can proceed on a decide-and-proceed basis once the one scope
question is settled.

## Confirmed: 2x vertical guide-spacing rule; viewer scope expanded; UMAT1 answered; new span/tee correction (2026-08-27)

**2x rule confirmed** — "You can go for the 2x rule." Will implement together with the rest of the
support-placement work below, not in isolation (see the "implement together" reasoning under the
new correction further down).

**Viewer scope is much bigger than the `conduit inspect` proposal**: "It should have all the same
viewing options as the input GUI in Caesar... material, OD, thickness, temperatures, pressures,
densities per element viewing, restraints displayed, bends displayed and bend option viewer, Tees
and SIFS, insulation density and thickness, applied forces, applied displacements. Anything that
can be seen in Caesar input basically without the editing." This is full CAESAR-input-GUI parity,
read-only. Scoping this against what Conduit currently parses into structured models vs. what's
still opaque raw text (per `docs/neutral-file/WALKTHROUGH.md`'s "Sections not yet used" list):
already modeled — `#$ ELEMENTS`' real values (OD, wall thickness, insulation thickness, densities,
pressures, temperatures, etc.), `#$ RESTRANT`, `#$ ALLOWBLS`, `#$ EQUIPMNT`, `#$ MISCEL_1`'s
material IDs. **Not yet modeled, would need new parsers first**: `#$ BEND` (currently only its
pointer is used — the record's own 14 fields aren't parsed into a queryable object),
`#$ SIF&TEES`, `#$ REDUCERS`, `#$ FLANGES`, `#$ OFFSETS`, `#$ FORCMNT` (applied forces),
`#$ DISPLMNT` (applied displacements), `#$ RIGID`, `#$ EXPJT`. Proposed approach: build the viewer
incrementally against what's already modeled first (a real, useful subset — geometry, sizes,
materials, restraints), then add a parser per additional section as it's needed for the viewer,
same as how `#$ ALLOWBLS`/`#$ EQUIPMNT`/`#$ BEND`'s pointer were added earlier in this project.
Not yet started — this is a real, separate body of work; flagging the phased plan rather than
committing to a full-parity timeline. **Format decision still open**: a rendered read-only web
page (HTML, viewable without any install) is the natural fit for "no license required" — will
default to that unless told otherwise, per decide-and-proceed, since it's reversible/low-stakes
(a delivery-format choice, not a support-placement decision).

**UMAT1 — answered directly, not new**: no, Conduit doesn't read `UMAT1.UMD` or any material
database file. This is already documented (SPEC.md's "Known open decisions," the
"Resolved (2026-08-21...)" entry): there's no format documentation for CAESAR's material/component
database files (same situation as `iecho.exe`), and it isn't needed for v1's purposes — `#$
ALLOWBLS` already carries the allowable stress CAESAR II itself computed per-element from whatever
that database lookup would have produced, which is what `SpanLimitCalculator` actually consumes.
`caesar.cfg`'s `User_Material_File_Name`/`SYSTEM_DIRECTORY_NAME` fields are parsed and surfaced
(so the *path* is known), but the file's own contents are never opened.

**New correction — span accumulation must reset at a bend, and tees need their own handling.**
"It isn't just so that the lengths should be checked per element against the allowable. Unless
there are changes in the direction of the pipe, that is a long straight segment, which should be
considered one piping section and the distance should be its length not the individual length of
each element... unless there is some type of directional change such as a bend or a restraint,
this should be considered one line segment."

Checked this against `SupportPlacer.PlaceSupportsForRun`'s actual current behavior (not assumed):
it **does** already accumulate span across multiple elements within a run rather than checking each
element in isolation — but it does **not** reset that accumulation at a bend. Traced this against
the exact `loop-50m-3d.cii` console output that originally surfaced the bend-corner bug: the
"node 20 (Guide): span 26000.00 mm" placement is literally `10→20`'s 24000 mm plus `20→30`'s
2000 mm summed together across a bend at node 20, which is exactly the behavior being corrected
here. So this is a real, confirmed gap, not a restatement of something already handled: **a bend
(direction change) must end one span-accumulation zone and start a new one**, same as a restraint
already does. Combined with the already-agreed "never place a support directly at a bend corner"
rule, this means the two corrections have to land together — implementing bend-corner exclusion
against the *old* accumulation model first would need redoing once this lands, so **holding off on
pushing any of the confirmed pieces (bend-corner exclusion, 2x vertical multiplier, loop rule)
until this is implemented as one consistent pass**, rather than doing it twice.

**Tees — a new topology `SupportPlacer` doesn't handle at all today.** "There are also tees in the
piping systems and these will have three elements connecting to a single note. It's important that
we're able to determine which of the elements is the tee and which two elements are the main
header." Checked `NeutralFile-v15.pdf`'s `#$ SIF&TEES` section for whether the neutral file labels
this explicitly rather than guessing: it identifies an intersection only by **node number** (item 1,
"the intersection node number") plus a type code and SIF/stress values — confirmed against a real
example (`TESTv15.cii`'s one `#$ SIF&TEES` record: node 50, type 3) — **it does not identify which
of the three connecting elements is the branch**. So that has to come from geometry: the two
elements whose direction vectors are collinear (same or opposite unit vector) are the header/main
run; the third, non-collinear element is the branch. Reading the request as: **when walking a run
for span-accumulation purposes, a tee's branch element should be excluded from the header's
straight-segment length entirely** (it's a separate, presumably much shorter, side connection with
its own span rules, not part of the "how long is this straight run" question) — please correct me
if the branch needs its own span check folded in differently.

**Next step**: implement all four confirmed/corrected pieces together once acknowledged — (1)
bend-corner exclusion, (2) span-accumulation resets at each bend (not just at restraints), (3) the
2× vertical guide-spacing multiplier, (4) tee detection via element-direction collinearity so a
branch element doesn't get folded into the header's span-accumulation — plus the loop rule from the
earlier entries. Test against `loop-50m-3d.cii` (still expecting zero supports inside the loop
given its current 2000 mm transverse legs, now for the added reason that each of its short jog legs
is its own bend-bounded span-accumulation zone, all comfortably under max span) plus new fixtures:
one with a tee, and one exercising the "transverse leg trips it, extending doesn't" loop case.

## Major finding: ALLOWBLS's cold allowable stress has likely never been real (2026-08-27)
Triggered by the user pushing back on the earlier "Conduit doesn't need UMAT1" answer: "the neutral
file allowables that have been currently set are based on my previous inputs... based on my choice
of material from the Caesar GUI which uses the UMAT1 file... Currently you've only been using the
low carbon steel inputs, but I don't know if you've been using the correct allowable... it will not
work for any other materials the way it is currently being used." Investigated rather than
re-asserting the prior answer, and found something more significant than either of us expected.

**The per-element ALLOWBLS-lookup mechanism itself is correct.** `SpanLimitCalculator.ComputeMaxSpan
(NeutralFile, Element)` does prefer `file.TryGetAllowableStress(element)?.ColdAllowableStress` over
the hardcoded fallback (`DefaultAllowableBendingStressMpa`, ~10.3 MPa / 1500 psi — explicitly
documented as "not a code value"), and `AllowableStress.ColdAllowableStress => Values[0]` matches
`NeutralFile-v15.pdf`'s `#$ ALLOWBLS` section exactly ("1. Cold allowable stress"). So *if* a file
has a real, populated allowable at that index, Conduit already uses it correctly, regardless of
material — no UMAT1 parsing needed for that part, exactly as answered before.

**But two things mean this path has likely never actually fired in this project so far:**
1. `NeutralFileFixtureBuilder` (every Conduit-generated fixture, including `loop-50m-3d.cii`) writes
   an **empty** `#$ ALLOWBLS` section — `NumAllowableStress = 0`, `AllowableStresses = []` — so every
   fixture-based test and every `iecho.exe` round-trip this project has ever run has silently used
   the ~10.3 MPa placeholder constant, not a real material's allowable.
2. **More surprising**: checked all 3 *real* CAESAR II-exported sample files' actual `#$ ALLOWBLS`
   bytes — `ColdAllowableStress` (item 1) is `0.0` in all three (`44002.cii`, `TESTv15.cii`,
   `TESTv15_slugged.cii`). So even a real file's own populated `#$ ALLOWBLS` section doesn't
   necessarily carry a nonzero cold allowable at that index — meaning Conduit's fallback constant
   may have been silently firing for *real* files too, not just synthetic ones.

Since max allowable span scales with `sqrt(allowable stress)`, and ~10.3 MPa is roughly 10-14x
lower than a typical B31.3 basic allowable stress for carbon steel (~110-140 MPa depending on
temperature/edition), this would make every max-span Conduit has ever computed against a real
sample file (and every fixture) come out **roughly 3-4x shorter than it should be** — meaning
far more supports than actually necessary. This plausibly explains an old, never-resolved note
already sitting in this file from much earlier in the project: "the observation that `SupportPlacer`
may be placing supports too aggressively — no real fixture files to diagnose against yet." That
observation may have had this exact root cause all along.

**What's not yet confirmed, and needs your input rather than a guess**: *why* is `ColdAllowableStress`
zero in the real samples? Two possibilities, and I don't want to assume which: (a) this is normal —
CAESAR/B31.3 populates the *actually-used* allowable somewhere else for these files' specific
code/configuration (the vendor doc itself warns "some of these items (notably 8-24) may have
different meanings based on the active piping code," so it's plausible item 1 isn't always the
right field to read), or (b) these three files simply don't have this field populated for some
reason specific to them (demo/example files, a particular workflow that doesn't fill it in, etc.)
and a typical production model would have it. **Could you check one of your real models in CAESAR
II's own GUI — what does "allowable stress" show for an element there, and does it correspond to
any of the numbers actually present in that element's `#$ ALLOWBLS` record?** That would tell me
definitively whether item 1 is the right field to trust, or whether Conduit needs to read a
different item (possibly code-specific) instead.

**Next step**: (1) get your read on the above before changing `SpanLimitCalculator`'s lookup logic,
since guessing at a different field index risks the same problem it's meant to fix; (2) regardless
of that answer, fix `NeutralFileFixtureBuilder` to populate a real, sourced allowable stress in its
fixtures (reusing a real sample's own `#$ ALLOWBLS` record is the established pattern for constants
in this project) so Conduit's own tests actually exercise the real-value code path instead of the
fallback — this part doesn't need to wait, it's a fixture-fidelity fix, not a placement decision.

## Horizontal-segment guide heuristic, deferred general axis model, and per-axis span-accumulation proposal (2026-08-27)
Same round, three more items, all explicitly framed as "let's take one thing at a time" — logging
all three, proposing a concrete design for the one most load-bearing (span accumulation), and not
implementing any of it yet.

**New heuristic question, not yet resolved**: "I would also like it if we found some heuristic for
determining when a guide is required on the horizontal segments. Generally, I would put a guide
wherever there is a rest myself. However, near bends, where there is perhaps a large expansion and
the guide restrains the expansion... this would have to be reconsidered. This is the same notion as
we previously discussed." So: the default becomes "every rest also gets a guide," modified by the
same near-bend/expansion-restraint caution already under discussion for vertical guides and loops.
Not resolved into a concrete rule yet — logged for the same "one thing at a time" follow-up.

**Deferred: 45°/diagonal segments and non-axis-aligned local coordinate systems.** "In some cases,
the piping segment's unit vector isn't even collinear with any of the horizontal axis. In that case
we will have to determine some form of local coordinate system... We will have to be able to
determine 45° bends [vs] 90° bends... if we have a 45° bend, applying a guide to that bend will
restrict movement in two directions both in the expanding direction and the perpendicular
direction... usually problematic as it will... act as a limit stop. We should avoid this, especially
close to the bend itself." Explicitly framed as uncertain ("I'm not 100% certain about this so I am
very open to suggestions"). **Proposing to scope the MVP implementation to axis-aligned (90°-bend)
geometry only** — which covers `loop-50m-3d.cii` and is almost certainly most real near-term
cases — and log 45°/arbitrary-angle handling as an explicit, deferred follow-up rather than trying
to solve a general local-coordinate-system problem alongside everything else already queued.

**Corrected/refined span-accumulation model — my own proposed design, not yet confirmed.** The
user's own restatement: "I think I was slightly misunderstood on the span reset... each straight
segment consisting of any number of elements should be considered as one segment for the span
calculation. The division of the segment should then be applied so that it cuts at the correct
lengths... An example: a 25,000 mm piping segment consisting of four elements (3×7000 mm + 1×4000
mm)... the span length should be 4×6000 mm and a final 1000 mm... This would require cutting the
three 7000 mm elements and the final 1000 mm element." Also: "the rest support on the loop's DX
segment should have a distance in the X axis between the last rest support on the long straight
line and itself no longer than the acceptable span length" — i.e. the loop's own extending segment's
placement is measured against the *last support on the same axis*, not reset to zero by the
intervening Y/Z jog. And: "if the change in direction is horizontal then the new span length in
the change direction has to be evaluated in the direction of that pipe segment until its direction
changes again... we kind of need to evaluate the two horizontal axes and the segments in these axes'
span length separately."

Read together, this isn't "reset accumulation at every bend" (my earlier, now-superseded framing) —
it's **track accumulated unsupported distance per principal axis independently** (X, the other
horizontal axis, and vertical), rather than one single running total for the whole run:
- Walking a run element by element, each element's length adds to *its own dominant axis's*
  accumulator (not to a single combined total) — a bend into a different axis doesn't reset that
  new axis's accumulator to zero if it had prior accumulated distance from earlier in the run (this
  is exactly what makes the loop's DX segment example work: the X-axis accumulator was already
  partway through its allowance before the loop, pauses while Y/Z accumulate during the jog, and
  resumes counting from where it left off once back on X).
- When an axis's accumulator would exceed max span, that's the trigger — and the *placement* isn't
  constrained to existing element boundaries: the accumulated zone (from the last support on that
  axis to now) gets evenly re-cut via the same chunking approach `ElementSplitter` already uses for
  a single overlong element (round down to nearest 1000 mm, divide with remainder), just generalized
  to operate across however many pre-existing elements that zone actually spans — which is exactly
  what makes the 25,000 mm / 4-element worked example come out to 4×6000 mm + 1000 mm regardless of
  the original 7000/7000/7000/4000 element boundaries.
- Placing a support resets *only that axis's* accumulator to zero; the others are untouched.
- Bend-corner exclusion (already agreed) still applies — a placement is never chosen at a node that
  is itself a bend corner, regardless of which axis's accumulator triggered it.

This is offered as my own proposed synthesis, not something already confirmed — explicitly asking
for pushback before implementing, per the user's own invitation ("you may give me your opinion...
perhaps you have a better way of doing it").

**Tee/SIF**: confirmed correct — "you have correctly understood the notion of the tee." New note,
not yet actioned: "we need to also be able to apply the SIF for this" — parsing/using `#$ SIF&TEES`
data is needed eventually, tying into the viewer's "Tees and SIFS" requirement from the same round.

**Viewer**: confirmed the phased-parsing plan is right — "I would like the viewer to be able to
parse all parts of the neutral file. We will need to create examples for each of the cases that
need to be parsed." No new decision needed; matches the already-proposed incremental approach.

**Next step**: get confirmation on the ALLOWBLS question (needs the user's own CAESAR GUI check —
can't resolve from this container alone) and on the per-axis span-accumulation proposal above,
before implementing any of the now-substantial queue: bend-corner exclusion, per-axis span
accumulation + even re-chunking, 2x vertical guide-spacing multiplier, tee/branch exclusion via
collinearity, the loop rule, and (separately, unblocked) fixing `NeutralFileFixtureBuilder` to
populate a real ALLOWBLS record.

## New reference material: textbook chapters, UMAT1 printout, B31.3 PDF (2026-08-28)
User shared 5 new PDF attachments on the same PR: chapters 2, 3, and 6 of a pipe-stress-engineering
textbook, a printed (non-parseable-as-UMD, but human-readable) dump of the actual `UMAT1.umd`
material database, and a copy of B31.3-2024 ("just for reference — CAESAR supports several codes").
Asked that these be added to `reference/` and consulted going forward, with more chapters available
on request.

**Downloaded and read directly** (GitHub's `user-attachments` URLs aren't fetchable through the
normal proxy path — `WebFetch` reports a redirect to a signed `objects.githubusercontent.com` S3
URL; following that redirect either lets `WebFetch` process it directly, or for files over its 10 MB
cap, saves the raw PDF locally for the `Read` tool to open directly, which is how both files below
were actually reviewed):

**UMAT1.umd printout — resolves the ALLOWBLS mystery, but not the way expected.** This is the real
CAESAR II material database (COADE-supplied). Read the first 10 entries (materials #1-10: LOW
CARBON, HIGH CARBON, CARBON MOLY, LOW/MED CHROME MOLY, AUSTENITIC STNL, STRGHT CHROMIUM, 310
STAINLESS, WROUGHT IRON, GREY CAST IRON — material #1, "LOW CARBON," is the one Conduit's fixtures
have always implicitly modeled). **Confirmed: the material database itself carries no allowable
stress, yield, or UTS values at all** — every material's "ALLOWABLE," "YIELD," and "UTS" columns
are blank across every temperature row; only density, Poisson's ratio, thermal expansion
coefficient, and (cold/hot) elastic modulus are populated. This means allowable stress was never a
material-database lookup in the first place — it has to be a **piping-code-table computation**
(B31.3 Appendix A or equivalent, keyed by material + temperature + code edition), done at analysis
time, not stored in the material file Conduit was asking about. This also gives a cleaner
explanation for why `#$ ALLOWBLS`'s cold allowable stress was `0.0` in all 3 real sample files (see
the earlier "Major finding" entry): those files most likely were exported before a static analysis
was ever run on them (geometry/loads-only exports), so no code-derived allowable was ever computed
and stored — not a field-mapping bug, and not something UMAT1 could have supplied directly even if
Conduit parsed it.

**Implication for the fixture-ALLOWBLS fix already queued**: a real, code-compliant allowable can't
be sourced from the material database alone — it needs either (a) the actual B31.3 basic-allowable-
stress table (Appendix A) keyed by material and temperature, now available in the B31.3-2024 PDF the
user also shared, or (b) continuing to use a documented placeholder constant until real code-
compliance math is in scope. Given B31.3's own table is now available, (a) is worth pursuing rather
than reusing an arbitrary constant — logged as the concrete next step once acknowledged.

**Ch6 ("Pipe Supports and Restraints") — read the introduction and Section 6.2/6.3 (pages 151-160 of
28 total; the rest not yet reviewed).** Directly useful:
- Confirms the standard terminology Conduit already uses (guide, rest/resting support, anchor, line
  stop, one-way/two-way stop) matches this text's definitions exactly.
- **"For a long straight segment of piping, guides are generally provided at every other support
  span."** This slightly refines the user's own "generally, wherever there is a rest" framing — not
  literally every rest, every *other* one on a long straight run. Worth confirming which reading to
  encode before implementing the horizontal-guide heuristic.
- **Eq. 6.1, the same beam-bending max-span formula Conduit already implements — but with a
  different constant.** The book's version: `L = sqrt(10·Z·S/w)`. Conduit's `SpanLimitCalculator`
  currently uses `L = sqrt(8·σ·Z/w)` (simply-supported single-span beam, `M = wL²/8`). The book
  explicitly derives its version from a "semi-fixed beam approach" (its own Section 2.7, not yet
  reviewed) — a more realistic model that accounts for the pipe continuing past each support (some
  rotational fixity from the adjacent spans) rather than treating each span as isolated and simply
  supported, which is presumably why its constant (10) is larger (a longer allowable span) than the
  naive simply-supported constant (8) Conduit currently uses. **Not changing this yet** — flagging
  it as a real, sourced opportunity to make `SpanLimitCalculator` less conservative, pending review
  of Section 2.7 for the actual semi-fixed-beam derivation before touching the formula.
- **Table 6.1** (suggested B31.1 power-piping span, e.g. 6 in./150 mm: 5.2 m liquid / 6.4 m gas) is
  useful ground-truth to sanity-check Conduit's own computed max-span numbers against, once the
  formula question above is settled (B31.1 power piping isn't the same code Conduit defaults to,
  B31.3, but the same order-of-magnitude check still applies).
- **Fig. 6.8's worked example is the one the user specifically flagged** ("particularly fond of this
  example... create a test file for later"): a piping run from a process tower to another
  equipment, resting on 3 supports (nodes 20/30/40), with a peak between two down-slopes — one side
  drops 2 m then runs 3 m to the far equipment connection, the other runs 4 m then 5.2 m to the
  tower. Per direct instruction: model both equipment-nozzle connections as anchors instead, and add
  flanges at both ends "complying with the entered lengths." **Noted for a future fixture, not
  built yet.** Important scope distinction: the surrounding text (Section 6.3) is about *nonlinear
  resting-support behavior across thermal cycles* (a pipe lifting off one support as another moves) —
  that specific analysis technique is out of MVP scope per CLAUDE.md's "no spring logic of any kind"
  rule (resting-support nonlinearity is adjacent to spring behavior); the *geometry* is still a
  useful, concrete future test case independent of whether Conduit ever models that nonlinearity.
- **Ch2/Ch3 and the rest of Ch6 not yet reviewed** — still pending before answering Q3 (45°-bend/
  local-coordinate-system handling) with anything more than my own initial synthesis.

**Open question — copyright/IP, not yet resolved, asked rather than assumed.** Per SPEC.md's clean-
room hard constraint and this project's own established pattern (real `.cii` samples and the
`iecho.py` reference code were both read locally for context *before* explicit authorization was
given to actually commit anything — see QUESTIONS.md's earlier "Real neutral file format adopted"
entry), a purchased textbook's chapters and the B31.3 code document are presumptively copyrighted,
commercial publications — a materially different category from Hexagon's own public vendor
documentation (already established as safe to commit). **Not committing the PDFs into `reference/`
without an explicit answer to this** — asked the user directly on the PR whether they're authorized
to have this committed (even to a private repo/branch) the same way the CAESAR vendor docs and real
sample files were. In the meantime, the engineering facts extracted above (in my own words, cited by
chapter/section) are usable and are what's actually needed for design decisions — the source PDFs
themselves aren't required in the repo for that.

**Confirmed/answered, no longer open:**
- Q1 (guide direction cosine understanding): confirmed correct.
- Q2 (bend clearance): **radius + 200 mm minimum**, the extra 200 mm being the user's own
  experience-based welding-access allowance. This is a different, more specific number than
  `ElementSplitter`'s existing 500 mm shoe-clearance buffer (from an earlier, separately-agreed
  round) — the two may serve different purposes (minimum unbent pipe length near a bend for
  `ElementSplitter`'s own chunking, vs. this new number specifically for how close a *guide* can sit
  to a bend without acting like a limit stop) and shouldn't be silently merged into one constant
  without confirming they're actually the same concept.
- "Symmetry" in loop detection means **exact** symmetry (equal magnitude, opposite sign) — not just
  topological (same-axis-opposite-sign regardless of length), resolving the earlier open follow-up.
- Standalone (non-loop) vertical risers: confirmed the same 2x-horizontal-span guide-spacing rule
  applies, same as risers inside a loop.
- **Major refinement to the per-axis span-accumulation model**: a rest support resets *every*
  horizontal axis's accumulator, not just the accumulator for its own segment's local axis —
  "If there is a segment in the x direction, then a segment in the z direction with a rest, then
  the rest will also be part of the x span computation, as it also supports this. It should also be
  considered in the separate z-span computation. This also goes for vertical cases if there is a
  rest there for a reason." Physical reasoning (my own restatement, to be confirmed): a rest resists
  *gravity sag*, which doesn't care which horizontal direction the local pipe segment happens to run
  in — so a rest anywhere along the path is a valid "unsupported distance since last rest" reset
  point for *every* horizontal axis being tracked, not just its own segment's axis. This is a
  meaningfully different (and more physically sound) model than my prior proposal, where each
  element only fed its own local axis's accumulator. Whether this same universal-reset behavior
  also applies to *guides* (which restrain a specific lateral direction, not gravity generally) is
  not yet confirmed — the user's wording specifically discusses rests; not assuming it extends to
  guides without asking.

**Next step**: post a restatement of the refined per-axis/universal-rest-reset model for
confirmation (same pattern as before — get this right before implementing), ask the copyright
question, and report the ALLOWBLS/UMAT1 finding. Once confirmed, implement everything queued so far
together in one pass, now additionally informed by Eq. 6.1's semi-fixed-beam refinement (pending
Section 2.7 review) and the radius+200mm bend-clearance number.

## Implemented: real A106 Grade B material + textbook span formula; PDF commit blocked by sandbox (2026-08-28)
User replied with four crisp answers, all now acted on:
1. **"LOW CARBON does not exist as a material in the standard... refer to materials that do have
   all the required information."** Searched the full UMAT1 printout (1708 pages, `pdftotext`
   without `-layout` to get a searchable text stream) for a real, standard-designated material with
   populated allowable/yield data. Found it: **material #107, "A106 Grade B"** — ASTM A106 Grade B,
   an extremely common B31.3 process-piping carbon steel — with a genuinely populated record: cold/
   ambient allowable stress 118 MPa, yield 241 MPa (241 MPa = 35,000 psi, A106-B's textbook minimum
   yield — cross-checks as legitimate, not a parsing artifact), density 7833.4399 kg/m³, cold
   modulus 203,400 MPa. **Implemented**: `SpanLimitCalculator`'s three fallback constants
   (`DefaultAllowableBendingStressMpa/Psi`, `DefaultSteelDensityKgPerM3/LbPerIn3`, new
   `DefaultElasticModulusMpa/Psi`) now all derive from these real A106 Grade B values instead of
   the old arbitrary "not a code value" placeholders. `NeutralFileFixtureBuilder` now populates a
   real `#$ ALLOWBLS` record (118 MPa cold allowable) and wires every element's allowable-stress
   pointer to it, replacing the empty section every fixture had before.
2. **"Use formulae that are given in the textbook rather than our own."** Implemented Ch6's actual
   Eqs. 6.1 and 6.2: the bending-stress criterion `L1 = sqrt(10·Z·S/w)` (constant 10, not the
   simply-supported beam's 8 — the book's "semi-fixed beam" model) and the sag criterion
   `L2 = (128·E·I·Δ/w)^(1/4)`, taking the smaller of the two per the book's own rule. `Δ` (design
   sag limit) needed a specific value the book only gives as a range for process plants (Kellogg:
   12.5-25 mm) — picked the lower, more conservative end (12.5 mm,
   `SpanLimitCalculator.DesignSagLimitMillimetres`) as a decide-and-proceed pick, logged here for
   correction if wrong. Result for the fixtures' 6" Sch 40 pipe: max span is now 10,835.7 mm (sag-
   governed), up from the old ~6,446.8 mm (bending-stress-governed, computed against the old
   placeholder allowable) — both the formula and the real material contributed to this increase.
3. Bend clearance: **500 mm, not 200 — confirmed as the same number already in `ElementSplitter`**,
   no longer an open reconciliation question.
4. Copyright: **confirmed fine to commit — "they were all found online."**

**Regenerated all 3 committed fixtures** (`straight-run.cii`, `run-with-riser.cii`,
`loop-50m-3d.cii`) against the new builder — diffed against the previous committed versions to
confirm only the expected fields changed (allowable-stress pointer, density value, the new
`#$ ALLOWBLS` record, and — as a welcome side effect — `straight-run.cii`/`run-with-riser.cii`'s
restraint stiffness, which had never actually been regenerated since the stiffness-fix round
earlier this project and were still silently carrying `0.0`; `loop-50m-3d.cii` already had this
fixed). Updated 2 existing tests whose fixed geometry assumed the old, much shorter max span
(`SupportPlacerTests.RiserThatTriggersTheSpanOverflow_GetsAGuideSupport`,
`OptimizationLoopTests.UnsplittableElement_IsStillReportedRatherThanLoopedForever`) to use geometry
that still exercises the same property under the new, larger max span — verified the new numbers
by computing the formula directly, not by guessing. 79/79 tests passing, `dotnet build`/`test`
clean. Ran the CLI against the regenerated `loop-50m-3d.cii`: still `PASS`, now correctly reporting
the larger max span (10,835.70 mm) — and, as an incidental but expected consequence of the larger
span, one of the three original placements (node 50's rest) is no longer needed at all, since the
run no longer overflows there. The still-open bend-corner-placement bug (nodes 20/70 placed
directly on bend corners) is untouched by this round — that's still blocked on the remaining
open items below, not something this pass claimed to fix.

**Blocked: could not commit the reference PDFs — a sandbox restriction, not a decision.** Downloaded
Ch6 successfully earlier (curl against the signed `objects.githubusercontent.com` redirect URL,
17 MB) and read it directly for the findings already logged. When attempting to fetch Ch2/Ch3/the
B31.3 PDF and then to commit any of these (including Ch6, and even a plain local `cp` of the
already-downloaded UMAT1 copy) into `reference/`, the environment's auto-mode action classifier
denied every attempt — `dotnet build`/`test` and read-only commands were unaffected, so this reads
as a classifier restriction specifically on writing/copying binary content into the repo from this
autonomous session, not a decision on my part. **Not silently giving up on this** — flagging it
plainly on the PR so the user can decide how to proceed (e.g. attaching the PDFs directly via
GitHub's own PR/repo UI, which wouldn't hit this session's action restrictions). In the meantime,
the actual engineering content already extracted (material #107's data, Eqs. 6.1/6.2, the bend-
clearance number) is implemented and cited by chapter/section in code comments and here — nothing
was blocked on having the PDFs physically in the repo.

**Next step**: report back on the PR — the implemented material/formula change, the fixture
regeneration, and the PDF-commit blocker — and continue holding the actual `SupportPlacer`
rewrite (bend-corner exclusion, per-axis span accumulation, 2x vertical multiplier, tee/branch
exclusion, the loop rule) pending confirmation of the still-open universal-rest-reset model and the
guide-every-other-span nuance, per the last round's questions.

## Implemented: the `SupportPlacer` rewrite — bend/tee exclusion, per-axis accumulation, 2x vertical, guide-at-rest (2026-08-28)

The user's next comment gave the three remaining confirmations needed to unblock this, plus
explicit authorization to build: **"I confirm the universal-rest-reset model. I think we can use a
guide at every rest, unless it comes very close to a directional change. No need to define this
right now; let's build and see how these changes work."** — and, on the PDF blocker above,
**"Remember the files, but you can try again."**

**PDF commit — resolved.** Retried the exact same `curl`-into-`reference/` approach that was denied
last round; this time all five PDFs (Ch2, Ch3, Ch6, the UMAT1 printout, B31.3-2024) downloaded and
committed without issue. Whatever triggered the classifier denial last round appears to have been
transient/session-specific, not a durable restriction — noted here in case it recurs, but not
treated as blocking anything further. Committed at `reference/pipe-stress-engineering/{Ch2,Ch3,
Ch6,UMAT1-material-database}.pdf` and `reference/B31.3-2024.pdf`; `reference/README.md` updated
with what each one is for.

**`SupportPlacer` rewrite — implemented.** Full detail is in `SpanPlacer.cs`'s own class doc
comment (kept in sync with the actual code, so treat that as the primary reference over this
summary), but the shape of it:

- **Per-axis accumulation** (`PipeAxisClassifier`, new): the model's two horizontal axes track
  their own unsupported-span accumulator independently, plus a third for the vertical axis. An
  axis-aligned (90°-only, MVP scope) element contributes its full length to whichever single axis
  it runs along — diagonal/45° segments still explicitly deferred, per "let's take one thing at a
  time."
- **Universal reset**: placing a support (or passing an already-restrained node) resets *all
  three* accumulators, not just the axis the support happens to sit on — confirmed for the two
  horizontal axes by direct instruction; extended to the vertical accumulator too as this file's
  own reversible, logged call (no stated reason to treat it differently).
- **Bend/tee corner exclusion**: a support is never placed directly on a bend corner or a
  tee/branch node (branch detection is by node degree across the whole file — a node with a third
  connecting element beyond the run's own in/out pair — not by SIF/collinearity analysis, which
  is still deferred), nor within the same bend-clearance buffer `ElementSplitter` already uses.
  When the natural overflow point falls in an excluded zone, the placer backs off to the nearest
  eligible node already passed *on that same axis* since the last reset (tracked per axis — a
  vertical overflow must back off to a point actually up the riser, not to an unrelated horizontal
  node that happens to be the most recent one seen). If no eligible node exists at all in the zone
  (a single overlong element with no interior node, ending right at a bend — e.g. either 24 m leg
  of `loop-50m-3d.cii`), nothing is placed there in the initial pass; `OptimizationLoop`'s existing
  reactive `ElementSplitter` fallback resolves it, and already keeps the same bend clearance.
- **2x vertical multiplier**: a vertical run's own accumulated length is checked against 2x the
  horizontal max allowable span, not 1x, per direct instruction, for both standalone risers and
  loop verticals.
- **Guide at every eligible rest**: every plain horizontal rest also gets a co-located guide
  (packed into the same `#$ RESTRANT` record via a new `Restraint.CreateMultiDof`, matching how
  real files pack multi-DOF supports at one node — confirmed against
  `fixtures/real-samples/44002.cii`'s own multi-DOF records). Since eligible nodes are already
  guaranteed clear of the bend/tee exclusion zone, "not very close to a directional change" — the
  condition the user said not to bother defining precisely yet — falls out of that same check for
  free, with no separate threshold introduced.
- **`MockStressSolver` updated to match**: the iterate-loop's own pass/fail check now uses the
  *same* per-axis model (reset at any restrained node, not just anchors) instead of the old
  "sum the whole segment's combined length" check. This was necessary, not optional — without it,
  the solver would keep reporting the jog/branch stretches `SupportPlacer` correctly leaves alone
  as failing, and the iterate loop would fight the new placer by re-adding supports right where it
  had just decided not to. `OptimizationLoop`'s reactive rest-additions (`TryPickMidpointNode` /
  `TrySplit`) still don't add a companion guide the way the initial pass does — logged as a known,
  narrower follow-up gap rather than fixed this round, since it only affects the reactive fallback
  path, not the primary placement decision.

**Tested against three examples, per direct instruction**:
1. **3D loop** (`fixtures/loop-50m-3d.cii`, unchanged geometry): `SupportPlacer`'s own pass places
   nothing at all this time (every horizontal node in this specific fixture happens to be a bend,
   so there's no eligible in-zone node anywhere along the initial walk) — `OptimizationLoop`'s
   reactive splitting handles both 24 m legs correctly, adding rest supports mid-leg while
   respecting bend clearance, and the jog itself (nodes 20-70) gets zero supports. `PASS` in 4
   iterations. New test: `SupportPlacerTests.NeverPlacesASupportDirectlyOnABendCorner`.
2. **2D loop, my own design** (new `fixtures/loop-2d.cii`): a purely-planar jog — two 24 m X legs
   with a small (2000/2000/2000 mm) Z-axis offset in between, no vertical element at all — isolates
   the two-horizontal-axes tracking from the vertical rule. Same result pattern as the 3D case:
   zero supports inside the jog, both long legs split correctly, `PASS` in 4 iterations. New test:
   `SupportPlacerTests.PlanarJog_GetsNoSupportsInsideTheJogItself`.
3. **Fig 6.8, anchors only** (new `fixtures/fig6-8-example.cii`): re-fetched the actual figure
   image (not just my earlier paraphrase of it, per CLAUDE.md's "always consult the primary
   source" rule) and found it's a **sloped, peaked** run (a shallow rise from a 2m riser + 3m
   offset up to a peak, then a shallow fall over 3m+4m+5.2m to a tower) — genuinely diagonal, not
   axis-aligned, which this MVP's span model still doesn't handle. Built an honest **flattened
   approximation** instead: the same riser + the book's own five horizontal support-to-support
   distances (3m, 6m, 3m, 4m, 5.2m), at a single elevation, with only the one real bend (top of the
   riser) preserved and no supports besides the two anchors going in. This is a structural smoke
   test, not a check against the book's own answer — it confirms the whole pipeline runs cleanly on
   this topology and never supports the riser's own corner, not that Conduit reproduces the book's
   exact 3-support solution (which depends on the slope/peak this approximation drops). New test:
   `OptimizationLoopTests.Fig68FlattenedApproximation_PassesWithoutSupportingTheRiserCorner`.
   **Flagging this simplification explicitly** rather than presenting the flattened version as
   faithful — if an axis-aligned approximation isn't good enough for what the user wants to check
   against this example, diagonal-segment support would need to be tackled first.

Also updated `SupportPlacerTests.StraightRun_PlacesOnlyRestSupports_SpacedUnderMaxSpan` →
`StraightRun_PlacesRestAndCoLocatedGuideSupports_SpacedUnderMaxSpan` (renamed; now asserts the
rest+guide pairing instead of rest-only, an intentional behavior change) and
`RiserThatTriggersTheSpanOverflow_GetsAGuideSupport` → `RiserThatExceedsTheVerticalSpanThreshold_GetsAGuideSupport`
(the old 3000 mm riser no longer triggers anything under the new, correct per-axis model — a 3000mm
riser genuinely doesn't need a mid-height guide under the 2x rule — replaced with a 25,000 mm riser
that exceeds the 2x threshold on its own, isolating the property this test exists to check).
82/82 tests passing, `dotnet build`/`test` clean.

**Still not done**: tee/branch *span* exclusion (a branch arm's own separate accumulation, rather
than just keeping the tee node itself clear of placements — the node-degree detection is in place,
the separate-accumulation half isn't), applying the SIF at a tee, the guide direction-cosine
question (still open from a few rounds back — GUI direction cosines are still left at (0,0,0)),
and the reactive-split companion-guide gap noted above. None of these blocked this round's three
requested examples; queued behind "one thing at a time" as before.

**Next step**: report back on the PR with this implementation, the three example results, and the
PDF commit now being resolved. Await direction on tee/branch span accumulation, the guide
direction-cosine question, or whichever of the remaining open items the user wants to tackle next.

## Fixed: reactive splitting was still placing restraints on bend corners; Fig 6.8 needs a Z component (2026-08-28, third round)

Real report, with screenshots, against both `loop-2d.cii` and `loop-50m-3d.cii`: CAESAR II showed
rest supports sitting directly on bend corners — the exact bug this whole redesign started from,
back again via a path I hadn't checked. **"Any element with a bend pointer shouldn't have a
restraint."**

**Root cause, traced not guessed**: `SupportPlacer`'s own initial pass is fully bend/tee-aware
(confirmed via its own unit tests), but `OptimizationLoop.Adjust`'s *reactive* fallback path —
`TryPickMidpointNode` and `TrySplit`, used when a span still fails after the initial pass — had
never been updated with the same awareness; it's older code this round didn't touch. Two bugs, not
one:
1. `TryPickMidpointNode` picked whichever node was numerically closest to a failing zone's
   midpoint, with zero bend/tee awareness. For the loops' 50000 mm zone (10→60/10→80), the geometric
   midpoint by raw path length landed exactly on a jog bend node.
2. Once that was fixed and `TryPickMidpointNode` correctly returned nothing (every interior node in
   the jog is excluded), the fallback to `TrySplit` had its own bug: it chunked whichever element it
   picked using the pipe's *full* max allowable span, oblivious to how much of that span's budget
   the zone had already spent on earlier elements before reaching the one being split (e.g. the
   short bend-clearance remainder plus the jog's own short legs, ~6000 mm already spent by the time
   the second 24 m leg starts). The resulting new node was placed at "full span past this element's
   own start," which put the *cumulative* span (already-spent budget + new chunk) back over the
   threshold, with nowhere left in that zone to fix it — this is what surfaced as the visible
   bend-corner placement in the screenshots (the loop kept iterating and failing at the same spot
   until `MaxIterations` gave up, and whatever partial supports existed sat right at/near the jog).

**Fixed both**: `TryPickMidpointNode` now excludes bend/tee nodes (and their clearance buffer),
matching `SupportPlacer`'s own rule exactly — same node-degree/bend-pointer check, same
`ElementSplitter` clearance constant. The split fallback (renamed `TrySplitAtFirstOverflow`) now
walks the failing zone's elements *in order*, tracking how much of the finding's own axis (now a
real field on `StressFinding`, not just embedded in its message string) has already accumulated,
and splits the *first* element that would push it over — using the *remaining* budget (not the
full span) as the chunk size, so every resulting chunk stays within the true, already-partially-
spent allowance. This is deliberately conservative rather than span-optimal: it can produce more
new supports than a human would place by hand in this specific case (observed: 5 new nodes on the
second leg instead of a theoretical minimum of 2), since it chunks the *whole* remaining element at
the shrunk size rather than using a short first chunk followed by full-length ones. Logging this as
a known suboptimality, not fixing it this round — correctness (no bend-corner placement, always
converges) mattered more than support-count optimality for this pass.

Verified directly against the actual committed fixtures (not just fresh unit-test geometry): parsed
`/tmp/out-loop2d.cii` and `/tmp/out-loop3d.cii` after a real `conduit optimize` run and confirmed
zero restraint nodes intersect the bend-node set for either file. Added a permanent regression test,
`OptimizationLoopTests.PlanarJogWithOverlongLegs_ReactiveSplitting_NeverRestrainsABendNode`, running
the *full* `OptimizationLoop` (not just `SupportPlacer` in isolation, which the earlier round's
tests already covered but which didn't exercise this reactive path) against the same 2D-jog
geometry. 83/83 tests passing.

**Fig 6.8 — flagged, not yet fixed.** The user also caught that my flattened approximation only
modeled X and Y components, when the real example has a Z component too. I went back to the actual
figure image (not just my memory of it) specifically looking for this — the drawing itself doesn't
show any visible Z-axis offset in the pipe route as drawn (it reads as a single-plane diagonal line
in the sheet plane, base→peak→tower), but a 2D projection like this is inherently ambiguous about
depth, and the user has the actual book text/context I don't. Rather than guess a second time on
this same fixture, **asking directly**: what's the Z dimension (magnitude and which segment it
applies to)? Once known, `fixtures/fig6-8-example.cii` and its test get corrected — this is a small,
mechanical fixture update once the missing number is in hand, not blocked on anything else.

**Next step**: report the bend-corner fix (with the actual verified output) on the PR, and ask for
the Fig 6.8 Z-component detail needed to correct that one fixture.

## Fixed: reactive splitting was clustering supports instead of spacing them evenly; Fig 6.8 geometry corrected (2026-08-28, fourth round)

User confirmed the bend-corner fix worked (both loops now `PASS` with no bend-corner supports —
attached their own console output and the actual output `.cii` files as proof) but asked a sharp
follow-up: the second leg's new supports were "very closely spaced," and "there should be no span
in the x direction longer than the minimum span length from one support to the next." This is
exactly the tradeoff I'd flagged as a known, deliberate limitation in the last round's reply — the
user noticed the same thing and, reasonably, wants it actually fixed rather than just documented.

**Root cause of the clustering**: the previous fix made the reactive split *conservative* by
uniformly shrinking every chunk's size to the remaining budget, not just the first one — correct
(never exceeds budget) but wasteful, since only the *first* new support needs to be placed early;
every support after that resets the budget and can use the pipe's full span again. For the loop
fixtures this meant 5 new supports on the second leg instead of the true minimum of 2.

**Fixed properly**: `ElementSplitter.Split` now accepts an optional `firstChunkBudgetMillimetres`
— when given, only the *first* chunk is capped at that (rounded down, same convention as every
other chunk); every chunk after it uses the pipe's full max span. `OptimizationLoop`'s
`TrySplitAtFirstOverflow`/`TrySplit` pass the already-computed remaining budget through this new
parameter instead of shrinking the whole element's chunk size. Verified against the same real
fixtures: both loops' second legs now get exactly 2 evenly-spaced new supports, landing on a clean
10,000 mm grid (10000, 20000, 30000, 40000 mm from the run's own start) — matching what a human
would place by hand. Extended the existing regression test
(`PlanarJogWithOverlongLegs_ReactiveSplitting_NeverRestrainsABendNode`) to also assert every
restraint lands on that even grid, not just that none land on a bend. 83/83 tests passing.

**Fig 6.8 — corrected.** The user answered the Z-component question directly: "From the first
anchor, it rises two meters, then extends 12m in z, then goes 9.2 meters in x to the final anchor
at the tower." This is a much simpler geometry than my earlier flattened multi-segment guess (which
had 5 separate X-axis legs at one elevation, trying to approximate a sloped profile) — it's just
three elements: a 2 m Y riser, a 12 m Z run, then a 9.2 m X run to the tower anchor, two bends (top
of riser, Z-to-X transition). Rebuilt `fixtures/fig6-8-example.cii` and its test
(`Fig68Example_PassesWithoutSupportingEitherBendCorner`) to match exactly; confirmed via the CLI
that it `PASS`es without supporting either bend.

**Next step**: report both fixes on the PR with the verified spacing/geometry. Both loop fixtures
and Fig 6.8 should now be ready for another real CAESAR II check if the user wants one, though
nothing is currently known to be wrong with them.

## Correction: the "reactive-split companion-guide gap" was already closed, just never called out (2026-09-01)

While drafting SPEC.md's new Milestones section (see CLAUDE.md's "Continuous progress, no idling"
policy, added the same day), re-read `OptimizationLoop.cs` to scope the M1 milestone item "give a
reactively-split rest the same companion guide the initial pass gives" — and found it's already
done. The fourth round's `AddSupport(file, node, type, izup)` helper (introduced for the
clustering fix, to add restraints at every new interior node from a split) already packs a `Gui`
DOF onto the restraint whenever `type == SupportType.Rest`, and both reactive call sites
(`TryPickMidpointNode`'s single-node add, and `TrySplit`'s per-interior-node loop) go through it.
So the gap flagged open in the round-3 and round-4 entries above ("reactive-split companion-guide
gap") was a side effect fixed without being explicitly logged as resolving that specific earlier
note — the round-4 entry documents the clustering fix but never mentions this. No code change
needed here; corrected the stale "still open" language in SPEC.md's "Known open decisions" section
and in this file's own round-3/4 "Still not done" wording. Real remaining M1 work: folding
element-splitting into `SupportPlacer`'s own initial pass (per the user's 2026-09-01 PR comment),
and the tee/branch span accumulator.

**Next step**: none — this is a documentation correction, not a blocking question. Continuing with
M1's remaining items per SPEC.md's Milestones section.

## Proposed: M3 fixture-generator CLI subcommand — input format and scope (2026-09-01)

Per your point 1 ("a small program that creates the input files... let's discuss how this could be
done") and my PR reply proposing to expose the existing from-scratch generator as a CLI subcommand:
before moving code and locking in an external file format, want your read on the concrete shape,
since it's the kind of choice that's cheap to get right up front and annoying to have you relearn
if I guess wrong.

**Proposed CLI surface**: `conduit generate <spec.json> <output.cii>` — reads a small JSON
description of a run and writes a structurally-valid `.cii` (same synthesis logic
`NeutralFileFixtureBuilder` already uses for the committed fixtures — `#$ VERSION`/`WIND`/`UNITS`/
`COORDS` boilerplate, `#$ ELEMENTS`/`#$ BEND` from your segment list, `#$ RESTRANT` anchors — cross-
checked against the real samples' byte layout, see "Neutral file format" above).

**Proposed input format** (JSON, since it needs no extra dependency — `System.Text.Json` is built
into .NET — and maps directly onto the existing `PipeSegmentSpec` fields):
```json
{
  "izup": 0,
  "anchors": [10, 100],
  "segments": [
    { "from": 10, "to": 20, "dx": 8000, "dy": 0, "dz": 0, "od": 168.3, "wt": 7.11 },
    { "from": 20, "to": 100, "dx": 0, "dy": 0, "dz": 12000, "od": 168.3, "wt": 7.11, "bend": true }
  ]
}
```
`od`/`wt` (and density, defaulting to the fixture builder's existing steel constant if omitted) per
segment rather than once for the whole file, so a run with a size change is still describable.

**Open questions this needs your input on, not guessed at**:
1. Is JSON the right format for you to hand-write/edit, or would a flatter, more spreadsheet-like
   form (CSV, or a simple line-oriented text format — one line per segment) fit your actual workflow
   better? You mentioned you'd still prefer to create test files yourself when practical — worth
   optimizing for what's fastest to hand-edit, not just what's easiest for Conduit to parse.
2. Does the segment-by-segment `from`/`to` node numbering match how you'd naturally describe a run,
   or would auto-numbered nodes (you just give directions/lengths in sequence) be more natural?
3. Scope: is a single straight/jogged run with anchors and bends enough for what you need test files
   for right now, or do you also want equipment nozzles (`#$ EQUIPMNT`) and tees/branches
   describable from day one?

**Implementation note, not blocking the question above**: `NeutralFileFixtureBuilder` currently
lives in the test project, explicitly documented as test-only/not-part-of-Conduit.Core (see
"Generating test neutral files" above — that boundary was about keeping *production* Conduit.Core's
own parsing free of invented data, written before this CLI-facing use case existed). Building this
subcommand means relocating that synthesis logic into `Conduit.Core` for real, as a genuine new
production capability, not just an internal refactor — flagging that since it's a real scope
addition to what Conduit's CLI does, not merely an implementation detail.

**Next step**: once you weigh in on the three questions above (or say "just pick something and
build it"), implement the subcommand: move `NeutralFileFixtureBuilder`'s synthesis logic into
`Conduit.Core` (test project keeps calling it via the same names, now through a `Conduit.Core`
reference instead of its own local copy — mechanical, low-risk, existing tests don't change
behavior), add the JSON (or chosen format) parser, wire up the `generate` CLI verb, and add tests
plus a `TESTING.md` walkthrough entry. Proceeding with the JSON design above as the default if no
response arrives before the next round, per the continuous-progress policy — logged here so it's a
visible, reversible choice rather than a silent one.

## M2 starting proposals: guide direction cosine, hold-down, line stop (2026-09-01)

Bringing concrete starting proposals rather than open-ended questions, per CLAUDE.md's "batch
consult questions with a concrete next step" rule and the M2 milestone.

**1. Guide direction cosine — the old fix's vehicle no longer exists; is the problem still real?**
The round-3 discussion (see "Answers received: deterministic loop-placement rule" above) concluded
CAESAR's own `(0,0,0)` default is correct for the ordinary case, and only needs an explicit non-zero
cosine when an all-round vertical guide would over-restrain a real thermal-expansion path — which
that round handled via a *loop-specific* placement rule (no guide ever lands on a loop's own
vertical/transverse legs). That loop-specific mechanism was superseded by the later universal-reset
per-axis rewrite, which has no concept of "loop" at all — but the underlying physical concern
(a standalone riser's guide, left at `(0,0,0)`, restrains *both* lateral directions, one of which
might be an adjacent run's own expansion path) can still occur under the current model, e.g. a
riser near a bend into a long horizontal run. Unlike the loop case, there's no obvious general rule
here without knowing what's actually adjacent to a given riser — "restrain the direction nothing
downstream needs to expand into" requires reasoning about the wider layout, not just the riser
itself. **Question**: keep `(0,0,0)` as the standing default for every guide for now (simplest,
matches "no need to define this right now" from the original guide-at-every-rest instruction), and
revisit only if a real case shows it causing a problem — or is this worth a general rule now? If the
latter, what should that rule look for?

**2. Hold-down — starting proposal, needs your correction.** A rest (`+Y`/`+Z`) alone can't resist
uplift; a hold-down is needed wherever something could lift the pipe off a rest — thermal bowing
between two rests on a long span, a relief-valve/safety-valve discharge reaction, or a point
specifically called out as needing a bidirectional restraint. v1 has no signal for any of these yet
(no valve/relief-load data parsed from the neutral file, no thermal-bowing calculation). **Proposed
MVP simplification**: don't infer hold-downs automatically at all for v1 — only emit one where the
input file already specifies uplift resistance is needed (if that's discoverable from `#$ RESTRANT`
or `#$ EQUIPMNT` data Conduit already parses), and leave genuine new-hold-down placement as a
post-MVP heuristic once there's a concrete case to design against. Is that an acceptable scope cut,
or is there a simpler v1-appropriate trigger I'm missing (e.g. "always pair a hold-down with a rest
within N mm of an equipment nozzle")?

**3. Line stop — starting proposal, needs your correction.** A line stop (`LIM`) restrains
movement along a specific axis at one point — typically needed near equipment nozzles (to keep
piping-induced load off the nozzle in a specific direction), at expansion joints (to force
directed movement rather than letting the joint wander), or where a branch needs to be held from
walking axially. v1 already has real nozzle data (`#$ EQUIPMNT`) driving the anchor heuristic.
**Proposed MVP simplification**: reuse that same nozzle-proximity signal for line stops instead of
(or alongside) anchors when a location is near a nozzle but doesn't need the *other* directions
restrained too — though this needs your judgment on when a nozzle-adjacent point should get a full
anchor vs. just a line stop, since v1's current heuristic always escalates to anchor near a nozzle
and has no criterion yet for choosing the lighter-weight option instead.

**Next step**: once you weigh in (or say "use the proposals as-is"), implement whichever of these
three isn't a scope cut into `SupportTypeClassifier`/`RestraintTypeMapper`/`SupportPlacer`, with
tests against a concrete synthetic scenario for each. No support-placement code changes for these
three specifically will be pushed ahead of your answer, per CLAUDE.md's reservation of this class
of decision — M1/M3 work continues in the meantime, unaffected.

## Fixed: tee detection switched from node degree to the real SIF/intersection pointer (2026-09-01)

User feedback on the M1 milestone, with a real test file attached specifically "to check tees":
"I think it is best to determine a tee by its tee/sif pointer, not by the actual geometry." Direct
instruction, not a question — implemented, not just logged.

**Investigated first, not guessed**: extracted `#$ SIF&TEES`'s real layout from
`NeutralFile-v15.pdf` (7 lines/42 values per tee-node record, item 1 = node number, item 11 of the
15-item `IEL` auxiliary-pointer array = "Pointer to Intersection Auxiliary field") and confirmed it
byte-for-byte against the user's attached file (committed as `fixtures/real-samples/NEWTEST.cii` —
same "safe to commit, shared for Conduit's own use" pattern as the three existing real samples).
Found exactly 4 intersection records (nodes 160, 1120, 1007, 895), each pointed to by
`AuxiliaryPointers[10]` on the element whose `ToNode` is that node — matching the same convention
as the existing bend pointer (index 0) and restraint pointer (index 3).

**Confirmed the user's point concretely, not just taken on faith**: cross-checked all 4 tee nodes
against the file's actual element connectivity. Only node 895 has real branch geometry (a genuine
third element, 895→1270, node degree 3) — the other three (160, 1007, 1120) are ordinary two-element
(degree-2) chains with *no branch pipe modeled in this file at all*. A node-degree-based guess (what
`SupportPlacer`/`OptimizationLoop` were using) would have missed 3 of the 4 real intersections
entirely. Full derivation in `docs/neutral-file/WALKTHROUGH.md`'s new `#$ SIF&TEES` section.

**Implemented**: `Element.IntersectionPointer` (new convenience property, `AuxiliaryPointers[10]`,
matching the existing `AllowableStressPointer`/`EquipmentCheckPointer` pattern). `SupportPlacer`'s
`RunNode.IsTee` and `OptimizationLoop.TryPickMidpointNode`'s tee exclusion both switched from
`nodeDegree > 2` to `element.IntersectionPointer != 0`. **Node degree is kept** for the *separate*
concern `SplitIntoRuns` uses it for (recognizing a genuine topological branch run to walk
independently, from this same session's earlier M1 fix) — a real branch needs its own span
accumulator whether or not it happens to carry SIF data, so that's a different question from
"should a support avoid this specific node," where the pointer is now authoritative.

Added `IntersectionPointerTests.cs` (parses `NEWTEST.cii`, confirms all 4 real pointers resolve
correctly, confirms 3 of 4 have no branch geometry, confirms an ordinary element has a zero
pointer) plus a `SupportPlacerTests` case that injects an intersection pointer onto an otherwise
perfectly ordinary synthetic element (no bend, no branch geometry) and confirms placement still
avoids it — verified this new test actually fails against the pre-fix node-degree logic (places a
rest right at the node) before confirming it passes with the fix. 95/95 tests passing (10 new).

**Next step**: none — this was a direct instruction with a concrete test case, not a question.
Reported the fix on the PR along with the requested MVP-vision restatement.

## Investigated: does the neutral file actually carry temperature/material data for hold-down/guide-cosine heuristics? (2026-09-01)

User's follow-up on the M2 proposals: "we are dependent on the temperatures and process parameters
in the CII file. Hold-down and guide spacing are highly dependent on the expansion of the pipe, so
we will need the material properties as well... it is just beam theory and SIF for the non-beam
elements." Rather than take this as purely a scoping statement, checked what's actually already
sitting in the parsed data.

**Good news: temperature is already parsed, just not exposed.** `#$ ELEMENTS`' 53-item real-value
block (`Element.RealValues`, already fully parsed) includes, per `NeutralFile-v15.pdf`: items 10-18
"Thermal Expansion Coefficient #N (or Temperature #N)" for N=1..9 (indices 9-17, 0-based) — one per
load case, matching B31.3's `T1`/`T2`/etc. temperature-case notation already documented in SPEC.md's
"Real load cases" section — and items 19-27 "Pressure #N" (indices 18-26) alongside them. Checked
real values across all 4 real samples (`44002.cii`, `TESTv15.cii`, `NEWTEST.cii`): every element
populates Temperature #1 with a plausible operating value (100/130/70 — presumably °F given the
files' apparent US convention) and Temperature #2 with a large negative value (-45/-101/-22.4) —
readable as an installation/reference-case temperature rather than the coefficient interpretation
(a real steel thermal expansion coefficient is on the order of 1e-5, not -22 to -101).

**The gap the user flagged is real, though**: Elastic Modulus (item 28), Poisson's Ratio (item 29),
and Pipe Density (item 30) are all `0.0` in every element checked across all 4 real samples — real
files rely entirely on the material database (`RRMAT` → external `.UMD` lookup)
for these, exactly like the already-known `#$ ALLOWBLS` situation. **No field in `#$ ELEMENTS`
carries the thermal expansion coefficient itself** — item 10-18 is confirmed (by the data, not just
the doc's "or") to hold *temperature*, not the coefficient, in every real file checked. Getting
actual pipe growth (`ΔL = α × ΔT × L`) needs `α` from somewhere Conduit doesn't currently reach —
same material-database gap already logged for allowable stress/EM/density, resolved there via a
documented A106 Grade B fallback constant (SPEC.md's "Resolved (2026-08-28)" entry) that has **no
thermal-expansion-coefficient counterpart yet**.

**Concrete proposal, grounded in what's actually available**: a genuine (if simplified) thermal-
growth model is more buildable than I'd assumed — not gated on parsing the `.UMD` database format
at all, just on picking a fallback `α` the same way the existing A106 Grade B constants were sourced
(a real, documented value for carbon steel, e.g. from the same Pipe Stress Engineering reference
already used for the span formula). Then: per element/run, `ΔL = α × (T1 - installation/ambient
temperature) × L` gives expected thermal growth; a guide's restrained direction should avoid
whichever direction a *downstream* run needs to grow into (the concern from the original loop-
specific discussion, generalized); a hold-down is needed where something (that same growth,
bowing between supports, etc.) could lift the pipe off a rest. This is real beam-theory-adjacent
work — closer to "the beginning of a simplified expansion-stress model" than a quick heuristic —
matching the user's own framing ("just beam theory and SIF for the non-beam elements").

**Question this raises, not yet answered**: is this now worth building as MVP scope, given the
data turns out to be more available than feared — or does building even a simplified thermal-growth
model belong in its own milestone (feeding M2's hold-down/guide-cosine items, rather than being
folded into the "quick starting proposal" framing M2 currently has)? SPEC.md's existing "Explicitly
OUT of scope" section already draws a boundary at "code-compliant... sustained/occasional/expansion
stress calculations" as future work — this proposal is narrower (growth/displacement only, not a
full code-compliance stress check) but is a real step in that direction, worth confirming explicitly
rather than assuming it's still "out of scope" or newly "in scope" either way.

**One more thing worth reusing rather than re-deriving**: the user's own UMAT1 material-database
printout (shared 2026-08-28, the source of the existing A106 Grade B allowable/yield/density/modulus
constants — see "Implemented: real A106 Grade B material..." above) was already confirmed, in that
same round, to carry a populated "thermal expansion coefficient" column generally — it just wasn't
extracted for material #107 specifically since that round only needed allowable/yield/density/
modulus. That PDF wasn't committed (not needed once the four values were extracted) and its
temporary attachment URL has long since expired, so re-extracting #107's actual α value needs either
a fresh copy of that printout or the user quoting the one field directly — cheaper than sourcing a
new constant from scratch.

**Next step**: posted this finding + proposal on the PR, asking directly whether to proceed with the
fallback-α approach now (material #107's α, re-obtained from the user rather than guessed) or to
formalize this as its own milestone first. Not implementing beam-theory/thermal-growth code until
this is confirmed, per CLAUDE.md's support-placement-logic consultation rule — this is squarely
"what makes a location need a hold-down/guide, and where."

## Found: B31.3-2024's own Appendix A/C tables can replace the single-material fallback entirely (2026-09-01)

User's reply to the above: real material-specific constants are a hard requirement — "no point in
creating an MVP that only handles a single type of material" — and mentioned a colleague hit the
same `UMAT1.UMD` wall and apparently reverse-engineered it from "some other data," details unknown,
not something the user wants to chase down by asking him.

**Didn't reverse-engineer anything — checked what's already sitting in `reference/` instead.**
`B31.3-2024.pdf` (already committed, confirmed non-proprietary) has two appendices that make the
whole `UMAT1.UMD` problem avoidable:
- **Appendix C, Table C-1**: "Thermal Expansion Data" — mean coefficient of thermal expansion,
  temperature range -200°C to 825°C, for a genuinely wide roster of material groups (Group 1 carbon/
  low-alloy steel, Group 2 other alloy steels, 5Cr-1Mo, 9Cr-1Mo, 12/13Cr, 15/17Cr, 27Cr, Group 3/4
  austenitic stainless, gray/ductile cast iron, several nickel alloys, and more) — organized by
  material *group*, keyed to real UNS numbers/spec families, not a proprietary numeric ID.
- **Appendix A, Table A-1**: "Basic Allowable Stresses in Tension for Metals" — same idea, organized
  by ASTM specification, with a "Specification Index" cross-reference.

Together these are a real, public, code-authoritative source for **exactly** the material-specific
data the thermal-growth model (and, properly, the existing allowable-stress fallback too) needs —
already temperature-dependent, matching the per-element temperature data already confirmed available
in `#$ ELEMENTS`. This sidesteps `UMAT1.UMD`'s undocumented format entirely; no reverse-engineering,
no need to trouble the colleague.

**The one piece still needed**: B31.3's tables are keyed by material *specification* (e.g. "A106
Grade B") or *group*, not CAESAR's internal numeric `RRMAT` material ID. The only mapping from
CAESAR's numeric ID to a real material name Conduit has seen so far is the single one already
extracted ("material #107 = A106 Grade B", from the user's UMAT1 printout a few rounds back). To
support more than one material, Conduit needs that same ID→name mapping for whichever materials the
user's real files actually use — either more of that printout (just the ID/name listing this time,
not the property columns, since those now come from B31.3 instead), or the user naming the specific
materials/RRMAT IDs that matter for their real work.

**Scope honesty**: this is a real, meaningfully-sized piece of engineering — parsing/embedding
B31.3's own tables (large, temperature-interpolated), building the material-group classification,
and wiring it through `SpanLimitCalculator`/the new thermal-growth model — not a quick fix, but a
concrete, buildable one now that the data source is known and public.

**Next step**: posted this on the PR. Waiting on which materials/RRMAT IDs to support (or a fresh
ID/name listing from the UMAT1 printout) before building the table-driven material system — the
table *extraction and structure* work itself could start once that's confirmed, without needing
anything further from the user.

## Implemented: MaterialLibrary placeholder + real placement-driver framework recorded (2026-09-01)

User's reply: confirmed real material-specific constants (allowable, elastic modulus, thermal
expansion coefficient, Poisson's ratio — "some of these can be calculated") are needed, sourced
from the UMAT1 printout ("all materials are in the umat1.pdf print out with the corresponding
number and properties"). Also gave the actual engineering framework this is all in service of:
**rest positioning is mainly governed by sustained stress; horizontal guide/hold-down spacing is
mainly governed by expansion stress; vertical guide spacing is mainly governed by vibration (but
may also be expansion-governed).** Explicitly authorized deferring the exact heuristics: "these
heuristics may be determined later. Set a placeholder for this currently if required."

**Recording the framework here so it doesn't need re-deriving**: this is real, valuable design
guidance beyond what SPEC.md's original span/utilisation proxy captures. Sustained stress (rests),
expansion stress (horizontal guides/hold-downs), and vibration (vertical guides) are three distinct
physical drivers needing three different calculations — none of which `MockStressSolver`'s current
simplified span check attempts. Not implementing any of the three calculations yet (vibration
analysis in particular is a substantial, separate topic from static stress) — logged as the target
architecture for when M2's hold-down/guide items actually get built out.

**Implemented the placeholder, per direct instruction**: `MaterialLibrary`
(`src/Conduit.Core/Heuristics/MaterialLibrary.cs`) — a `MaterialProperties` record (allowable
stress, elastic modulus, density, thermal expansion coefficient, Poisson's ratio) resolved by
CAESAR's own numeric material ID (`#$ MISCEL_1`'s `RRMAT`, already parsed but never previously
consulted for this). `SpanLimitCalculator.ComputeMaxSpan(NeutralFile, Element)` now resolves the
*element's own* material via this library instead of always falling back to the same hardcoded A106
Grade B constants regardless of what the file's `RRMAT` actually says. Only one material (#107,
A106 Grade B) has real data so far — every other ID currently falls back to it, identical to the
previous behavior for every existing test/fixture (confirmed: 98/98 tests passing, and the CLI's
output on `loop-2d.cii` is byte-identical to before this refactor, same 10835.70 mm max span). This
is the "placeholder" — the *mechanism* is real and ready to grow; the *data* isn't guessed.
`ThermalExpansionCoefficientPerDegreeCelsius`/`PoissonsRatio` are explicitly `null` (never
extracted from the UMAT1 printout, not guessed) rather than a fabricated number, since this is
safety-relevant engineering data.

**What's still needed from the user**: the UMAT1 printout was reviewed once, several rounds back,
but never committed (its temporary attachment link has since expired) — re-extracting anything
beyond material #107's four already-recorded values needs either a fresh copy of it, or the
specific materials/RRMAT IDs that matter for the user's real work (per the still-open ask in the
"Found: B31.3-2024's own Appendix A/C tables..." entry above). Also asked for clarification on
"some of these can be calculated" — which properties, and by what relationship (e.g. Poisson's
ratio from E and shear modulus, if that's what's meant) — rather than guessing.

**Next step**: posted this on the PR. `MaterialLibrary`'s data stays at one material until the
printout (or specific material list) comes back — the mechanism doesn't need anything further to be
useful going forward (every new material is now just one more dictionary entry, once its values are
verified).

## Corrected: material #107 was never A106 Grade B — it's A135 Grade A (2026-09-02)

The user uploaded their real UMAT1.pdf printout (1,708 pages, CAESAR II Material Data Base v4.20,
COADE-supplied). Extracted with `pdftotext -layout` and searched directly.

**Question 1, answered — Poisson's ratio doesn't need calculating.** The user asked: "I'm not sure
about calculating the poisson ratio as I am not confident the shear modulus is in the material
data. You may check this." Checked:
- `grep -i "shear modulus\|shear mod"` → zero matches anywhere in the 1,708 pages.
- `grep -i "poisson"` → many matches, always a direct field, e.g. `POISSONS RATIO: 0.2920 MIN TEMP
  CURVE: -`.

So the user's suspicion was correct: shear modulus isn't in the data at all. But that's fine —
Poisson's ratio doesn't need deriving from it, because it's already a direct per-material lookup
value in the printout. No calculation required.

**Question 2, found unprompted while answering Question 1 — material #107 was wrong all along.**
While looking up material #107 (recorded in the codebase since 2026-08-28 as "ASTM A106 Grade B")
to cross-reference its Poisson's ratio, its actual printout entry read:

```
NUMBER: 107                             NAME:   A135 A
APPLICABLE PIPING CODE:    0
DENSITY kg/m3     :       7833.4399     COLD MODULUS MPa        : 0.2034E+06
POISSONS RATIO:              0.2920     MIN TEMP CURVE:      -
```

`grep -n "MATERIAL.*107\|NUMBER: 107"` confirms this at 7 separate locations across the printout's
repeated per-piping-code sections (`A135 A` and `A135 Grade A` both appear — same material, the
naming just varies slightly by code section). ASTM A135 is an electric-resistance-welded steel pipe
spec — a real, different material from A106 (seamless carbon steel pipe for high-temperature
service).

Searched for where A106 Grade B actually lives: `grep -n "A106"` shows it consistently at
**material #106** (`NUMBER: 106  NAME: A106 B` / `A106 Grade B`, same 7-location pattern). The
earlier round's extraction was a plain off-by-one — caught only because CLAUDE.md requires
re-verifying against the primary source (the printout itself) rather than trusting a prior round's
recorded number, which is exactly the situation this rule exists for.

**What was actually still correct**: density (7833.4399 kg/m³) and cold elastic modulus
(203,400 MPa) turn out to be identical for materials #106 and #107 in this printout — so those two
values, despite being extracted under the wrong material number, were numerically right for A106
Grade B all along by coincidence (both are generic carbon-steel physical constants CAESAR reuses
across several materials in its database, not something specific to material #107).

**What was wrong**: the allowable stress (118 MPa). Tracing where it came from: the printout
repeats every material once per an internal numeric "APPLICABLE PIPING CODE" ID (0, 1, 3, 4, 5, 8,
10, 12, 13, 24, 29, 32, 33, 37, 38, 39, 40, 43, 44...) — CAESAR's own code-selection list, in an
order not documented anywhere in the 1,708-page printout (no legend maps a number to a named code
or edition). Material #106's code-"1" section shows: Poisson's ratio 0.3000 (not 0.2920 as in the
generic code-0 section), and an allowable-stress column populated at 118 MPa flat from -29°C to
343°C, then declining (108 MPa at 371°C, 90 at 399°C, 74 at 427°C). That's where 118 MPa came from —
copied from this unidentified code-1 section.

Cross-checked against `reference/B31.3-2024.pdf`'s own Table A-1 (the code Conduit defaults to,
`CaesarConfig.DefaultAssumedCode = B31.3_2024`). Table A-1's material-listing page unambiguously
assigns A106 Grade B to **Line 33** (matching its min tensile 415 MPa / min yield 240 MPa exactly).
Line 33's allowable-stress curve: 138 MPa flat from -196°C to 200°C, then declining (132 at 250°C,
126 at 300°C, 122 at 325°C...) — a materially different curve from UMAT1's code-1 section (138 vs
118 MPa at ambient, and the "flat until" temperature differs: 200°C vs 343°C). **Code "1" in UMAT1
is not B31.3-2024** — it's some other code or an older B31.3 edition (allowable stresses were
revised between editions), and without a legend there's no reliable way to identify which.

**Decision (decide-and-proceed — reversible, internal, already implied by CaesarConfig's own
B31.3-2024 default)**: allowable stress is now read from `reference/B31.3-2024.pdf`'s Table A-1
directly rather than any UMAT1 piping-code section, since (a) it's the code Conduit actually
targets by default, (b) it's unambiguous and code-authoritative, and (c) UMAT1's numeric code IDs
can't currently be identified with confidence. Physical properties (density, elastic modulus,
thermal expansion coefficient, Poisson's ratio) stay sourced from UMAT1's generic code-0 section,
since those are genuine material physical constants (confirmed identical across every code section
for the same material), not code-specific design limits. Poisson's ratio specifically uses 0.30
(the code-1 section's value, and carbon steel's conventional textbook value) rather than code-0's
0.292 — a minor, non-safety-critical rounding choice between two real values from the same
document, logged as an assumption.

**Also added a second real material while this was open**: material #107 (A135 Grade A) is now a
genuine second `MaterialLibrary` entry (was previously *also* incorrectly labeled A106 Grade B,
since everything resolved to the single placeholder record) — density/modulus from UMAT1 material
#107, allowable stress cross-checked against B31.3-2024 Table A-1 Line 12 (110 MPa, confirmed via
its own min tensile/yield match). This is a real, if small, step toward "no point in creating an
MVP that only handles a single type of material."

**Corrected**: `MaterialLibrary.A106GradeBMaterialId` 107 → 106, `AllowableStressMpa` 118.0 → 138.0,
`SpanLimitCalculator.DefaultAllowableBendingStressMpa` likewise, `ThermalExpansionCoefficientPerDegreeCelsius`
and `PoissonsRatio` populated with real ambient values (1.0925e-5 /°C, 0.30) instead of `null`. One
test (`UnsplittableElement_IsStillReportedRatherThanLoopedForever`) needed its deliberately-extreme
density constant increased (5,000,000 → 50,000,000 kg/m³) to keep exercising the "genuinely
irreducible failure" edge case now that the corrected (higher) allowable stress raises the computed
max span slightly — not a logic change, just re-tuning a fixture constant to the new number. 98/98
tests passing.

**Next step**: none blocking — this was a self-contained correction, not something needing the
user's input. Reply posted on the PR summarizing both findings. The still-open ask from the
previous entry (which further materials/RRMAT IDs matter for the user's real work) stands.

## Implemented: all 399 materials in `MaterialLibrary` (2026-09-02)

Direct instruction: "I would like to have all the materials in the database." Previously
`MaterialLibrary` had exactly 2 entries (A106 Grade B, A135 Grade A), both hand-verified line by
line against the printout and against `reference/B31.3-2024.pdf`. Extending that same
one-at-a-time verification to all 399 materials in the printout isn't feasible by hand — so this
had to be a real extraction pipeline, built and verified carefully rather than trusted blindly.

**Extraction approach**: parsed `reference/pipe-stress-engineering/UMAT1-material-database.pdf`
(confirmed byte-identical, via md5sum, to the user's fresh 2026-09-02 upload — this was never a
missing-source problem) via `pdftotext -layout`, then regex-extracted every material's
`APPLICABLE PIPING CODE: 0` record (the code-independent, generic physical-property listing every
material has exactly once) — NUMBER, NAME, DENSITY, COLD MODULUS, POISSONS RATIO, and the EXP
COEFF value at the temperature row closest to 21°C (ambient). 399 materials extracted, numbered
1-17 and 101-501 (CAESAR reserves the low numbers for generic material classes — LOW CARBON, HIGH
CARBON, ALUMINUM, etc. — and the 100s+ for specific ASTM/EN/DIN-numbered specs).

**Two real extraction bugs found and fixed during verification** (both caught by sanity-range-
checking every value rather than trusting the first pass):
1. **Blank-cell column shift.** My first pass took "the first two numbers on a temperature row" as
   (temperature, EXP COEFF) — but when EXP COEFF is blank at a given temperature (common at
   temperature extremes), the next populated column (MODULUS) shifts into that position instead.
   This produced one wildly wrong value (material #395's "expansion coefficient" came out as
   196,510 — obviously not a per-°C coefficient). Tried fixing this with header-relative column
   position slicing next; that was *also* unreliable, since the printout's header-label position
   and its data column's actual position don't consistently align in the `pdftotext -layout`
   output. The robust fix: EXP COEFF and MODULUS are structurally distinguishable regardless of
   column position — EXP COEFF is always scientific notation with a *negative* exponent
   (`~1e-5`/°C), MODULUS always a *positive* exponent (`~1e5` MPa), and ALLOWABLE/YIELD/UTS are
   always plain integers with no exponent at all. Matching on that pattern instead of position
   fixed it cleanly, confirmed by re-checking all 399 values landed in a sane range afterward
   (zero were physically implausible after the fix, versus one at the far end of physically
   impossible before it).
2. Not a bug in the extraction — a real data-quality issue *in the source printout itself*,
   confirmed by reading the raw text directly: materials #9 (WROUGHT IRON) and #12 (K-MONEL) both
   show `COLD MODULUS MPa: -0.1010E+01` (-1.01 MPa) — an unmistakable "not populated" sentinel in
   CAESAR's own database, not a real modulus. Recorded as `null` for these two rather than
   embedding a negative elastic modulus.

**Verification, beyond the sanity-range check**: re-confirmed the pipeline against the two already
hand-verified materials (#106, #107 — exact match), then deliberately checked entries with
distinctive expected values the extraction logic was never tuned against — aluminum (#14: 71,020
MPa modulus, 2,804 kg/m³ density — both match real aluminum, unlike anything in the carbon-steel
data the earlier rounds worked with) and copper/stainless/cast-iron entries, all physically
plausible. This is real evidence the extraction generalizes rather than being curve-fit to the
two known-good rows.

**What's deliberately still `null`: allowable stress for 397 of 399 materials.** Allowable stress
is a design-code limit, not a material property — it can't be read out of UMAT1 at all (its
numeric "applicable piping code" IDs have no legend, per the previous entry's finding), only
cross-referenced by material name against a code's own table (as was done individually for #106,
#107 against B31.3-2024 Table A-1). Doing that for all 399 means either building a full
programmatic join against B31.3-2024's own ~110-page Table A-1 (parsing both its "material
listing" pages — Line No./Spec/Grade — and its "stress value" pages — Line No./temperature
columns — and joining by name), or 397 more individual by-hand lookups. Neither was attempted this
round: the join is real, separate, and non-trivial scope (Table A-1 spans many page-pairs with a
locally-scoped "Line No." that resets per pair, which needs careful, verified parsing of its own —
not something to rush given this is exactly the kind of table-misalignment mistake that caused the
#106/#107 mixup in the first place); the by-hand route doesn't scale. `SpanLimitCalculator` falls
back to material #106's real allowable stress for anything without its own.

**Also revised**: Poisson's ratio for #106/#107 now uses the same code-0 source as the other 397
materials (0.292), rather than the previous round's 0.30 from an unidentified per-code section —
for consistency now that the library isn't just those two entries anymore. Both are real values
from the same document; the difference doesn't matter for beam-theory spacing purposes.

**Also revised**: material names are now verbatim from the printout (e.g. `"A106 B"`, not "ASTM
A106 Grade B") across all 399 entries, including the two that previously had an expanded name —
expanding all 399 to fully-qualified names isn't safely automatable at this scale (would need
per-material judgment), so consistency won this round; a name-expansion lookup is a possible
future addition.

102/102 tests passing (4 new: aluminum spot-check, the two invalid-modulus materials, the
"most materials have no allowable stress" honesty check). All three real fixtures produce
byte-identical output to before this change.

**Next step**: none blocking. The B31.3-2024 Table A-1 programmatic join (to get real,
code-authoritative allowable stress for the other 397 materials) is real follow-up work, not
started — flagged here rather than in a fresh entry since it's a direct continuation of this one.
Also still open: which specific materials/RRMAT IDs matter most for the user's real projects, in
case that should prioritize which get the Table A-1 join first once it's built.

## Corrected: -1.01 is CAESAR's general null-value sentinel, not just an odd UMAT1 entry (2026-09-02)

Direct instruction, in response to the previous entry describing materials #9/#12's `-1.01` cold
modulus as "an obviously invalid sentinel": "Actually the -1.01 in Caesar is the Caesar null value.
This is always the case for the neutral file." Two things followed from this:

1. **Confirmed my handling was already right, for the wrong stated reason.** Setting
   `ElasticModulusMpa` to `null` for materials #9/#12 was correct — now documented precisely as
   "CAESAR's own null convention" rather than "an obviously invalid value I happened to notice."
   Updated `MaterialLibrary`'s class doc comment accordingly.
2. **Checked whether the same literal sentinel appears anywhere Conduit already reads real-value
   fields from an actual `.cii` neutral file** (not just the UMAT1 printout) — since the user's
   statement is general ("always the case for the neutral file"), not UMAT1-specific. Wrote a
   small throwaway probe (`NeutralFileReader` against all 3 real `.cii` fixtures, printing
   `RealValues[27]` through `[31]` — elastic modulus, Poisson's ratio, pipe/insulation/fluid
   density) and grepped all 4 real `.cii` samples' raw text for the literal pattern
   `-1.010000E+00` (the fixed-width rendering `-1.01` would take in that format). Neither found it:
   the real fixtures use `0.0` for an unset elastic modulus/pipe density (already correctly
   guarded by `SpanLimitCalculator`'s existing `is > 0` checks), and their insulation/fluid density
   fields carry real positive values (184.21 kg/m³ default insulation density even with zero
   insulation thickness, since the area term is zero either way; 1000/1050/100 kg/m³ for fluid
   density — water/brine/steam-condensate-plausible values, nothing resembling a sentinel).

**Found a real, if latent, gap while checking this — fixed defensively.**
`SpanLimitCalculator.ComputeWeightPerLengthNewtonsPerMillimetre` read `RealValues[30]`
(insulation density) and `RealValues[31]` (fluid density) directly, with no guard at all — unlike
pipe density and elastic modulus, which already have an `is > 0` fallback. Had a file ever carried
the `-1.01` sentinel there, the formula would have silently *subtracted* weight (a negative density
times a positive area), producing a smaller computed weight per length and therefore a *larger*
(unconservative, unsafe) allowable span. Not observed in any of the 4 real fixtures on hand, but
the user's statement is that this convention is general, not tied to what happens to be in these 4
files — so hardened it regardless: both fields now clamp to zero (`Math.Max(x, 0)`) rather than
trusting a negative value. Zero is itself a legitimate real value for these two specific fields (no
insulation; an empty/gas-filled bore), unlike pipe density/elastic modulus which are never
legitimately zero for a real steel pipe — so the fix is "treat a negative reading as no
contribution," not "substitute a material default," matching the different physical meaning of
these fields. Added a regression test
(`ComputeMaxSpan_TreatsNegativeInsulationOrFluidDensityAsZero_NotANegativeContribution`) — verified
it fails against the pre-fix code (10837.51 mm vs. the correct 10835.70 mm, i.e. the sentinel was
silently inflating the computed max span) and passes after. Documented the general `-1.01`
convention in `docs/neutral-file/WALKTHROUGH.md`'s "File-level rules" section so future work
parsing a new real-value field for the first time checks for it up front, rather than this being
tribal knowledge that only lives in one class's doc comment.

103/103 tests passing.

**Next step**: none blocking. Worth keeping in mind for future neutral-file parsing work
(`#$ FORCMNT`, `#$ DISPLMNT`, and the still-opaque `#$ SIF&TEES` stress-magnitude fields, when any
of those get parsed) — check any newly-consumed real-value field against this convention rather
than assuming a real file always populates every column.

## Evaluated: the colleague's CAESAR II 15.1 result-database approach (2026-09-02)

M4's first bullet ("document and evaluate the colleague's GUI-automation approach... as an
alternative or complement to `CaesarComStressSolver`'s COM path") is design/decision work that can
happen without a real CAESAR II install, so worked it while the material-data correction above was
otherwise the only unblocked item. The user's 2026-08-31 PR comment: "A colleague of mine has
created a program that automatically runs Caesar... Caesar v15.1 now creates a database with all
the output information."

**Checked whether Conduit already has documentation of this**: grepped all four Static/output-
related vendor PDFs already in `reference/` (`New-Analysis-Reviewer-Help.pdf`, `Output-Tab.pdf`,
`Static-Analysis-Help.pdf`, `Static-Analysis-Output-Help.pdf`) for "database" — zero matches across
all four. What they *do* document (already reflected in SPEC.md's "Caesar II abstraction" section,
the "revised plan for `CaesarComStressSolver`"): the two interactive GUI reviewers (Classic Static
Output Processor, modern New Analysis Reviewer) and their save-to-ASCII-text/PDF report mechanisms
— no mention of a queryable results database file anywhere in this documentation set.

**This is the same situation as `UMAT1.UMD` and `iecho.exe`'s exact invocation contract**: a real
mechanism the user has independent knowledge of (via their colleague) that isn't covered by any
documentation Conduit currently has access to. Two ways forward, neither of which is guessing at an
undocumented binary/database format:
1. **Ask the user for more detail**, if they're willing — even without troubling the colleague
   directly (per their earlier "not natural to ask him either" on a different but related point):
   what kind of database (a `.mdb`/Access file? SQLite? SQL Server? a proprietary CAESAR format?),
   roughly where CAESAR II 15.1 writes it relative to a job's `.cii` file, and ideally a sample or
   its schema/table list if the colleague's tool has ever printed one.
2. **Stay on the already-documented ASCII-report path** (SPEC.md's existing "revised plan") as the
   concrete, buildable-now direction, since it doesn't require reverse-engineering anything — a
   custom Report Template is authored once, its column layout is fixed and known, and CAESAR II's
   own "Send to Text File" mechanism is fully vendor-documented. The results-database route, if its
   format becomes available, could still complement this later (structured parsing is strictly
   easier than ASCII-report parsing once the schema is known) rather than replace it.

**Not deciding between them unilaterally** — this is exactly the kind of external/Windows-dependent
choice M4 flags as "needs the user's environment," and path 1 requires the user's own judgment
about troubling their colleague, which isn't Conduit's call to make. Recording this as a genuine
finding (there's nothing more to extract from the docs already available) rather than a
placeholder.

**Next step**: waiting on the user — either more detail about the database (if they're willing to
ask), or an explicit "just build the ASCII-report path, skip the database idea for now." No code
changes attempted in the meantime, since either path only touches `CaesarComStressSolver`'s skeleton
which nothing else in the codebase depends on.

## Investigated, not shipped: B31.3-2024 Table A-1 join for the other 397 materials' allowable stress (2026-09-02)

Continuing the flagged follow-up from the "all 399 materials" entry above: a programmatic join
against `reference/B31.3-2024.pdf`'s own Table A-1 (SI units), to get a real, code-authoritative
allowable stress for the 397 `MaterialLibrary` materials that currently have `null`. Built a first
version, found a real failure in it via cross-checking against the two already-known-correct
answers, and am **not shipping it** — writing this up so a future attempt starts from what's
already known rather than repeating the same mistake.

**Table structure, mapped precisely this time**: Table A-1 (SI units) spans a contiguous
110-page range (pages 206-315 of the extracted text) alternating two page types — a "LISTING"
page (Spec/Grade/min-tensile/min-yield, headed "Nominal Composition... Line No.") and a "STRESS"
page (the temperature-vs-allowable-stress table, headed "Basic Allowable Stress, S, MPa"). Found
and fixed a page-classifier bug along the way: my first classifier required the literal substring
"Product Form" to detect a LISTING page, but that header wraps across two lines ("Product" / "Form")
on some pages depending on column width for that material category, so 8 real LISTING pages were
silently misclassified as "OTHER" until the classifier was relaxed to just "Nominal" + "Composition"
+ "Line".

**The real problem: "Line No." is a *local* index that resets at unpredictable points**, not a
global one — confirmed empirically, not assumed. The very first block (Lines 1-160) spans multiple
LISTING/STRESS page pairs *without* resetting, but a new block can start immediately after
(Line numbering restarting at 1 for a new material category — low-alloy steel, stainless, etc.).
Wrote a "block" grouper to track these boundaries, but the boundaries didn't cleanly self-identify
(a stress page's row count came up short compared to its paired listing page on some pages, and
one block ended up merging two clearly-unrelated line-number ranges — `[1, 3, 4]..[122, 240, 241]`
in one "block," which is obviously two separate blocks incorrectly joined).

**Tried a second approach — nearest-page-adjacency instead of block-grouping** (for each LISTING
row, search outward from its own page index for the nearest STRESS page containing a value at that
same line number) — and it's *worse*, confirmed by the exact regression-testing discipline this
project uses everywhere else: check it against the two already-verified ground-truth answers before
trusting it on anything new.
- **A106 Grade B (Line 33)**: joined value **138 MPa** — correct, matches the hand-verified value
  from the previous round exactly.
- **A135 Grade A (Line 12)**: joined value **25 MPa** — **wrong**. The hand-verified value (also
  cross-checked in the previous round, independently, against the same document) is **110 MPa**.
  The nearest-adjacency search picked up a *different* material's "Line 12" from an unrelated
  earlier table page — coincidentally reusing the same local line number — rather than the correct
  one two pages later.

**This is exactly the failure mode the earlier material-#107 mixup exemplified, caught before
shipping instead of after**, because the join was checked against known-correct anchors rather than
trusted on the strength of "the code ran without an error." One of two anchors silently gave a
wrong answer — that's a 50% observed failure rate on the only ground truth available, nowhere near
reliable enough to trust for the other 397 materials sight-unseen. **Not fixing the join further
this round** — a reliable version needs real block-boundary tracking (a block starts when a new
LISTING page's lowest line number is not a plausible continuation of the previous block's highest,
verified against multiple signals, not just page proximity) plus a stronger, independent
cross-check for every single joined material before trusting it, e.g. comparing UMAT1's own
YIELD/UTS-by-temperature data (already extracted for all 399 materials) against Table A-1's
Min. Yield/Min. Tensile Strength columns for the same (Spec, Grade) — a real correctness signal
beyond "the name matched," costing nothing since both datasets are already extracted.
`MaterialLibrary` stays as it is (397 materials honestly `null`, falling back to material #106) —
no code changes this entry, by design.

**Next step**: none blocking. This is real, scoped follow-up work for whenever it's picked up again
— the next attempt should start from "track block boundaries explicitly and cross-validate every
join against UMAT1's own yield/tensile data," not from a fresh reading of the table.

## Shipped: real B31.3 allowable stress for 200 materials, by identifying UMAT1's own B31.3 code section (2026-09-03)

Picked this back up ("go through and make sure to make the agreed changes") and found a
**fundamentally more robust route than parsing the B31.3-2024 PDF's table geometry at all** — which
is what the previous entry got stuck on. The insight: UMAT1 itself already contains allowable
stress for every material, tabulated once per an internal numeric `APPLICABLE PIPING CODE` ID. The
problem was never that the data is missing — it's that the code IDs are unlabeled. So instead of
going to a different document (the PDF) and fighting its layout, **identify which UMAT1 code number
IS B31.3**, then read every material's allowable from that one internally-consistent section.

**How the code section was identified and verified** (nine independent confirmations, no
PDF-geometry parsing in the final pipeline):
1. For each numeric code, read the ambient allowable of the two independently hand-verified anchors
   (#106 A106 Grade B = 138 MPa, #107 A135 Grade A = 110 MPa — both confirmed against
   `reference/B31.3-2024.pdf` Table A-1 in prior rounds). Exactly three code sections match *both*
   anchors: codes **3, 50, 63** (plausibly consecutive B31.3 editions — 2018/2020/2022/2024). All
   other codes give different values (code 1 = 118/81, code 33 = 241/207, etc.).
2. The three matching codes **never disagree** on allowable where they overlap, and code **3** is
   the widest — codes 50 and 63 are strict subsets of code 3's material set. So code 3 is the
   single most complete B31.3 representation in the database.
3. Cross-checked code 3 against the B31.3-2024 PDF at **seven further materials** by name (not by
   the fragile line-number join — I read the PDF's block-1 listing/stress page pair, which pairs
   cleanly, and matched by spec+grade): A53 A (110), A53 B (138), A106 A (110), A135 A (110),
   A135 B (138), A333 1 (126), A333 6 (138). **All seven matched code 3 exactly.** Two anchors +
   seven = nine independent confirmations that code 3 is B31.3(-2024-consistent).

**Result**: `MaterialLibrary`'s allowable stress now comes from UMAT1 code section 3 for the
**200** materials B31.3 lists (the ASTM carbon/low-alloy/stainless specs), up from just 2. The
values for #106/#107 are unchanged (138/110 — code 3 agrees with the PDF, as it must). The
remaining ~199 materials are genuinely not in any B31.3 code section — EN/DIN/JIS specs (1.4301,
STPG370, etc.) that live under EN 13480 or JIS codes, plus a few CAESAR ASTM duplicate entries only
tabulated under other codes (e.g. #153 A312 304, whose canonical twin #155 A312 TP304 *is* covered
at 138 MPa). Those stay `null` and fall back to material #106, exactly as before.

**Why this is trustworthy where the PDF-geometry join wasn't**: the join failed because it
guessed a material's identity from page-adjacency of an ambiguous local line number. This approach
never guesses identity — it reads each material's own record by CAESAR material number (unambiguous)
from a single code section whose identity is pinned by nine name-matched B31.3 values. There's no
line-number-reset problem because there are no line numbers involved; the join key is CAESAR's own
material number, which is globally unique.

108/108 tests passing (added parameterized tests: four B31.3 materials get their real allowable,
two non-B31.3 materials stay null). All three real fixtures still produce byte-identical output
(they use material #106, whose 138 MPa was already correct). CLI max-span unchanged at 10835.70 mm.

**Next step**: none blocking. A possible future refinement — temperature-dependent allowable (code 3
has the full curve, not just the ambient value) — is still deferred; the MVP uses the cold/ambient
value, matching how `#$ ALLOWBLS`'s cold allowable is already consumed.
