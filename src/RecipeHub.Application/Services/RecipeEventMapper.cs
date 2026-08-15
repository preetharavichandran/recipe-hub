using System.Text.Json;
using System.Text.Json.Serialization;
using RecipeHub.Application.Dtos;
using RecipeHub.Contracts.Events;
using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Enums;

namespace RecipeHub.Application.Services;

public static class RecipeEventMapper
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static RecipeCreatedOrUpdatedPayload ToCreatedOrUpdated(
        Recipe recipe,
        IReadOnlyDictionary<Guid, string> ingredientNames)
    {
        var ingredients = recipe.Ingredients
            .OrderBy(i => i.SortOrder)
            .Select(i => new RecipeIngredientPayload(
                i.IngredientId,
                ingredientNames.TryGetValue(i.IngredientId, out var name)
                    ? name
                    : i.Ingredient?.Name ?? string.Empty,
                i.Quantity,
                UnitParsing.ToApi(i.Unit)))
            .ToList();

        return new RecipeCreatedOrUpdatedPayload(
            recipe.Id,
            recipe.Title,
            recipe.Author,
            recipe.CreatorId,
            recipe.MealSlots.Select(MealSlotParsing.ToApi).ToList(),
            ingredients,
            recipe.UpdatedAt);
    }

    public static RecipeDeletedPayload ToDeleted(Recipe recipe, DateTimeOffset deletedAt) =>
        new(recipe.Id, deletedAt, recipe.Author);

    public static CloudEventEnvelope<T> Wrap<T>(
        string eventType,
        Guid eventId,
        DateTimeOffset time,
        T data,
        string source = RecipeEventTypes.DefaultSource) =>
        new(
            RecipeEventTypes.SpecVersion,
            eventId.ToString(),
            source,
            eventType,
            time,
            "application/json",
            RecipeEventTypes.EventVersion,
            data);

    public static string SerializeCloudEvent<T>(CloudEventEnvelope<T> envelope) =>
        JsonSerializer.Serialize(envelope, JsonOptions);

    public static OutboxMessage CreateOutboxMessage<T>(
        string eventType,
        Guid aggregateId,
        DateTimeOffset occurredAt,
        T data,
        Guid? eventId = null,
        string source = RecipeEventTypes.DefaultSource)
    {
        var id = eventId ?? Guid.NewGuid();
        var envelope = Wrap(eventType, id, occurredAt, data, source);
        return new OutboxMessage
        {
            Id = id,
            EventType = eventType,
            AggregateId = aggregateId,
            Payload = SerializeCloudEvent(envelope),
            OccurredAt = occurredAt,
            Status = OutboxStatus.Pending,
            AttemptCount = 0
        };
    }
}
