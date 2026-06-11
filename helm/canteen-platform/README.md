# canteen-platform — Helm chart

Deploys the canteen platform to Kubernetes.

## Install

```bash
helm install canteen ./helm/canteen-platform \
  --namespace canteen --create-namespace \
  --set image.repository=ghcr.io/your-org/canteen-platform \
  --set image.tag=1.0.0 \
  --set secrets.authSigningKey="$(openssl rand -base64 48)" \
  --set secrets.connectionString='Server=managed-sql.internal,1433;Database=canteen_saas;User Id=app;Password=…;TrustServerCertificate=True' \
  --set secrets.redisConnectionString='redis-master.cache.svc.cluster.local:6379'
```

Or with a values file:

```bash
helm install canteen ./helm/canteen-platform -f values.prod.yaml
```

## What it provisions

| Object | Purpose |
|---|---|
| Deployment | App pods (`runAsNonRoot: 10000`, OPA-friendly security context) |
| Service | ClusterIP exposing port 80 → container 8080 |
| Ingress | TLS via cert-manager (defaults to nginx + Let's Encrypt prod issuer) |
| ConfigMap | Non-secret env (Observability, Migration toggles) |
| Secret | `Auth__SigningKey`, DB connection string, optional Redis |
| PersistentVolumeClaim | DataProtection key ring (encrypted tenant settings depend on it) |
| Job (pre-install/upgrade) | `dotnet ef`-style migration run — schema applied BEFORE pods start serving |
| HorizontalPodAutoscaler | Optional CPU-based autoscaler |

## Upgrade

```bash
helm upgrade canteen ./helm/canteen-platform --reuse-values --set image.tag=1.1.0
```

The pre-upgrade Job applies the new schema. If the migration fails the upgrade
aborts and the previous pods keep serving.

## Rollback

```bash
helm rollback canteen
```

DataProtection key ring stays put — encrypted tenant settings survive.
