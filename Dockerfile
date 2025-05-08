# --- Stage 1: Build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:35792ea4ad1db051981f62b313f1be3b46b1f45cadbaa3c288cd0d3056eefb83 AS build-env
WORKDIR /source

# Copy the solution file
# Source: PlaylistPlayer/FleetManager.sln relative to build context (root)
# Destination: ./ relative to WORKDIR (/source)
COPY PlaylistPlayer/FleetManager.sln .

# Copy the project directory containing the .csproj file and source code
# Source: PlaylistPlayer/FleetManager/ relative to build context (root)
# Destination: ./FleetManager/ relative to WORKDIR (/source)
COPY PlaylistPlayer/FleetManager/ ./FleetManager/

# Restore dependencies using the solution file (now located at /source/FleetManager.sln)
# It should find the project at ./FleetManager/FleetManager.csproj relative to the .sln
RUN dotnet restore FleetManager.sln

# Publish the specific FleetManager project
# Path is relative to WORKDIR (/source)
RUN dotnet publish ./FleetManager/FleetManager.csproj -c Release -o /app/out

# --- Stage 2: Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0@sha256:6c4df091e4e531bb93bdbfe7e7f0998e7ced344f54426b7e874116a3dc3233ff
WORKDIR /app
COPY --from=build-env /app/out .

ENTRYPOINT ["dotnet", "FleetManager.dll"]
