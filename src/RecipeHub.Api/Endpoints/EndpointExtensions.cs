using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RecipeHub.Application.Abstractions;
using RecipeHub.Application.Dtos;
using RecipeHub.Application.Services;

namespace RecipeHub.Api.Endpoints;

public static class EndpointExtensions
{
    public static WebApplication MapRecipeHubEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
            .WithName("Health")
            .WithTags("Health")
            .AllowAnonymous();

        var ingredients = app.MapGroup("/ingredients").WithTags("Ingredients");
        ingredients.MapGet("/", async (string? q, IngredientService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(q, ct))).AllowAnonymous();

        ingredients.MapGet("/{id:guid}", async (Guid id, IngredientService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(id, ct))).AllowAnonymous();

        ingredients.MapPost("/", async (
            CreateIngredientRequest request,
            IngredientService service,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, user, ct);
            return Results.Created($"/ingredients/{created.Id}", created);
        }).RequireAuthorization();

        var recipes = app.MapGroup("/recipes").WithTags("Recipes");
        recipes.MapGet("/", async (
            string? author,
            string? mealSlot,
            string? title,
            RecipeService service,
            CancellationToken ct) =>
            Results.Ok(await service.ListAsync(author, mealSlot, title, ct))).AllowAnonymous();

        recipes.MapGet("/{id:guid}", async (Guid id, RecipeService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(id, ct))).AllowAnonymous();

        recipes.MapPost("/", async (
            HttpRequest http,
            CreateRecipeRequest request,
            RecipeService service,
            ICurrentUser user,
            IIdempotencyStore idempotency,
            CancellationToken ct) =>
        {
            return await WithIdempotencyAsync(
                http, user, idempotency, async () =>
                {
                    var created = await service.CreateAsync(request, user, ct);
                    return Results.Created($"/recipes/{created.Id}", created);
                }, ct);
        }).RequireAuthorization();

        recipes.MapPut("/{id:guid}", async (
            Guid id,
            HttpRequest http,
            UpdateRecipeRequest request,
            RecipeService service,
            ICurrentUser user,
            IIdempotencyStore idempotency,
            CancellationToken ct) =>
        {
            return await WithIdempotencyAsync(
                http, user, idempotency, async () =>
                {
                    var updated = await service.UpdateAsync(id, request, user, ct);
                    return Results.Ok(updated);
                }, ct);
        }).RequireAuthorization();

        recipes.MapPatch("/{id:guid}", async (
            Guid id,
            HttpRequest http,
            UpdateRecipeRequest request,
            RecipeService service,
            ICurrentUser user,
            IIdempotencyStore idempotency,
            CancellationToken ct) =>
        {
            return await WithIdempotencyAsync(
                http, user, idempotency, async () =>
                {
                    var updated = await service.UpdateAsync(id, request, user, ct);
                    return Results.Ok(updated);
                }, ct);
        }).RequireAuthorization();

        recipes.MapDelete("/{id:guid}", async (
            Guid id,
            RecipeService service,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            await service.SoftDeleteAsync(id, user, ct);
            return Results.NoContent();
        }).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> WithIdempotencyAsync(
        HttpRequest http,
        ICurrentUser user,
        IIdempotencyStore store,
        Func<Task<IResult>> action,
        CancellationToken ct)
    {
        if (!http.Headers.TryGetValue("Idempotency-Key", out var keyValues)
            || string.IsNullOrWhiteSpace(keyValues.ToString()))
        {
            throw new Application.Exceptions.ValidationException(
                "Idempotency-Key",
                "Idempotency-Key header is required on recipe create/update.");
        }

        if (string.IsNullOrWhiteSpace(user.CreatorId))
            throw new Application.Exceptions.ForbiddenException("Authentication required.");

        var key = keyValues.ToString().Trim();
        var claim = await store.BeginAsync(user.CreatorId, key, http.Method, http.Path, ct);
        switch (claim)
        {
            case IdempotencyClaimReplay replay:
                return Results.Content(replay.ResponseBody, "application/json", statusCode: replay.StatusCode);

            case IdempotencyClaimInProgress:
                throw new Application.Exceptions.ConflictException(
                    "A request with this Idempotency-Key is already in progress.");

            case IdempotencyClaimAcquired acquired:
                try
                {
                    var result = await action();
                    if (result is IValueHttpResult { Value: not null } valued
                        && result is IStatusCodeHttpResult { StatusCode: not null } status)
                    {
                        var body = JsonSerializer.Serialize(valued.Value, JsonOptions);
                        await store.CompleteAsync(acquired.ClaimId, status.StatusCode.Value, body, ct);
                    }
                    else
                    {
                        await store.AbortAsync(ct);
                    }

                    return result;
                }
                catch
                {
                    await store.AbortAsync(ct);
                    throw;
                }

            default:
                throw new InvalidOperationException($"Unexpected idempotency claim result: {claim.GetType().Name}");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

public static class ExceptionHandlingExtensions
{
    public static IApplicationBuilder UseRecipeHubExceptionHandler(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception ex)
            {
                var logger = context.RequestServices.GetService<ILoggerFactory>()
                    ?.CreateLogger("RecipeHub.ExceptionHandler");
                logger?.LogError(ex, "Unhandled error on {Method} {Path}", context.Request.Method, context.Request.Path);
                await WriteProblemAsync(context, ex);
            }
        });
        return app;
    }

    private static async Task WriteProblemAsync(HttpContext context, Exception ex)
    {
        var (status, title, extensions) = ex switch
        {
            Application.Exceptions.NotFoundException => (StatusCodes.Status404NotFound, ex.Message, (IDictionary<string, object?>?)null),
            Application.Exceptions.ForbiddenException => (StatusCodes.Status403Forbidden, ex.Message, null),
            Application.Exceptions.ConflictException => (StatusCodes.Status409Conflict, ex.Message, null),
            Application.Exceptions.ValidationException vex => (StatusCodes.Status400BadRequest, vex.Message,
                new Dictionary<string, object?> { ["errors"] = vex.Errors }),
            ArgumentException => (StatusCodes.Status400BadRequest, ex.Message, null),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", null)
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = $"https://httpstatuses.com/{status}",
            Instance = context.Request.Path
        };

        if (extensions is not null)
        {
            foreach (var (k, v) in extensions)
                problem.Extensions[k] = v;
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
