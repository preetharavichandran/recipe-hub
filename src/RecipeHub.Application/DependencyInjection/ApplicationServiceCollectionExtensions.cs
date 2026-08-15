using Microsoft.Extensions.DependencyInjection;
using RecipeHub.Application.Abstractions;
using RecipeHub.Application.Services;

namespace RecipeHub.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IngredientService>();
        services.AddScoped<RecipeService>();
        services.AddScoped<IRecipeIntegrationEvents, RecipeIntegrationEvents>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatchService>();
        return services;
    }
}
