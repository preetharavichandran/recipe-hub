using RecipeHub.Domain.Enums;

namespace RecipeHub.Domain.Entities;

public sealed class Ingredient
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public Unit DefaultUnit { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    public List<IngredientAlias> Aliases { get; set; } = [];
}
