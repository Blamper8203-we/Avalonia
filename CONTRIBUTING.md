# Contributing to DINBoard

Thank you for contributing to DINBoard.

This repository contains both ordinary desktop application code and
domain-sensitive engineering logic, so contribution quality matters more than
raw speed.

## Table of Contents

1. [Code Architecture](#code-architecture)
2. [Pull Request Guidelines](#pull-request-guidelines)
3. [Code Style](#code-style)
4. [Testing Requirements](#testing-requirements)
5. [Documentation Standards](#documentation-standards)
6. [Git Workflow](#git-workflow)
7. [Common Patterns](#common-patterns)

---

## Code Architecture

### Single Responsibility Principle

Each class should have one clear reason to change.

If a change introduces a second concern, prefer extracting it instead of making
an existing class absorb more responsibility.

### Pragmatic Class Size Limits

- target: `<= 300` lines per class
- warning zone: `301-450` lines
- refactor required before adding more responsibility: `451+` lines
- target: `<= 50` lines per method where practical

Treat those numbers as guardrails, not as the only quality metric.

Before growing a large class, ask:
- does this class still have one reason to change
- can I test its core logic without UI or file-system dependencies
- did I add a concern that belongs in a dedicated Service
- did I increase coupling by adding new `new` calls in a View or ViewModel

### Good Example

```csharp
// GOOD - focused responsibility
public partial class ProjectWorkspaceViewModel : ObservableObject
{
    public async Task SaveProjectAsync()
    {
        // Project lifecycle and workspace concerns only
    }

    public async Task OpenProjectAsync()
    {
        // Project lifecycle and workspace concerns only
    }
}
```

### Bad Example

```csharp
// BAD - one coordinator accumulating unrelated concerns
public partial class MainViewModel : ObservableObject
{
    public async Task OpenProjectAsync() { }
    public async Task ExportPdfAsync() { }
    public void RecalculatePhaseBalance() { }
    public void ApplyCircuitEditPreset() { }
    public void RebuildSchematicLayout() { }
}
```

### Layered Architecture

```text
Models/              -> Domain and project state
|- SymbolItem.cs
|- Circuit.cs
`- Project.cs

ViewModels/          -> UI state, commands, orchestration
|- MainViewModel.cs
|- ProjectWorkspaceViewModel.cs
|- PowerBalanceViewModel.cs
`- LayoutViewModel.cs

Views/               -> UI only
|- MainWindow.axaml
|- Views/CircuitEditPanelView.axaml
`- Views/ModulesPaletteView.axaml

Services/            -> Shared domain, technical, and infrastructure logic
|- PhaseDistributionCalculator.cs
|- ProjectPersistenceService.cs
|- CircuitEditFieldDefinitionProvider.cs
`- CircuitEditValueApplier.cs

Helpers/             -> Supporting utilities
|- LocalizationHelper.cs
`- SvgHelper.cs

Tests/               -> Behavior and regression coverage
|- Tests/PowerBalanceViewModelTests.cs
|- Tests/PhaseDistributionCalculatorTests.cs
`- Tests/ViewModels/ProjectWorkspaceViewModelTests.cs
```

### Architecture Rules That Matter

- Views should remain visual.
- ViewModels should orchestrate UI state and commands.
- Services should hold domain, technical, or infrastructure logic.
- Do not move business logic into code-behind.
- Do not silently change electrical formulas, validation rules, persistence,
  undo/redo behavior, or exported data.

---

## Pull Request Guidelines

### Pre-Submission Checklist

Every non-trivial PR should satisfy these checks:

```text
[ ] Code builds
[ ] Relevant tests were run
[ ] Risky logic has targeted tests or characterization coverage
[ ] No silent behavior change in critical areas
[ ] Class and method growth is justified
[ ] No dead code or debug leftovers
[ ] Documentation updated if architecture or workflow changed
```

### PR Title Format

```text
[COMPONENT] Brief description
```

Examples:
- `[ViewModel] Simplify MainViewModel construction`
- `[Services] Extract field definitions from CircuitEditPanelView`
- `[Tests] Add phase distribution planner coverage`

Avoid vague titles like:
- `fix`
- `update`
- `refactoring`

### PR Description Template

```markdown
## What
Brief summary of the change.

## Why
Problem, risk, or motivation.

## How
Smallest safe approach used.

## Testing
Tests run and manual checks performed.

## Notes
Anything reviewers should pay special attention to.
```

### Review Expectations

- keep changes reviewable
- avoid mixing broad refactors with feature work unless the task requires it
- call out manual smoke-test steps when UI behavior may be affected
- if the change touches a critical subsystem, explain current behavior and risk

---

## Code Style

### Naming Conventions

```csharp
// GOOD
var projectMetadata = projectService.GetProjectMetadata();
void RecalculatePhaseBalance(Project project, IReadOnlyList<SymbolItem> symbols);
private readonly RecentProjectsService _recentProjectsService;
private const double DefaultViewBoxWidth = 596;

// BAD
var x = GetData();
void Calculate(int a, string b, bool c);
private readonly RecentProjectsService _svc;
private const double VIEWBOX_W = 596;
```

### Avoid Magic Numbers

```csharp
// BAD
var scale = Math.Min(width / 596, height / 842);

// GOOD
const double DefaultViewBoxWidth = 596;
const double DefaultViewBoxHeight = 842;
var scale = Math.Min(width / DefaultViewBoxWidth, height / DefaultViewBoxHeight);
```

### Prefer Descriptive Domain Names

Use names that reflect the domain:
- `phaseLoad`
- `lockedGroups`
- `workspaceState`
- `recentProjects`
- `fieldDefinitions`

Avoid names like:
- `data`
- `item2`
- `temp`
- `svc`

### Avoid Magic Strings

```csharp
// BAD
if (symbol.Type == "MCB" || symbol.Type == "RCD") { }

// GOOD
if (ModuleTypes.IsMcbOrRcd(symbol.Type)) { }

public static class ModuleTypes
{
    public const string MCB = "MCB";
    public const string RCD = "RCD";
}
```

### Avoid Commented-Out Code

Delete it.

If you are unsure, rely on git history instead of leaving inactive code in the
file.

---

## Testing Requirements

### General Rule

Run the most relevant tests for the subsystem you touched.

If the change is broad or risky, run the whole suite:

```powershell
dotnet test Tests\Avalonia.Tests.csproj --no-restore
```

### Critical Suites

For electrical logic:
- `Tests/PhaseDistributionCalculatorTests.cs`
- `Tests/ElectricalValidationTests.cs`
- `Tests/PowerBalanceViewModelTests.cs`

For schematic and canvas:
- `Tests/SchematicLayoutEngineTests.cs`
- `Tests/SchematicDragDropControllerTests.cs`
- `Tests/SchematicViewModelTests.cs`

For infrastructure and data safety:
- `Tests/UndoRedoTests.cs`
- `Tests/ProjectRoundTripTests.cs`
- `Tests/PdfExportTests.cs`

### Unit Test Minimum

New logic should come with focused tests.

For a new ViewModel or Service, aim for at least five meaningful tests that
cover:
- initialization
- happy path behavior
- invalid or edge input
- one regression-prone case
- one state transition or command scenario

### Example Test Shape

```csharp
public class ProjectWorkspaceViewModelTests
{
    [Fact]
    public void Constructor_InitializesRecentProjects()
    {
    }

    [Fact]
    public async Task OpenProjectAsync_WithValidFile_LoadsProject()
    {
    }

    [Fact]
    public async Task SaveProjectAsync_WithoutCurrentProject_DoesNotThrow()
    {
    }

    [Fact]
    public async Task OpenRecentProjectAsync_WithMissingFile_ShowsExpectedHandling()
    {
    }

    [Fact]
    public void UpdateLicenseState_RefreshesHomeScreenFlags()
    {
    }
}
```

### Test Naming Convention

```text
[Method]_[Scenario]_[ExpectedResult]
```

Examples:
- `Constructor_InitializesRecentProjects()`
- `CreateBalancePlan_WithLockedGroups_ExcludesLockedLoads()`
- `ApplyValue_WithRcdPreset_UpdatesExpectedFields()`

Avoid names like:
- `Test1`
- `Check`
- `Works`

---

## Documentation Standards

### Document These Things

- public APIs with non-obvious behavior
- domain-sensitive algorithms
- important design constraints
- configuration and workflow expectations
- known limitations or temporary safety tradeoffs

### Documentation Example

```csharp
/// <summary>
/// Creates a balance plan for the current set of symbols without directly
/// applying UI animation or mutating view state.
/// </summary>
/// <remarks>
/// This method is part of the electrical balancing pipeline and should preserve
/// the current electrical result while keeping planning separate from execution.
/// </remarks>
public BalancePlan CreateBalancePlan(
    IReadOnlyList<SymbolItem> symbols,
    Project project)
{
}
```

### Keep Docs Honest

If you change:
- architecture boundaries
- workflow
- test counts
- setup instructions

then update the related markdown files in the same change where practical.

---

## Git Workflow

### Commit Message Format

```text
[COMPONENT] Brief description
```

Examples:
- `[Services] Extract CircuitEditValueApplier`
- `[ViewModel] Inject workspace dependencies explicitly`
- `[Docs] Update quick start and code quality guides`

### Commit Size

- one commit should represent one logical change where practical
- do not mix refactor and feature work unless required
- prefer small, reviewable sequences over one giant commit

Good sequence:

```text
1. [Tests] Add coverage for phase distribution planning
2. [Services] Extract balance execution helper
3. [Services] Route calculator through helper
4. [Docs] Update architecture notes
```

Bad sequence:

```text
1. refactor everything and fix bugs
```

### Branch Naming

Examples:
- `feature/project-workspace-cleanup`
- `bugfix/null-phase-indicator`
- `refactor/phase-distribution-planner`
- `docs/update-quick-start`

Avoid:
- `f/new`
- `changes`
- `test`

---

## Common Patterns

### Dependency Injection

```csharp
// GOOD
public class MainViewModel
{
    private readonly RecentProjectsService _recentProjectsService;

    public MainViewModel(RecentProjectsService recentProjectsService)
    {
        _recentProjectsService = recentProjectsService;
    }
}

// BAD
public class MainViewModel
{
    private readonly RecentProjectsService _recentProjectsService =
        new RecentProjectsService();
}
```

### Extract Logic Out Of Views

```csharp
// GOOD
var fields = _fieldDefinitionProvider.GetDefinitions(symbol);
_valueApplier.Apply(symbol, values);

// BAD
// View decides field definitions, parses presets, and mutates the domain object
// inline in code-behind.
```

### Separate Planning From Execution

```csharp
// GOOD
var plan = _phaseDistributionCalculator.CreateBalancePlan(symbols, project);
await _executionHelper.ApplyGreedyAssignmentsAsync(plan, delayMs);

// BAD
// One large method does planning, selection animation, delays, and all domain
// calculations inline.
```

### MVVM Toolkit Observable Properties

```csharp
public partial class PowerBalanceViewModel : ObservableObject
{
    [ObservableProperty]
    private double _phaseImbalancePercent;

    partial void OnPhaseImbalancePercentChanged(double oldValue, double newValue)
    {
        // Optional follow-up logic
    }
}
```

### Null Safety

Prefer explicit null checks and safe guard clauses when UI containers or project
state may be absent.

Do not assume that optional View references, current project state, or
design-time paths always exist.

---

## Questions

- Check existing code for current patterns first.
- Use `AI_CONTEXT.md` and `ARCHITECTURE_MAP.md` before bigger changes.
- When in doubt, choose the smaller and safer change.
