using Microsoft.EntityFrameworkCore;
using RecipeHub.Application.Abstractions;
using RecipeHub.Application.Dtos;
using RecipeHub.Application.Exceptions;
using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Enums;

namespace RecipeHub.Application.Services;

public sealed class RecipeService(IRecipeHubDbContext db, IRecipeIntegrationEvents integrationEvents)
{
    public async Task<IReadOnlyList<RecipeDto>> ListAsync(
        string? author,
        string? mealSlot,
        string? title,
        CancellationToken ct)
    {
        var query = db.Recipes.AsNoTracking()
            .Where(r => r.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(author))
        {
            var term = author.Trim().ToLowerInvariant();
            query = query.Where(r => r.Author != null && r.Author.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            var term = title.Trim().ToLowerInvariant();
            query = query.Where(r => r.Title.ToLower().Contains(term));
        }

        // MealSlots is jsonb + value conversion — EF cannot translate Contains() to SQL.
        // Resolve matching ids in memory, then load the full graph for those recipes.
        MealSlot? slotFilter = null;
        if (!string.IsNullOrWhiteSpace(mealSlot))
        {
            try
            {
                slotFilter = MealSlotParsing.Parse(mealSlot);
            }
            catch (ArgumentException ex)
            {
                throw new ValidationException("mealSlot", ex.Message);
            }

            var matches = await query
                .Select(r => new { r.Id, r.MealSlots })
                .ToListAsync(ct);
            var ids = matches
                .Where(r => r.MealSlots.Contains(slotFilter.Value))
                .Select(r => r.Id)
                .Take(200)
                .ToList();

            query = db.Recipes.AsNoTracking()
                .Where(r => r.DeletedAt == null && ids.Contains(r.Id));
        }

        var recipes = await query
            .Include(r => r.Ingredients).ThenInclude(ri => ri.Ingredient)
            .Include(r => r.Steps)
            .OrderBy(r => r.Title)
            .Take(200)
            .ToListAsync(ct);

        return recipes.Select(Map).ToList();
    }

    public async Task<RecipeDto> GetAsync(Guid id, CancellationToken ct)
    {
        var recipe = await LoadAsync(id, tracking: false, ct);
        if (recipe.IsDeleted)
            throw new NotFoundException($"Recipe '{id}' was not found.");
        return Map(recipe);
    }

    public async Task<RecipeDto> CreateAsync(CreateRecipeRequest request, ICurrentUser user, CancellationToken ct)
    {
        if (!user.IsAuthenticated || string.IsNullOrWhiteSpace(user.CreatorId))
            throw new ForbiddenException("Authentication required.");

        var now = DateTimeOffset.UtcNow;
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Title = ValidateTitle(request.Title),
            Author = NormalizeAuthor(request.Author),
            CreatorId = user.CreatorId,
            IsPlatform = false,
            CreatedAt = now,
            UpdatedAt = now,
            MealSlots = ParseMealSlots(request.MealSlots),
            CuisineTags = NormalizeTags(request.CuisineTags),
            Ingredients = await BuildLinesAsync(request.Ingredients, ct),
            Steps = BuildSteps(request.Steps)
        };

        foreach (var line in recipe.Ingredients)
            line.RecipeId = recipe.Id;
        foreach (var step in recipe.Steps)
            step.RecipeId = recipe.Id;

        var ingredientNames = await LoadIngredientNamesAsync(
            recipe.Ingredients.Select(i => i.IngredientId), ct);

        db.Add(recipe);
        integrationEvents.RecordCreated(recipe, ingredientNames, now);
        await db.SaveChangesAsync(ct);

        return await GetAsync(recipe.Id, ct);
    }

    public async Task<RecipeDto> UpdateAsync(Guid id, UpdateRecipeRequest request, ICurrentUser user, CancellationToken ct)
    {
        if (!user.IsAuthenticated || string.IsNullOrWhiteSpace(user.CreatorId))
            throw new ForbiddenException("Authentication required.");

        var recipe = await LoadAsync(id, tracking: true, ct);
        EnsureCanMutate(recipe, user);

        recipe.Title = ValidateTitle(request.Title);
        recipe.Author = NormalizeAuthor(request.Author);
        recipe.MealSlots = ParseMealSlots(request.MealSlots);
        recipe.CuisineTags = NormalizeTags(request.CuisineTags);
        recipe.UpdatedAt = DateTimeOffset.UtcNow;

        // Client-assigned Guids: must Add explicitly (nav-only marks Modified → concurrency errors).
        // Do not also Add to the navigation — EF fixup would duplicate entries used for outbox payloads.
        db.RemoveRange(recipe.Ingredients.ToList());
        db.RemoveRange(recipe.Steps.ToList());
        recipe.Ingredients.Clear();
        recipe.Steps.Clear();

        var lines = await BuildLinesAsync(request.Ingredients, ct);
        foreach (var line in lines)
        {
            line.RecipeId = recipe.Id;
            db.Add(line);
        }

        var steps = BuildSteps(request.Steps);
        foreach (var step in steps)
        {
            step.RecipeId = recipe.Id;
            db.Add(step);
        }

        var ingredientNames = await LoadIngredientNamesAsync(
            lines.Select(i => i.IngredientId), ct);

        var eventRecipe = new Recipe
        {
            Id = recipe.Id,
            Title = recipe.Title,
            Author = recipe.Author,
            CreatorId = recipe.CreatorId,
            UpdatedAt = recipe.UpdatedAt,
            MealSlots = recipe.MealSlots,
            Ingredients = lines
        };
        integrationEvents.RecordUpdated(eventRecipe, ingredientNames, recipe.UpdatedAt);
        await db.SaveChangesAsync(ct);
        return await GetAsync(recipe.Id, ct);
    }

    public async Task SoftDeleteAsync(Guid id, ICurrentUser user, CancellationToken ct)
    {
        if (!user.IsAuthenticated || string.IsNullOrWhiteSpace(user.CreatorId))
            throw new ForbiddenException("Authentication required.");

        var recipe = await LoadAsync(id, tracking: true, ct);
        EnsureCanMutate(recipe, user);

        var deletedAt = DateTimeOffset.UtcNow;
        recipe.SoftDelete(deletedAt);
        integrationEvents.RecordDeleted(recipe, deletedAt);
        await db.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadIngredientNamesAsync(
        IEnumerable<Guid> ingredientIds,
        CancellationToken ct)
    {
        var ids = ingredientIds.Distinct().ToList();
        return await db.Ingredients.AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Name, ct);
    }

    private async Task<Recipe> LoadAsync(Guid id, bool tracking, CancellationToken ct)
    {
        IQueryable<Recipe> query = db.Recipes
            .Include(r => r.Ingredients).ThenInclude(ri => ri.Ingredient)
            .Include(r => r.Steps);

        if (!tracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException($"Recipe '{id}' was not found.");
    }

    private static void EnsureCanMutate(Recipe recipe, ICurrentUser user)
    {
        if (recipe.IsDeleted)
            throw new NotFoundException($"Recipe '{recipe.Id}' was not found.");
        if (recipe.IsPlatform)
            throw new ForbiddenException("Platform starter recipes are immutable. Create a new recipe via GET + POST.");
        if (!string.Equals(recipe.CreatorId, user.CreatorId, StringComparison.Ordinal))
            throw new ForbiddenException("Only the creator may update or delete this recipe. Create a new recipe instead.");
    }

    private async Task<List<RecipeIngredient>> BuildLinesAsync(
        IReadOnlyList<RecipeIngredientInput>? inputs,
        CancellationToken ct)
    {
        if (inputs is null || inputs.Count == 0)
            throw new ValidationException("ingredients", "At least one ingredient line is required.");

        var ids = inputs.Select(i => i.IngredientId).Distinct().ToList();
        var found = await db.Ingredients.AsNoTracking()
            .Where(i => ids.Contains(i.Id) && i.IsActive)
            .Select(i => i.Id)
            .ToListAsync(ct);

        var missing = ids.Except(found).ToList();
        if (missing.Count > 0)
            throw new ValidationException("ingredients",
                $"Unknown or inactive ingredient ids: {string.Join(", ", missing)}");

        var lines = new List<RecipeIngredient>();
        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            Unit unit;
            try
            {
                unit = UnitParsing.Parse(input.Unit);
            }
            catch (ArgumentException ex)
            {
                throw new ValidationException($"ingredients[{i}].unit", ex.Message);
            }

            if (input.Quantity <= 0)
                throw new ValidationException($"ingredients[{i}].quantity", "Quantity must be greater than zero.");

            lines.Add(new RecipeIngredient
            {
                Id = Guid.NewGuid(),
                IngredientId = input.IngredientId,
                Quantity = input.Quantity,
                Unit = unit,
                Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
                SortOrder = i
            });
        }

        return lines;
    }

    private static List<RecipeStep> BuildSteps(IReadOnlyList<string>? steps) =>
        (steps ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select((s, i) => new RecipeStep
            {
                Id = Guid.NewGuid(),
                StepNumber = i + 1,
                Instruction = s.Trim()
            })
            .ToList();

    private static string ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ValidationException("title", "Title is required.");
        return title.Trim();
    }

    private static string? NormalizeAuthor(string? author) =>
        string.IsNullOrWhiteSpace(author) ? null : author.Trim();

    private static List<string> NormalizeTags(IReadOnlyList<string>? tags) =>
        (tags ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<MealSlot> ParseMealSlots(IReadOnlyList<string>? slots)
    {
        if (slots is null || slots.Count == 0)
            return [];

        try
        {
            return slots.Select(MealSlotParsing.Parse).Distinct().ToList();
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException("mealSlots", ex.Message);
        }
    }

    private static RecipeDto Map(Recipe r) =>
        new(
            r.Id,
            r.Title,
            r.Author,
            r.CreatorId,
            r.IsPlatform,
            r.MealSlots.Select(MealSlotParsing.ToApi).ToList(),
            r.CuisineTags,
            r.Ingredients
                .OrderBy(i => i.SortOrder)
                .Select(i => new RecipeIngredientLineDto(
                    i.IngredientId,
                    i.Ingredient?.Name ?? string.Empty,
                    i.Quantity,
                    UnitParsing.ToApi(i.Unit),
                    i.Notes))
                .ToList(),
            r.Steps.OrderBy(s => s.StepNumber).Select(s => s.Instruction).ToList(),
            r.CreatedAt,
            r.UpdatedAt);
}
