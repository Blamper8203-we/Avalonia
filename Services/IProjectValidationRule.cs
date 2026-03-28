namespace DINBoard.Services;

public interface IProjectValidationRule
{
    int Order { get; }
    void Apply(ProjectValidationContext context, ValidationResult result);
}
