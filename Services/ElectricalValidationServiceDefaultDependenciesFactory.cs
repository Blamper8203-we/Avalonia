namespace DINBoard.Services;

internal static class ElectricalValidationServiceDefaultDependenciesFactory
{
    public static ElectricalValidationServiceDependencies Create()
    {
        var protectionCurrentParser = new ProtectionCurrentParser();
        var cableVoltageDropCalculator = new CableVoltageDropCalculator();
        var cableSizeValidationCalculator = new CableSizeValidationCalculator(cableVoltageDropCalculator);
        var currentFromPowerCalculator = new CurrentFromPowerCalculator();
        var voltageDropLimitProvider = new CircuitVoltageDropLimitProvider();
        var phaseLoadCalculationService = new PhaseLoadCalculationService();

        var pipeline = new ProjectValidationPipeline(new IProjectValidationRule[]
        {
            new PhaseImbalanceProjectValidationRule(new PhaseImbalanceWarningRule()),
            new CableSafetyProjectValidationRule(new CableSafetyValidationRule(
                currentFromPowerCalculator,
                cableSizeValidationCalculator,
                voltageDropLimitProvider)),
            new ProtectionMismatchProjectValidationRule(new ProtectionMismatchValidationRule(protectionCurrentParser)),
            new NoRcdProtectionProjectValidationRule(new NoRcdProtectionWarningRule()),
            new MainOverloadProjectValidationRule(new MainOverloadValidationRule()),
            new MainBreakerProjectValidationRule(new MainBreakerWarningRule()),
            new RcdOverloadProjectValidationRule(new RcdOverloadWarningRule(protectionCurrentParser))
        });

        return new ElectricalValidationServiceDependencies(
            phaseLoadCalculationService,
            cableSizeValidationCalculator,
            cableVoltageDropCalculator,
            pipeline);
    }
}

internal sealed record ElectricalValidationServiceDependencies(
    IPhaseLoadCalculationService PhaseLoadCalculationService,
    ICableSizeValidationCalculator CableSizeValidationCalculator,
    ICableVoltageDropCalculator CableVoltageDropCalculator,
    IProjectValidationPipeline ProjectValidationPipeline);
