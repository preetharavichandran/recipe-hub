using RecipeHub.Domain.Enums;

namespace RecipeHub.Application.Dtos;

public sealed record IngredientDto(
    Guid Id,
    string Name,
    IReadOnlyList<string> Aliases,
    string DefaultUnit,
    bool IsActive);

public sealed record CreateIngredientRequest(
    string Name,
    IReadOnlyList<string>? Aliases,
    string DefaultUnit);

public sealed record RecipeIngredientLineDto(
    Guid IngredientId,
    string Name,
    decimal Quantity,
    string Unit,
    string? Notes);

public sealed record RecipeDto(
    Guid Id,
    string Title,
    string? Author,
    string? CreatorId,
    bool IsPlatform,
    IReadOnlyList<string> MealSlots,
    IReadOnlyList<string> CuisineTags,
    IReadOnlyList<RecipeIngredientLineDto> Ingredients,
    IReadOnlyList<string> Steps,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record RecipeIngredientInput(
    Guid IngredientId,
    decimal Quantity,
    string Unit,
    string? Notes);

public sealed record CreateRecipeRequest(
    string Title,
    string? Author,
    IReadOnlyList<string>? MealSlots,
    IReadOnlyList<string>? CuisineTags,
    IReadOnlyList<RecipeIngredientInput> Ingredients,
    IReadOnlyList<string>? Steps);

public sealed record UpdateRecipeRequest(
    string Title,
    string? Author,
    IReadOnlyList<string>? MealSlots,
    IReadOnlyList<string>? CuisineTags,
    IReadOnlyList<RecipeIngredientInput> Ingredients,
    IReadOnlyList<string>? Steps);

public static class UnitParsing
{
    public static Unit Parse(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "pcs" => Unit.Pcs,
            "g" => Unit.G,
            "ml" => Unit.Ml,
            "pack" => Unit.Pack,
            _ => throw new ArgumentException($"Unknown unit '{value}'. Expected pcs|g|ml|pack.")
        };

    public static string ToApi(Unit unit) => unit switch
    {
        Unit.Pcs => "pcs",
        Unit.G => "g",
        Unit.Ml => "ml",
        Unit.Pack => "pack",
        _ => unit.ToString().ToLowerInvariant()
    };
}

public static class MealSlotParsing
{
    public static MealSlot Parse(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "breakfast" => MealSlot.Breakfast,
            "lunch" => MealSlot.Lunch,
            "dinner" => MealSlot.Dinner,
            _ => throw new ArgumentException($"Unknown meal slot '{value}'. Expected breakfast|lunch|dinner.")
        };

    public static string ToApi(MealSlot slot) => slot switch
    {
        MealSlot.Breakfast => "breakfast",
        MealSlot.Lunch => "lunch",
        MealSlot.Dinner => "dinner",
        _ => slot.ToString().ToLowerInvariant()
    };
}
