docker pull postgres:15.4-alpine
docker pull rabbitmq:3.13.1-management
docker pull mcr.microsoft.com/dotnet/sdk:8.0
docker pull mcr.microsoft.com/dotnet/aspnet:8.0
kind create cluster --name test-temp
kind delete cluster --name test-temp
