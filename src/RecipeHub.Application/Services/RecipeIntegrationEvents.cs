using RecipeHub.Application.Abstractions;
using RecipeHub.Contracts.Events;
using RecipeHub.Domain.Entities;

namespace RecipeHub.Application.Services;

public sealed class RecipeIntegrationEvents(IRecipeHubDbContext db) : IRecipeIntegrationEvents
{
    public void RecordCreated(
        Recipe recipe,
        IReadOnlyDictionary<Guid, string> ingredientNames,
        DateTimeOffset occurredAt) =>
        db.Add(RecipeEventMapper.CreateOutboxMessage(
            RecipeEventTypes.Created,
            recipe.Id,
            occurredAt,
            RecipeEventMapper.ToCreatedOrUpdated(recipe, ingredientNames)));

    public void RecordUpdated(
        Recipe recipe,
        IReadOnlyDictionary<Guid, string> ingredientNames,
        DateTimeOffset occurredAt) =>
        db.Add(RecipeEventMapper.CreateOutboxMessage(
            RecipeEventTypes.Updated,
            recipe.Id,
            occurredAt,
            RecipeEventMapper.ToCreatedOrUpdated(recipe, ingredientNames)));

    public void RecordDeleted(Recipe recipe, DateTimeOffset deletedAt) =>
        db.Add(RecipeEventMapper.CreateOutboxMessage(
            RecipeEventTypes.Deleted,
            recipe.Id,
            deletedAt,
            RecipeEventMapper.ToDeleted(recipe, deletedAt)));
}
