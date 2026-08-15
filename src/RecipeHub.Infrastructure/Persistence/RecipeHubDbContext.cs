using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RecipeHub.Application.Abstractions;
using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Enums;

namespace RecipeHub.Infrastructure.Persistence;

public sealed class RecipeHubDbContext(DbContextOptions<RecipeHubDbContext> options)
    : DbContext(options), IRecipeHubDbContext
{
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<IngredientAlias> IngredientAliases => Set<IngredientAlias>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    IQueryable<Ingredient> IRecipeHubDbContext.Ingredients => Ingredients;
    IQueryable<IngredientAlias> IRecipeHubDbContext.IngredientAliases => IngredientAliases;
    IQueryable<Recipe> IRecipeHubDbContext.Recipes => Recipes;
    IQueryable<IdempotencyRecord> IRecipeHubDbContext.IdempotencyRecords => IdempotencyRecords;
    IQueryable<OutboxMessage> IRecipeHubDbContext.OutboxMessages => OutboxMessages;

    void IRecipeHubDbContext.Add<T>(T entity) => Set<T>().Add(entity);
    void IRecipeHubDbContext.RemoveRange<T>(IEnumerable<T> entities) => Set<T>().RemoveRange(entities);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var mealSlotsConverter = new ValueConverter<List<MealSlot>, string>(
            v => JsonSerializer.Serialize(v.Select(s => s.ToString()).ToList(), (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null)!
                .Select(Enum.Parse<MealSlot>)
                .ToList());

        var mealSlotsComparer = new ValueComparer<List<MealSlot>>(
            (a, b) => a!.SequenceEqual(b!),
            v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
            v => v.ToList());

        var stringListConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        var stringListComparer = new ValueComparer<List<string>>(
            (a, b) => a!.SequenceEqual(b!),
            v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
            v => v.ToList());

        modelBuilder.Entity<Ingredient>(e =>
        {
            e.ToTable("ingredients");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.DefaultUnit).HasConversion<string>().HasMaxLength(16);
            e.HasMany(x => x.Aliases).WithOne(x => x.Ingredient!).HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IngredientAlias>(e =>
        {
            e.ToTable("ingredient_aliases");
            e.HasKey(x => x.Id);
            e.Property(x => x.Alias).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Alias);
            e.HasIndex(x => new { x.IngredientId, x.Alias }).IsUnique();
        });

        modelBuilder.Entity<Recipe>(e =>
        {
            e.ToTable("recipes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Author).HasMaxLength(200);
            e.Property(x => x.CreatorId).HasMaxLength(128);
            e.Property(x => x.MealSlots).HasColumnType("jsonb").HasConversion(mealSlotsConverter, mealSlotsComparer);
            e.Property(x => x.CuisineTags).HasColumnType("jsonb").HasConversion(stringListConverter, stringListComparer);
            e.HasIndex(x => x.Author);
            e.HasIndex(x => x.DeletedAt);
            e.HasIndex(x => x.IsPlatform);
            e.HasMany(x => x.Ingredients).WithOne(x => x.Recipe!).HasForeignKey(x => x.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Steps).WithOne(x => x.Recipe!).HasForeignKey(x => x.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecipeIngredient>(e =>
        {
            e.ToTable("recipe_ingredients");
            e.HasKey(x => x.Id);
            e.Property(x => x.Quantity).HasPrecision(12, 3);
            e.Property(x => x.Unit).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RecipeStep>(e =>
        {
            e.ToTable("recipe_steps");
            e.HasKey(x => x.Id);
            e.Property(x => x.Instruction).HasMaxLength(2000).IsRequired();
        });

        modelBuilder.Entity<IdempotencyRecord>(e =>
        {
            e.ToTable("idempotency_records");
            e.HasKey(x => x.Id);
            e.Property(x => x.CreatorId).HasMaxLength(128).IsRequired();
            e.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
            e.Property(x => x.HttpMethod).HasMaxLength(16).IsRequired();
            e.Property(x => x.Path).HasMaxLength(512).IsRequired();
            e.Property(x => x.ResponseBody).HasColumnType("text").IsRequired();
            e.HasIndex(x => new { x.CreatorId, x.IdempotencyKey }).IsUnique();
            e.HasIndex(x => x.ExpiresAt);
        });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.ToTable("integration_outbox");
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).HasMaxLength(128).IsRequired();
            e.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.LastError).HasMaxLength(2000);
            e.HasIndex(x => new { x.Status, x.OccurredAt });
            e.HasIndex(x => x.AggregateId);
            e.HasIndex(x => x.EventType);
        });
    }
}
