using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DINBoard.Services;

public class ValidationResult
{
    public bool IsValid { get; set; } = true;
    public Collection<ValidationMessage> Errors { get; } = new();
    public Collection<ValidationMessage> Warnings { get; } = new();
    public Collection<ValidationMessage> Info { get; } = new();

    public IEnumerable<ValidationMessage> AllMessages => Errors.Concat(Warnings).Concat(Info);
}

public class ValidationMessage
{
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Details { get; set; }
    public ValidationSeverity Severity { get; set; }
    public string? SymbolId { get; set; }
}

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}

public class PhaseLoadResult
{
    public double L1PowerW { get; set; }
    public double L2PowerW { get; set; }
    public double L3PowerW { get; set; }
    public double L1CurrentA { get; set; }
    public double L2CurrentA { get; set; }
    public double L3CurrentA { get; set; }
    public double ImbalancePercent { get; set; }

    public double TotalPowerW => L1PowerW + L2PowerW + L3PowerW;
    public double TotalCurrentA => L1CurrentA + L2CurrentA + L3CurrentA;
}

public class CableSizeValidation
{
    public double CrossSectionMm2 { get; set; }
    public double CurrentA { get; set; }
    public double MaxCurrentA { get; set; }
    public double LengthM { get; set; }
    public double VoltageDropV { get; set; }
    public double VoltageDropPercent { get; set; }
    public bool IsValid { get; set; }
    public bool IsVoltageDropOk { get; set; }
}
