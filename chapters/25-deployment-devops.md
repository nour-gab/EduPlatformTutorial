# Chapter 25 — Deployment & DevOps

> **Learning goals** — by the end of this chapter you can:
> 1. Write a multi-stage Dockerfile for the LMS that builds, tests, and packages.
> 2. Configure `docker-compose.yml` for local dev (SQL Server + app) and prod (separate per service).
> 3. Build a GitHub Actions CI/CD pipeline that builds, tests, and deploys on each push.
> 4. Bundle EF Core migrations so they apply on container startup.
> 5. Plan a blue/green deployment with rollback.

---

## 25.1 The multi-stage Dockerfile

A single Dockerfile produces a small production image. Multi-stage builds keep the build tools out of the runtime image — 1 GB SDK image at build time, 100 MB runtime image at run time.

```dockerfile
# EduPlatform.Web/Dockerfile

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files first (better layer caching)
COPY ["src/EduPlatform.Web/EduPlatform.Web.csproj", "src/EduPlatform.Web/"]
COPY ["src/EduPlatform.Application/EduPlatform.Application.csproj", "src/EduPlatform.Application/"]
COPY ["src/EduPlatform.Domain/EduPlatform.Domain.csproj", "src/EduPlatform.Domain/"]
COPY ["src/EduPlatform.Infrastructure/EduPlatform.Infrastructure.csproj", "src/EduPlatform.Infrastructure/"]
COPY ["src/EduPlatform.Persistence/EduPlatform.Persistence.csproj", "src/EduPlatform.Persistence/"]
RUN dotnet restore "src/EduPlatform.Web/EduPlatform.Web.csproj"

# Copy source and build
COPY . .
WORKDIR /src/src/EduPlatform.Web
RUN dotnet publish -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "EduPlatform.Web.dll"]
```

A few things:

1. **`COPY *.csproj` first, then `RUN dotnet restore`** — only re-runs restore when `.csproj` files change. Most builds skip this step (cached layer).
2. **`/p:UseAppHost=false`** — don't generate a native executable; we'll run via `dotnet EduPlatform.Web.dll`. Smaller image.
3. **`mcr.microsoft.com/dotnet/aspnet:8.0`** runtime image — includes the ASP.NET Core runtime but not the SDK. ~100 MB.
4. **Port 8080, not 80** — non-root containers can't bind to port 80. The load balancer (nginx, App Service) terminates SSL and forwards to 8080.

> **Architect's note — *Tag your images with the git SHA, not "latest".***
> Production images should be tagged with the git commit SHA (`eduplatform:abc1234`), not `eduplatform:latest`. `latest` is non-reversible: when something breaks, you can't tell which version was running. SHA-tagged images let you say "production is on `abc1234`; we rolled back to `def5678`".

---

## 25.2 `docker-compose` for local dev

```yaml
# docker-compose.yml (root)
version: "3.9"
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "YourStrong!Passw0rd"
      MSSQL_PID: "Express"
    ports:
      - "1433:1433"
    volumes:
      - sqldata:/var/opt/mssql
    healthcheck:
      test: ["CMD", "/opt/mssql-tools/bin/sqlcmd", "-S", "localhost", "-U", "sa", "-P", "YourStrong!Passw0rd", "-Q", "SELECT 1"]
      interval: 5s
      timeout: 3s
      retries: 10

  web:
    build:
      context: .
      dockerfile: src/EduPlatform.Web/Dockerfile
    ports:
      - "8080:8080"
    environment:
      ConnectionStrings__DefaultConnection: "Server=sqlserver;Database=EduPlatform;User=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=true;MultipleActiveResultSets=true"
      ASPNETCORE_ENVIRONMENT: "Development"
      Serilog__MinimumLevel__Default: "Information"
    depends_on:
      sqlserver:
        condition: service_healthy
    volumes:
      - ./:/src  # hot reload in dev

volumes:
  sqldata:
```

Run with `docker-compose up`. The web container waits for SQL Server to be healthy before starting.

> **Architect's note — *`docker-compose` for dev vs Kubernetes for prod.***
> `docker-compose` is the right tool for local dev — one command, one host, all services. Kubernetes is the right tool for prod — multiple replicas, rolling updates, autoscaling. Don't try to use Kubernetes for local dev (minikube, kind) — the cognitive overhead kills productivity. Use compose locally; ship to K8s in prod. The Dockerfile is shared.

---

## 25.3 The CI/CD pipeline — GitHub Actions

```yaml
# .github/workflows/ci.yml
name: CI
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Unit tests
        run: dotnet test --no-build --configuration Release --filter "FullyQualifiedName~UnitTests"

      - name: Integration tests
        run: dotnet test --no-build --configuration Release --filter "FullyQualifiedName~IntegrationTests"

      - name: Docker build
        run: docker build -f src/EduPlatform.Web/Dockerfile -t eduplatform:${{ github.sha }} .

      - name: Login to container registry
        if: github.ref == 'refs/heads/main'
        uses: azure/docker-login@v1
        with:
          login-server: eduplatformregistry.azurecr.io
          username: ${{ secrets.ACR_USERNAME }}
          password: ${{ secrets.ACR_PASSWORD }}

      - name: Push image
        if: github.ref == 'refs/heads/main'
        run: |
          docker tag eduplatform:${{ github.sha }} eduplatformregistry.azurecr.io/eduplatform:${{ github.sha }}
          docker tag eduplatform:${{ github.sha }} eduplatformregistry.azurecr.io/eduplatform:latest
          docker push eduplatformregistry.azurecr.io/eduplatform:${{ github.sha }}
          docker push eduplatformregistry.azurecr.io/eduplatform:latest

  deploy-staging:
    needs: build-test
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    steps:
      - name: Deploy to Azure App Service (staging slot)
        uses: azure/webapps-deploy@v3
        with:
          app-name: 'eduplatform-prod'
          slot-name: 'staging'
          images: 'eduplatformregistry.azurecr.io/eduplatform:${{ github.sha }}'

      - name: Apply EF migrations (staging)
        run: |
          docker run --rm \
            -e ConnectionStrings__DefaultConnection=${{ secrets.STAGING_DB }} \
            eduplatformregistry.azurecr.io/eduplatform:${{ github.sha }} \
            dotnet ef database update --no-build --project src/EduPlatform.Persistence --startup-project src/EduPlatform.Web

      - name: Swap staging → production
        uses: azure/webapps-deploy@v3
        with:
          app-name: 'eduplatform-prod'
          slot-name: 'production'
          images: 'eduplatformregistry.azurecr.io/eduplatform:${{ github.sha }}'
```

The pipeline:
1. Builds, runs unit + integration tests.
2. Builds the Docker image, tags with the git SHA.
3. On `main` push, pushes the image to Azure Container Registry.
4. Deploys to the staging slot of Azure App Service.
5. Applies migrations against the staging DB.
6. Swaps staging → production (zero-downtime swap).

> **Architect's note — *CI is non-negotiable. CD is conditional.***
> CI (build + test on every push) should be mandatory for any team > 1 person. CD (auto-deploy on main push) is a maturity milestone — adopt when:
> - Your test suite is fast (< 10 min).
> - You trust your tests (no "I'll deploy and watch" mindset).
> - You have a rollback strategy.
> Until then, deploy manually from `main` after a CI green build. The CI builds the artifact; you promote it.

---

## 25.4 EF Core migrations on startup — the bundle

Instead of running `dotnet ef database update` (which requires the SDK), we bundle migrations into a self-contained executable:

```bash
dotnet ef migrations bundle \
    --project src/EduPlatform.Persistence \
    --startup-project src/EduPlatform.Web \
    --output ./ef-bundle \
    --configuration Release \
    --self-contained
```

This produces a single executable `ef-bundle` that runs the migrations against the configured DB. The bundle is part of the Docker image; on container startup:

```csharp
// Program.cs (Web)
if (!app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync(); // applies pending migrations
}
```

Or, for finer control, run the bundle as a Kubernetes init container:

```yaml
# kubernetes/deployment.yaml
spec:
  initContainers:
    - name: migrate
      image: eduplatformregistry.azurecr.io/eduplatform:abc1234
      command: ["/app/ef-bundle", "--connection", "$(ConnectionStrings__DefaultConnection)"]
      env:
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: db-connection
              key: connection
  containers:
    - name: app
      image: eduplatformregistry.azurecr.io/eduplatform:abc1234
      # ...
```

The init container runs first, applies migrations, then the app container starts. If migrations fail, the app doesn't start — protecting against a deploy that breaks the schema.

> **Architect's note — *Forward-only migrations; never roll back.***
> A common mistake: "I'll write a migration, ship it, then write a `Down()` migration to undo it if something breaks." This never works in practice — by the time you know it broke, you've already inserted data with the new schema. Rolling back would lose data.
> The right approach: forward-only migrations + a forward fix migration. If `AddColumnX` breaks prod, write a new migration `RemoveColumnX`. The rollback strategy is *another forward migration*, not a `Down()`.

---

## 25.5 Blue/green deployment

In blue/green, you maintain two identical environments ("blue" and "green"). New deploys go to the inactive one; you test it; you switch traffic.

For Azure App Service, this is built-in via deployment *slots*:

1. Production is on the "production" slot.
2. New deploy goes to "staging" slot.
3. Test staging.
4. "Swap" staging ↔ production. Azure routes new connections to staging (now production). Old connections drain from the old slot.
5. The old production slot becomes "staging" for the next deploy.

For Kubernetes, blue/green is a `Deployment` with a `selector` swap:

```yaml
# v1 deployment (blue)
apiVersion: apps/v1
kind: Deployment
metadata:
  name: eduplatform-blue
spec:
  replicas: 3
  selector:
    matchLabels:
      app: eduplatform
      version: blue
  template:
    metadata:
      labels:
        app: eduplatform
        version: blue
    spec:
      containers:
        - name: app
          image: eduplatformregistry.azurecr.io/eduplatform:abc1234

# Service routes to "blue" (current production)
---
apiVersion: v1
kind: Service
metadata:
  name: eduplatform
spec:
  selector:
    app: eduplatform
    version: blue
  ports:
    - port: 80
      targetPort: 8080
```

To deploy v2:
1. Create `eduplatform-green` deployment with `version: green` and the new image.
2. Wait for green to be healthy.
3. Update the Service's `selector.version` from `blue` to `green`. Traffic now goes to green.
4. Delete blue (or keep for rollback).

> **Architect's note — *Blue/green for schema changes is hard.***
> Blue/green is straightforward for stateless app servers. For database schema changes, it's tricky:
> - You can't have two app versions using different schemas simultaneously.
> - A migration is applied once, then both blue and green see the new schema.
> The pattern: deploy "expand" migrations (add columns, never remove). Old app version tolerates new columns (ignores them). New app version uses them. After green is stable, deploy "contract" migration (drop the unused columns). This two-phase migration is called *expand-and-contract*.

---

## 25.6 Azure App Service vs Kubernetes vs on-prem

| Concern | Azure App Service | Kubernetes | On-prem (IIS) |
|---------|-------------------|------------|---------------|
| Setup | Minutes | Days | Weeks |
| Ops overhead | Trivial | Significant | Heavy |
| Cost (small scale) | Cheap | Expensive (control plane) | Sunk cost |
| Cost (large scale) | Linear | Better than linear | Fixed |
| Autoscaling | Built-in | HPA, KEDA | Manual |
| Rollback | Slot swap | Deployment rollback | Manual |
| Multi-cloud | Azure only | Cloud-agnostic | N/A |

For an LMS at the scale of this tutorial:
- **Day 1 to 1000 users**: Azure App Service. Minutes to set up; trivial ops.
- **1000 to 100k users**: App Service Premium or Azure Container Apps (managed K8s).
- **100k+ users or multi-region**: Kubernetes with Helm.

> **Architect's note — *Don't start with Kubernetes.***
> A startup that begins on Kubernetes will spend 30% of engineering time on K8s operations for the first 6 months. App Service gives you 95% of the benefit (autoscaling, zero-downtime deploys, slots, monitoring) at 5% of the cognitive cost. Move to K8s when:
> - You need multi-cloud (vendor lock-in concern).
> - You have a 24/7 SRE team.
> - Your workloads are heterogeneous (stateful + stateless + batch).
> Until then: App Service.

---

## 25.7 Monitoring & alerting

### 25.7.1 Application Insights (Azure)

```csharp
// Program.cs
builder.Services.AddApplicationInsightsTelemetry(opts =>
{
    opts.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
});
```

App Insights gives you:
- Request traces (every HTTP request with latency, status).
- Exception traces.
- Dependency traces (every SQL query, HTTP call).
- Live metrics stream (req/s, error rate, in real time).
- Custom events (e.g., `EnrollButtonClicked`).

### 25.7.2 Alerts

```csharp
// In a controller, log a custom event
_telemetryClient.TrackEvent("EnrollmentCompleted", new Dictionary<string, string>
{
    ["CourseId"] = courseId.ToString(),
    ["StudentId"] = studentId.ToString(),
});
```

In Azure Portal, set up alerts:
- "If error rate > 1% for 5 min, email the on-call".
- "If p99 latency > 2s for 5 min, page the on-call".
- "If App Service CPU > 80% for 10 min, scale up".

> **Architect's note — *Alerts are for humans; metrics are for systems.***
> Every alert should be actionable: when it fires, the on-call knows what to do. "Error rate > 1%" is actionable (investigate, rollback if needed). "CPU > 80%" is not actionable on its own — it might be normal (big batch job). Distinguish *alerts* (page a human) from *metrics* (display on a dashboard).

---

## 25.8 Hands-on exercise

> 1. Build the Dockerfile locally: `docker build -f src/EduPlatform.Web/Dockerfile -t eduplatform:test .`. Confirm the image size is < 200 MB.
> 2. Run `docker-compose up`. Hit `http://localhost:8080`. Confirm the app connects to the SQL Server container.
> 3. Generate the EF bundle: `dotnet ef migrations bundle ...`. Run it against the dev DB: `./ef-bundle --connection "..."`. Confirm it applies migrations.
> 4. Set up the GitHub Actions workflow in a fork of the repo. Trigger a push, watch the pipeline.
> 5. Configure an Azure App Service (free tier), deploy your image. Hit the public URL.

---

## 25.9 Further reading

- Microsoft Docs, *"Container tools in Visual Studio"* — learn.microsoft.com/visualstudio/containers/.
- Microsoft Docs, *"Deploy to Azure App Service"* — learn.microsoft.com/azure/app-service/.
- Kelsey Hightower, *"Kubernetes the Hard Way"* — github.com/kelseyhightower/kubernetes-the-hard-way.
- Application Insights documentation — learn.microsoft.com/azure/azure-monitor/app/app-insights-overview.
- Charity Majors, *"Observability for Beginners"* — youtube.com/watch?v=… ( observability talks).

---

**Next:** *Chapter 26 — Appendix: Glossary & Cheat Sheet.* Quick-reference for every term, the dotnet CLI commands, and a curated reading list.
