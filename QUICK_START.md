# DINBoard Quick Start

This document is the shortest safe path to becoming productive in the
repository without breaking critical engineering logic.

## What DINBoard Is

DINBoard is a .NET 10 + Avalonia desktop application for electrical panel
design, circuit management, phase balancing, validation, and PDF
documentation.

The repo contains both ordinary UI code and high-risk engineering logic.
Treat changes accordingly.

## First 10 Minutes

1. Read [AGENTS.md](./AGENTS.md).
2. Read [AI_CONTEXT.md](./AI_CONTEXT.md).
3. Read [ARCHITECTURE_MAP.md](./ARCHITECTURE_MAP.md).
4. Restore packages:

```powershell
dotnet restore Avalonia.sln
```

5. Build the app:

```powershell
dotnet build DINBoard.csproj
```

6. Run tests:

```powershell
dotnet test Tests\Avalonia.Tests.csproj --no-restore
```

7. Launch the desktop app:

```powershell
dotnet run --project DINBoard.csproj
```

8. Validate a release candidate:

```powershell
.\scripts\Validate-Release.ps1
```

Then follow [RELEASE_CHECKLIST.md](./RELEASE_CHECKLIST.md).

Current repo state on 2026-03-21:
- main app project: `DINBoard.csproj`
- test project: `Tests/Avalonia.Tests.csproj`
- solution: `Avalonia.sln`
- passing automated tests in the current suite: `314`

## Daily Workflow

1. Identify the subsystem you are touching.
2. Decide whether it is critical.
3. Make the smallest safe change.
4. Preserve current behavior unless the task explicitly asks for a behavior
   change.
5. Run the most relevant tests for the area you touched.
6. Update documentation if the architecture or workflow moved.

## Critical Areas

Read these rules before making bigger changes:
- electrical logic: `Services/PhaseDistributionCalculator.cs`,
  `Services/ElectricalValidationService.cs`, `Services/PowerBusbarGenerator.cs`
- canvas and schematic editor: `Controls/*`, `Services/Schematic*`
- editing infrastructure: `Services/UndoRedoService.cs`,
  `Services/UndoableCommands.cs`
- persistence and export: `Services/ProjectPersistenceService.cs`,
  `Services/ProjectService.cs`, `Services/Pdf*`

If you are touching one of those areas:
- avoid silent behavior changes
- prefer characterization tests first
- keep UI logic, domain logic, and infrastructure responsibilities separate

## Which Tests To Run

For electrical logic changes:

```powershell
dotnet test Tests\Avalonia.Tests.csproj --no-restore --filter "PhaseDistributionCalculatorTests|ElectricalValidationTests|PowerBalanceViewModelTests"
```

For schematic and canvas changes:

```powershell
dotnet test Tests\Avalonia.Tests.csproj --no-restore --filter "SchematicLayoutEngineTests|SchematicDragDropControllerTests|SchematicViewModelTests"
```

For persistence, undo, and export changes:

```powershell
dotnet test Tests\Avalonia.Tests.csproj --no-restore --filter "UndoRedoTests|ProjectRoundTripTests|PdfExportTests"
```

If the change is cross-cutting or risky, run the full suite.

## Architecture Rules That Matter Most

- Views stay visual.
- ViewModels orchestrate UI state and commands.
- Services hold shared, domain, or technical logic.
- Do not move business logic into code-behind.
- Do not silently change electrical formulas, validation rules, undo/redo
  behavior, persistence, or exported data.

## Practical Starting Points

If you need the shortest orientation path:
- architecture and subsystem map: [ARCHITECTURE_MAP.md](./ARCHITECTURE_MAP.md)
- repo guardrails: [AGENTS.md](./AGENTS.md)
- contribution rules: [CONTRIBUTING.md](./CONTRIBUTING.md)
- code quality overview: [CODE_QUALITY.md](./CODE_QUALITY.md)
- anti-chaos checklist: [PREVENTING_CODE_MESS.md](./PREVENTING_CODE_MESS.md)

## Current Reality of This Repo

This repository already has useful quality guardrails:
- analyzers enabled in `DINBoard.csproj`
- `.editorconfig`
- `.stylecop.json`
- architecture documents
- broad automated test coverage

What is not currently committed in this repo:
- `.github` workflow files
- `.githooks` hook scripts
- extra `docs/` process folder referenced by some older text

That means local discipline matters:
- run the relevant tests yourself
- run `.\scripts\Validate-Release.ps1` before release candidates
- keep changes reviewable
- keep documentation honest and current

## Safe First Contribution

A safe first contribution usually looks like this:
- fix or extract one local responsibility
- add or update targeted tests
- avoid multi-subsystem refactors
- explain what should be smoke-tested manually

If a change feels broad, split it before you start coding.
