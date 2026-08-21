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
