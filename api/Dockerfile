# syntax=docker/dockerfile:1.7

# ---------- Restore stage ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS restore
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY JobTracker.sln ./

COPY src/BuildingBlocks/SharedKernel/JobTracker.SharedKernel.csproj                       src/BuildingBlocks/SharedKernel/
COPY src/BuildingBlocks/Application/JobTracker.BuildingBlocks.Application.csproj          src/BuildingBlocks/Application/
COPY src/BuildingBlocks/Infrastructure/JobTracker.BuildingBlocks.Infrastructure.csproj    src/BuildingBlocks/Infrastructure/
COPY src/BuildingBlocks/Presentation/JobTracker.BuildingBlocks.Presentation.csproj        src/BuildingBlocks/Presentation/
COPY src/Modules/Jobs/Jobs.Domain/Jobs.Domain.csproj                                      src/Modules/Jobs/Jobs.Domain/
COPY src/Modules/Jobs/Jobs.IntegrationEvents/Jobs.IntegrationEvents.csproj                src/Modules/Jobs/Jobs.IntegrationEvents/
COPY src/Modules/Jobs/Jobs.Application/Jobs.Application.csproj                            src/Modules/Jobs/Jobs.Application/
COPY src/Modules/Jobs/Jobs.Infrastructure/Jobs.Infrastructure.csproj                      src/Modules/Jobs/Jobs.Infrastructure/
COPY src/Modules/Jobs/Jobs.Presentation/Jobs.Presentation.csproj                          src/Modules/Jobs/Jobs.Presentation/
COPY src/Host/JobTracker.Api/JobTracker.Api.csproj                                        src/Host/JobTracker.Api/
COPY tests/JobTracker.Tests.Unit/JobTracker.Tests.Unit.csproj                             tests/JobTracker.Tests.Unit/
COPY tests/JobTracker.Tests.Architecture/JobTracker.Tests.Architecture.csproj             tests/JobTracker.Tests.Architecture/
COPY tests/JobTracker.Tests.Integration/JobTracker.Tests.Integration.csproj               tests/JobTracker.Tests.Integration/

RUN dotnet restore JobTracker.sln

# ---------- Build stage ----------
FROM restore AS build
COPY . .
RUN dotnet build src/Host/JobTracker.Api/JobTracker.Api.csproj \
    --configuration Release \
    --no-restore

# ---------- Publish stage ----------
FROM build AS publish
RUN dotnet publish src/Host/JobTracker.Api/JobTracker.Api.csproj \
    --configuration Release \
    --no-build \
    --output /app/publish \
    /p:UseAppHost=false

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# The base image already ships a non-root "app" user (UID 1654) since .NET 8.
COPY --from=publish --chown=app:app /app/publish .

# Ensure the uploads directory exists and is owned by "app" BEFORE dropping privileges.
# Docker copies this ownership into a named volume the first time it is mounted here.
RUN mkdir -p /app/uploads && chown -R app:app /app/uploads

USER app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD wget -qO- http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "JobTracker.Api.dll"]
