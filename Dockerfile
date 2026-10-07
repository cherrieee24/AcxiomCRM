# syntax=docker/dockerfile:1

# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Restore first so the dependency layer is cached between code changes
COPY global.json AcxiomCRM.sln ./
COPY src/AcxiomCRM/AcxiomCRM.csproj src/AcxiomCRM/
RUN dotnet restore src/AcxiomCRM/AcxiomCRM.csproj

COPY src/ src/
RUN dotnet publish src/AcxiomCRM/AcxiomCRM.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__DefaultConnection="DataSource=/app/data/acxiomcrm.db;Cache=Shared" \
    DataProtection__KeysPath=/app/data/keys

# SQLite database + data-protection keys live on a volume, owned by the non-root "app" user
RUN mkdir -p /app/data && chown -R $APP_UID /app/data
VOLUME /app/data

COPY --from=build /app .

USER $APP_UID
EXPOSE 8080
# Health endpoint for load balancers / orchestrators: GET /health
ENTRYPOINT ["dotnet", "AcxiomCRM.dll"]
