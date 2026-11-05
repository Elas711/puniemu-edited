# Build:  docker build -t puniemu .
# Run:    see docker-compose.yml (includes PostgreSQL)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Puniemu.csproj ./
RUN dotnet restore Puniemu.csproj
COPY . ./
RUN dotnet publish Puniemu.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Game data and assets are mounted at runtime (see docker-compose.yml), so the image contains no game files.
ENV PORT=8080 \
    PUNIEMU_RESOURCES_PATH=/app/Resources \
    ServerStatePath=/app/state/server_state.json
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD wget -qO- http://localhost:8080/healthz || exit 1
ENTRYPOINT ["dotnet", "Puniemu.dll"]
