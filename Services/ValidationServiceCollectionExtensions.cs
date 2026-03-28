using System;
using Microsoft.Extensions.DependencyInjection;

namespace DINBoard.Services;

public static class ValidationServiceCollectionExtensions
{
    public static IServiceCollection AddValidationServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IProtectionCurrentParser, ProtectionCurrentParser>();
        services.AddSingleton<IPhaseLoadCalculationService, PhaseLoadCalculationService>();
        services.AddSingleton<ICurrentFromPowerCalculator, CurrentFromPowerCalculator>();
        services.AddSingleton<ICableVoltageDropCalculator, CableVoltageDropCalculator>();
        services.AddSingleton<ICableSizeValidationCalculator, CableSizeValidationCalculator>();
        services.AddSingleton<ICircuitVoltageDropLimitProvider, CircuitVoltageDropLimitProvider>();

        services.AddSingleton<IPhaseImbalanceWarningRule, PhaseImbalanceWarningRule>();
        services.AddSingleton<ICableSafetyValidationRule, CableSafetyValidationRule>();
        services.AddSingleton<IProtectionMismatchValidationRule, ProtectionMismatchValidationRule>();
        services.AddSingleton<INoRcdProtectionWarningRule, NoRcdProtectionWarningRule>();
        services.AddSingleton<IMainOverloadValidationRule, MainOverloadValidationRule>();
        services.AddSingleton<IMainBreakerWarningRule, MainBreakerWarningRule>();
        services.AddSingleton<IRcdOverloadWarningRule, RcdOverloadWarningRule>();

        services.AddSingleton<IProjectValidationRule, PhaseImbalanceProjectValidationRule>();
        services.AddSingleton<IProjectValidationRule, CableSafetyProjectValidationRule>();
        services.AddSingleton<IProjectValidationRule, ProtectionMismatchProjectValidationRule>();
        services.AddSingleton<IProjectValidationRule, NoRcdProtectionProjectValidationRule>();
        services.AddSingleton<IProjectValidationRule, MainOverloadProjectValidationRule>();
        services.AddSingleton<IProjectValidationRule, MainBreakerProjectValidationRule>();
        services.AddSingleton<IProjectValidationRule, RcdOverloadProjectValidationRule>();
        services.AddSingleton<IProjectValidationPipeline, ProjectValidationPipeline>();
        services.AddSingleton<IElectricalValidationService, ElectricalValidationService>();

        return services;
    }
}
