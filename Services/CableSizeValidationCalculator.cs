namespace DINBoard.Services;

public interface ICableSizeValidationCalculator
{
    CableSizeValidation Validate(double currentA, double crossSectionMm2, double lengthM, int voltage = 230, bool isThreePhase = false);
}

public sealed class CableSizeValidationCalculator : ICableSizeValidationCalculator
{
    // General max voltage drop criterion used by IsVoltageDropOk flag.
    private const double MaxVoltageDropPercent = 3.0;

    private readonly ICableVoltageDropCalculator _cableVoltageDropCalculator;

    public CableSizeValidationCalculator(ICableVoltageDropCalculator cableVoltageDropCalculator)
    {
        _cableVoltageDropCalculator = cableVoltageDropCalculator ?? throw new System.ArgumentNullException(nameof(cableVoltageDropCalculator));
    }

    public CableSizeValidation Validate(double currentA, double crossSectionMm2, double lengthM, int voltage = 230, bool isThreePhase = false)
    {
        var maxCurrent = CommonHelpers.GetCableCapacity(crossSectionMm2, CommonHelpers.CableAmpacityMethodB2);
        var voltageDrop = _cableVoltageDropCalculator.Calculate(currentA, crossSectionMm2, lengthM, voltage, isThreePhase);

        return new CableSizeValidation
        {
            CrossSectionMm2 = crossSectionMm2,
            CurrentA = currentA,
            MaxCurrentA = maxCurrent,
            LengthM = lengthM,
            VoltageDropV = voltageDrop * voltage / 100.0,
            VoltageDropPercent = voltageDrop,
            IsValid = currentA <= maxCurrent,
            IsVoltageDropOk = voltageDrop <= MaxVoltageDropPercent
        };
    }
}
