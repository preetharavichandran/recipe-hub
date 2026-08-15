using RecipeHub.Domain.Entities;

namespace RecipeHub.Domain.Tests;

public class RecipeSoftDeleteTests
{
    [Fact]
    public void SoftDelete_sets_DeletedAt_for_user_recipe()
    {
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Title = "Dal",
            CreatorId = "user-1",
            IsPlatform = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var when = DateTimeOffset.Parse("2026-07-31T12:00:00Z");
        recipe.SoftDelete(when);

        Assert.Equal(when, recipe.DeletedAt);
        Assert.True(recipe.IsDeleted);
    }

    [Fact]
    public void SoftDelete_throws_for_platform_starter()
    {
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Title = "Oatmeal",
            IsPlatform = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        Assert.Throws<InvalidOperationException>(() =>
            recipe.SoftDelete(DateTimeOffset.UtcNow));
    }
}
