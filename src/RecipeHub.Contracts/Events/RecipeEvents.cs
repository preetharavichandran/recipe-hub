using System.Text.Json.Serialization;

namespace RecipeHub.Contracts.Events;

public static class RecipeEventTypes
{
    public const string Created = "lifeatlas.recipe.created";
    public const string Updated = "lifeatlas.recipe.updated";
    public const string Deleted = "lifeatlas.recipe.deleted";
    public const string EventVersion = "1.0";
    public const string SpecVersion = "1.0";
    public const string DefaultSource = "urn:lifeatlas:recipe-hub";
}

/// <summary>CloudEvents 1.0 envelope with RecipeHub extension <c>eventVersion</c>.</summary>
public sealed record CloudEventEnvelope<T>(
    [property: JsonPropertyName("specversion")] string SpecVersion,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("time")] DateTimeOffset Time,
    [property: JsonPropertyName("datacontenttype")] string DataContentType,
    [property: JsonPropertyName("eventVersion")] string EventVersion,
    [property: JsonPropertyName("data")] T Data);

public sealed record RecipeIngredientPayload(
    Guid IngredientId,
    string Name,
    decimal Quantity,
    string Unit);

public sealed record RecipeCreatedOrUpdatedPayload(
    Guid RecipeId,
    string Title,
    string? Author,
    string? CreatorId,
    IReadOnlyList<string> MealSlots,
    IReadOnlyList<RecipeIngredientPayload> Ingredients,
    DateTimeOffset UpdatedAt);

public sealed record RecipeDeletedPayload(
    Guid RecipeId,
    DateTimeOffset DeletedAt,
    string? Author);
