using RecipeHub.Domain.Enums;

namespace RecipeHub.Domain.Entities;

public sealed class Recipe
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Author { get; set; }
    /// <summary>Google JWT sub for user recipes; null for platform starters.</summary>
    public string? CreatorId { get; set; }
    public bool IsPlatform { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<MealSlot> MealSlots { get; set; } = [];
    public List<string> CuisineTags { get; set; } = [];
    public List<RecipeIngredient> Ingredients { get; set; } = [];
    public List<RecipeStep> Steps { get; set; } = [];

    public bool IsDeleted => DeletedAt is not null;
    public bool IsMutable => !IsPlatform && !IsDeleted;

    public void SoftDelete(DateTimeOffset deletedAt)
    {
        if (IsPlatform)
            throw new InvalidOperationException("Platform starter recipes cannot be deleted.");
        if (IsDeleted)
            return;
        DeletedAt = deletedAt;
        UpdatedAt = deletedAt;
    }
}
