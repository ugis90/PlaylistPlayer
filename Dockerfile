# --- Stage 1: Build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /source

COPY PlaylistPlayer/FleetManager.sln .
COPY PlaylistPlayer/FleetManager/ ./FleetManager/
# If you have other .csproj files referenced by the .sln, copy them too
# e.g. COPY OtherProjectFolder/OtherProject.csproj ./OtherProjectFolder/

RUN dotnet restore FleetManager.sln

# COPY all source code for the main project and any referenced projects
# This ensures all .cs files are available for publish
# COPY PlaylistPlayer/FleetManager/ ./FleetManager/ # Already done
# COPY OtherProjectFolder/ ./OtherProjectFolder/ # If applicable

RUN dotnet publish ./FleetManager/FleetManager.csproj -c Release -o /app/out --no-restore

# --- Stage 2: Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build-env /app/out .

ENTRYPOINT ["dotnet", "FleetManager.dll"]
