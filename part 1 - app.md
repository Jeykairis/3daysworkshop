# Part 1 - Containerization, local DevEx

## 1.1 Creating a new project

Navigate to the root of the project (`git clone https://github.com/tpetrina/3daysworkshop)

```
mkdir src
cd src
dotnet new webapi -o api
dotnet new sln
dotnet add sln api
```

To run it, either:

- navigate into `api` and write `dotnet run`
- use `dotnet run --p api`

## 1.2 Containerization

There are many ways to containerize a service. We will showcase two different
ways.

### 1.2.1 Dockerfile

Create a `Dockerfile` in the `src/api` directory with the following content:

```
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

COPY ./api.csproj .

RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out ./api.csproj

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT [ "dotnet", "api.dll" ]
```

To create a container, run `docker build .` This will build an untagged container.
To tag it, run `docker build -t workshop-api:latest .`.
The usual structure is `registry/service-name:tag`. In production setting using
`latest` is strongly discouraged, but we will keep it here for simplicity.

### 1.2.2 Built-in containerization

.NET SDK comes with its own containerization when simply packing an app i.e. when
you don't need advanced customizations of the Dockerfile.

First, add the following lines to `<PropertyGroup>` in the `api.csproj` file:

```
    <ContainerRepository>workshop-api</ContainerRepository>
    <ContainerImageTags>latest;1.0.0</ContainerImageTags>
```

Run from the terminal:

`dotnet publish /t:PublishContainer`

The result is the service in a container. Let's run it straight from the container:

```
docker run --rm -it -p 6565:8080 workshop-api:latest
```

<details>
  <summary>src/api/Makefile</summary>

To simplify running these operations over and over again, create Makefile with the following content:

```
.PHONY: all

build-docker:
    docker build -t workshop-api:latest .

build:
    dotnet publish /t:PublishContainer

run:
    docker run --rm -it -p 6565:8080 workshop-api:latest
```

</details>

1.3 Local DevEx

Downloading, building and running services will quickly grow in complexity.
Services might bring dependencies, use different SDK versions or even languages.
To simplify running an entire _project_ (a combination of services and dependencies),
we can use Docker Compose.

In the root of our project, create `docker-compose.yml` file with the following content:

```
version: "3"

services:
  api:
    image: workshop-api:latest
    ports:
      - 6565:8080
```

To run it, write `docker compose up`.

This will run the service we built in the previous step. What if we make a change?
Then, we have to stop Docker Compose, build a project and rerun it. This is not
suitable when developing a service, only when consuming it!

1.3.1 Docker Compose for local development

We can use Docker Compose for development by running the code in the container.

First, create a `Dockerfile.dev` file in the `src/api` folder with the following
content:

```
FROM mcr.microsoft.com/dotnet/sdk:8.0

WORKDIR /app
COPY . /app

RUN echo "· Setting certs..."
RUN dotnet dev-certs https --trust
RUN echo "🚀 Good to go"
```

Edit `src/api/Properties/launchSettings.json` and add new section at the end:

```
,
    "watch": {
      "commandName": "Project",
      "executablePath": "dotnet",
      "workingDirectory": "$(ProjectDir)",
      "hotReloadEnabled": true,
      "hotReloadProfile": "aspnetcore",
      "launchBrowser": false,
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "ASPNETCORE_URLS": "http://+:80"
      },
      "useSSL": false
    }
```

In the root of our project, create `Dockerfile.dev` with content:

```
version: "3"

services:
  api:
    build:
      context: ./src/api
      dockerfile: Dockerfile.dev
    ports:
      - 6565:80
    volumes:
      - ./src/api:/app
    working_dir: /app
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - DOTNET_USE_POLLING_FILE_WATCHER=true
    command: ["dotnet", "watch", "run", "--non-interactive", "--launch-profile=watch"]
```

Run it from the terminal with:

```
docker compose --file docker-compose.dev.yml up --build
```

<details>
  <summary>Root Makefile</summary>

To simplify often run commands.

```
.PHONY: all

dev:
	docker compose --file docker-compose.dev.yml up --build

build:
	$(MAKE) -C src/api build

run:
	docker compose up --build
```

</details>

## 1.4 Running in cluster

Let us simulate a "production" environment by running our service in `kind` cluster.

Given a file `infra/kind-config.yaml` with content

```
kind: Cluster
apiVersion: kind.x-k8s.io/v1alpha4
nodes:
- role: control-plane
  kubeadmConfigPatches:
  - |
    kind: InitConfiguration
    nodeRegistration:
      kubeletExtraArgs:
        node-labels: "ingress-ready=true"
  extraPortMappings:
  - containerPort: 80
    hostPort: 80
    protocol: TCP
  - containerPort: 443
    hostPort: 443
    protocol: TCP
```

Run from the root of the project: `kind create cluster --config infra/kind-config.yaml`.
Once it is done, run `kind get kubeconfig` to ensure `kubectl` can reach it.

```sh
# List contexts
kubectl config get-contexts

# Switch to `kind-kind`
kubectl config use-context kind-kind
```

Let's deploy previously containerized app.

```sh
# Push our local image to kind
kind load docker-image workshop-api:latest --name kind

# Check it is there
docker exec -it kind-control-plane crictl images
```

Create `manifests/api.yml` with:

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: workshop-3days
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: api
  namespace: workshop-3days
spec:
  selector:
    matchLabels:
      app: api
  replicas: 1
  template:
    metadata:
      labels:
        app: api
    spec:
      containers:
        - name: api
          image: docker.io/library/workshop-api:latest
          imagePullPolicy: IfNotPresent
          ports:
            - name: http
              containerPort: 8080

---
apiVersion: v1
kind: Service
metadata:
  name: api
  namespace: workshop-3days
spec:
  selector:
    app: api
  ports:
    - name: web
      port: 80
      targetPort: 8080
```

To deploy and run, use the following:

```sh
# Deploy infrastructure
kubectl apply -f manifests/api.yml

# Check if pods are up
kubectl get po -n workshop-3days

# Run it against local port
kubectl port-forward svc/api -n workshop-3days 6565:80
```

To simplify deployment, add the following to `src/api/Makefile`

```Makefile
push-kind:
	kind load docker-image workshop-api:latest --name kind
```

And the following to `Makefile`

```Makefile
deploy-app:
	kubectl apply -f manifests

deploy-e2e:
	$(MAKE) -C src/api build
	$(MAKE) -C src/api push-kind
	$(MAKE) deploy-app
	kubectl rollout restart deploy -n workshop-3days api
```

Now we can deploy new version to the cluster by running `make deploy-e2e`

### 1.4.1 Getting nginx to work in cluster

To avoid having to port forward, let's run nginx in our cluster and use localhost
for accessing services.

First install nginx:

```sh
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/main/deploy/static/provider/kind/deploy.yaml
```

Then wait for it to be initialized

```sh
kubectl wait --namespace ingress-nginx \
  --for=condition=ready pod \
  --selector=app.kubernetes.io/component=controller \
  --timeout=90s
```

If you open `localhost` you will be greeted with `nginx` error message 404.

Append the following kubernetes resource to `manifests/api.yml`

```yaml
---
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: api-ingress
  namespace: workshop-3days
spec:
  ingressClassName: nginx
  rules:
    - http:
        paths:
          - pathType: Prefix
            path: /
            backend:
              service:
                name: api
                port:
                  number: 80
```

And run `kubectl apply -f manifests` (or `make deploy-app`) to configure Ingress.

Open `localhost/weatherforecast` in your browser and you should familiar response.

## 1.5 Serilog

To improve on default logs, we will use Serilog.

In `src/api` run `dotnet add package Serilog.AspNetCore`.

Add to `Program.cs` after the first line:

```csharp
builder.Logging.ClearProviders();
var logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();
// Register Serilog
builder.Logging.AddSerilog(logger);
```

Don't forget to import `Serilog` namespace.

> To fix colors when using `dotnet watch run`, open `launchSettings.json` and
> change `launchBrowser` to `false` in the `http` section.

Cleanup `appsettings.*.json` files since the old configuration is no longer needed.
