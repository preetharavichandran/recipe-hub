using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using RecipeHub.Infrastructure.Persistence.Seed;

namespace RecipeHub.Api.Tests;

public class RecipeApiTests : IClassFixture<RecipeHubWebApplicationFactory>
{
    private readonly RecipeHubWebApplicationFactory _factory;

    public RecipeApiTests(RecipeHubWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_is_anonymous()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task List_ingredients_includes_seed()
    {
        if (!_factory.DatabaseAvailable)
            return;

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/ingredients");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<IngredientResponse>>();
        body.Should().NotBeNull();
        body!.Any(i => i.Id == SeedIds.Oats).Should().BeTrue();
    }

    [Fact]
    public async Task Create_recipe_requires_idempotency_key()
    {
        if (!_factory.DatabaseAvailable)
            return;

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", DevToken.Create("user-a"));

        var response = await client.PostAsJsonAsync("/recipes", new
        {
            title = "Missing key",
            ingredients = new[]
            {
                new { ingredientId = SeedIds.Oats, quantity = 50, unit = "g" }
            }
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Create_recipe_requires_auth()
    {
        if (!_factory.DatabaseAvailable)
            return;

        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/recipes")
        {
            Content = JsonContent.Create(new
            {
                title = "Test",
                ingredients = new[]
                {
                    new { ingredientId = SeedIds.Oats, quantity = 50, unit = "g" }
                }
            })
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Creator_can_create_and_list_recipe()
    {
        if (!_factory.DatabaseAvailable)
            return;

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DevToken.Create("user-a"));

        var create = await PostRecipeAsync(client, new
        {
            title = "User oatmeal",
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
        recipe!.CreatorId.Should().Be("user-a");
        recipe.IsPlatform.Should().BeFalse();

        var get = await client.GetAsync($"/recipes/{recipe.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Creator_can_update_and_soft_delete_own_recipe()
    {
        if (!_factory.DatabaseAvailable)
            return;

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DevToken.Create("user-a"));

        var create = await PostRecipeAsync(client, new
        {
            title = "Editable oats",
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

        var update = await PutRecipeAsync(client, recipe!.Id, new
        {
            title = "Editable oats (updated)",
            author = "Alice",
            mealSlots = new[] { "breakfast", "lunch" },
            cuisineTags = new[] { "quick" },
            ingredients = new[]
            {
                new { ingredientId = SeedIds.Oats, quantity = 80, unit = "g", notes = "steel-cut" },
                new { ingredientId = SeedIds.Banana, quantity = 1, unit = "pcs", notes = (string?)null }
            },
            steps = new[] { "Boil", "Add oats", "Eat" }
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<RecipeResponse>();
        updated!.Title.Should().Be("Editable oats (updated)");

        var other = _factory.CreateClient();
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DevToken.Create("user-b"));
        var forbidden = await PutRecipeAsync(other, recipe.Id, new
        {
            title = "Hacked",
            ingredients = new[]
            {
                new { ingredientId = SeedIds.Oats, quantity = 50, unit = "g", notes = (string?)null }
            }
        });
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var delete = await client.DeleteAsync($"/recipes/{recipe.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.GetAsync($"/recipes/{recipe.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Platform_starter_cannot_be_updated()
    {
        if (!_factory.DatabaseAvailable)
            return;

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DevToken.Create("user-a"));

        var response = await PutRecipeAsync(client, SeedIds.StarterOatmeal, new
        {
            title = "Hacked oatmeal",
            ingredients = new[]
            {
                new { ingredientId = SeedIds.Oats, quantity = 50, unit = "g", notes = (string?)null }
            }
        });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Concurrent_posts_with_same_idempotency_key_create_one_recipe()
    {
        if (!_factory.DatabaseAvailable)
            return;

        var key = $"concurrent-{Guid.NewGuid():N}";
        var payload = new
        {
            title = $"Concurrent oats {key}",
            author = "Alice",
            mealSlots = new[] { "breakfast" },
            ingredients = new[]
            {
                new { ingredientId = SeedIds.Oats, quantity = 50, unit = "g", notes = (string?)null }
            },
            steps = new[] { "Cook." }
        };

        async Task<HttpResponseMessage> PostOnce()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", DevToken.Create("user-a"));
            var request = new HttpRequestMessage(HttpMethod.Post, "/recipes")
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
            return await client.SendAsync(request);
        }

        var responses = await Task.WhenAll(PostOnce(), PostOnce(), PostOnce());
        var statuses = responses.Select(r => r.StatusCode).ToList();

        statuses.Count(s => s == HttpStatusCode.Created).Should().BeGreaterThanOrEqualTo(1);
        statuses.Should().OnlyContain(s =>
            s == HttpStatusCode.Created || s == HttpStatusCode.Conflict);

        var createdBodies = new List<RecipeResponse>();
        foreach (var response in responses.Where(r => r.StatusCode == HttpStatusCode.Created))
        {
            var body = await response.Content.ReadFromJsonAsync<RecipeResponse>();
            body.Should().NotBeNull();
            createdBodies.Add(body!);
        }

        createdBodies.Select(r => r.Id).Distinct().Should().ContainSingle();

        // Follow-up with the same key must replay the created recipe (not 409).
        var replayClient = _factory.CreateClient();
        replayClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", DevToken.Create("user-a"));
        var replayRequest = new HttpRequestMessage(HttpMethod.Post, "/recipes")
        {
            Content = JsonContent.Create(payload)
        };
        replayRequest.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        var replay = await replayClient.SendAsync(replayRequest);
        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        var replayed = await replay.Content.ReadFromJsonAsync<RecipeResponse>();
        replayed!.Id.Should().Be(createdBodies[0].Id);

        var list = await replayClient.GetFromJsonAsync<List<RecipeResponse>>(
            $"/recipes?title={Uri.EscapeDataString(payload.title)}");
        list.Should().NotBeNull();
        list!.Count.Should().Be(1);
    }

    private sealed record IngredientResponse(Guid Id, string Name);
    private sealed record RecipeResponse(Guid Id, string Title, string? CreatorId, bool IsPlatform);

    internal static Task<HttpResponseMessage> PostRecipeAsync(HttpClient client, object payload, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/recipes")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString("N"));
        return client.SendAsync(request);
    }

    internal static Task<HttpResponseMessage> PutRecipeAsync(HttpClient client, Guid id, object payload, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/recipes/{id}")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString("N"));
        return client.SendAsync(request);
    }
}

public sealed class RecipeHubWebApplicationFactory : WebApplicationFactory<Program>
{
    public bool DatabaseAvailable { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var cs = Environment.GetEnvironmentVariable("RECIPEHUB_TEST_CONNECTION")
            ?? "Host=localhost;Port=5433;Database=recipehub_test;Username=recipehub;Password=recipehub";

        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = cs,
                ["Authentication:Mode"] = "Development",
                ["Authentication:DevelopmentSigningKey"] = "RecipeHub-Dev-Signing-Key-At-Least-32-Chars!",
                ["Authentication:DevelopmentIssuer"] = "recipehub-dev",
                ["Authentication:DevelopmentAudience"] = "recipehub",
                ["Authentication:AdminSubs:0"] = "dev-admin",
                ["PUBLISH_MODE"] = "console",
                ["Publishing:DispatcherIntervalSeconds"] = "1",
                ["Publishing:DispatcherBatchSize"] = "50",
                ["Publishing:MaxPublishAttempts"] = "5"
            });
        });

        DatabaseAvailable = CanConnect(cs);
    }

    private static bool CanConnect(string cs)
    {
        try
        {
            using var conn = new Npgsql.NpgsqlConnection(cs);
            conn.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }
}

internal static class DevToken
{
    public static string Create(string sub, TimeSpan? lifetime = null)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("RecipeHub-Dev-Signing-Key-At-Least-32-Chars!"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "recipehub-dev",
            audience: "recipehub",
            claims: [new Claim("sub", sub)],
            expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromHours(1)),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
