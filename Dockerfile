# syntax=docker/dockerfile:1

# ---- Build stage: full SDK, restore before source copy for layer caching ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the csproj first so `restore` is cached until dependencies change.
COPY ["src/PartsInventory.Api/PartsInventory.Api.csproj", "src/PartsInventory.Api/"]
RUN dotnet restore "src/PartsInventory.Api/PartsInventory.Api.csproj"

# Now copy the rest of the source and publish.
COPY src/ src/
RUN dotnet publish "src/PartsInventory.Api/PartsInventory.Api.csproj" \
    -c Release -o /app/publish /p:UseAppHost=false

# Stage an empty, app-owned data directory for the SQLite file (chiseled has no shell to mkdir).
RUN mkdir -p /var/appdata

# ---- Runtime stage: slim, chiseled, non-root ----
# noble-chiseled: no shell/package manager -> much smaller attack surface. Trade-off: you
# cannot exec a shell into it to debug, and a curl-based Docker HEALTHCHECK won't work
# (no curl); probe /health from the host or an orchestrator instead.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app

COPY --from=build /app/publish .
COPY --from=build --chown=app:app /var/appdata /data

# .NET 8+ images already default to the non-root `app` user and port 8080; set both explicitly.
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
# Externalized config (never baked as a secret). Default points at the mountable /data volume.
ENV ConnectionStrings__Default="Data Source=/data/partsinventory.db"

EXPOSE 8080
ENTRYPOINT ["dotnet", "PartsInventory.Api.dll"]
