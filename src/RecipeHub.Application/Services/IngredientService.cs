using Microsoft.EntityFrameworkCore;
using RecipeHub.Application.Abstractions;
using RecipeHub.Application.Dtos;
using RecipeHub.Application.Exceptions;
using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Enums;

namespace RecipeHub.Application.Services;

public sealed class IngredientService(IRecipeHubDbContext db)
{
    public async Task<IReadOnlyList<IngredientDto>> ListAsync(string? q, CancellationToken ct)
    {
        var query = db.Ingredients.AsNoTracking().Where(i => i.IsActive);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(i =>
                i.Name.ToLower().Contains(term) ||
                i.Aliases.Any(a => a.Alias.ToLower().Contains(term)));
        }

        var items = await query
            .Include(i => i.Aliases)
            .OrderBy(i => i.Name)
            .Take(200)
            .ToListAsync(ct);

        return items.Select(Map).ToList();
    }

    public async Task<IngredientDto> GetAsync(Guid id, CancellationToken ct)
    {
        var item = await db.Ingredients.AsNoTracking()
            .Include(i => i.Aliases)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException($"Ingredient '{id}' was not found.");

        return Map(item);
    }

    public async Task<IngredientDto> CreateAsync(CreateIngredientRequest request, ICurrentUser user, CancellationToken ct)
    {
        if (!user.IsAuthenticated)
            throw new ForbiddenException("Authentication required.");
        if (!user.IsAdmin)
            throw new ForbiddenException("Only admin callers may add ingredients.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("name", "Name is required.");

        Unit unit;
        try
        {
            unit = UnitParsing.Parse(request.DefaultUnit);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException("defaultUnit", ex.Message);
        }

        var name = request.Name.Trim();
        var exists = await db.Ingredients.AnyAsync(
            i => i.Name.ToLower() == name.ToLowerInvariant(), ct);
        if (exists)
            throw new ConflictException($"Ingredient '{name}' already exists.");

        var now = DateTimeOffset.UtcNow;
        var ingredient = new Ingredient
        {
            Id = Guid.NewGuid(),
            Name = name,
            DefaultUnit = unit,
            IsActive = true,
            CreatedAt = now,
            Aliases = (request.Aliases ?? [])
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(a => new IngredientAlias { Id = Guid.NewGuid(), Alias = a })
                .ToList()
        };

        foreach (var alias in ingredient.Aliases)
            alias.IngredientId = ingredient.Id;

        db.Add(ingredient);
        await db.SaveChangesAsync(ct);
        return Map(ingredient);
    }

    private static IngredientDto Map(Ingredient i) =>
        new(i.Id, i.Name, i.Aliases.Select(a => a.Alias).ToList(), UnitParsing.ToApi(i.DefaultUnit), i.IsActive);
}
