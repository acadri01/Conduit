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
