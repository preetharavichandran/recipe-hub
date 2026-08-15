using FluentAssertions;
using RecipeHub.Application.Abstractions;
using RecipeHub.Application.Services;
using RecipeHub.Contracts.Events;
using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Enums;

namespace RecipeHub.Application.Tests;

public class RecipeIntegrationEventsTests
{
    [Fact]
    public void RecordCreated_adds_pending_created_outbox_row()
    {
        var db = new CapturingDbContext();
        var sut = new RecipeIntegrationEvents(db);
        var oatsId = Guid.NewGuid();
        var recipe = SampleRecipe(oatsId);
        var when = DateTimeOffset.Parse("2026-08-14T14:00:00Z");

        sut.RecordCreated(recipe, new Dictionary<Guid, string> { [oatsId] = "Oats" }, when);

        var message = db.Added.OfType<OutboxMessage>().Should().ContainSingle().Subject;
        message.EventType.Should().Be(RecipeEventTypes.Created);
        message.AggregateId.Should().Be(recipe.Id);
        message.Status.Should().Be(OutboxStatus.Pending);
        message.Payload.Should().Contain("Oatmeal");
        message.Payload.Should().Contain(RecipeEventTypes.Created);
    }

    [Fact]
    public void RecordUpdated_adds_updated_outbox_row()
    {
        var db = new CapturingDbContext();
        var sut = new RecipeIntegrationEvents(db);
        var oatsId = Guid.NewGuid();
        var recipe = SampleRecipe(oatsId);

        sut.RecordUpdated(recipe, new Dictionary<Guid, string> { [oatsId] = "Oats" }, DateTimeOffset.UtcNow);

        db.Added.OfType<OutboxMessage>().Single().EventType.Should().Be(RecipeEventTypes.Updated);
    }

    [Fact]
    public void RecordDeleted_adds_deleted_outbox_row()
    {
        var db = new CapturingDbContext();
        var sut = new RecipeIntegrationEvents(db);
        var recipe = SampleRecipe(Guid.NewGuid());
        var when = DateTimeOffset.Parse("2026-08-14T15:00:00Z");

        sut.RecordDeleted(recipe, when);

        var message = db.Added.OfType<OutboxMessage>().Single();
        message.EventType.Should().Be(RecipeEventTypes.Deleted);
        message.Payload.Should().Contain("\"author\":\"Alice\"");
        message.Payload.Should().NotContain("ingredients");
    }

    private static Recipe SampleRecipe(Guid ingredientId) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = "Oatmeal",
            Author = "Alice",
            CreatorId = "user-a",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            MealSlots = [MealSlot.Breakfast],
            Ingredients =
            [
                new RecipeIngredient
                {
                    IngredientId = ingredientId,
                    Quantity = 50,
                    Unit = Unit.G,
                    SortOrder = 0
                }
            ]
        };

    private sealed class CapturingDbContext : IRecipeHubDbContext
    {
        public List<object> Added { get; } = [];

        public IQueryable<Ingredient> Ingredients => Enumerable.Empty<Ingredient>().AsQueryable();
        public IQueryable<IngredientAlias> IngredientAliases => Enumerable.Empty<IngredientAlias>().AsQueryable();
        public IQueryable<Recipe> Recipes => Enumerable.Empty<Recipe>().AsQueryable();
        public IQueryable<IdempotencyRecord> IdempotencyRecords => Enumerable.Empty<IdempotencyRecord>().AsQueryable();
        public IQueryable<OutboxMessage> OutboxMessages => Enumerable.Empty<OutboxMessage>().AsQueryable();

        public void Add<T>(T entity) where T : class => Added.Add(entity!);
        public void RemoveRange<T>(IEnumerable<T> entities) where T : class { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
