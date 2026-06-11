# =============================================================================
# Canteen Management System — Multi-stage Dockerfile
# -----------------------------------------------------------------------------
# Stage 1 (build): restore + publish a self-contained, trimmed Release.
# Stage 2 (runtime): copy the publish output onto the aspnet runtime image.
#
# BUILD
#   docker build -t canteen:latest .
#
# RUN (compose with SQL Server + Redis)
#   docker run -p 8080:8080
#     -e ConnectionStrings__DefaultConnection="Server=db;..."
#     -e ConnectionStrings__Redis="redis:6379"
#     canteen:latest
# =============================================================================

ARG DOTNET_VERSION=10.0

# ----------------------------- build stage ----------------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

# Copy only csproj files first for cached restore.
COPY ["CanteenManagementSystem.sln", "./"]
COPY ["src/CanteenManagementSystem.Domain/CanteenManagementSystem.Domain.csproj", "src/CanteenManagementSystem.Domain/"]
COPY ["src/CanteenManagementSystem.Application/CanteenManagementSystem.Application.csproj", "src/CanteenManagementSystem.Application/"]
COPY ["src/CanteenManagementSystem.Infrastructure/CanteenManagementSystem.Infrastructure.csproj", "src/CanteenManagementSystem.Infrastructure/"]
COPY ["src/CanteenManagementSystem.Presentation/CanteenManagementSystem.Presentation.csproj", "src/CanteenManagementSystem.Presentation/"]
RUN dotnet restore "src/CanteenManagementSystem.Presentation/CanteenManagementSystem.Presentation.csproj"

# Copy the rest of the source.
COPY . .
WORKDIR /src/src/CanteenManagementSystem.Presentation
RUN dotnet publish -c Release -o /app /p:UseAppHost=false

# ----------------------------- runtime stage -------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

# Run as non-root for least-privilege.
RUN groupadd --system --gid 1000 canteen \
 && useradd --system --uid 1000 --gid canteen --no-create-home canteen
USER canteen

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_USE_POLLING_FILE_WATCHER=false

EXPOSE 8080
COPY --from=build /app ./

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD wget --quiet --tries=1 --spider http://localhost:8080/api/v1/health || exit 1

ENTRYPOINT ["dotnet", "CanteenManagementSystem.Presentation.dll"]
