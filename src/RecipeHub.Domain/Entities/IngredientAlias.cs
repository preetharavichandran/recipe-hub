namespace RecipeHub.Domain.Entities;

public sealed class IngredientAlias
{
    public Guid Id { get; set; }
    public Guid IngredientId { get; set; }
    public required string Alias { get; set; }

    public Ingredient? Ingredient { get; set; }
}
