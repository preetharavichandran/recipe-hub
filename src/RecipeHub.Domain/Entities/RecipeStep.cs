namespace RecipeHub.Domain.Entities;

public sealed class RecipeStep
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public int StepNumber { get; set; }
    public required string Instruction { get; set; }

    public Recipe? Recipe { get; set; }
}
