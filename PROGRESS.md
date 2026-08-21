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
  else unblocked was completed first, per CLAUDE.md.
