using Microsoft.EntityFrameworkCore;
using RecipeHub.Application.Services;
using RecipeHub.Contracts.Events;
using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Enums;

namespace RecipeHub.Infrastructure.Persistence.Seed;

/// <summary>Stable seed ids so demos and tests can reference catalog ingredients.</summary>
public static class SeedIds
{
    // Staples
    public static readonly Guid Oats = Guid.Parse("11111111-1111-1111-1111-111111110001");
    public static readonly Guid Milk = Guid.Parse("11111111-1111-1111-1111-111111110002");
    public static readonly Guid Banana = Guid.Parse("11111111-1111-1111-1111-111111110003");
    public static readonly Guid Egg = Guid.Parse("11111111-1111-1111-1111-111111110004");
    public static readonly Guid Bread = Guid.Parse("11111111-1111-1111-1111-111111110005");
    public static readonly Guid Butter = Guid.Parse("11111111-1111-1111-1111-111111110006");
    public static readonly Guid Yogurt = Guid.Parse("11111111-1111-1111-1111-111111110007");
    public static readonly Guid Honey = Guid.Parse("11111111-1111-1111-1111-111111110008");
    public static readonly Guid Rice = Guid.Parse("11111111-1111-1111-1111-111111110009");
    public static readonly Guid Lentils = Guid.Parse("11111111-1111-1111-1111-11111111000a");
    public static readonly Guid Onion = Guid.Parse("11111111-1111-1111-1111-11111111000b");
    public static readonly Guid Garlic = Guid.Parse("11111111-1111-1111-1111-11111111000c");
    public static readonly Guid Tomato = Guid.Parse("11111111-1111-1111-1111-11111111000d");
    public static readonly Guid Potato = Guid.Parse("11111111-1111-1111-1111-11111111000e");
    public static readonly Guid Chicken = Guid.Parse("11111111-1111-1111-1111-11111111000f");
    public static readonly Guid OliveOil = Guid.Parse("11111111-1111-1111-1111-111111110010");
    public static readonly Guid Salt = Guid.Parse("11111111-1111-1111-1111-111111110011");
    public static readonly Guid BlackPepper = Guid.Parse("11111111-1111-1111-1111-111111110012");
    public static readonly Guid Cumin = Guid.Parse("11111111-1111-1111-1111-111111110013");
    public static readonly Guid Turmeric = Guid.Parse("11111111-1111-1111-1111-111111110014");
    public static readonly Guid Chickpeas = Guid.Parse("11111111-1111-1111-1111-111111110015");
    public static readonly Guid Spinach = Guid.Parse("11111111-1111-1111-1111-111111110016");
    public static readonly Guid Pasta = Guid.Parse("11111111-1111-1111-1111-111111110017");
    public static readonly Guid Cheese = Guid.Parse("11111111-1111-1111-1111-111111110018");
    public static readonly Guid Lemon = Guid.Parse("11111111-1111-1111-1111-111111110019");
    public static readonly Guid Cucumber = Guid.Parse("11111111-1111-1111-1111-11111111001a");
    public static readonly Guid Carrot = Guid.Parse("11111111-1111-1111-1111-11111111001b");
    public static readonly Guid Beans = Guid.Parse("11111111-1111-1111-1111-11111111001c");
    public static readonly Guid Flour = Guid.Parse("11111111-1111-1111-1111-11111111001d");
    public static readonly Guid Sugar = Guid.Parse("11111111-1111-1111-1111-11111111001e");
    public static readonly Guid Tea = Guid.Parse("11111111-1111-1111-1111-11111111001f");
    public static readonly Guid Coffee = Guid.Parse("11111111-1111-1111-1111-111111110020");
    public static readonly Guid PeanutButter = Guid.Parse("11111111-1111-1111-1111-111111110021");
    public static readonly Guid Apple = Guid.Parse("11111111-1111-1111-1111-111111110022");
    public static readonly Guid Broccoli = Guid.Parse("11111111-1111-1111-1111-111111110023");
    public static readonly Guid Salmon = Guid.Parse("11111111-1111-1111-1111-111111110024");
    public static readonly Guid Tofu = Guid.Parse("11111111-1111-1111-1111-111111110025");
    public static readonly Guid CoconutMilk = Guid.Parse("11111111-1111-1111-1111-111111110026");
    public static readonly Guid Ginger = Guid.Parse("11111111-1111-1111-1111-111111110027");
    public static readonly Guid Chili = Guid.Parse("11111111-1111-1111-1111-111111110028");
    public static readonly Guid Coriander = Guid.Parse("11111111-1111-1111-1111-111111110029");
    public static readonly Guid SoySauce = Guid.Parse("11111111-1111-1111-1111-11111111002a");
    public static readonly Guid Tortilla = Guid.Parse("11111111-1111-1111-1111-11111111002b");
    public static readonly Guid Avocado = Guid.Parse("11111111-1111-1111-1111-11111111002c");
    public static readonly Guid BellPepper = Guid.Parse("11111111-1111-1111-1111-11111111002d");
    public static readonly Guid Mushrooms = Guid.Parse("11111111-1111-1111-1111-11111111002e");
    public static readonly Guid Water = Guid.Parse("11111111-1111-1111-1111-11111111002f");

    public static readonly Guid StarterOatmeal = Guid.Parse("22222222-2222-2222-2222-222222220001");
    public static readonly Guid StarterEggsToast = Guid.Parse("22222222-2222-2222-2222-222222220002");
    public static readonly Guid StarterYogurtBowl = Guid.Parse("22222222-2222-2222-2222-222222220003");
    public static readonly Guid StarterDalRice = Guid.Parse("22222222-2222-2222-2222-222222220004");
    public static readonly Guid StarterPasta = Guid.Parse("22222222-2222-2222-2222-222222220005");
    public static readonly Guid StarterChickenRice = Guid.Parse("22222222-2222-2222-2222-222222220006");
    public static readonly Guid StarterChickpeaCurry = Guid.Parse("22222222-2222-2222-2222-222222220007");
    public static readonly Guid StarterVegStirFry = Guid.Parse("22222222-2222-2222-2222-222222220008");
}

public static class CatalogSeed
{
    public static async Task EnsureSeededAsync(RecipeHubDbContext db, CancellationToken ct = default)
    {
        if (!await db.Ingredients.AnyAsync(ct))
        {
            var now = DateTimeOffset.UtcNow;
            var ingredients = BuildIngredients(now);
            db.Ingredients.AddRange(ingredients);
            await db.SaveChangesAsync(ct);

            var starters = BuildStarters(now);
            db.Recipes.AddRange(starters);
            RecordStarterCreatedEvents(db, starters, ingredients.ToDictionary(i => i.Id, i => i.Name), now);
            await db.SaveChangesAsync(ct);
            return;
        }

        // Existing DBs seeded before outbox: emit created events once for platform starters.
        await EnsureStarterCreatedEventsAsync(db, ct);
    }

    private static async Task EnsureStarterCreatedEventsAsync(RecipeHubDbContext db, CancellationToken ct)
    {
        var starters = await db.Recipes
            .Include(r => r.Ingredients)
            .Where(r => r.IsPlatform)
            .ToListAsync(ct);
        if (starters.Count == 0)
            return;

        var starterIds = starters.Select(r => r.Id).ToList();
        var alreadyEmitted = await db.OutboxMessages
            .Where(m => starterIds.Contains(m.AggregateId) && m.EventType == RecipeEventTypes.Created)
            .Select(m => m.AggregateId)
            .ToListAsync(ct);

        var missing = starters.Where(r => !alreadyEmitted.Contains(r.Id)).ToList();
        if (missing.Count == 0)
            return;

        var ingredientIds = missing.SelectMany(r => r.Ingredients.Select(i => i.IngredientId)).Distinct().ToList();
        var names = await db.Ingredients.AsNoTracking()
            .Where(i => ingredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Name, ct);

        var now = DateTimeOffset.UtcNow;
        RecordStarterCreatedEvents(db, missing, names, now);
        await db.SaveChangesAsync(ct);
    }

    private static void RecordStarterCreatedEvents(
        RecipeHubDbContext db,
        IEnumerable<Recipe> starters,
        IReadOnlyDictionary<Guid, string> ingredientNames,
        DateTimeOffset occurredAt)
    {
        foreach (var recipe in starters)
        {
            db.OutboxMessages.Add(RecipeEventMapper.CreateOutboxMessage(
                RecipeEventTypes.Created,
                recipe.Id,
                occurredAt,
                RecipeEventMapper.ToCreatedOrUpdated(recipe, ingredientNames)));
        }
    }

    private static List<Ingredient> BuildIngredients(DateTimeOffset now)
    {
        Ingredient I(Guid id, string name, Unit unit, params string[] aliases) => new()
        {
            Id = id,
            Name = name,
            DefaultUnit = unit,
            IsActive = true,
            CreatedAt = now,
            Aliases = aliases.Select(a => new IngredientAlias
            {
                Id = Guid.NewGuid(),
                IngredientId = id,
                Alias = a
            }).ToList()
        };

        return
        [
            I(SeedIds.Oats, "Oats", Unit.G, "rolled oats", "oatmeal"),
            I(SeedIds.Milk, "Milk", Unit.Ml, "whole milk", "dairy milk"),
            I(SeedIds.Banana, "Banana", Unit.Pcs, "bananas"),
            I(SeedIds.Egg, "Egg", Unit.Pcs, "eggs"),
            I(SeedIds.Bread, "Bread", Unit.Pcs, "toast", "sliced bread"),
            I(SeedIds.Butter, "Butter", Unit.G, "unsalted butter"),
            I(SeedIds.Yogurt, "Yogurt", Unit.G, "yoghurt", "plain yogurt"),
            I(SeedIds.Honey, "Honey", Unit.Ml, "raw honey"),
            I(SeedIds.Rice, "Rice", Unit.G, "basmati", "white rice"),
            I(SeedIds.Lentils, "Lentils", Unit.G, "dal", "red lentils", "masoor dal"),
            I(SeedIds.Onion, "Onion", Unit.Pcs, "onions", "yellow onion"),
            I(SeedIds.Garlic, "Garlic", Unit.Pcs, "garlic clove", "garlic cloves"),
            I(SeedIds.Tomato, "Tomato", Unit.Pcs, "tomatoes"),
            I(SeedIds.Potato, "Potato", Unit.Pcs, "potatoes"),
            I(SeedIds.Chicken, "Chicken", Unit.G, "chicken breast", "chicken thigh"),
            I(SeedIds.OliveOil, "Olive oil", Unit.Ml, "extra virgin olive oil", "EVOO"),
            I(SeedIds.Salt, "Salt", Unit.G, "table salt", "sea salt"),
            I(SeedIds.BlackPepper, "Black pepper", Unit.G, "pepper", "ground pepper"),
            I(SeedIds.Cumin, "Cumin", Unit.G, "jeera", "cumin seeds"),
            I(SeedIds.Turmeric, "Turmeric", Unit.G, "haldi", "turmeric powder"),
            I(SeedIds.Chickpeas, "Chickpeas", Unit.G, "chana", "garbanzo beans"),
            I(SeedIds.Spinach, "Spinach", Unit.G, "baby spinach"),
            I(SeedIds.Pasta, "Pasta", Unit.G, "spaghetti", "penne"),
            I(SeedIds.Cheese, "Cheese", Unit.G, "cheddar", "parmesan"),
            I(SeedIds.Lemon, "Lemon", Unit.Pcs, "lemons"),
            I(SeedIds.Cucumber, "Cucumber", Unit.Pcs, "cucumbers"),
            I(SeedIds.Carrot, "Carrot", Unit.Pcs, "carrots"),
            I(SeedIds.Beans, "Beans", Unit.G, "black beans", "kidney beans"),
            I(SeedIds.Flour, "Flour", Unit.G, "all-purpose flour", "wheat flour"),
            I(SeedIds.Sugar, "Sugar", Unit.G, "white sugar"),
            I(SeedIds.Tea, "Tea", Unit.G, "black tea", "tea bags"),
            I(SeedIds.Coffee, "Coffee", Unit.G, "ground coffee"),
            I(SeedIds.PeanutButter, "Peanut butter", Unit.G, "PB"),
            I(SeedIds.Apple, "Apple", Unit.Pcs, "apples"),
            I(SeedIds.Broccoli, "Broccoli", Unit.G, "broccoli florets"),
            I(SeedIds.Salmon, "Salmon", Unit.G, "salmon fillet"),
            I(SeedIds.Tofu, "Tofu", Unit.G, "firm tofu"),
            I(SeedIds.CoconutMilk, "Coconut milk", Unit.Ml, "coconut cream"),
            I(SeedIds.Ginger, "Ginger", Unit.G, "fresh ginger", "ginger root"),
            I(SeedIds.Chili, "Chili", Unit.Pcs, "chilli", "green chili", "red chili"),
            I(SeedIds.Coriander, "Coriander", Unit.G, "cilantro", "fresh coriander"),
            I(SeedIds.SoySauce, "Soy sauce", Unit.Ml, "soya sauce"),
            I(SeedIds.Tortilla, "Tortilla", Unit.Pcs, "wraps", "flour tortilla"),
            I(SeedIds.Avocado, "Avocado", Unit.Pcs, "avocados"),
            I(SeedIds.BellPepper, "Bell pepper", Unit.Pcs, "capsicum", "pepper"),
            I(SeedIds.Mushrooms, "Mushrooms", Unit.G, "button mushrooms"),
            I(SeedIds.Water, "Water", Unit.Ml, "tap water")
        ];
    }

    private static List<Recipe> BuildStarters(DateTimeOffset now)
    {
        Recipe R(Guid id, string title, MealSlot slot, string[] steps, params (Guid ing, decimal qty, Unit unit)[] lines) =>
            new()
            {
                Id = id,
                Title = title,
                Author = "RecipeHub",
                CreatorId = null,
                IsPlatform = true,
                CreatedAt = now,
                UpdatedAt = now,
                MealSlots = [slot],
                CuisineTags = [],
                Steps = steps.Select((s, i) => new RecipeStep
                {
                    Id = Guid.NewGuid(),
                    RecipeId = id,
                    StepNumber = i + 1,
                    Instruction = s
                }).ToList(),
                Ingredients = lines.Select((l, i) => new RecipeIngredient
                {
                    Id = Guid.NewGuid(),
                    RecipeId = id,
                    IngredientId = l.ing,
                    Quantity = l.qty,
                    Unit = l.unit,
                    SortOrder = i
                }).ToList()
            };

        return
        [
            R(SeedIds.StarterOatmeal, "Weekday oatmeal", MealSlot.Breakfast,
                ["Bring oats and milk to a simmer.", "Cook 5 minutes, top with banana and honey."],
                (SeedIds.Oats, 60, Unit.G), (SeedIds.Milk, 250, Unit.Ml), (SeedIds.Banana, 1, Unit.Pcs), (SeedIds.Honey, 15, Unit.Ml)),

            R(SeedIds.StarterEggsToast, "Eggs on toast", MealSlot.Breakfast,
                ["Toast the bread.", "Fry or scramble eggs in butter.", "Season and serve on toast."],
                (SeedIds.Egg, 2, Unit.Pcs), (SeedIds.Bread, 2, Unit.Pcs), (SeedIds.Butter, 10, Unit.G), (SeedIds.Salt, 1, Unit.G)),

            R(SeedIds.StarterYogurtBowl, "Yogurt fruit bowl", MealSlot.Breakfast,
                ["Spoon yogurt into a bowl.", "Add banana and honey."],
                (SeedIds.Yogurt, 200, Unit.G), (SeedIds.Banana, 1, Unit.Pcs), (SeedIds.Honey, 10, Unit.Ml)),

            R(SeedIds.StarterDalRice, "Simple dal and rice", MealSlot.Dinner,
                ["Rinse lentils; simmer with turmeric and salt until soft.", "Sauté onion, garlic, cumin in oil; stir into dal.", "Serve with rice."],
                (SeedIds.Lentils, 150, Unit.G), (SeedIds.Rice, 150, Unit.G), (SeedIds.Onion, 1, Unit.Pcs),
                (SeedIds.Garlic, 2, Unit.Pcs), (SeedIds.Turmeric, 2, Unit.G), (SeedIds.Cumin, 2, Unit.G),
                (SeedIds.OliveOil, 15, Unit.Ml), (SeedIds.Salt, 3, Unit.G), (SeedIds.Water, 600, Unit.Ml)),

            R(SeedIds.StarterPasta, "Tomato garlic pasta", MealSlot.Dinner,
                ["Boil pasta.", "Sauté garlic and tomato in olive oil.", "Toss with pasta, salt, and pepper."],
                (SeedIds.Pasta, 200, Unit.G), (SeedIds.Tomato, 3, Unit.Pcs), (SeedIds.Garlic, 3, Unit.Pcs),
                (SeedIds.OliveOil, 20, Unit.Ml), (SeedIds.Salt, 3, Unit.G), (SeedIds.BlackPepper, 1, Unit.G)),

            R(SeedIds.StarterChickenRice, "Chicken and rice bowl", MealSlot.Dinner,
                ["Season chicken; pan-cook in oil.", "Cook rice.", "Serve chicken over rice with lemon."],
                (SeedIds.Chicken, 300, Unit.G), (SeedIds.Rice, 150, Unit.G), (SeedIds.OliveOil, 15, Unit.Ml),
                (SeedIds.Salt, 3, Unit.G), (SeedIds.BlackPepper, 1, Unit.G), (SeedIds.Lemon, 1, Unit.Pcs)),

            R(SeedIds.StarterChickpeaCurry, "Chickpea curry", MealSlot.Dinner,
                ["Sauté onion, garlic, ginger, chili.", "Add tomato, spices, chickpeas, coconut milk; simmer.", "Finish with coriander."],
                (SeedIds.Chickpeas, 250, Unit.G), (SeedIds.Onion, 1, Unit.Pcs), (SeedIds.Garlic, 2, Unit.Pcs),
                (SeedIds.Ginger, 10, Unit.G), (SeedIds.Tomato, 2, Unit.Pcs), (SeedIds.CoconutMilk, 200, Unit.Ml),
                (SeedIds.Cumin, 2, Unit.G), (SeedIds.Turmeric, 2, Unit.G), (SeedIds.Chili, 1, Unit.Pcs),
                (SeedIds.Coriander, 10, Unit.G), (SeedIds.Salt, 3, Unit.G), (SeedIds.OliveOil, 15, Unit.Ml)),

            R(SeedIds.StarterVegStirFry, "Veg stir-fry", MealSlot.Dinner,
                ["Stir-fry vegetables in oil.", "Add soy sauce and serve over rice or alone."],
                (SeedIds.Broccoli, 200, Unit.G), (SeedIds.BellPepper, 1, Unit.Pcs), (SeedIds.Carrot, 1, Unit.Pcs),
                (SeedIds.Mushrooms, 100, Unit.G), (SeedIds.SoySauce, 30, Unit.Ml), (SeedIds.Garlic, 2, Unit.Pcs),
                (SeedIds.OliveOil, 15, Unit.Ml), (SeedIds.Rice, 150, Unit.G))
        ];
    }
}
