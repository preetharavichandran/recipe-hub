using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Enums;

namespace RecipeHub.Application.Abstractions;

public interface IRecipeHubDbContext
{
    IQueryable<Ingredient> Ingredients { get; }
    IQueryable<IngredientAlias> IngredientAliases { get; }
    IQueryable<Recipe> Recipes { get; }
    IQueryable<IdempotencyRecord> IdempotencyRecords { get; }
    IQueryable<OutboxMessage> OutboxMessages { get; }

    void Add<T>(T entity) where T : class;
    void RemoveRange<T>(IEnumerable<T> entities) where T : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICurrentUser
{
    string? CreatorId { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
}

public interface IIdempotencyStore
{
    /// <summary>
    /// Claims <paramref name="key"/> for <paramref name="creatorId"/> inside a DB transaction
    /// shared with subsequent recipe writes on the same scoped DbContext.
    /// Pending claims use <c>StatusCode = 0</c>.
    /// </summary>
    Task<IdempotencyClaimResult> BeginAsync(
        string creatorId,
        string key,
        string httpMethod,
        string path,
        CancellationToken ct);

    Task CompleteAsync(Guid claimId, int statusCode, string responseBody, CancellationToken ct);

    Task AbortAsync(CancellationToken ct);
}

public abstract record IdempotencyClaimResult;

/// <summary>A prior request already finished; return the stored response.</summary>
public sealed record IdempotencyClaimReplay(int StatusCode, string ResponseBody) : IdempotencyClaimResult;

/// <summary>Another request holds this key and has not finished; callers should not wait/retry server-side.</summary>
public sealed record IdempotencyClaimInProgress : IdempotencyClaimResult;

/// <summary>This request owns the key; run the write then <see cref="IIdempotencyStore.CompleteAsync"/>.</summary>
public sealed record IdempotencyClaimAcquired(Guid ClaimId) : IdempotencyClaimResult;

/// <summary>Records integration events into the outbox in the same unit of work as recipe writes.</summary>
public interface IRecipeIntegrationEvents
{
    void RecordCreated(Recipe recipe, IReadOnlyDictionary<Guid, string> ingredientNames, DateTimeOffset occurredAt);
    void RecordUpdated(Recipe recipe, IReadOnlyDictionary<Guid, string> ingredientNames, DateTimeOffset occurredAt);
    void RecordDeleted(Recipe recipe, DateTimeOffset deletedAt);
}

/// <summary>Publishes a serialized CloudEvent to a broker (or console).</summary>
public interface IEventPublisher
{
    Task PublishAsync(string eventType, string cloudEventJson, CancellationToken cancellationToken = default);
}

/// <summary>Claims and marks outbox rows during dispatch.</summary>
public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(int batchSize, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}

public interface IOutboxDispatcher
{
    /// <summary>Publishes up to <paramref name="batchSize"/> pending outbox messages. Returns count published.</summary>
    Task<int> DispatchPendingAsync(
        int batchSize = 50,
        int maxAttempts = 5,
        CancellationToken cancellationToken = default);
}
