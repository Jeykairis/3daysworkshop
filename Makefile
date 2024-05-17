.PHONY: all

dev:
	docker compose --file docker-compose.dev.yml up --build

build:
	$(MAKE) -C src/api build

run:
	docker compose up --build

kind-create:
	kind create cluster --config infra/kind-config.yaml

infra-db:
	kubectl apply -f infra/postgresql-pvc.yaml
	kubectl create namespace db || true
	helm upgrade db bitnami/postgresql \
		--install \
		--namespace db \
		--values infra/postgresql-values.yaml

infra-rabbitmq:
	helm upgrade --install rabbitmq bitnami/rabbitmq \
		--set auth.password=RabbitMqPassword \
		--set auth.erlangCookie=M8pbajEbtndUplgF

infra-prom:
	helm repo add prometheus https://prometheus-community.github.io/helm-charts
	kubectl create namespace prometheus || true
	helm upgrade --install prometheus prometheus/kube-prometheus-stack \
		--namespace prometheus \
		--values infra/prometheus.yaml

deploy-app:
	kubectl apply -f manifests

deploy-e2e:
	$(MAKE) -C src/api build
	$(MAKE) -C src/api push
	$(MAKE) deploy-app
	kubectl rollout restart deploy -n workshop-3days api

forward-rabbitmq:
	kubectl port-forward svc/rabbitmq -n default 15673:15672

forward-prometheus:
	kubectl port-forward -n prometheus svc/prometheus-kube-prometheus-prometheus 9090:9090