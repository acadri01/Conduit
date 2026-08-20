where Claude parks non-blocking questions + logs assumptions

## Assumptions made (Phase 1 interview, 2026-08-20)
- Starting the C# project from scratch in this repo — README's "Hello World"/parser status note
  is treated as aspirational/stale; no prior code exists to pull in.
- Caesar II feedback loop is abstracted behind `IStressSolver`. `MockStressSolver` is the only
  functional implementation for v1; `CaesarComStressSolver` is a compiled skeleton only (no COM
  calls), to be completed later on a Windows machine with a licensed Caesar II install.
- No real `.c2` sample files or format docs available yet (user has them, will provide later).
  v1 parses/writes a documented synthetic neutral-file format instead of the official one. The
  parser will need revision once real samples arrive — tracked below as a follow-up, not blocking.
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
- Replace the synthetic neutral-file format with a parser for the real Caesar II `.c2` format
  once the user supplies sample files or format documentation.
- Implement and validate `CaesarComStressSolver` against a real licensed Caesar II install
  (Windows COM automation) — cannot be done in this headless container.
- Replace the simplified span/support-type heuristics with code-compliant B31.3 calculations and
  WRC 297/537 nozzle load checks (currently explicitly out of v1 scope per SPEC.md).
