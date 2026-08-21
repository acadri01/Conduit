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
Guide, v15 interface — public vendor docs) plus four real `.cii` files from their own projects.
Decision, per SPEC.md's existing clean-room hard constraint (set by the user's own README "IP
considerations" section, i.e. not a new rule Claude is inventing here):
- **Format documentation (the PDF)**: used directly and cited in SPEC.md. It's Hexagon's public
  product documentation, not the user's proprietary material — no IP concern.
- **The four real `.cii` sample files**: reviewed locally in this session to confirm the
  published format spec matches real-world files (it does — same section structure, same
  fixed-width columnar layout, same `#$ RESTRANT` DOF-block structure). **Not copied into the
  repo, not committed, not used as the literal content of any fixture.** They are real client
  project files (node layouts, line numbers, geometry, tag data for a specific job), which is
  exactly the "proprietary project files" SPEC.md's hard constraint rules out, even though their
  title blocks (PROJECT/CLIENT/ANALYST) happened to be blank.
- v1's `fixtures/` directory will instead contain freshly authored, structurally-valid `.cii`
  files with invented node numbers/geometry/tags — real CAESAR II syntax, fictitious project.
- Flagged explicitly to the user in the Phase 1 chat response (not just buried here) given the
  IP/legal stakes — if the user did intend for the real files to be committed (e.g. they already
  have clearance to do so), they can say so and this decision is easy to reverse.
