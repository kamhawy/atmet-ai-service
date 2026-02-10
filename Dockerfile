FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj files and restore as distinct layers for better caching
COPY src/ATMET.AI.Core/ATMET.AI.Core.csproj src/ATMET.AI.Core/
COPY src/ATMET.AI.Infrastructure/ATMET.AI.Infrastructure.csproj src/ATMET.AI.Infrastructure/
COPY src/ATMET.AI.Api/ATMET.AI.Api.csproj src/ATMET.AI.Api/
RUN dotnet restore src/ATMET.AI.Api/ATMET.AI.Api.csproj

# Copy everything and build
COPY src/ src/
WORKDIR /src/src/ATMET.AI.Api
RUN dotnet build -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Run as non-root user
USER $APP_UID

HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "ATMET.AI.Api.dll"]
