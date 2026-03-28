# DINBoard Code Quality

This document describes the quality guardrails that are actually present in the
repository today and the habits that keep DINBoard maintainable.

## Core Documents

- [START_HERE.md](./START_HERE.md)
- [QUICK_START.md](./QUICK_START.md)
- [CONTRIBUTING.md](./CONTRIBUTING.md)
- [CONTRIBUTING.pl.md](./CONTRIBUTING.pl.md)
- [AI_CONTEXT.md](./AI_CONTEXT.md)
- [ARCHITECTURE_MAP.md](./ARCHITECTURE_MAP.md)
- [PREVENTING_CODE_MESS.md](./PREVENTING_CODE_MESS.md)

## What Is Really Enforced Today

### In Source Control

- analyzers enabled in `DINBoard.csproj`
- `.editorconfig`
- `.stylecop.json`
- architecture and workflow documentation
- automated tests in `Tests/Avalonia.Tests.csproj`

### By Team Discipline

- small, reviewable changes
- targeted test execution for touched subsystems
- no silent behavior changes in critical engineering logic
- keeping Views thin and logic in ViewModels and Services
- keeping documentation aligned with the real repo state

## What Is Not Committed Today

These items are referenced by some older wording, but are not currently present
in the repository:

- `.github` workflow files
- `.githooks` hook scripts
- extra `docs/` process folders

So the quality system is currently based on code structure, analyzers, tests,
and review discipline, not on repository-hosted CI automation.

## Architectural Guardrails

The most important rules are:

- preserve current behavior unless the task explicitly asks for a change
- do not move business logic into Views or code-behind
- keep electrical calculations, validation, persistence, undo/redo, canvas, and
  PDF export changes very small and very explicit
- prefer extraction of responsibilities over broad rewrites
- prefer dependency injection over ad-hoc `new` in Views and ViewModels

## Pragmatic Class Size Policy

Treat the line-count rule as a guardrail, not a law:

- `0-300` lines: healthy if SRP is intact
- `301-450` lines: warning zone, document split points in review
- `451+` lines: do not add new responsibilities without refactoring first

The more important question is still responsibility: one class should have one
clear reason to change.

## Split-Or-Keep Checklist

Before merging changes to a large class, ask:

- does the class still have one reason to change
- can I test its core logic without UI or file-system dependencies
- did I add a new concern that belongs in a dedicated Service
- did I introduce new `new` calls in a View or ViewModel that should be DI
- if the class is already large, did I identify a safe extraction point

If multiple answers are "no", split the work before growing the class further.

## Testing Expectations

- run the relevant tests for the subsystem you touched
- add tests for new logic or for refactors in risky areas
- use descriptive test names
- run the whole suite for broad or risky changes

Important suites to keep in mind:
- `Tests/PhaseDistributionCalculatorTests.cs`
- `Tests/ElectricalValidationTests.cs`
- `Tests/SchematicLayoutEngineTests.cs`
- `Tests/SchematicDragDropControllerTests.cs`
- `Tests/SchematicViewModelTests.cs`
- `Tests/PowerBalanceViewModelTests.cs`
- `Tests/UndoRedoTests.cs`
- `Tests/PdfExportTests.cs`
- `Tests/ProjectRoundTripTests.cs`

Current suite status captured in this repo state:
- `311` passing tests on `2026-03-17`

## Review Expectations

- one commit should represent one logical change where practical
- do not mix broad refactor and new feature work unless the task requires it
- call out manual smoke-test steps when UI behavior may be affected
- if a change touches critical engineering logic, explain the current behavior,
  the problem, and the safety strategy

## Concrete Signs Of Health

Compared with older architecture drift, the repo now has:
- child ViewModel responsibilities extracted from `MainViewModel`
- `ProjectWorkspaceViewModel` handling project lifecycle concerns
- `CircuitEditPanelView` relying on field-definition and value-application
  services instead of carrying more domain logic in the view
- `PhaseDistributionCalculator` split into smaller planning and execution
  helpers while preserving the electrical result
- `311` passing automated tests

## Day-To-Day Workflow

1. Read the architecture docs if the change is bigger than trivial.
2. Identify the subsystem and its risk level.
3. Make the smallest safe change.
4. Run the relevant tests.
5. Update docs if architecture or process moved.

## Version

- system version: `1.2`
- last updated: `2026-03-17`
- scope: current repository state, not aspirational future automation
