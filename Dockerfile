# ── Stage 1: build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy .csproj files first — Docker layer-cache: restore only re-runs when
# dependency files change, not on every source edit.
COPY AgriPredict.Core/AgriPredict.Core.csproj             AgriPredict.Core/
COPY AgriPredict.DataIngestion/AgriPredict.DataIngestion.csproj AgriPredict.DataIngestion/
COPY AgriPredict.Training/AgriPredict.Training.csproj     AgriPredict.Training/
COPY AgriPredict.Api/AgriPredict.Api.csproj               AgriPredict.Api/
RUN dotnet restore AgriPredict.Api/AgriPredict.Api.csproj

# Copy the rest of the source and publish
COPY . .
RUN dotnet publish AgriPredict.Api/AgriPredict.Api.csproj \
    -c Release -o /app/publish --no-restore

# ── Stage 2: runtime ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install curl for the docker-compose healthcheck
# aspnet:8.0 is Debian-based but ships without curl
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Pre-create volume mount targets so Docker doesn't create them as root-owned
RUN mkdir -p /app/data /app/logs

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "AgriPredict.Api.dll"]
