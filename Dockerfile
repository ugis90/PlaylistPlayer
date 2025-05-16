# --- Stage 1: Build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /source

# Copy solution file
COPY PlaylistPlayer/FleetManager.sln .

# Copy main project
COPY PlaylistPlayer/FleetManager/ ./FleetManager/

COPY PlaylistPlayer/FleetManager.Tests/ ./FleetManager.Tests/

# Restore dependencies for the entire solution
RUN dotnet restore FleetManager.sln

# Publish the specific FleetManager project
RUN dotnet publish ./FleetManager/FleetManager.csproj -c Release -o /app/out --no-restore

# --- Stage 2: Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build-env /app/out .

ENTRYPOINT ["dotnet", "FleetManager.dll"]
