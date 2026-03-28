namespace DINBoard.Services;

public interface IProjectValidationPipeline
{
    void Apply(ProjectValidationContext context, ValidationResult result);
}
