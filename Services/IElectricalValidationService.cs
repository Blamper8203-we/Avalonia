using System.Collections.Generic;
using DINBoard.Models;

namespace DINBoard.Services;

public interface IElectricalValidationService
{
    ValidationResult ValidateProject(Project project, IEnumerable<SymbolItem> symbols);
    PhaseLoadResult CalculatePhaseLoads(IEnumerable<SymbolItem> symbols, int voltage = 230);
    CableSizeValidation ValidateCableSize(double currentA, double crossSectionMm2, double lengthM, int voltage = 230, bool isThreePhase = false);
    double CalculateVoltageDrop(double currentA, double crossSectionMm2, double lengthM, int voltage = 230, bool isThreePhase = false);
}
