using RecipeHub.Api.Auth;
using RecipeHub.Api.Endpoints;
using RecipeHub.Application.DependencyInjection;
using RecipeHub.Infrastructure.DependencyInjection;
using RecipeHub.Infrastructure.Messaging;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RetentionOptions>(
    builder.Configuration.GetSection(RetentionOptions.SectionName));
builder.Services.Configure<PublishingOptions>(opts =>
{
    builder.Configuration.GetSection(PublishingOptions.SectionName).Bind(opts);
    // PLAN: PUBLISH_MODE=console|kafka|sns|both takes precedence when set.
    var publishMode = builder.Configuration["PUBLISH_MODE"];
    if (!string.IsNullOrWhiteSpace(publishMode))
        opts.Mode = publishMode.Trim();
});

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5433;Database=recipehub;Username=recipehub;Password=recipehub";

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);
builder.Services.AddRecipeHubAuth(builder.Configuration);
builder.Services.AddRecipeHubOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseRecipeHubExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("RecipeHub")
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
    app.MapGet("/", () => Results.Redirect("/scalar"))
        .ExcludeFromDescription();
}

app.MapRecipeHubEndpoints();

await app.Services.InitializeDatabaseAsync();

app.Run();

public partial class Program;
