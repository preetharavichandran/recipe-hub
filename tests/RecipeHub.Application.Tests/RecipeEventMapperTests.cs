using System.Text.Json;
using FluentAssertions;
using RecipeHub.Application.Services;
using RecipeHub.Contracts.Events;
using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Enums;

namespace RecipeHub.Application.Tests;

public class RecipeEventMapperTests
{
    [Fact]
    public void ToCreatedOrUpdated_maps_full_snapshot_without_household()
    {
        var recipeId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var oatsId = Guid.Parse("11111111-1111-1111-1111-111111110001");
        var when = DateTimeOffset.Parse("2026-08-14T10:00:00Z");

        var recipe = new Recipe
        {
            Id = recipeId,
            Title = "Oatmeal",
            Author = "Alice",
            CreatorId = "user-a",
            UpdatedAt = when,
            MealSlots = [MealSlot.Breakfast],
            Ingredients =
            [
                new RecipeIngredient
                {
                    IngredientId = oatsId,
                    Quantity = 50,
                    Unit = Unit.G,
                    SortOrder = 0
                }
            ]
        };

        var payload = RecipeEventMapper.ToCreatedOrUpdated(
            recipe,
            new Dictionary<Guid, string> { [oatsId] = "Oats" });

        payload.RecipeId.Should().Be(recipeId);
        payload.Title.Should().Be("Oatmeal");
        payload.Author.Should().Be("Alice");
        payload.CreatorId.Should().Be("user-a");
        payload.MealSlots.Should().Equal("breakfast");
        payload.UpdatedAt.Should().Be(when);
        payload.Ingredients.Should().ContainSingle();
        payload.Ingredients[0].Should().Be(new RecipeIngredientPayload(oatsId, "Oats", 50, "g"));

        var json = JsonSerializer.Serialize(payload, RecipeEventMapper.JsonOptions);
        json.ToLowerInvariant().Should().NotContain("household");
    }

    [Fact]
    public void ToDeleted_maps_minimal_payload()
    {
        var recipeId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var when = DateTimeOffset.Parse("2026-08-14T11:00:00Z");
        var recipe = new Recipe
        {
            Id = recipeId,
            Title = "Gone",
            Author = "Bob",
            CreatorId = "user-b",
            CreatedAt = when,
            UpdatedAt = when
        };

        var payload = RecipeEventMapper.ToDeleted(recipe, when);

        payload.Should().Be(new RecipeDeletedPayload(recipeId, when, "Bob"));
    }

    [Fact]
    public void SerializeCloudEvent_uses_cloudevents_attribute_names_and_eventVersion()
    {
        var eventId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var when = DateTimeOffset.Parse("2026-08-14T12:00:00Z");
        var data = new RecipeDeletedPayload(
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            when,
            "Ann");

        var envelope = RecipeEventMapper.Wrap(RecipeEventTypes.Deleted, eventId, when, data);
        var json = RecipeEventMapper.SerializeCloudEvent(envelope);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("specversion").GetString().Should().Be("1.0");
        root.GetProperty("id").GetString().Should().Be(eventId.ToString());
        root.GetProperty("source").GetString().Should().Be(RecipeEventTypes.DefaultSource);
        root.GetProperty("type").GetString().Should().Be(RecipeEventTypes.Deleted);
        root.GetProperty("datacontenttype").GetString().Should().Be("application/json");
        root.GetProperty("eventVersion").GetString().Should().Be("1.0");
        root.GetProperty("data").GetProperty("recipeId").GetGuid()
            .Should().Be(data.RecipeId);
        root.TryGetProperty("householdId", out _).Should().BeFalse();
    }

    [Fact]
    public void CreateOutboxMessage_is_pending_with_event_payload()
    {
        var aggregateId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var eventId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var when = DateTimeOffset.Parse("2026-08-14T13:00:00Z");

        var message = RecipeEventMapper.CreateOutboxMessage(
            RecipeEventTypes.Created,
            aggregateId,
            when,
            new RecipeDeletedPayload(aggregateId, when, null),
            eventId);

        message.Id.Should().Be(eventId);
        message.AggregateId.Should().Be(aggregateId);
        message.EventType.Should().Be(RecipeEventTypes.Created);
        message.Status.Should().Be(OutboxStatus.Pending);
        message.AttemptCount.Should().Be(0);
        message.Payload.Should().Contain(RecipeEventTypes.Created);
    }
}
