# Deployment

## Local docker-compose

```bash
docker compose up -d
# app:    http://localhost:8080
# Jaeger: http://localhost:16686
# Swagger: http://localhost:8080/swagger
```

Compose stack:

* `app` — the canteen container (this project).
* `db` — SQL Server 2022.
* `cache` — Redis 7.
* `otel-collector` — receives OTLP from the app.
* `jaeger` — visualises traces.

## Production build

The `Dockerfile` is multi-stage. Build with:

```bash
docker build -t canteen:latest .
```

Run as non-root (`uid=1000 canteen`), exposes `:8080`, has a health probe on
`/api/v1/health`.

## CI/CD

`.github/workflows/build.yml`:

* Restore + build + test on every push and PR.
* Upload TRX artifacts.
* Build (no-push) the Docker image on push to main / changing-all.

`.github/workflows/publish-image.yml`:

* On `v*.*.*` tags, build + push to `ghcr.io/{owner}/canteen:{tag}` and
  `:sha-{commit}`.

## Azure App Service

```bash
az webapp create -g canteen-rg -p canteen-plan -n canteen-prod \
  --deployment-container-image-name ghcr.io/<owner>/canteen:latest
az webapp config appsettings set -g canteen-rg -n canteen-prod --settings \
  ConnectionStrings__DefaultConnection="…" \
  ConnectionStrings__Redis="…" \
  Observability__OtlpEndpoint="https://…"
```

## Migrations

`dotnet ef` is the only sanctioned way to update schema:

```bash
dotnet ef migrations add Feature_Foo \
  --project src/CanteenManagementSystem.Infrastructure \
  --startup-project src/CanteenManagementSystem.Presentation
dotnet ef database update \
  --project src/CanteenManagementSystem.Infrastructure \
  --startup-project src/CanteenManagementSystem.Presentation
```

CI should gate `database update` behind an approval environment.
