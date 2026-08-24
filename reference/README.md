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

Not here, and never committed: the real sample `.cii` files and the Python neutral-file-generator
programs the user has shared for context — those stay off-repo per the clean-room constraint (see
QUESTIONS.md). This folder is for the *public* documentation only.
