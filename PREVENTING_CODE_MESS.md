# Preventing Code Mess In DINBoard

This document is the practical anti-chaos checklist for DINBoard.

It is not about abstract clean-code theory. It is about keeping a production
engineering desktop app stable while the codebase continues to evolve.

## Why This Repo Can Go Wrong Fast

DINBoard mixes ordinary application code with domain-sensitive engineering
logic:

- electrical calculations
- validation rules
- diagram layout and canvas interaction
- undo/redo
- project persistence
- PDF documentation output

In a repo like this, code mess rarely starts as one huge mistake. It starts as
small shortcuts:
- a View starts owning form logic
- a ViewModel becomes the default home for every new responsibility
- a Service mixes calculations with UI behavior
- documentation stops matching reality
- a risky refactor happens without characterization tests

## The Core Rule

Prefer the smallest safe change that keeps behavior stable.

That means:
- preserve current behavior unless the task explicitly asks for a change
- avoid cross-subsystem rewrites
- split risky work into reviewable commits
- protect critical areas with targeted tests

## High-Risk Areas

Treat these as high risk by default:

- `Services/PhaseDistributionCalculator.cs`
- `Services/ElectricalValidationService.cs`
- `Services/PowerBusbarGenerator.cs`
- `Services/BusbarPlacementService.cs`
- `Services/SchematicNodeBuilderService.cs`
- `Controls/SingleLineDiagramCanvas.*`
- `Services/Schematic*`
- `Services/UndoRedoService.cs`
- `Services/UndoableCommands.cs`
- `Services/ProjectPersistenceService.cs`
- `Services/ProjectService.cs`
- `Services/Pdf*`

If your change touches one of them, slow down and tighten scope.

## What Good Changes Look Like

Good changes in DINBoard usually have these traits:

- one subsystem at a time
- one clear reason for the change
- existing behavior preserved
- tests added or updated where risk is real
- architecture boundaries improved, not blurred
- documentation kept honest

## What Bad Changes Usually Look Like

These are common warning signs:

- domain logic added to Views or code-behind
- UI animation and pure calculations mixed in one method
- new `new` calls added in ViewModels instead of using DI
- giant constructor growth in coordinator classes
- "while I am here" edits across unrelated subsystems
- old docs still describing deleted tooling or wrong test counts

## Safe Refactor Patterns

These patterns are usually safe and high-value in this repo:

### 1. Characterization Tests First

Before touching risky logic:
- capture current behavior with tests
- only then extract or simplify internals

This is especially important for:
- phase balancing
- validation
- undo/redo
- persistence

### 2. Extract Behind The Existing API

Prefer this sequence:
- keep the public method
- move one internal responsibility to a helper
- route the old method through the helper
- verify nothing user-visible changed

This is how you reduce risk in large existing classes.

### 3. Move Form Logic Out Of Views

If a View starts deciding:
- which fields exist
- how values map to domain objects
- how presets are parsed

then extract that logic to a Service or ViewModel helper and leave the View with
rendering and visual behavior only.

### 4. Separate Planning From Execution

In calculation-heavy code, prefer:
- pure planning logic
- then execution/application logic

This makes testing easier and reduces accidental coupling between domain logic
and UI behavior.

### 5. Simplify Construction Before Bigger Splits

If a large ViewModel creates half of its dependencies with `new`, clean up the
construction model before attempting deeper architectural changes.

## Pre-Change Checklist

Before a non-trivial change:

1. Read `AI_CONTEXT.md`.
2. Read `ARCHITECTURE_MAP.md`.
3. Identify the subsystem.
4. Decide if it is critical.
5. Pick the smallest safe change.
6. Decide which tests must run.

## Pre-Merge Checklist

Before calling work "done", check:

- the change stays inside the intended subsystem
- no critical behavior changed silently
- relevant tests were run
- manual smoke-test steps are listed when UI behavior could be affected
- docs were updated if architecture or workflow changed
- the diff is understandable without reading unrelated files

## Fast Smell Check

Stop and reconsider if any of these become true:

- "I need to touch three unrelated subsystems to finish this"
- "It is faster if I just put the logic in the View"
- "I will skip tests because the refactor is internal"
- "This constructor can take one more service"
- "The docs are outdated anyway"

Those are usually the first moments where maintainability starts slipping.

## Practical Commands

Run the full suite:

```powershell
dotnet test Tests\Avalonia.Tests.csproj --no-restore
```

Run electrical-risk tests:

```powershell
dotnet test Tests\Avalonia.Tests.csproj --no-restore --filter "PhaseDistributionCalculatorTests|ElectricalValidationTests|PowerBalanceViewModelTests"
```

Run schematic-risk tests:

```powershell
dotnet test Tests\Avalonia.Tests.csproj --no-restore --filter "SchematicLayoutEngineTests|SchematicDragDropControllerTests|SchematicViewModelTests"
```

## Current Baseline

As of `2026-03-17`, the repo baseline includes:

- `311` passing tests in the current suite
- architecture docs aligned with extracted `ProjectWorkspaceViewModel`
- thinner `CircuitEditPanelView` responsibilities
- phase balancing internals split into smaller helpers without changing the
  electrical contract

That baseline is worth protecting.
