using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace RecipeHub.Api.Auth;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddRecipeHubAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        var auth = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();

        services.AddHttpContextAccessor();
        services.AddScoped<Application.Abstractions.ICurrentUser, HttpCurrentUser>();

        if (string.Equals(auth.Mode, "Google", StringComparison.OrdinalIgnoreCase))
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = auth.GoogleAuthority;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = auth.GoogleAuthority,
                        ValidateAudience = true,
                        ValidAudience = auth.GoogleAudience,
                        ValidateLifetime = true,
                        NameClaimType = "sub"
                    };
                });
        }
        else
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.DevelopmentSigningKey));
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = auth.DevelopmentIssuer,
                        ValidateAudience = true,
                        ValidAudience = auth.DevelopmentAudience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = key,
                        ValidateLifetime = true,
                        NameClaimType = "sub"
                    };
                });
        }

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddRecipeHubOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi();
        return services;
    }
}
