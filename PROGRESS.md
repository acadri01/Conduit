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
- 2026-08-26: user retested — the ELEMENTS fix confirmed correct (no more "ELEMENT section"
  error), but a new one appeared further along: "Error processing OFFSETS section." Byte-diffed
  the WIND→OFFSETS transition against the real samples and found: `TESTv15.cii`/
  `TESTv15_slugged.cii` both have a completely empty `#$ WIND` (`NumWindLoads = 0`), directly
  contradicting this project's own earlier claim that `#$ WIND` "is never truly empty" — that
  claim came from checking real samples that all happened to have a wind load applied. Conduit's
  fixture builder unconditionally wrote a 1-line WIND default row while `NumWindLoads` stayed 0 —
  a count/content mismatch that desyncs `iecho.exe`'s reader and surfaces as an error several
  sections later (at OFFSETS), not at WIND itself. Fixed: WIND is now empty by default, matching
  `NumWindLoads = 0`. Regenerated all three built fixtures again. Added
  `SectionCountConsistencyTests` checking every count-gated section against its own `#$ CONTROL`
  field, for real samples and Conduit's own output alike — guards this whole class of bug, not
  just this one field. Corrected the now-wrong "WIND always populated" claims in SPEC.md and
  docs/neutral-file/WALKTHROUGH.md. 50/50 tests passing (4 new), `dotnet build`/`test` clean.
  Asked the user to retest again.
- 2026-08-26: user retested via `.C2` conversion — WIND fix confirmed, new error: "Error
  processing MISCEL_1 section." `#$ MISCEL_1` turns out to contain RRMAT (material IDs) plus an
  unconditional trailing block (hanger-table defaults, execution options) present even with zero
  hangers/nozzles in all 3 real samples — `NeutralFileFixtureBuilder` only ever wrote RRMAT. Fixed
  by reusing the exact trailing block confirmed byte-identical between 2 of the 3 real samples
  (the third differs slightly in a few fields — logged as a low-priority open question, not
  structural). Added `Miscel1FormatTests`. Regenerated all three built fixtures again. 51/51 tests
  passing (1 new), `dotnet build`/`test` clean. Asked the user to retest again.
- 2026-08-26: milestone — `.C2` conversion now works; `fixtures/loop-50m-3d.cii` is the first
  Conduit-generated file confirmed to convert successfully on a real CAESAR II install. Brought
  docs/neutral-file/WALKTHROUGH.md fully up to date as the confirmed-correct reference. Per direct
  instruction, corrected the loop's geometry: the original open zigzag wasn't a real expansion
  loop — rebuilt as a proper closed U/camelback shape (horizontal approach, riser up, top segment
  with the 3D component, riser down, horizontal departure) with 4 bends, matching the user's
  reference sketches, total X span exactly 50 m. Added `#$ BEND` support to
  `NeutralFileFixtureBuilder` (new — researched via the vendor doc plus `44002.cii`'s 13 real
  bends; corner elements get a bend pointer, tangent-point node numbers follow the real sample's
  (corner-1, corner-2) convention, radius/angle/fitting values reused from the real sample rather
  than derived). Added `BendFormatTests`. 55/55 tests passing (4 new), `dotnet build`/`test`
  clean.
- 2026-08-26: `.C2` conversion confirmed working again, but the loop's shape was still wrong (a
  diagonal element instead of two separate legs) — rebuilt to the user's exact 7-element/6-bend
  sequence (+DX, +DY, -DZ, +DX, +DZ, -DY, +DX). Per direct instruction, also implemented
  element-splitting: `ElementSplitter` (chunking math, unit-tested against the user's own worked
  example — 25550 mm over a 6446.76 mm max span becomes 4×6000 mm + a 1550 mm remainder) and
  `NeutralFile.ReplaceElement` (the first production capability that adds/mutates pipe elements,
  not just restraints — surgically splices into both `#$ ELEMENTS` and `#$ MISCEL_1`'s RRMAT
  array so the two can't desync), wired into `OptimizationLoop.Adjust` as the fallback when no
  existing node is available. Refactored `Element.ToRawLines()` out as the single shared
  element-formatting path for both production and test-fixture code. Caught and fixed a real bug
  before shipping: the first version copied a bend pointer to every split chunk instead of just
  the final one. Verified: the loop file's two 24 m legs now split and pass in 2 iterations
  instead of failing after 5, matching the user's own description exactly. Also added a "Test
  this now" section to TESTING.md (rewritten every round with the current ask, per direct
  instruction) and a CLAUDE.md bullet codifying it. 62/62 tests passing (7 new), `dotnet
  build`/`test` clean. Asked the user to retest again.
- 2026-08-26: proactive follow-up (not a bug report) — bends need a minimum straight length
  depending on pipe size, plus a 500 mm shoe-clearance buffer, and the default bend radius should
  be "Long" (confirmed via a CAESAR II screenshot: the radius dropdown offers Short/Long/3D/5D).
  Confirmed via the vendor doc that the neutral file only stores a plain radius number, no
  separate "type" field, so this only needed computing the right value: "Long" = 1.5x OD (ASME
  B16.9, approximated from actual OD since Conduit has no NPS table).
  `NeutralFileFixtureBuilder.BuildBendLines` now computes radius per-bend instead of reusing a
  flat 381 mm. Implemented the minimum-chunk-near-a-bend constraint in `ElementSplitter`: a
  too-short remainder next to a bend gets merged into the previous chunk instead of standing
  alone. Logged one known gap rather than guessing: this only covers a bend at the split
  element's own `ToNode`, not a bend at its `FromNode` (needs neighbor-element context
  `OptimizationLoop` doesn't thread through yet) — not exercised by our own fixture either way.
  64/64 tests passing (2 new), `dotnet build`/`test` clean. Regenerated `loop-50m-3d.cii` with the
  new radius; `conduit optimize` still passes in 2 iterations. Asked the user to retest.
- 2026-08-27: user's fifth retest confirmed splitting/geometry work, but reported no restraints
  actually appeared after converting the neutral file — correctly guessed a missing pointer.
  Root cause confirmed: `NeutralFile.AddRestraint` never set the owning element's 4th auxiliary
  pointer (the actual CAESAR II mechanism linking a `#$ RESTRANT` record to a node), so every
  restraint Conduit wrote was valid but unreferenced and invisible on import. Fixed with a
  `ToNode`-preferred/`FromNode`-fallback owner-selection convention (with collision-avoidance for
  two restraints wanting the same connecting element), plus matching pointer-preservation logic in
  `ElementSplitter.Split`. Found and fixed a second, independent bug in the same pass: every
  restraint's stiffness was left at `0` (a zero-resistance spring, not a rigid support) — now uses
  CAESAR II's confirmed rigid constant (`1e12 lbf/in`, converted via `#$ UNITS`' CNVTSF constant).
  Axis-implied restraint types now also get correct direction cosines; `GUI`'s is left an open
  question (only one ambiguous real example) rather than guessed, logged in QUESTIONS.md per
  CLAUDE.md's support-placement-logic consultation rule. 79/79 tests passing (15 new), `dotnet
  build`/`test` clean. Regenerated `loop-50m-3d.cii`; `conduit optimize` output unchanged but
  restraints are now correctly wired and rigid. Updated `docs/neutral-file/WALKTHROUGH.md` with a
  new `#$ RESTRANT` section covering all of this. Asked the user to retest, and logged the
  still-unaddressed bend-radius-pointer question from the same PR comment as the next task.
- 2026-08-27: re-verified the bend-radius question from the same PR comment (the user was
  confident there's a proper pointer/preset field for Short/Long/3D/5D, not just a plain resolved
  number). Re-extracted `NeutralFile-v15.pdf`'s text directly and re-read `#$ BEND` fresh rather
  than trusting the earlier summary, and cross-checked all 3 real samples' actual bend bytes.
  Conclusion unchanged: no such field exists in the neutral file — field 1 is a plain physical
  radius, field 2 ("Type") is the weld type, not a radius preset; every real sample's bends within
  one file share one constant radius value (a physical distance, not an enum code). CAESAR II's UI
  dropdown resolves to a number before it ever reaches the neutral file; if `.c2` keeps the
  dropdown selection internally, that's out of reach for a `.cii`-only tool regardless. No code
  change needed — `NeutralFileFixtureBuilder.BuildBendLines`'s existing "Long = 1.5x OD" approach
  is already correct. Replied on the PR with the finding and evidence.
- 2026-08-27: user's sixth retest confirmed restraints now show up correctly, but flagged (with a
  screenshot) that all three of `SupportPlacer`'s initial placements in `loop-50m-3d.cii` (nodes
  20, 50, 70) landed exactly on bend corner nodes — not buildable without a trunnion fitting.
  Root cause: `SupportPlacer`/`OptimizationLoop.TrySplit` have zero bend-corner awareness when
  picking a candidate node, unlike `ElementSplitter` (which already has a bend-clearance rule, but
  only within its own splitting logic). Also raised: GUI's direction cosine should be the pipe's
  perpendicular unit vector rather than left all-round (ties into the open question from two
  rounds ago); guides need minimum clearance from bends ("stresses" — exact rule TBD); a loop's
  rest support should be centered on its dominant straight ("dx") segment, not any short jog
  between bends. Explicitly asked to pause and realign on the overall plan before continuing.
  Per CLAUDE.md's rule reserving support-placement logic for direct consultation, and the user's
  own request, logged all of this as a blocking question batch in QUESTIONS.md and replied on the
  PR with a diagnosis, the specific clarifying questions needed to implement precisely, and a
  state-of-the-project recap for the vision-realignment conversation. No code pushed this round —
  waiting on the user's answers before touching support-placement logic further.
- 2026-08-27: user answered all three questions in detail and asked for a restatement to confirm
  before implementing. Restated and logged in QUESTIONS.md: (1) CAESAR itself auto-resolves a
  GUI's perpendicular direction on a horizontal run from a zero direction cosine; on a vertical run
  a zero direction cosine becomes an "all-round guide" (restrains both perpendicular directions),
  which over-restrains a designed expansion path near a bend — fixed not by computing a general
  direction cosine but by the loop rule below, which stops a guide from ever landing there; (2) no
  fixed minimum-clearance constant exists — it's a stress question needing a real solver, not a
  distance threshold; (3) a full deterministic loop-detection and placement rule: a chain where two
  axes each appear as an opposite-sign pair and the third matches the long run's own direction is a
  "loop" — its extending segment gets a single centered rest only if the transverse leg's length
  exceeds the max allowable span, and no support goes on the loop's other legs at all. Verified
  this reading against `loop-50m-3d.cii`'s actual geometry (matches exactly) and worked out that
  under this rule the loop currently needs zero supports (its 2000 mm transverse legs are well
  under the 6446.76 mm max span) — meaning the original bug-triggering placements weren't load-
  bearing decisions to begin with. Also logged (separately, not blocking): a reference `iecho.py`
  wrapper the user shared confirms `.cii`→`.C2` is fully scriptable but `.C2`→`.cii` only works
  through `iecho.exe`'s interactive UI — folded into SPEC.md's "Native file adapter (iecho)"
  section for when `IechoConverter` gets implemented for real. Posted the restatement plus three
  narrow follow-up questions on the PR; still no code changes pushed — waiting on confirmation.
- 2026-08-27: user replied with corrections and three new items. Corrected the loop restatement:
  transverse and extending legs are NOT immune from ordinary span rules (guides can legitimately
  appear on loop-internal legs, especially in large loops) — the loop-specific rule only kicks in
  when the transverse leg alone triggers a need but the extending segment doesn't independently,
  in which case the extending segment gets one centered rest; if the extending segment also
  independently needs support, place multiple supports symmetrically on it instead of a single
  center point. Confirmed the 2D loop (4 bends/3 segments, one out-and-back pair) and 3D loop (6
  bends/5 segments, two out-and-back pairs — unchanged from before) taxonomy, and introduced a
  third pattern, the S-loop (also 6 bends/5 segments but topologically different, harder to detect
  deterministically) — logged with the user's worked example, explicitly deferred as future work.
  Also asked for research (not implementation) on a deterministic vertical-riser guide-spacing
  heuristic — researched via `reference/`'s vendor PDFs (nothing relevant — software docs, not
  design-practice references) and a web search, finding a well-corroborated industry rule of thumb
  (guide spacing on a vertical riser ≈ 2× the ordinary horizontal max allowable span), directly
  implementable against `SpanLimitCalculator.ComputeMaxSpan`'s existing output — reported findings
  with sources on the PR, not yet implemented. Also logged two new non-support-placement requests:
  a neutral-file viewer (proposed starting with a low-cost `conduit inspect` text-table CLI command,
  asked whether that's sufficient or a graphical rendering is the real goal) and a list of CAESAR-
  related files Conduit depends on (answered directly — `reference/*.pdf`, `fixtures/real-samples/
  *.cii`, `fixtures/caesar.cfg`, plus `iecho.exe`/the CAESAR install tree as external, not-committed
  dependencies). All logged in QUESTIONS.md; still no support-placement code pushed.
- 2026-08-27: user confirmed the 2x vertical guide-spacing rule, expanded the viewer scope to full
  CAESAR-input-GUI parity (read-only) — scoped against what's already parsed vs. what needs new
  parsers (`#$ BEND`'s own fields, `#$ SIF&TEES`, `#$ REDUCERS`, `#$ FLANGES`, `#$ OFFSETS`,
  `#$ FORCMNT`, `#$ DISPLMNT`, `#$ RIGID`, `#$ EXPJT`), proposed a phased/incremental build starting
  from what's already modeled, defaulting to an HTML read-only page format unless told otherwise —
  answered the UMAT1 question directly (not used; already documented, `#$ ALLOWBLS` covers what's
  needed). Also raised a real, previously-unhandled gap: `SupportPlacer` accumulates span across
  multiple elements but never resets that accumulation at a bend (confirmed by tracing the actual
  `loop-50m-3d.cii` console output — the node-20 placement literally sums two elements across a
  bend) — a bend must end one span-accumulation zone and start a new one, same as a restraint
  already does. Also flagged tees (3 elements at one node) as an unhandled topology; checked
  `NeutralFile-v15.pdf`'s `#$ SIF&TEES` section and a real sample and confirmed the neutral file
  only identifies an intersection by node, not which element is the branch — that has to come from
  geometric collinearity. Given this interacts with everything already agreed (bend-corner
  exclusion, loop rule, 2x multiplier), decided to hold all support-placement code until this lands
  together as one consistent pass rather than implementing pieces against a model that's about to
  change. Logged in QUESTIONS.md; replied on the PR restating the correction for confirmation.
- 2026-08-27: user pushed back on the "Conduit doesn't need UMAT1" answer, prompting a real
  investigation rather than re-asserting it. Found the per-element ALLOWBLS-lookup mechanism itself
  is correct (field indices match the vendor PDF exactly), but that it's likely never actually
  fired: `NeutralFileFixtureBuilder` writes an empty `#$ ALLOWBLS` section in every fixture, AND all
  3 real sample files' own `ColdAllowableStress` field is 0.0 too — so Conduit's span math has
  probably always used the ~10.3 MPa placeholder fallback (vs. a real ~110-140 MPa B31.3 carbon-
  steel allowable), producing max spans roughly 3-4x too short and likely far more supports than
  necessary. This plausibly explains an old, unresolved note already in QUESTIONS.md about
  `SupportPlacer` placing supports too aggressively. Asked the user to check a real model in
  CAESAR's own GUI to confirm whether `#$ ALLOWBLS` item 1 is genuinely the right field to trust
  before changing the lookup logic — can't resolve this from the container alone. Also refined the
  span-accumulation model further per the user's correction (it's not "reset at every bend" as
  previously framed, but "track accumulated distance per principal axis independently, re-cutting
  the accumulated zone evenly regardless of original element boundaries") — proposed this as my own
  design, explicitly asking for confirmation/pushback, and scoped 45°/diagonal-segment handling as
  an explicit deferred follow-up. Logged a new open item (guide-on-horizontal-segment heuristic,
  "generally wherever there's a rest, except near bends") and confirmed tee/SIF understanding is
  correct. Still holding all support-placement code pending confirmation; the ALLOWBLS-in-fixtures
  fix is unblocked and independent, queued as next concrete step regardless of the other answers.
- 2026-08-28: user shared 5 new PDFs (textbook Ch2/3/6, a UMAT1.umd printout, B31.3-2024) and
  answered several open questions. Downloaded and read the UMAT1 printout and Ch6's opening
  section directly (GitHub attachment URLs needed a WebFetch-redirect-then-curl/Read workaround).
  Found the material database itself carries no allowable/yield/UTS data at all (only density,
  Poisson's ratio, expansion coefficient, modulus) — confirms allowable stress is a piping-code-
  table computation, not a material lookup, and gives a cleaner explanation for why real sample
  files' ALLOWBLS was zero (likely exported before ever running an analysis). Ch6 confirmed
  Conduit's existing support terminology matches the standard textbook definitions, surfaced a
  refined max-span formula (semi-fixed-beam constant of 10 vs. Conduit's simply-supported constant
  of 8 — not changed yet, pending Section 2.7 review), a Table 6.1 sanity-check reference, and the
  Fig 6.8 worked example the user wants as a future test fixture (equipment as anchors, flanges at
  both ends) — noted its surrounding nonlinear-resting-support discussion is out of MVP scope.
  Confirmed: bend clearance is radius+200mm (a different number from ElementSplitter's existing
  500mm buffer — not merged without confirming they're the same concept), loop symmetry means
  exact magnitude match, 2x guide-spacing confirmed for standalone risers too, and a significant
  refinement to the per-axis span model — a rest resets every horizontal axis's accumulator, not
  just its own segment's axis, since gravity support doesn't care about local pipe direction.
  Raised (not yet resolved) a copyright question about committing the textbook/code PDFs into
  reference/, following this project's established "read for context, commit only with explicit
  authorization" pattern. All logged in QUESTIONS.md; still no support-placement code pushed.
- 2026-08-28: user gave four direct answers, all acted on now. (1) "materials that do have all the
  required information" + "low carbon steel does not exist as a material in the standard" → swapped
  every `SpanLimitCalculator` fallback constant (allowable stress, elastic modulus, density) for
  real ASTM A106 Grade B data (UMAT1.umd material #107: cold allowable 118 MPa, yield 241 MPa =
  35,000 psi matching the textbook's own A106-B minimum, density 7833.4399 kg/m3, elastic modulus
  203,400 MPa), and populated `NeutralFileFixtureBuilder`'s `#$ ALLOWBLS` block with the same 118
  MPa instead of leaving it empty. (2) "use formulae that are given in the textbook rather than our
  own" → replaced the simply-supported-beam derivation with the book's actual Eqs. 6.1/6.2
  (semi-fixed-beam bending criterion, constant 10 not 8; sag/deflection criterion using a 12.5mm
  design sag limit, the lower/more conservative end of the book's Kellogg range) and take the span
  as min(L1, L2), the book's own rule. Standard 6" Sch 40 fixture pipe's max span moved from
  ~6,446.76mm to ~10,835.70mm (sag-governed) as a direct result of real vs. placeholder material
  data plus the formula change. (3) "500 mm not 200 mm, typo" → confirms bend clearance already
  matches `ElementSplitter`'s existing radius+500mm constant exactly; no code change needed, just
  resolves the earlier open reconciliation question. (4) "commitment is fine" → cleared to commit
  the shared PDFs, but every attempt (curl download, local cp of an already-cached copy) was denied
  by the sandbox's own action classifier even after retry, while unrelated commands kept working
  normally — not a decision on my part, logged transparently in QUESTIONS.md and flagged on the PR
  for the user to advise (e.g. attaching the PDFs directly via GitHub's own UI instead). Regenerated
  all 3 committed fixtures for the new ALLOWBLS/density values, diff-verified only expected fields
  changed, and incidentally caught straight-run.cii/run-with-riser.cii's restraint stiffness never
  having been regenerated since an earlier fix (now corrected too). Adjusted two tests' input
  geometry/density to keep exercising their intended overflow conditions under the new, much larger
  max span. 79/79 tests passing. Support-placement rewrite itself (bend-corner exclusion, per-axis
  span accumulation, 2x guide multiplier, tee/SIF handling, loop rule) still held, unchanged this
  round, pending confirmation of the universal-rest-reset model and the guide-every-other-span
  nuance from the prior round.
