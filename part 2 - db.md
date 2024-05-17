# Part 2 - Adding Database

We will use PostgreSQL and Entity Framework Core in this section.

# 2.1 Run PostgreSQL

We will use Docker Compose to run our dependencies. Change `docker-compose.yml`
to:

```yaml
version: "3"

services:
  # We will run app with dotnet watch run
  # api:
  #   image: workshop-api:latest
  #   ports:
  #     - 6565:8080

  pg:
    image: postgres:15.4-alpine
    restart: always
    environment:
      - POSTGRES_USER=postgres
      - POSTGRES_PASSWORD=postgres
    ports:
      - "5432:5432"
    volumes:
      - db:/var/lib/postgresql/data

volumes:
  db:
    driver: local
```

Run it in background with `docker compose up -d`

# 2.2 Using Entity Framework Core

Run the following in `src/api`

```sh
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

Add the following configuration to `appsettings.Development.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost; Port=5432; Database=postgres; Username=postgres; Password=postgres"
  }
}
```

We will use code first which means we need a model and a context. Create a file
`WeatherForecastContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace api;

class WeatherForecastEntity
{
    public int Id { get; set; }
    public string? Location { get; set; }
    public DateTime Date { get; set; }
    public int TemperatureC { get; set; }
    public string? Summary { get; set; }
}

class WeatherForecastContext : DbContext
{
    public DbSet<WeatherForecastEntity> Forecasts { get; set; }

    public WeatherForecastContext(DbContextOptions<WeatherForecastContext> options)
        : base(options)
    {
    }
}
```

Add to `Program.cs`

```csharp
builder.Services.AddDbContext<WeatherForecastContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    logger.Information($"Adding DbContext: {connectionString}");
    options.UseNpgsql(connectionString);
});
```

Finally, let's use EF to extract data from the database:

```csharp
app.MapGet("/forecasts", (WeatherForecastContext context) =>
{
    return context.Forecasts.ToList();
});
```

Run the application and open `localhost:PORT/forecasts` and...

...get an error message `PostgresException: 42P01: relation "Forecasts" does not exist`.

## 2.3 Migrations

Ensure you have [`dotnet-ef`](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) installed:

```shell
dotnet tool install --global dotnet-ef
```

Stop the application and write

```shell
dotnet ef migrations add AddForecastModel
dotnet ef database update
```

This will create and run migrations against our database.
Open `http://localhost:5052/forecasts` and notice it is empty.

Add a new endpoint:

```csharp
app.MapPost("/forecast", (WeatherForecastEntity forecast, WeatherForecastContext context) =>
{
    context.Forecasts.Add(forecast);
    context.SaveChanges();
    return forecast;
});
```

And use Swagger to add an entity to the database.

# 2.3 Deploy to kind

We will use `helm` for deployment of the database to kubernetes as it is a bit
more complicated system. Helm deploys charts which are located in repositories.
Our chart is `bitnami/postgresql` - ensure it is available by running:

```sh
helm repo add bitnami https://charts.bitnami.com/bitnami
helm repo update
```

Creating infra is slightly more involved. You need `infra/postgresql-pvc.yaml`:

```yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: postgres-pvc
  namespace: db
  labels:
    app: postgres-pvc
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 2Gi
```

Then you need `postgresql-values.yaml`:

```yaml
# Base values: https://github.com/bitnami/charts/blob/main/bitnami/postgresql/values.yaml

# define default database user, name, and password for PostgreSQL deployment
auth:
  enablePostgresUser: true
  postgresPassword: "StrongPassword"
  username: "app1"
  password: "AppPassword"
  database: "app_db"

# The postgres helm chart deployment will be using PVC postgresql-data-claim
primary:
  persistence:
    enabled: true
    existingClaim: postgres-pvc

volumePermissions:
  enabled: true
```

Now we can install our chart with the following command:

```sh
kubectl apply -f infra/postgresql-pvc.yaml
kubectl create namespace db || true
helm upgrade db bitnami/postgresql \
  --install \
  --namespace db \
  --values infra/postgresql-values.yaml
```

# 2.4 App healthchecks

Before we deploy our app, let's add healthchecks.

Add a package for EF Core healthchecks in `src/api`:

```sh
dotnet add package Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore
```

Register services:

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<WeatherForecastContext>();
```

And map endpoints:

```csharp
app.MapHealthChecks("/health");
```

Open `localhost:PORT/health`.

Finally, update our `manifests/api.yml` and add the following section:

```yaml
containers:
  - name: api
    image: docker.io/library/workshop-api:latest
    imagePullPolicy: IfNotPresent
    # NEW -------
    livenessProbe:
      httpGet:
        path: /health
        port: 8080
      initialDelaySeconds: 3
      periodSeconds: 3
    # END NEW ---
    ports:
      - name: http
        containerPort: 8080
```

Deploy the app and notice it will soon break down. The problem is that we don't
have the database connection string!

Let's add it after `ports:` section:

````yaml
        env:
          - name: CUSTOMCONNSTR_DefaultConnection
            value: "Host=db-postgresql.db.svc.cluster.local; Port=5432; Database=app_db; Username=app1; Password=AppPassword"
            ```
````

Now the app is healthy, but navigating to `localhost/forecasts` reveals another issue...

# 2.5 Migrations in production

The database in production is "lagging" behind. Unlike before, we cannot update
database from terminal (if you can, you shouldn't).

We need to run migrations somehow on demand. There are many ways to do this and
it is well documented in the documentation [Applying Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying?tabs=dotnet-core-cli).

Let's simplify this by replacing `app.Run();` with the following code:

```csharp
if (args.Length > 0)
{
    switch (args[0])
    {
        case "--migrate":
            {
                app.Logger.LogInformation("Migrating database");
                using var scope = app.Services.CreateScope();
                using var context = scope.ServiceProvider.GetService<WeatherForecastContext>();
                context?.Database.Migrate();
                return;
            }
    }
}
else
{
    app.Run();
}
```

To run this locally, write `dotnet run -- --migrate`. It is the equivalent of
`dotnet ef database update`.

Now, add a new `initContainer` to `manifests/api.yml`:

```yaml
spec:
  template:
    spec:
      # NEW
      initContainers:
        - name: migrate
          image: docker.io/library/workshop-api:latest
          imagePullPolicy: IfNotPresent
          command:
            - dotnet
            - api.dll
            - "--migrate"
          env:
            - name: CUSTOMCONNSTR_DefaultConnection
              value: "Host=db-postgresql.db.svc.cluster.local; Port=5432; Database=app_db; Username=app1; Password=AppPassword"
      # END NEW
      containers:
```

Finally, `localhost/forecasts` works without errors.

# 2.5 Secrets (bonus)

```sh
kubectl create secret -n workshop-3days generic db \
  --from-literal='CUSTOMCONNSTR_DefaultConnection'='Host=db-postgresql.db.svc.cluster.local; Port=5432; Database=app_db; Username=app1; Password=AppPassword'
```

And replace `env:` section in `manifests/api.yml` to:

```yaml
envFrom:
  - secretRef:
      name: db
```
