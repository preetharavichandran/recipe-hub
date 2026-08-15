using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecipeHub.Application.Abstractions;
using RecipeHub.Contracts.Events;
using RecipeHub.Domain.Enums;
using RecipeHub.Infrastructure.Messaging;
using RecipeHub.Infrastructure.Persistence;
using RecipeHub.Infrastructure.Persistence.Seed;

namespace RecipeHub.Api.Tests;

public class OutboxApiTests : IClassFixture<RecipeHubWebApplicationFactory>
{
    private readonly RecipeHubWebApplicationFactory _factory;

    public OutboxApiTests(RecipeHubWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_recipe_writes_pending_created_outbox_then_console_publish()
    {
        if (!_factory.DatabaseAvailable)
            return;

        var client = AuthenticatedClient("outbox-user-a");
        var console = _factory.Services.GetRequiredService<ConsoleEventPublisher>();
        console.ClearCapturedOutput();

        var create = await RecipeApiTests.PostRecipeAsync(client, new
        {
            title = $"Outbox oats {Guid.NewGuid():N}",
            author = "Alice",
            mealSlots = new[] { "breakfast" },
            ingredients = new[]
            {
                new { ingredientId = SeedIds.Oats, quantity = 50, unit = "g", notes = (string?)null }
            },
            steps = new[] { "Cook oats." }
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var recipe = await create.Content.ReadFromJsonAsync<RecipeResponse>();
        recipe.Should().NotBeNull();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecipeHubDbContext>();
            var row = await db.OutboxMessages.AsNoTracking()
                .SingleAsync(m => m.AggregateId == recipe!.Id && m.EventType == RecipeEventTypes.Created);

            row.Status.Should().BeOneOf(OutboxStatus.Pending, OutboxStatus.Published);
            using var doc = JsonDocument.Parse(row.Payload);
            var root = doc.RootElement;
            root.GetProperty("specversion").GetString().Should().Be("1.0");
            root.GetProperty("type").GetString().Should().Be(RecipeEventTypes.Created);
            root.GetProperty("eventVersion").GetString().Should().Be("1.0");
            root.GetProperty("data").GetProperty("recipeId").GetGuid().Should().Be(recipe.Id);
            root.GetProperty("data").GetProperty("title").GetString().Should().Be(recipe.Title);
            root.GetProperty("data").GetProperty("ingredients")[0].GetProperty("name").GetString()
                .Should().Be("Oats");
            root.TryGetProperty("householdId", out _).Should().BeFalse();
        }

        await WaitForPublishedAsync(recipe!.Id, RecipeEventTypes.Created);

        console.CapturedOutput.Should().Contain(RecipeEventTypes.Created);
        console.CapturedOutput.Should().Contain(recipe.Id.ToString());
    }

    [Fact]
    public async Task Update_and_soft_delete_emit_updated_and_deleted_events()
    {
        if (!_factory.DatabaseAvailable)
            return;

        var client = AuthenticatedClient("outbox-user-b");

        var create = await RecipeApiTests.PostRecipeAsync(client, new
        {
            title = $"Outbox mutable {Guid.NewGuid():N}",
            author = "Bob",
            mealSlots = new[] { "dinner" },
            ingredients = new[]
            {
                new { ingredientId = SeedIds.Rice, quantity = 100, unit = "g", notes = (string?)null }
            }
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var recipe = await create.Content.ReadFromJsonAsync<RecipeResponse>();

        var update = await RecipeApiTests.PutRecipeAsync(client, recipe!.Id, new
        {
            title = recipe.Title + " updated",
            author = "Bob",
            mealSlots = new[] { "dinner" },
            ingredients = new[]
            {
                new { ingredientId = SeedIds.Rice, quantity = 150, unit = "g", notes = "basmati" }
            }
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var delete = await client.DeleteAsync($"/recipes/{recipe.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RecipeHubDbContext>();
        var types = await db.OutboxMessages.AsNoTracking()
            .Where(m => m.AggregateId == recipe.Id)
            .Select(m => m.EventType)
            .ToListAsync();

        types.Should().Contain(RecipeEventTypes.Created);
        types.Should().Contain(RecipeEventTypes.Updated);
        types.Should().Contain(RecipeEventTypes.Deleted);

        await WaitForPublishedAsync(recipe.Id, RecipeEventTypes.Deleted);

        var deleted = await db.OutboxMessages.AsNoTracking()
            .SingleAsync(m => m.AggregateId == recipe.Id && m.EventType == RecipeEventTypes.Deleted);
        deleted.Status.Should().Be(OutboxStatus.Published);
        using var doc = JsonDocument.Parse(deleted.Payload);
        doc.RootElement.GetProperty("data").GetProperty("author").GetString().Should().Be("Bob");
        doc.RootElement.GetProperty("data").TryGetProperty("ingredients", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Manual_dispatch_publishes_pending_outbox_rows()
    {
        if (!_factory.DatabaseAvailable)
            return;

        var client = AuthenticatedClient("outbox-user-c");
        var create = await RecipeApiTests.PostRecipeAsync(client, new
        {
            title = $"Dispatch now {Guid.NewGuid():N}",
            ingredients = new[]
            {
                new { ingredientId = SeedIds.Banana, quantity = 1, unit = "pcs", notes = (string?)null }
            }
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var recipe = await create.Content.ReadFromJsonAsync<RecipeResponse>();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();
        await dispatcher.DispatchPendingAsync(100);

        var db = scope.ServiceProvider.GetRequiredService<RecipeHubDbContext>();
        var row = await db.OutboxMessages
            .SingleAsync(m => m.AggregateId == recipe!.Id && m.EventType == RecipeEventTypes.Created);
        row.Status.Should().Be(OutboxStatus.Published);
        row.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Seed_starters_emit_created_outbox_events()
    {
        if (!_factory.DatabaseAvailable)
            return;

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RecipeHubDbContext>();
        var starterEvents = await db.OutboxMessages.AsNoTracking()
            .CountAsync(m => m.AggregateId == SeedIds.StarterOatmeal
                             && m.EventType == RecipeEventTypes.Created);
        starterEvents.Should().Be(1);

        var payload = await db.OutboxMessages.AsNoTracking()
            .Where(m => m.AggregateId == SeedIds.StarterOatmeal && m.EventType == RecipeEventTypes.Created)
            .Select(m => m.Payload)
            .SingleAsync();
        using var doc = JsonDocument.Parse(payload);
        doc.RootElement.GetProperty("data").GetProperty("title").GetString()
            .Should().Be("Weekday oatmeal");
        doc.RootElement.GetProperty("data").GetProperty("author").GetString()
            .Should().Be("RecipeHub");
    }

    private HttpClient AuthenticatedClient(string sub)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", DevToken.Create(sub));
        return client;
    }

    private async Task WaitForPublishedAsync(Guid aggregateId, string eventType)
    {
        for (var i = 0; i < 40; i++)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RecipeHubDbContext>();
            var row = await db.OutboxMessages.AsNoTracking()
                .FirstOrDefaultAsync(m => m.AggregateId == aggregateId && m.EventType == eventType);
            if (row?.Status == OutboxStatus.Published)
                return;

            // Nudge dispatcher in case the hosted poll has not fired yet.
            var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();
            await dispatcher.DispatchPendingAsync(100);
            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"Timed out waiting for outbox {eventType} on aggregate {aggregateId} to become Published.");
    }

    private sealed record RecipeResponse(Guid Id, string Title, string? CreatorId, bool IsPlatform);
}
