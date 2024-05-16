# 3 Days of DevOps in 1 Day: From Local to Production

Requirements:

- Docker
- kubectl
  - Optionally: kubectx+kubens
- Kubernetes cluster
  - kind
  - Docker desktop
  - Minikube
  - k3s
  - Managed kubernetes
- k9s - https://k9scli.io/topics/install/

## Windows

Installs kind

```
choco install -y kind kubernetes-helm k9s
```

## macOS

```
brew install kind helm kubectx derailed/k9s/k9s
```

## How to use this repository?

The course is divided in 6 parts. Each part has a pdf presentation followed by exercises written in a markdown file.

Additionally, `infra` and `manifests` folders contain some manifests for the infrastructure part that can speed up following the course and can be used as a starting point for following exercises.
