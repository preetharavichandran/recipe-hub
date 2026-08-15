FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY RecipeHub.sln ./
COPY src/RecipeHub.Domain/RecipeHub.Domain.csproj src/RecipeHub.Domain/
COPY src/RecipeHub.Application/RecipeHub.Application.csproj src/RecipeHub.Application/
COPY src/RecipeHub.Infrastructure/RecipeHub.Infrastructure.csproj src/RecipeHub.Infrastructure/
COPY src/RecipeHub.Contracts/RecipeHub.Contracts.csproj src/RecipeHub.Contracts/
COPY src/RecipeHub.Api/RecipeHub.Api.csproj src/RecipeHub.Api/
RUN dotnet restore src/RecipeHub.Api/RecipeHub.Api.csproj
COPY src/ src/
RUN dotnet publish src/RecipeHub.Api/RecipeHub.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "RecipeHub.Api.dll"]
