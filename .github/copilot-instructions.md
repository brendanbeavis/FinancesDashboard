# FinancesDashboard Development Instructions

FinancesDashboard is a ground-up finances dashboard web UI, API and DB written in C# using .NET 10.

The /docs directory contains the authoritative design documentation.

## General Guidelines
- Before implementing a significant feature:
  - Read the relevant documentation in /docs.
  - Understand the existing domain model.
  - Reuse existing concepts rather than creating duplicates.
  - Do not invent major domain concepts without justification.
  - Preserve the separation between simulation and presentation.

## Code Structure
- Use practical file names (no generic Class1/UnitTest1).
- Place each class/type in its own .cs file.
- Put model types in a Models subfolder with one model per file.

## Testing
- Write unit tests for domain behaviour and simulation rules.
- When uncertain about a design decision, inspect the relevant /docs files before making assumptions.