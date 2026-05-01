# Research & Design Decisions Template

---

**Purpose**: Capture discovery findings, architectural investigations, and rationale that inform the technical design.

**Usage**:
- Log research activities and outcomes during the discovery phase.
- Document design decision trade-offs that are too detailed for `design.md`.
- Provide references and evidence for future audits or reuse.
---

## Summary
- **Feature**: fix-local-variable-reassignment
- **Discovery Scope**: Extension (Modifying existing code)
- **Key Findings**:
  - The analyzer is incorrectly using LVP0001_MessageFormat instead of LVP0003_MessageFormat
  - Resource files have the correct LVP0003 messages in both Japanese and English
  - This is a simple resource lookup bug, not a new feature

## Research Log
Document notable investigation steps and their outcomes. Group entries by topic for readability.

### Resource String Configuration
- **Context**: Investigating why LVP0003 is displaying the wrong error message
- **Sources Consulted**:
  - `/home/loach/project/PureSharp/src/PureSharp.Core/ImmutableNamingSuggestionAnalyzer.cs`
  - `/home/loach/project/PureSharp/src/PureSharp.Core/Resources/DiagnosticResources.ja.resx`
  - `/home/loach/project/PureSharp/src/PureSharp.Core/Resources/DiagnosticResources.resx`
- **Findings**:
  - Line 21 of ImmutableNamingSuggestionAnalyzer.cs uses `LVP0001_MessageFormat` instead of `LVP0003_MessageFormat`
  - Japanese resource file (line 102) has the correct message: "ローカル変数 '{0}' は実質的に不変です。アンダースコア '_' で 始まる名前にすることを検討してください"
  - English resource file (line 102) has the correct message: "Local variable '{0}' is effectively immutable. Consider starting its name with an underscore '_'."
  - The displayed message "不変ローカル変数 'c' に値を再代入することはできません" is from LVP0001 (line 84 in ja.resx), confirming the resource lookup bug
- **Implications**: Fix requires changing one line in the analyzer code to use the correct resource string

### Analyzer Code Analysis
- **Context**: Understanding how the analyzer reports diagnostics
- **Sources Consulted**:
  - `/home/loach/project/PureSharp/src/PureSharp.Core/ImmutableNamingSuggestionAnalyzer.cs` (full file)
- **Findings**:
  - The analyzer correctly identifies variables that are never reassigned (0 reassignment count)
  - It correctly excludes catch clauses, using statements, and foreach loop variables
  - It correctly ignores variables that already start with underscore
  - The diagnostic is created at line 131: `context.ReportDiagnostic(Diagnostic.Create(LVP0003, location, symbol.Name));`
  - The `symbol.Name` parameter is passed correctly for variable substitution
- **Implications**: The analyzer logic is correct; only the message format resource is wrong

## Architecture Pattern Evaluation
List candidate patterns or approaches that were considered. Use the table format where helpful.

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| Simple Fix | Change one line to use correct resource string | Minimal risk, matches existing patterns | None identified | Already in scope of this feature |
| New Diagnostic | Create separate LVP0003b for suggestion | More granular, future-proof | Unnecessary complexity | Over-engineering for simple bug |

## Design Decisions
Record major decisions that influence `design.md`. Focus on choices with significant trade-offs.

### Decision: Fix Resource Lookup Bug Directly
- **Context**: The analyzer incorrectly uses LVP0001_MessageFormat instead of LVP0003_MessageFormat
- **Alternatives Considered**:
  1. Create a new LVP0003b diagnostic — unnecessary complexity
  2. Create a new localizable string constant — possible but not idiomatic
  3. Fix the existing LVP0003 to use its own message format — simplest and correct approach
- **Selected Approach**: Change line 21 in ImmutableNamingSuggestionAnalyzer.cs from `LVP0001_MessageFormat` to `LVP0003_MessageFormat`
- **Rationale**: Resource strings are already defined correctly in all language files; only the lookup is wrong
- **Trade-offs**:
  - ✅ Minimal code change reduces risk
  - ✅ Uses existing translatable resources
  - ✅ Fixes the immediate user-facing bug
  - ✅ No new resources to manage
- **Follow-up**: Verify all language resource files have the correct LVP0003 message

## Risks & Mitigations
- **Risk 1**: New message might conflict with other existing LVP0003 usages
  - **Mitigation**: Verify no other LVP0003 diagnostics exist in the codebase; the analyzer is the only one using this code path
- **Risk 2**: Tests might fail if they expect the old (incorrect) message
  - **Mitigation**: Update ImmutableNamingSuggestionAnalyzerTests.cs to verify the correct message format

## References
Provide canonical links and citations (official docs, standards, ADRs, internal guidelines).
- [Roslyn DiagnosticAnalyzer API](https://docs.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.diagnostics.diagnosticanalyzer) — Diagnostic resource string usage pattern
- [C# Resource Files (.resx)](https://docs.microsoft.com/en-us/dotnet/framework/resources/creating-and-accessing-resource-files) — LocalizableResourceString construction
