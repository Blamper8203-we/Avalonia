using System;
using System.Collections.Generic;
using System.Linq;
using DINBoard.Models;

namespace DINBoard.Services;

/// <summary>
/// Serwis walidacji elektrycznej.
/// Orkiestruje kalkulatory oraz pipeline reguł walidacji projektu.
/// </summary>
public class ElectricalValidationService : IElectricalValidationService
{
    private readonly IPhaseLoadCalculationService _phaseLoadCalculationService;
    private readonly ICableSizeValidationCalculator _cableSizeValidationCalculator;
    private readonly ICableVoltageDropCalculator _cableVoltageDropCalculator;
    private readonly IProjectValidationPipeline _projectValidationPipeline;

    public ElectricalValidationService()
        : this(ElectricalValidationServiceDefaultDependenciesFactory.Create())
    {
    }

    public ElectricalValidationService(
        IPhaseLoadCalculationService phaseLoadCalculationService,
        ICableSizeValidationCalculator cableSizeValidationCalculator,
        ICableVoltageDropCalculator cableVoltageDropCalculator,
        IProjectValidationPipeline projectValidationPipeline)
    {
        _phaseLoadCalculationService = phaseLoadCalculationService ?? throw new ArgumentNullException(nameof(phaseLoadCalculationService));
        _cableSizeValidationCalculator = cableSizeValidationCalculator ?? throw new ArgumentNullException(nameof(cableSizeValidationCalculator));
        _cableVoltageDropCalculator = cableVoltageDropCalculator ?? throw new ArgumentNullException(nameof(cableVoltageDropCalculator));
        _projectValidationPipeline = projectValidationPipeline ?? throw new ArgumentNullException(nameof(projectValidationPipeline));
    }

    private ElectricalValidationService(ElectricalValidationServiceDependencies dependencies)
        : this(
            dependencies.PhaseLoadCalculationService,
            dependencies.CableSizeValidationCalculator,
            dependencies.CableVoltageDropCalculator,
            dependencies.ProjectValidationPipeline)
    {
    }

    public ValidationResult ValidateProject(Project project, IEnumerable<SymbolItem> symbols)
    {
        var safeProject = project ?? new Project();
        var symbolList = symbols?.ToList() ?? throw new ArgumentNullException(nameof(symbols));

        // Napięcie fazowe wyliczane zgodnie z dotychczasowym zachowaniem.
        var lineVoltage = safeProject.PowerConfig?.Voltage ?? 400;
        var phaseVoltage = (int)(lineVoltage / Math.Sqrt(3));
        var phaseLoads = _phaseLoadCalculationService.Calculate(symbolList, phaseVoltage);

        var context = new ProjectValidationContext(safeProject, symbolList, phaseVoltage, phaseLoads);
        var result = new ValidationResult();

        _projectValidationPipeline.Apply(context, result);

        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    public PhaseLoadResult CalculatePhaseLoads(IEnumerable<SymbolItem> symbols, int voltage = 230)
    {
        return _phaseLoadCalculationService.Calculate(symbols, voltage);
    }

    public CableSizeValidation ValidateCableSize(double currentA, double crossSectionMm2, double lengthM, int voltage = 230, bool isThreePhase = false)
    {
        return _cableSizeValidationCalculator.Validate(currentA, crossSectionMm2, lengthM, voltage, isThreePhase);
    }

    public double CalculateVoltageDrop(double currentA, double crossSectionMm2, double lengthM, int voltage = 230, bool isThreePhase = false)
    {
        return _cableVoltageDropCalculator.Calculate(currentA, crossSectionMm2, lengthM, voltage, isThreePhase);
    }
}
