# Contributing to DINBoard

Thank you for your interest in contributing to DINBoard! This document provides guidelines and best practices for contributing to this project.

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

### Single Responsibility Principle (SRP)

Every class should have **one reason to change**. If you're writing more than one responsibility, consider splitting it.

**Class Size Limits:**
- **Maximum 300 lines** per class
- **Maximum 50 lines** per method
- If a class exceeds these limits → **refactor immediately**

**Pragmatic interpretation (traffic light):**
- 🟢 **0-300 lines**: healthy
- 🟡 **301-450 lines**: warning zone, add split plan in PR
- 🔴 **451+ lines**: split/refactor required before adding responsibilities

If 2+ answers below are "No", split before merge:
- Does this class still have one reason to change?
- Can core logic be unit-tested without UI/file-system dependencies?
- Does new logic belong in a dedicated Service instead?
- Is DI preserved (no new unnecessary `new` in View/ViewModel)?

**Good Example:**
```csharp
// ✅ GOOD - Focused responsibility
public partial class WireDrawingViewModel : ObservableObject
{
    // Only wire drawing logic
    public void AddDrawingPoint(Point point) { }
    public void StartDrawing() { }
    public void FinishDrawing() { }
}
```

**Bad Example:**
```csharp
// ❌ BAD - Too many responsibilities
public partial class MainViewModel : ObservableObject
{
    // Contains: wire drawing, power balance, validation, export, undo/redo...
    public void DrawWire() { }
    public void CalculatePowerBalance() { }
    public void ValidateCircuit() { }
    public void ExportPDF() { }
    // ... 1000+ lines
}
```

### Layered Architecture

```
Models/              → Data (SymbolItem, WireConnection, Circuit)
├─ SymbolItem.cs
├─ WireConnection.cs
└─ Project.cs

ViewModels/          → Application Logic (Coordinator + Specialists)
├─ MainViewModel.cs         (Coordinator - coordinates other VMs)
├─ WireDrawingViewModel.cs  (Wire drawing logic)
├─ PowerBalanceViewModel.cs (Power calculations)
└─ ProjectThemeViewModel.cs (UI theme settings)

Views/               → UI Layer (XAML + Code-behind)
├─ MainWindow.xaml
├─ MainWindow.xaml.cs
└─ ModulesPaletteView.xaml

Services/            → Business Logic (One Interface = One Responsibility)
├─ ISymbolImporter.cs       (Imports symbols)
├─ ISymbolValidator.cs      (Validates circuits)
├─ IPdfExporter.cs          (Exports to PDF)
└─ SymbolImportService.cs   (Implementation)

Helpers/             → Utilities & Constants
├─ SvgHelper.cs     (SVG operations)
├─ PathHelper.cs    (Path operations)
└─ Constants.cs

Tests/               → Unit Tests (Mirror structure)
├─ WireDrawingViewModelTests.cs
├─ PowerBalanceViewModelTests.cs
└─ SvgHelperTests.cs
```

---

## Pull Request Guidelines

### Pre-Submission Checklist

Every PR must pass ALL checks before merging:

```
☐ Code compiles without errors
☐ No new warnings (CS0*)
☐ Class size < 300 lines
☐ Method size < 50 lines
☐ No code duplication (DRY principle)
☐ All public methods have XML documentation
☐ Unit tests written for new features
☐ Test coverage > 70%
☐ Git commit messages are clear and descriptive
☐ No debug code (Console.WriteLine, TODO comments for hacky solutions)
```

### PR Title Format

```
[COMPONENT] Brief description

Examples:
✅ [ViewModel] Split MainViewModel into specialized ViewModels
✅ [Services] Extract SVG utilities to SvgHelper
✅ [Tests] Add unit tests for WireDrawingViewModel
❌ "fix"
❌ "refactoring"
❌ "update"
```

### PR Description Template

```markdown
## What does this PR do?
Brief summary of changes.

## Why?
Motivation and context.

## How?
Technical approach and key changes.

## Testing
How to verify the changes work correctly.

## Related Issues
Closes #123

## Screenshots (if applicable)
Before/After screenshots.

## Checklist
- [ ] Passes all checks
- [ ] Tests added
- [ ] Documentation updated
```

---

## Code Style

### Naming Conventions

```csharp
// ✅ GOOD - Clear, descriptive names
var symbolData = symbolService.GetSymbolData();
void CalculatePhaseBalance(int voltage, string phase, bool includeDefaults);
private List<string> availableThemes;
private const double DefaultViewBoxWidth = 596;

// ❌ BAD - Ambiguous, cryptic
var x = GetData();
void Calculate(int a, string b, bool c);
private List<string> lst;
private const double VIEWBOX_W = 596; // What is this?
```

### Constants - Never Hardcode Magic Numbers

```csharp
// ❌ BAD
var scale = Math.Min(w / 596, h / 842); // What are 596 and 842?

// ✅ GOOD
const double DefaultViewBoxWidth = 596;  // Distribution block viewBox width
const double DefaultViewBoxHeight = 842; // Distribution block viewBox height
var scale = Math.Min(w / DefaultViewBoxWidth, h / DefaultViewBoxHeight);
```

### XML Documentation

Every public class, method, and property must have XML documentation:

```csharp
/// <summary>
/// Calculates phase balance and power distribution across L1, L2, L3.
/// </summary>
/// <remarks>
/// IMPORTANT: Uses the voltage from CurrentProject.PowerConfig.
/// If voltage is 0, currents are set to 0.
/// </remarks>
/// <param name="symbols">Collection of symbols to analyze</param>
/// <param name="project">Project containing power configuration</param>
/// <exception cref="ArgumentNullException">If symbols is null</exception>
public void RecalculatePhaseBalance(ObservableCollection<SymbolItem> symbols, Project? project)
{
}
```

### Avoid Magic Strings

```csharp
// ❌ BAD
if (symbol.Type == "MCB" || symbol.Type == "RCD") { }
if (theme == "Dark (Anthracite)") { }

// ✅ GOOD
if (ModuleTypes.IsMcbOrRcd(symbol.Type)) { }
if (theme == AvailableThemes.DarkAnthracite) { }

// Or use constants:
public static class ModuleTypes
{
    public const string MCB = "MCB";
    public const string RCD = "RCD";
}
```

### Avoid Commented-Out Code

```csharp
// ❌ BAD
// public void OldMethod() { }
// var x = 5; // maybe use later?
// if (someCondition) { }

// ✅ GOOD
// Use git history to find old code
// Create an issue if unsure about removal
// Remove it completely
```

---

## Testing Requirements

### Unit Test Minimum

Every new ViewModel or Service must have at least **5 unit tests**:

```csharp
public class WireDrawingViewModelTests
{
    [Fact]
    public void Constructor_InitializesWithEmptyCollections()
    {
        // Arrange
        // Act
        // Assert
    }

    [Fact]
    public void StartDrawing_SetsDrawingState()
    {
        // Arrange
        // Act
        // Assert
    }

    [Fact]
    public void AddDrawingPoint_AddsPointToCollection()
    {
        // Arrange
        // Act
        // Assert
    }

    [Fact]
    public void CanFinishDrawing_RequiresAtLeastTwoPoints()
    {
        // Arrange
        // Act
        // Assert
    }

    [Fact]
    public void CancelDrawing_ClearsPointsAndState()
    {
        // Arrange
        // Act
        // Assert
    }
}
```

### Test Naming Convention

```csharp
// Format: [MethodName]_[Scenario]_[ExpectedResult]

✅ Constructor_InitializesWithEmptyCollections()
✅ AddDrawingPoint_WithValidPoint_AddsToCollection()
✅ CalculateBalance_WithAsymmetricalLoad_ReturnsImbalancePercent()
✅ GetColor_ForL1Phase_ReturnsCorrectHexCode()

❌ Test1()
❌ TestMethod()
❌ Check()
```

### Test Coverage Target

- **Minimum 70%** code coverage for new code
- **Unit tests** for all public methods
- **Integration tests** for Services

---

## Documentation Standards

### What to Document

✅ **DO document:**
- Public classes and methods
- Complex algorithms or business logic
- Non-obvious design decisions
- Configuration requirements
- Known limitations or workarounds

❌ **DON'T document:**
- Obvious getter/setter properties
- Self-explanatory method names
- Simple loop implementations
- Language features

### Documentation Example

```csharp
/// <summary>
/// Detects overlapping wire segments and applies offset so parallel wires don't overlap.
/// Groups wires that share segments and assigns symmetric offsets.
/// </summary>
/// <remarks>
/// Algorithm:
/// 1. Collect all segments from each wire
/// 2. Find wires with overlapping segments (proximity-based)
/// 3. Group overlapping wires into bundles
/// 4. Assign symmetric offsets: -N/2, -N/2+1, ..., 0, ..., N/2-1, N/2
///
/// Performance: O(n²) where n = number of wires
/// </remarks>
public void RecalculateParallelOffsets()
{
}
```

---

## Git Workflow

### Commit Message Format

```
[COMPONENT] Brief description

Longer explanation of why this change was necessary.
Include relevant technical details.

Related issues: #123, #456
```

**Examples:**

```
✅ [ViewModel] Extract WireDrawingViewModel from MainViewModel

Separates wire drawing logic into its own ViewModel following SRP.
This improves testability and allows reuse in other projects.

Related issues: #45

✅ [Tests] Add 8 unit tests for WireDrawingViewModel

Tests cover: initialization, drawing state, point addition, 
snapping, and cancellation.

Fixes: #89

❌ "fix bug"
❌ "update code"
❌ "refactoring changes"
```

### Commit Size

- **One commit = One logical change**
- Keep commits atomic and reviewable
- Never mix refactoring with feature changes

**Good commit sequence:**
```
1. [Services] Extract SvgHelper with GetDimensionsFromViewBox()
2. [Services] Update SymbolImportService to use SvgHelper
3. [Services] Remove duplicate GetDimensionsFromViewBox() methods
4. [Tests] Add tests for SvgHelper
```

**Bad:**
```
1. "refactor everything, fix bugs, add features, update tests"
```

### Branch Naming

```
feature/[description]      → feature/wire-drawing-viewmodel
bugfix/[description]       → bugfix/null-reference-snap-service
refactor/[description]     → refactor/split-mainviewmodel
docs/[description]         → docs/add-contributing-guide

✅ feature/split-mainviewmodel
❌ f/split
❌ new-feature
```

---

## Common Patterns

### Dependency Injection

```csharp
// ✅ GOOD - Loose coupling
public class MainViewModel
{
    private readonly ISymbolService _symbolService;
    
    public MainViewModel(ISymbolService symbolService)
    {
        _symbolService = symbolService;
    }
}

// In App.cs
services.AddScoped<ISymbolService, SymbolService>();
services.AddScoped<MainViewModel>();

// ❌ BAD - Tight coupling
public class MainViewModel
{
    private var _service = new SymbolService(); // Hard to test
}
```

### No Code Duplication (DRY)

```csharp
// ❌ BAD - Duplicated in 3 places
// In SymbolImportService:
var (w, h) = GetDimensionsFromViewBox(svg);

// In SchematicController:
var (w, h) = GetDimensionsFromViewBox(svg);

// In SvgModuleImporter:
var (w, h) = GetDimensionsFromViewBox(svg);

// ✅ GOOD - Centralized in SvgHelper
public static class SvgHelper
{
    public static (double Width, double Height) GetDimensionsFromViewBox(string svgContent)
    {
    }
}

// Used everywhere:
var (w, h) = SvgHelper.GetDimensionsFromViewBox(svg);
```

### Observable Property Pattern

```csharp
// ✅ GOOD - Using MVVM Toolkit
public partial class PowerBalanceViewModel : ObservableObject
{
    [ObservableProperty]
    private double _l1PowerW;
    
    [ObservableProperty]
    private double _phaseImbalancePercent;
    
    // Partial method for property change logic
    partial void OnL1PowerWChanged(double oldValue, double newValue)
    {
        RecalculatePhaseBalance();
    }
}

// ❌ BAD - Manual property implementation
public class PowerBalanceViewModel
{
    private double _l1PowerW;
    public double L1PowerW
    {
        get => _l1PowerW;
        set
        {
            if (_l1PowerW != value)
            {
                _l1PowerW = value;
                OnPropertyChanged(nameof(L1PowerW));
                RecalculatePhaseBalance();
            }
        }
    }
}
```

### Null Safety

```csharp
// ✅ GOOD - Null-aware checks
if (_groupOverlaysContainer != null)
{
    _groupOverlayController = new GroupOverlayController(
        ViewModel,
        _groupOverlaysContainer,
        () => ((App)Application.Current!).Services.GetRequiredService<IDialogService>(),
        () => _groupOverlayController?.RegenerateOverlays());
}

// ❌ BAD - Assuming non-null
var controller = new GroupOverlayController(
    ViewModel,
    _groupOverlaysContainer,  // Could be null!
    ...
);
```

---

## Questions?

- Check existing code for patterns
- Ask in Pull Request discussions
- Reference this guide when in doubt

**Happy coding! 🚀**
