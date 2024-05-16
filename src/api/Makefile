.PHONY: all

build-docker:
    docker build -t workshop-api:latest .

build:
    dotnet publish /t:PublishContainer

run:
    docker run --rm -it -p 6565\:8080 workshop-api:latest
 # escaped :


# How to have multiple Makefile ?
# .PHONY: all

dev:
	docker compose --file docker-compose.dev.yml up --build

push-kind:
	kind load docker-image workshop-api:latest --name kind