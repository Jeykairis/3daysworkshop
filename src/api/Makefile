.PHONY: all

build-docker:
	docker build -t workshop-api:latest .

build:
	dotnet publish /t:PublishContainer

run:
	docker run --rm -it -p 6565:8080 workshop-api:latest

push:
	kind load docker-image workshop-api:latest --name kind

forward:
	kubectl port-forward svc/api -n workshop-3days 6565:80