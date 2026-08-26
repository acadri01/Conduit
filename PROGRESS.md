running status log Claude appends to (skim this from mobile)

- 2026-08-20: Phase 1 complete. Wrote SPEC.md (Stage 1 support-optimisation MVP: C#/.NET 8,
  synthetic neutral-file format, IStressSolver abstraction with MockStressSolver for v1 and a
  CaesarComStressSolver skeleton for later) and setup.sh (installs .NET SDK, builds/tests if a
  .sln exists). No application code written yet — that's Phase 2. Assumptions logged in
  QUESTIONS.md.
- 2026-08-21: User supplied the official Hexagon CAESAR II neutral file documentation (v15) plus
  four real `.cii` project files. Rewrote SPEC.md's "Neutral file format" section to target the
  real, official `.cii` structure (fixed-width columnar records, `#$ VERSION`/`#$ CONTROL`/
  `#$ ELEMENTS`/`#$ AUX_DATA` incl. `#$ RESTRANT`, opaque round-trip of unmodeled sections)
  instead of the earlier invented synthetic format. The real sample files were reviewed locally
  to validate the spec but were not committed (clean-room constraint) — v1 fixtures will be
  freshly authored synthetic `.cii` files instead. setup.sh unchanged (still just a headless
  .NET bootstrap; no new dependency from adopting the real format, since it's a plain-text
  parser with no vendor SDK). Reasoning logged in QUESTIONS.md. No application code written yet.
- 2026-08-21: User supplied CAESAR II 15.1 "Output Tab" and "New Analysis Reviewer Help" docs.
  These cover results review/reporting only (GUI reviewers, PDF/Word/Excel export) — no
  batch-parseable results file format is documented, which confirms (rather than changes) the
  existing `CaesarComStressSolver`-via-COM design; added a note to SPEC.md explaining why, plus
  the list of piping codes CAESAR II 15.1 supports (for citing which B31.3 edition v1's
  heuristics approximate). No change to the `.cii` input format already documented — these docs
  don't touch it. No application code written yet.
- 2026-08-21: User supplied CAESAR II 15.1 "Static Analysis Help" and "Static Analysis Output
  Help" docs (the latter read through the Standard Reports section, ~40 of 76 pages). These
  corrected the earlier "no parseable results format" claim: CAESAR II can save standard reports
  (Code Compliance, Restraints, Displacements, Stresses) to ASCII text files, and a custom Report
  Template gives a stable per-field column layout. Revised SPEC.md's `CaesarComStressSolver` plan
  accordingly (drive analysis via COM, emit reports to text files via a custom template, parse
  those files) and documented the real load-case/stress-type model (OPE/SUS/EXP/OCC/FAT/etc. +
  combination methods) as context for future non-mock stress work. `MockStressSolver`'s v1 scope
  is unchanged. No application code written yet.
- 2026-08-21: User shared two Python files (iecho.py, lift_case_builder.py) from a different
  internal project, for requirements context only (not copied, not committed). Clarified that
  real production files are CAESAR II's native `.C2`/`._A` format, not `.cii` — `.cii` is purely
  an interchange format. Added a new `INeutralFileConverter` interface + `IechoConverter`
  skeleton to SPEC.md (same treatment as `CaesarComStressSolver`: not implemented/tested here,
  deferred to Windows), so users won't have to run `iecho.exe` by hand once it's built. Flagged
  an open question about whether iecho's `.C2`→`.cii` export direction can be silent or needs
  interactive-launch-and-poll, per an asymmetry seen in the reference implementation. v1's CLI
  still only accepts `.cii` directly. No application code written yet. PR #1 already merged, so
  restarted this branch from the current `main` (same content, no reset needed) before this
  commit.
- 2026-08-21: Phase 2 complete. Built the full C# solution per SPEC.md: `Conduit.Core` (real
  `.cii` fixed-width parser/writer with byte-identical round-trip for untouched sections;
  `RestraintType` enum; `SpanLimitCalculator` beam-theory span formula; `SupportTypeClassifier` +
  `SupportPlacer` walking runs between anchors; `MockStressSolver`/`CaesarComStressSolver`
  skeleton behind `IStressSolver`; `IechoConverter` skeleton behind `INeutralFileConverter`;
  `OptimizationLoop` iterate-and-adjust), `Conduit.Cli` (`conduit optimize <in> <out>`), and
  `Conduit.Tests` (22 xUnit tests: round-trip, span heuristic, classifier, placer, loop). Authored
  two synthetic `.cii` fixtures (`straight-run.cii`, `run-with-riser.cii`) plus `malformed.cii`
  for the parse-error path, all via a shared `NeutralFileFixtureBuilder` test helper (built once,
  used to generate the committed fixtures and directly in unit tests). Fixed `setup.sh` to install
  the .NET SDK via `apt` (the `dot.net` install script is blocked by this sandbox's egress proxy).
  Verified end-to-end: `setup.sh` → `dotnet build`/`test` clean, `conduit optimize` runs on both
  fixtures and produces a diff confined to the `#$ CONTROL` restraint count and appended
  `#$ RESTRANT` records, and the malformed fixture fails fast with no output file written. Design
  decisions/assumptions logged in QUESTIONS.md (notably: span-limit as a documented formula
  rather than a recited table; spring-candidate as a loop escalation, not an initial placement
  rule; mandatory guides on vertical risers, found via a failing test).
- 2026-08-21: Responded to PR #3's architectural review. Parsed `#$ ALLOWBLS` (allowable stress,
  linked via each element's pointer array) and wired the real cold allowable stress into
  `SpanLimitCalculator` in place of the placeholder constant; parsed `#$ MISCEL_1`'s `RRMAT`
  material-ID array onto `NeutralFile.MaterialIds`; parsed `#$ EQUIPMNT` nozzle/load-limit
  records and used real nozzle node positions (when present) as `SupportPlacer`'s near-equipment
  signal instead of the run-endpoint-fraction proxy. Removed the "mandatory guide at every
  vertical segment's start" rule the reviewer flagged as unsound (breaks on short verticals);
  reverted to classifying only the element that actually triggers the span-overflow check, with
  the resulting gap (a short riser not always getting its own guide) documented as a known
  limitation pending element-splitting. Corrected the restraint taxonomy per review: rest is
  one-directional `+Y`/`+Z` (not bidirectional `Y`), hold-down is the opposite one-directional
  restraint, guide/line-stop/anchor per `GUI`/`LIM`/`ANC`, via a new `RestraintTypeMapper`.
  Corrected SPEC.md/QUESTIONS.md wording that had mischaracterized the supplied sample `.cii`
  files as real client project files — they're demonstration/example files per the user's
  clarification; the not-committed decision itself is unchanged pending explicit confirmation.
  Updated SPEC.md's in/out-of-scope and "Known open decisions" sections to match. Fixed fallout in
  `NeutralFileFixtureBuilder` (needs real `#$ MISCEL_1` content now) and `SupportPlacerTests`
  (restraint-code assertions, reworked the riser test to match the corrected, honest trigger
  condition); all 22 tests green, `dotnet build`/`test`/CLI verified end-to-end. Two items from
  the review are logged as blocking questions in SPEC.md's "Known open decisions" rather than
  resolved here (material database source for allowable/density lookups; database-backed
  iteration tracking, which contradicts the existing "no database" hard constraint) — everything
  else unblocked was completed first, per CLAUDE.md. PR #3 turned out to already be merged, so per
  the merged-PR convention the branch was restarted from `main` and this commit rebased onto it
  (force-with-lease, user-approved); opened PR #4 as the follow-up since #3 can't track new work.
- 2026-08-21: User answered both blocking questions on PR #4's thread, and shared a real
  `caesar.cfg` example (confirmed a pure demonstration case, committed at `fixtures/caesar.cfg`).
  Material-database question: every model directory's `caesar.cfg` names the database locations
  (`SYSTEM_DIRECTORY_NAME`, `User_Material_File_Name`) and the default piping code+edition
  (`DEFAULT_CODE`) directly. Added `CaesarConfig`/`CaesarConfigReader` (best-effort parser, no
  vendor doc for this format — same treatment as `iecho.exe`) and wired it into the CLI: looks for
  `caesar.cfg` next to the input file, cross-checks its `Z_AXIS_UP` against the file's own
  `#$ CONTROL.Izup` (warns, doesn't override — logged as a reversible decision in QUESTIONS.md),
  and surfaces `DefaultCode`/material-file locations in the run summary. Actually parsing the
  referenced material-database files stays deferred (no format spec, and `#$ ALLOWBLS` already
  covers v1's need). Storage question: confirmed not needed yet, so the "no database" constraint
  is unchanged for v1; logged as a real future direction (empirical-knowledge accumulation over
  iteration history, once placement itself is solid) in SPEC.md rather than built now. Added
  `CaesarConfigReader` unit tests (4 new, 22 → 26 total); `dotnet build`/`test`/CLI verified
  end-to-end including a run with `caesar.cfg` present.
- 2026-08-21: Per direct user instruction: defaulted the piping code to `B31.3_2024`
  (`CaesarConfig.DefaultAssumedCode`) when no `caesar.cfg`/`DEFAULT_CODE` is found, with
  `CaesarConfig.EffectiveCode` always preferring the config's own value when present; the CLI now
  always prints the effective code (was previously conditional on `caesar.cfg` existing) — verified
  both paths manually. Added TESTING.md (automated + manual test instructions, fixture-generation
  workflow, what's intentionally untested) and a CLAUDE.md instruction to keep it current. Added a
  CLAUDE.md instruction that every blocking-question entry in QUESTIONS.md must state its own next
  implementation step; retrofitted the one still-open item (real `.cii` sample files) with this.
  4 new tests (`CaesarConfigTests`, 26 → 30 total); `dotnet build`/`test` clean.
- 2026-08-21: User confirmed the real CAESAR II install path — `C:\ProgramData\Intergraph
  CAS\CAESAR II\<version>\System` — and that the build targets version 15.00 and up. Added
  `CaesarInstallationLocator` (enumerates installed versions under an injectable root, filters to
  the 15.00 floor, resolves each version's `System` directory) — pure `System.IO`, fully
  unit-tested on Linux even though the default root is Windows-specific. Corrected SPEC.md's
  "Native file adapter (iecho)" section per the user's explicit note that `iecho.exe` lives in a
  different install branch than this `ProgramData`/`System` tree, so it needs independent
  discovery logic — not wired up yet either way. Not yet wired into the CLI, since nothing in v1
  reads the database files this locator points at (still no format spec for their content). 5 new
  tests (`CaesarInstallationLocatorTests`, 30 → 35 total); `dotnet build`/`test` clean. The same
  message said "hold off on committing the example files," which is ambiguous with the
  already-merged `fixtures/caesar.cfg` from PR #4 — asked the user to clarify rather than guess
  whether that should be reverted (see QUESTIONS.md); proceeded with everything else unblocked.
- 2026-08-24: User reported the real neutral file converter rejects Conduit's output
  ("the iecho does not accept it"). Root-caused and fixed a real bug: `NeutralFileWriter` wrote
  LF-only line endings on every platform, while `NeutralFileReader` reads either; every real
  `.cii` sample checked uses CRLF (confirmed directly against the files, still local, not
  committed), matching `iecho.exe`/CAESAR II's Windows/Fortran heritage. Fixed the writer to
  always emit CRLF, added `.gitattributes` pinning `*.cii` to `eol=crlf`, converted the committed
  fixtures to match, and added a byte-level regression test (the existing round-trip tests
  compare EOL-agnostic string content and couldn't have caught this). Added `reference/` — the 5
  public Hexagon vendor PDFs, committed with an index — and a CLAUDE.md instruction to always
  consult them for anything touching neutral-file format/I-O correctness, since this bug traces
  back to relying on a paraphrase instead of the primary source. Removed all spring logic per
  direct instruction (`SupportType.SpringCandidate`, its restraint mapping, and
  `OptimizationLoop`'s escalation path — an unresolvable span is now just reported), with
  historical QUESTIONS.md/PROGRESS.md entries left intact as record but SPEC.md/TESTING.md/tests
  updated to match current behavior; kept `RestraintType.Xspr` itself since it's real CAESAR II
  vocabulary needed for round-tripping. Added a CLAUDE.md instruction that support-placement
  logic is defined one support type at a time with the user consulted first. Added
  `scripts/run-and-log.ps1`/`.sh` (verified the bash twin actually runs; the PowerShell version
  is carefully written but not executable-verified here — no `pwsh` available in this container)
  so the user can capture a full console transcript to `test-logs/` and commit it back for
  review, documented in TESTING.md. Logged (not yet actioned — no real fixture files to test
  against) the user's observation that `SupportPlacer` may be over-placing supports. 35 → 36
  tests (one new CRLF regression test; the spring-test rewrites were a net-neutral count change);
  `dotnet build`/`test` clean.
- 2026-08-24: Per direct instruction ("I want the logger to make each choice clear. Show the
  reason for the restraint decisions being made"), added per-decision reasoning to the optimize
  log. `SupportTypeClassifier.Classify` now returns a `SupportClassification(Type, Reason)`
  record instead of a bare `SupportType`, with a plain-language reason for each of its three
  branches (vertical → guide; near a run endpoint/equipment → anchor; otherwise → rest).
  `PlacedSupport` gained a `Reason` field combining the span-trigger fact (which span, which max
  allowable, which node) with the classifier's own reason. `OptimizationLoop.Run` now emits one
  note per placed support (each showing its full reason) instead of one combined summary line;
  verified the CLI's actual printed output against both fixtures. Updated TESTING.md's sample
  output block and stale test count (30 → 37) to match. 36 → 37 tests (one new classifier-reason
  coverage test); `dotnet build`/`test` clean.
- 2026-08-24: Per direct instruction ("make sure you are able to create functioning neutral files
  for us to use for testing"), investigated using the real `.cii` samples/PDFs as reference (still
  local, not committed). Found `NeutralFileFixtureBuilder` already matched real files' 20-section
  skeleton, but three sections were structurally wrong: `#$ VERSION` was 1 line instead of the
  vendor doc's required 61 (1 info line + 60 title-page lines) — likely the actual cause of the
  `iecho.exe` "line # 62" error if that was against a Conduit-synthesized file, since line 62 in a
  real file is exactly where `#$ CONTROL` starts; `#$ WIND` was header-only instead of always
  carrying a 1-line default row; `#$ UNITS` was empty instead of its fixed 28-line
  conversion-constants-and-labels block. All three fixed and verified byte-for-byte against 4 real
  samples and `NeutralFile-v15.pdf`; `#$ COORDS` now also always writes its required count line.
  Asked the user (via AskUserQuestion) how Conduit should produce valid test files going forward:
  decided on a blend — patch a real CAESAR II seed file (mirrors the user's own Python tooling,
  read for context, not committed) now, keep pushing from-scratch synthesis in parallel; generated
  files with no real project data get committed like the existing fixtures; unit-system default
  is CAESAR II's own standard metric preset (exact name TBD, logged as an open question) rather
  than the company-specific "AIBEL (mm)" name found in the real samples. Regenerated the committed
  `fixtures/straight-run.cii`/`run-with-riser.cii` with the section fixes (same geometry/
  restraints, only the previously-broken sections changed); updated SPEC.md's neutral-file-format
  section with the newly-confirmed structural facts and a new "Generating test neutral files"
  section documenting the decision. Still blocked on the "patch a real seed" half of the plan —
  needs the user to export a throwaway, non-proprietary test model from their own CAESAR II.
  37/37 tests still passing (same count — this was a section-content fix, not new tests);
  `dotnet build`/`test` clean; manually re-ran the CLI against the regenerated fixture to confirm
  behavior is unchanged.
- 2026-08-24: Searched for publicly downloadable CAESAR II `.c2`/`.cii` sample files (per direct
  suggestion, to unblock the seed-file need without waiting on the user to build one). No direct
  downloadable sample-file archive found. Better lead surfaced instead: CAESAR II's own installer
  (including the free trial/demo) ships Hexagon's own official tutorial jobs, which the user
  likely already has locally — logged as the next thing to try before a from-scratch throwaway
  model. No code changes this step.
- 2026-08-26: The user shared a new `iecho.exe` error (converting `out.cii`) plus three real
  `.cii` files explicitly marked safe to commit — `fixtures/real-samples/{TESTv15,
  TESTv15_slugged,44002}.cii`. These reconfirmed every VERSION/WIND/UNITS structural fix from the
  previous round byte-for-byte, and surfaced a new fact: their `#$ ELEMENTS` geometry is in
  millimetres (confirmed via a 355.6 mm OD element being exactly a 14" pipe OD in mm), unlike
  every existing fixture which uses inch-scale numbers. Per direct instruction, built
  `fixtures/loop-50m-3d.cii`: a 50 m leg in X with a 3D expansion loop (up in Y, out in Z) at the
  midpoint, in millimetre-scale geometry matching the real samples. Running `conduit optimize`
  against it exposed (not a new bug — an already-documented assumption in
  `SpanLimitCalculator`'s own XML doc, now empirically triggered) that its span-heuristic
  constants are calibrated for inch/psi/lb units and produce nonsensical results on real mm-scale
  geometry; logged as a blocking question in QUESTIONS.md since it's cross-cutting support math,
  not something to fix unilaterally per the one-support-type-at-a-time consultation rule. Also
  logged, not yet actioned: the user's CNODES note (CNODE-bearing nodes are not anchor supports)
  and 44002.cii's "equipment as zero-weight rigid elements, ignore for support considerations"
  note, both future support-placement inputs. 37/37 tests still passing; `dotnet build`/`test`
  clean. Asked the user to test `fixtures/loop-50m-3d.cii` directly against `iecho.exe` and report
  back.
- 2026-08-26: `iecho.exe` still rejected `loop-50m-3d.cii` ("Error processing ELEMENT section,
  line # 79"). Byte-diffed against the real samples at that line and found + fixed the actual
  cause: `NeutralFileFixtureBuilder` wrote the element's "line color, line visibility" field in
  real/E-notation format, while all 3 real samples (49 elements checked) write it as plain
  integers (`-1 -1`) instead — contradicts `NeutralFile-v15.pdf`'s own stated format for that
  field, so the real files won per CLAUDE.md's "trust the primary source" rule extended to mean
  "trust real output over a doc's prose when they disagree." Regenerated
  `loop-50m-3d.cii`/`straight-run.cii`/`run-with-riser.cii` with the fix. Also resolved the
  `SpanLimitCalculator` unit-blindness question from the previous round, per direct instruction:
  added `UnitsSection` (parses `#$ UNITS`'s CNVLEN constant), made mm/metric Conduit's default
  computation unit system with automatic conversion for non-metric files, and added " mm" labels
  to every span/distance message across `SupportPlacer`/`MockStressSolver`/`OptimizationLoop`.
  Verified against `fixtures/real-samples/TESTv15.cii`: `conduit optimize` now reports
  "10834.11 mm > 7035.44 mm" (physically sane) instead of the previous "10834.11 > 12.60"
  (nonsensical psi/lb-calibrated garbage), and passes instead of failing after 5 iterations —
  exactly the symptom the user reported. Also built `docs/neutral-file/WALKTHROUGH.md` (the
  dedicated field-by-field build guide the user asked for) and
  `ElementSectionFormatTests`/`UnitsSection`-related tests (46/46 passing, up from 37) guarding
  the fixes against silent regression. Updated SPEC.md/QUESTIONS.md/TESTING.md accordingly; asked
  the user to retest `loop-50m-3d.cii` against `iecho.exe`.
