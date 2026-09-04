# Reference documentation

Official Hexagon CAESAR II documentation — public vendor material, not proprietary. Per
CLAUDE.md, **always consult these before making any claim about, or writing code that touches,
the neutral file format or CAESAR II's input/output behavior.** Prose summaries in SPEC.md are a
starting point, not a substitute — when in doubt, re-check the primary source here; the CRLF
line-ending bug (see SPEC.md's "Neutral file format" and PROGRESS.md) came from relying on a
paraphrase instead of verifying against real files and these documents together.

- **`NeutralFile-v15.pdf`** — "CAESAR II Neutral File" chapter of the CAESAR II Users Guide (v15
  interface). The authoritative source for `.cii` structure: section layout, fixed-width record
  formats, field-by-field meaning of `#$ CONTROL`, `#$ ELEMENTS`, `#$ AUX_DATA` and its
  subsections, restraint type codes, etc. Read this before touching `NeutralFileReader`,
  `NeutralFileWriter`, `FixedWidth`, or any `NeutralFiles/*.cs` model class.
- **`Output-Tab.pdf`** — CAESAR II 15.1 "Output Tab" help: the GUI results-review surface
  (Classic Static Output Processor vs. New Analysis Reviewer).
- **`New-Analysis-Reviewer-Help.pdf`** — the modern results reviewer: supported piping codes,
  navigation, what each report shows.
- **`Static-Analysis-Help.pdf`** — running a static analysis (error check, batch run) and what
  "Send to Text (ASCII) File" / report export actually produces — relevant to `CaesarComStressSolver`'s
  eventual COM-driven-analysis-then-parse-report plan (see SPEC.md's "Caesar II abstraction").
- **`Static-Analysis-Output-Help.pdf`** — the Standard Reports themselves (Code Compliance,
  Restraints, Displacements, Stresses, …) and the Report Template Editor for defining a stable,
  parseable custom report layout.

## `pipe-stress-engineering/`

Excerpts from "Pipe Stress Engineering" (a commercial textbook, not Hexagon vendor material) plus
the user's own UMAT1 material database printout, shared directly by the user and explicitly
cleared for commitment ("Commitment is fine they were all found online" — 2026-08-28). Consult
these, not just SPEC.md's prose summaries of them, before touching span-limit or material-related
logic.

- **`Ch2.pdf`**, **`Ch3.pdf`** — general pipe stress background chapters, shared for context; not
  yet the basis of any implemented logic (see QUESTIONS.md for what's still pending review).
- **`Ch6.pdf`** — support spacing and placement: the source for `SpanLimitCalculator`'s Eqs.
  6.1/6.2 (semi-fixed-beam bending criterion + sag/deflection criterion), Table 6.1's sanity-check
  span values, standard support-type definitions (guide/rest/anchor/line stop), and Fig. 6.8 — the
  worked example approximated by `fixtures/fig6-8-example.cii`.
- **`UMAT1-material-database.pdf`** — a printed (non-machine-readable) dump of the user's own
  UMAT1.umd CAESAR II material database. The source for `SpanLimitCalculator`'s real fallback
  material (ASTM A106 Grade B, material #107) and `NeutralFileFixtureBuilder`'s populated
  `#$ ALLOWBLS` block — see SPEC.md's "Known open decisions" for the full derivation.
- **`../B31.3-2024.pdf`** (repo root, not this folder — see below) — the full ASME B31.3-2024
  code, shared "for reference" per the user, since CAESAR II supports multiple piping codes and
  this is just the one Conduit defaults to.

Not here, and never committed: the Python neutral-file-generator programs (`iecho.py`,
`lift_case_builder.py`) the user has shared for context — those stay off-repo per the clean-room
constraint (see QUESTIONS.md). This folder is for the *public* documentation only. Three real
`.cii` samples the user explicitly authorized committing (a narrow exception to the clean-room
rule, for these three files only) live at `fixtures/real-samples/` instead — see
`docs/neutral-file/WALKTHROUGH.md` for how they're used to confirm this documentation's claims
against actual CAESAR II output.
