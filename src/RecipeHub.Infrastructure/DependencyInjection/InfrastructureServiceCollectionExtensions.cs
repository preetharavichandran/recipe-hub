using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RecipeHub.Application.Abstractions;
using RecipeHub.Domain.Entities;
using RecipeHub.Infrastructure.Hosting;
using RecipeHub.Infrastructure.Messaging;
using RecipeHub.Infrastructure.Persistence;
using RecipeHub.Infrastructure.Persistence.Seed;

namespace RecipeHub.Infrastructure.DependencyInjection;

public sealed class RetentionOptions
{
    public const string SectionName = "Retention";
    public int SoftDeleteRetentionDays { get; set; } = 90;
    public int PurgeIntervalHours { get; set; } = 24;
}

public sealed class IdempotencyStore(RecipeHubDbContext db) : IIdempotencyStore
{
    public const int PendingStatusCode = 0;

    private IDbContextTransaction? _transaction;

    public async Task<IdempotencyClaimResult> BeginAsync(
        string creatorId,
        string key,
        string httpMethod,
        string path,
        CancellationToken ct)
    {
        var existing = await FindAsync(creatorId, key, ct);
        if (existing is not null)
            return ToClaimResult(existing);

        _transaction = await db.Database.BeginTransactionAsync(ct);

        var claim = new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            CreatorId = creatorId,
            IdempotencyKey = key,
            HttpMethod = httpMethod,
            Path = path,
            StatusCode = PendingStatusCode,
            ResponseBody = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        };

        db.IdempotencyRecords.Add(claim);
        try
        {
            await db.SaveChangesAsync(ct);
            return new IdempotencyClaimAcquired(claim.Id);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            await AbortAsync(ct);
            var again = await FindAsync(creatorId, key, ct);
            if (again is null)
                throw;
            return ToClaimResult(again);
        }
    }

    public async Task CompleteAsync(Guid claimId, int statusCode, string responseBody, CancellationToken ct)
    {
        var claim = await db.IdempotencyRecords.FirstAsync(r => r.Id == claimId, ct);
        claim.StatusCode = statusCode;
        claim.ResponseBody = responseBody;
        await db.SaveChangesAsync(ct);

        if (_transaction is not null)
        {
            await _transaction.CommitAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task AbortAsync(CancellationToken ct)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        db.ChangeTracker.Clear();
    }

    private Task<IdempotencyRecord?> FindAsync(string creatorId, string key, CancellationToken ct) =>
        db.IdempotencyRecords.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.CreatorId == creatorId && r.IdempotencyKey == key && r.ExpiresAt > DateTimeOffset.UtcNow,
                ct);

    private static IdempotencyClaimResult ToClaimResult(IdempotencyRecord record) =>
        record.StatusCode == PendingStatusCode
            ? new IdempotencyClaimInProgress()
            : new IdempotencyClaimReplay(record.StatusCode, record.ResponseBody);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddOptions<RetentionOptions>();
        services.AddOptions<PublishingOptions>();

        services.AddDbContext<RecipeHubDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

        services.AddScoped<IRecipeHubDbContext>(sp => sp.GetRequiredService<RecipeHubDbContext>());
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<IOutboxStore, EfOutboxStore>();
        services.AddSingleton<ConsoleEventPublisher>();
        services.AddSingleton<IKafkaEventPublisher, KafkaEventPublisher>();
        services.AddSingleton<ISnsEventPublisher, SnsEventPublisher>();
        services.AddSingleton<IEventPublisher, ConfiguredEventPublisher>();
        services.AddHostedService<SoftDeletePurgeService>();
        services.AddHostedService<OutboxDispatcherHostedService>();

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RecipeHubDbContext>();
        await db.Database.MigrateAsync(ct);
        await CatalogSeed.EnsureSeededAsync(db, ct);
    }
}
