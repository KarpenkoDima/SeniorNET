# Kubernetes для Senior .NET разработчика

## Содержание

1. [Что такое Kubernetes и зачем он нужен](#1-что-такое-kubernetes-и-зачем-он-нужен)
2. [Архитектура Kubernetes](#2-архитектура-kubernetes)
3. [Основные объекты Kubernetes](#3-основные-объекты-kubernetes)
4. [Типы Service](#4-типы-service)
5. [Стратегии деплоя](#5-стратегии-деплоя)
6. [Horizontal Pod Autoscaler (HPA)](#6-horizontal-pod-autoscaler-hpa)
7. [Health Checks (Probes)](#7-health-checks-probes)
8. [Volumes и хранение данных](#8-volumes-и-хранение-данных)
9. [Helm Charts](#9-helm-charts)
10. [kubectl: основные команды](#10-kubectl-основные-команды)
11. [Dockerfile для .NET приложения](#11-dockerfile-для-net-приложения)
12. [Kubernetes манифесты для .NET приложения](#12-kubernetes-манифесты-для-net-приложения)
13. [Resource Limits и Requests](#13-resource-limits-и-requests)
14. [RBAC (Role-Based Access Control)](#14-rbac-role-based-access-control)
15. [Service Mesh](#15-service-mesh-istio-linkerd)
16. [Мониторинг: Prometheus + Grafana](#16-мониторинг-prometheus--grafana)
17. [Логирование: ELK Stack, Fluentd](#17-логирование-elk-stack-fluentd)
18. [Вопросы на собеседовании](#18-вопросы-на-собеседовании)

---

## 1. Что такое Kubernetes и зачем он нужен

**Kubernetes (K8s)** — это платформа оркестрации контейнеров с открытым исходным кодом, разработанная Google и переданная в Cloud Native Computing Foundation (CNCF). Kubernetes автоматизирует развёртывание, масштабирование и управление контейнеризированными приложениями.

### Зачем нужен Kubernetes:

- **Оркестрация контейнеров** — управление жизненным циклом сотен и тысяч контейнеров
- **Автоматическое масштабирование** — горизонтальное и вертикальное масштабирование на основе нагрузки
- **Самовосстановление (Self-healing)** — автоматический перезапуск упавших контейнеров
- **Service Discovery и балансировка нагрузки** — автоматическое обнаружение сервисов и распределение трафика
- **Декларативная конфигурация** — описание желаемого состояния системы в YAML-манифестах
- **Rolling Updates и Rollbacks** — обновление без простоев с возможностью отката
- **Управление секретами и конфигурацией** — безопасное хранение паролей, токенов, настроек

### Kubernetes в контексте .NET:

Для Senior .NET разработчика Kubernetes особенно важен, поскольку современные .NET-приложения (ASP.NET Core Web API, gRPC-сервисы, Worker Services) чаще всего разворачиваются в контейнерах и управляются через K8s. Понимание K8s необходимо для проектирования микросервисной архитектуры.

---

## 2. Архитектура Kubernetes

Кластер Kubernetes состоит из **Master Node** (Control Plane) и **Worker Nodes**.

### Master Node (Control Plane)

| Компонент | Описание |
|-----------|----------|
| **API Server (kube-apiserver)** | Центральная точка входа для всех REST-запросов. Все компоненты взаимодействуют через API Server. Обрабатывает запросы от kubectl, UI, SDK. |
| **Scheduler (kube-scheduler)** | Распределяет Pod-ы по Worker Nodes на основе доступных ресурсов, affinity/anti-affinity правил, taints/tolerations. |
| **Controller Manager (kube-controller-manager)** | Запускает контроллеры (ReplicaSet Controller, Deployment Controller, Node Controller и др.), которые следят за текущим состоянием кластера и приводят его к желаемому. |
| **etcd** | Распределённое key-value хранилище, содержащее всё состояние кластера: конфигурации, секреты, состояние объектов. Является единственным stateful-компонентом Control Plane. |

### Worker Node

| Компонент | Описание |
|-----------|----------|
| **kubelet** | Агент, работающий на каждом Worker Node. Получает спецификации Pod-ов от API Server и обеспечивает запуск контейнеров через container runtime. |
| **kube-proxy** | Сетевой прокси на каждом узле. Обеспечивает сетевые правила (iptables/IPVS) для маршрутизации трафика к Pod-ам. Реализует абстракцию Service. |
| **Container Runtime** | Среда выполнения контейнеров: containerd, CRI-O (Docker Engine как runtime устарел начиная с K8s 1.24). |

### Схема взаимодействия

```
                    ┌─────────────────────────────────────────────┐
                    │              Master Node                     │
                    │                                              │
                    │  ┌──────────┐  ┌───────────┐  ┌──────────┐ │
  kubectl ──────────┼─►│API Server│  │ Scheduler │  │Controller│ │
                    │  └────┬─────┘  └─────┬─────┘  │ Manager  │ │
                    │       │              │         └────┬─────┘ │
                    │       │    ┌─────────┘              │       │
                    │       ▼    ▼                        ▼       │
                    │       ┌────────┐                            │
                    │       │  etcd  │                            │
                    │       └────────┘                            │
                    └─────────────────────────────────────────────┘
                               │
                    ┌──────────┼──────────────────────────────────┐
                    │          ▼        Worker Node                │
                    │  ┌───────────┐  ┌──────────┐  ┌──────────┐ │
                    │  │  kubelet  │  │kube-proxy│  │Container │ │
                    │  └─────┬─────┘  └──────────┘  │ Runtime  │ │
                    │        │                       └────┬─────┘ │
                    │        ▼                            ▼       │
                    │  ┌─────────┐   ┌─────────┐   ┌─────────┐  │
                    │  │  Pod A  │   │  Pod B  │   │  Pod C  │  │
                    │  └─────────┘   └─────────┘   └─────────┘  │
                    └─────────────────────────────────────────────┘
```

---

## 3. Основные объекты Kubernetes

### Pod

Минимальная единица развёртывания в Kubernetes. Pod содержит один или несколько контейнеров, которые разделяют сетевое пространство (IP-адрес) и volumes.

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: dotnet-api-pod
  labels:
    app: dotnet-api
spec:
  containers:
    - name: api
      image: myregistry/dotnet-api:1.0
      ports:
        - containerPort: 8080
```

### ReplicaSet

Гарантирует, что указанное количество реплик Pod-а всегда запущено. Обычно не создаётся напрямую — используется через Deployment.

```yaml
apiVersion: apps/v1
kind: ReplicaSet
metadata:
  name: dotnet-api-rs
spec:
  replicas: 3
  selector:
    matchLabels:
      app: dotnet-api
  template:
    metadata:
      labels:
        app: dotnet-api
    spec:
      containers:
        - name: api
          image: myregistry/dotnet-api:1.0
```

### Deployment

Управляет ReplicaSet и обеспечивает декларативное обновление Pod-ов. Поддерживает Rolling Update и Rollback.

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: dotnet-api
spec:
  replicas: 3
  selector:
    matchLabels:
      app: dotnet-api
  template:
    metadata:
      labels:
        app: dotnet-api
    spec:
      containers:
        - name: api
          image: myregistry/dotnet-api:2.0
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0
```

### Service

Абстракция, предоставляющая стабильный сетевой endpoint для набора Pod-ов. Service обеспечивает Service Discovery и балансировку нагрузки.

```yaml
apiVersion: v1
kind: Service
metadata:
  name: dotnet-api-svc
spec:
  selector:
    app: dotnet-api
  ports:
    - protocol: TCP
      port: 80
      targetPort: 8080
  type: ClusterIP
```

### Ingress

Управляет внешним HTTP/HTTPS-доступом к Service-ам в кластере. Обеспечивает маршрутизацию на основе hostname и path, SSL/TLS termination.

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: dotnet-api-ingress
  annotations:
    nginx.ingress.kubernetes.io/rewrite-target: /
spec:
  ingressClassName: nginx
  tls:
    - hosts:
        - api.example.com
      secretName: tls-secret
  rules:
    - host: api.example.com
      http:
        paths:
          - path: /api
            pathType: Prefix
            backend:
              service:
                name: dotnet-api-svc
                port:
                  number: 80
```

### ConfigMap

Хранит неконфиденциальные данные конфигурации в виде пар key-value. Может быть подключён как переменные окружения или как файлы в volume.

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: dotnet-api-config
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  ConnectionStrings__DefaultTimeout: "30"
  appsettings.Production.json: |
    {
      "Logging": {
        "LogLevel": {
          "Default": "Warning"
        }
      },
      "AllowedHosts": "*"
    }
```

### Secret

Хранит конфиденциальные данные (пароли, токены, ключи). Значения хранятся в формате base64 (но это не шифрование!). Для реального шифрования используйте Sealed Secrets, HashiCorp Vault или Azure Key Vault.

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: dotnet-api-secret
type: Opaque
data:
  ConnectionStrings__Default: U2VydmVyPXNxbDtEYXRhYmFzZT1teWRiO1VzZXI9c2E7UGFzc3dvcmQ9UEBzc3cwcmQ=
  JWT__SecretKey: c3VwZXJfc2VjcmV0X2tleV8xMjM0NTY=
```

### Namespace

Логическое разделение ресурсов кластера. Используется для изоляции окружений (dev, staging, production) или команд.

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: production
  labels:
    environment: production
```

---

## 4. Типы Service

### ClusterIP (по умолчанию)

Создаёт внутренний IP-адрес, доступный только внутри кластера. Используется для внутренней коммуникации между сервисами.

```yaml
apiVersion: v1
kind: Service
metadata:
  name: internal-api
spec:
  type: ClusterIP
  selector:
    app: dotnet-api
  ports:
    - port: 80
      targetPort: 8080
```

### NodePort

Открывает статический порт (30000-32767) на каждом узле кластера. Трафик с `<NodeIP>:<NodePort>` перенаправляется в Service.

```yaml
apiVersion: v1
kind: Service
metadata:
  name: nodeport-api
spec:
  type: NodePort
  selector:
    app: dotnet-api
  ports:
    - port: 80
      targetPort: 8080
      nodePort: 30080
```

### LoadBalancer

Создаёт внешний балансировщик нагрузки (в облачных провайдерах: AWS ELB, Azure Load Balancer, GCP LB). Автоматически получает внешний IP.

```yaml
apiVersion: v1
kind: Service
metadata:
  name: external-api
spec:
  type: LoadBalancer
  selector:
    app: dotnet-api
  ports:
    - port: 80
      targetPort: 8080
```

### Сравнение типов Service

| Тип | Доступность | Использование |
|-----|------------|---------------|
| ClusterIP | Только внутри кластера | Внутренняя коммуникация сервисов |
| NodePort | Извне через `NodeIP:Port` | Разработка, тестирование |
| LoadBalancer | Извне через внешний IP | Production в облаке |

---

## 5. Стратегии деплоя

### Rolling Update (по умолчанию)

Постепенно заменяет старые Pod-ы новыми. Гарантирует отсутствие простоя.

```yaml
spec:
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1        # макс. количество Pod-ов сверх replicas при обновлении
      maxUnavailable: 0   # макс. количество недоступных Pod-ов при обновлении
```

- **maxSurge: 1, maxUnavailable: 0** — самая безопасная стратегия: сначала поднимаем новый, потом убираем старый
- Подходит для большинства .NET-приложений

### Recreate

Полностью останавливает все старые Pod-ы, затем создаёт новые. Вызывает кратковременный простой.

```yaml
spec:
  strategy:
    type: Recreate
```

- Используется, когда нельзя запускать две версии одновременно (например, миграции БД)

### Blue/Green

Разворачиваются две идентичные среды (Blue — текущая, Green — новая). После тестирования Green трафик переключается.

Реализация через два Deployment и переключение selector в Service:

```yaml
# Service указывает на blue
apiVersion: v1
kind: Service
metadata:
  name: dotnet-api
spec:
  selector:
    app: dotnet-api
    version: blue    # переключить на green после тестирования
  ports:
    - port: 80
      targetPort: 8080
```

### Canary

Небольшая часть трафика (например, 5-10%) направляется на новую версию. При успешной проверке — постепенный перевод всего трафика.

```yaml
# Canary Deployment: 1 реплика новой версии
apiVersion: apps/v1
kind: Deployment
metadata:
  name: dotnet-api-canary
spec:
  replicas: 1
  selector:
    matchLabels:
      app: dotnet-api
      track: canary
  template:
    metadata:
      labels:
        app: dotnet-api
        track: canary
    spec:
      containers:
        - name: api
          image: myregistry/dotnet-api:2.1-canary
```

Для более гибкого управления Canary-деплоями используют Istio, Flagger или Argo Rollouts.

---

## 6. Horizontal Pod Autoscaler (HPA)

HPA автоматически масштабирует количество реплик Pod-а на основе метрик (CPU, Memory, custom metrics).

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: dotnet-api-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: dotnet-api
  minReplicas: 2
  maxReplicas: 10
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 70
    - type: Resource
      resource:
        name: memory
        target:
          type: Utilization
          averageUtilization: 80
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
        - type: Percent
          value: 10
          periodSeconds: 60
    scaleUp:
      stabilizationWindowSeconds: 0
      policies:
        - type: Percent
          value: 100
          periodSeconds: 15
```

**Требования:**
- На Pod-ах должны быть установлены resource requests
- В кластере должен быть установлен Metrics Server

---

## 7. Health Checks (Probes)

Kubernetes использует три типа проверок для управления жизненным циклом Pod-ов.

### Liveness Probe

Определяет, жив ли контейнер. Если проверка не проходит, kubelet перезапускает контейнер.

```yaml
livenessProbe:
  httpGet:
    path: /healthz
    port: 8080
  initialDelaySeconds: 15
  periodSeconds: 10
  failureThreshold: 3
  timeoutSeconds: 5
```

### Readiness Probe

Определяет, готов ли контейнер принимать трафик. Если не готов — Pod убирается из endpoints Service.

```yaml
readinessProbe:
  httpGet:
    path: /ready
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 5
  failureThreshold: 3
  timeoutSeconds: 3
```

### Startup Probe

Определяет, завершилась ли инициализация контейнера. Пока Startup Probe не пройдёт, Liveness и Readiness Probes не запускаются. Полезно для приложений с долгим стартом.

```yaml
startupProbe:
  httpGet:
    path: /healthz
    port: 8080
  failureThreshold: 30
  periodSeconds: 10
```

### Пример реализации Health Check в ASP.NET Core

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "database")
    .AddRedis(redisConnection, name: "redis")
    .AddCheck("self", () => HealthCheckResult.Healthy());

app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    Predicate = check => check.Name == "self"
});

app.MapHealthChecks("/ready", new HealthCheckOptions
{
    Predicate = _ => true
});
```

### Полная спецификация Pod с Probes

```yaml
spec:
  containers:
    - name: api
      image: myregistry/dotnet-api:1.0
      ports:
        - containerPort: 8080
      startupProbe:
        httpGet:
          path: /healthz
          port: 8080
        failureThreshold: 30
        periodSeconds: 10
      livenessProbe:
        httpGet:
          path: /healthz
          port: 8080
        initialDelaySeconds: 0
        periodSeconds: 10
        failureThreshold: 3
      readinessProbe:
        httpGet:
          path: /ready
          port: 8080
        initialDelaySeconds: 0
        periodSeconds: 5
        failureThreshold: 3
```

---

## 8. Volumes и хранение данных

### PersistentVolume (PV)

Ресурс хранилища на уровне кластера. Создаётся администратором или динамически через StorageClass.

```yaml
apiVersion: v1
kind: PersistentVolume
metadata:
  name: pv-data
spec:
  capacity:
    storage: 10Gi
  accessModes:
    - ReadWriteOnce
  persistentVolumeReclaimPolicy: Retain
  storageClassName: standard
  hostPath:
    path: /mnt/data
```

### PersistentVolumeClaim (PVC)

Запрос на выделение хранилища от пользователя/приложения.

```yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: dotnet-api-pvc
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 5Gi
  storageClassName: standard
```

### StorageClass

Определяет тип хранилища и параметры динамического provisioning.

```yaml
apiVersion: storage.k8s.io/v1
kind: StorageClass
metadata:
  name: fast-ssd
provisioner: kubernetes.io/azure-disk
parameters:
  storageaccounttype: Premium_LRS
  kind: Managed
reclaimPolicy: Delete
allowVolumeExpansion: true
```

### Использование Volume в Pod

```yaml
spec:
  containers:
    - name: api
      image: myregistry/dotnet-api:1.0
      volumeMounts:
        - name: data-volume
          mountPath: /app/data
        - name: config-volume
          mountPath: /app/config
  volumes:
    - name: data-volume
      persistentVolumeClaim:
        claimName: dotnet-api-pvc
    - name: config-volume
      configMap:
        name: dotnet-api-config
```

### Режимы доступа (Access Modes)

| Режим | Описание |
|-------|----------|
| ReadWriteOnce (RWO) | Чтение/запись одним узлом |
| ReadOnlyMany (ROX) | Только чтение несколькими узлами |
| ReadWriteMany (RWX) | Чтение/запись несколькими узлами |

---

## 9. Helm Charts

**Helm** — пакетный менеджер для Kubernetes. Chart — это набор шаблонов Kubernetes-манифестов с параметризацией через `values.yaml`.

### Структура Helm Chart

```
mychart/
  Chart.yaml          # метаданные chart
  values.yaml         # значения по умолчанию
  templates/
    deployment.yaml   # шаблон Deployment
    service.yaml      # шаблон Service
    ingress.yaml      # шаблон Ingress
    _helpers.tpl      # вспомогательные шаблоны
    NOTES.txt         # информация после установки
```

### Пример values.yaml

```yaml
replicaCount: 3

image:
  repository: myregistry/dotnet-api
  tag: "1.0"
  pullPolicy: IfNotPresent

service:
  type: ClusterIP
  port: 80

ingress:
  enabled: true
  host: api.example.com

resources:
  limits:
    cpu: 500m
    memory: 256Mi
  requests:
    cpu: 250m
    memory: 128Mi

autoscaling:
  enabled: true
  minReplicas: 2
  maxReplicas: 10
  targetCPUUtilization: 70
```

### Пример шаблона deployment.yaml

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {{ include "mychart.fullname" . }}
  labels:
    {{- include "mychart.labels" . | nindent 4 }}
spec:
  replicas: {{ .Values.replicaCount }}
  selector:
    matchLabels:
      {{- include "mychart.selectorLabels" . | nindent 6 }}
  template:
    metadata:
      labels:
        {{- include "mychart.selectorLabels" . | nindent 8 }}
    spec:
      containers:
        - name: {{ .Chart.Name }}
          image: "{{ .Values.image.repository }}:{{ .Values.image.tag }}"
          ports:
            - containerPort: 8080
          resources:
            {{- toYaml .Values.resources | nindent 12 }}
```

### Основные команды Helm

```bash
# Установка chart
helm install my-release ./mychart -f values-prod.yaml

# Обновление
helm upgrade my-release ./mychart -f values-prod.yaml

# Откат
helm rollback my-release 1

# Удаление
helm uninstall my-release

# Просмотр установленных релизов
helm list

# Просмотр сгенерированных манифестов (без установки)
helm template my-release ./mychart -f values-prod.yaml
```

---

## 10. kubectl: основные команды

### Информация о кластере и ресурсах

```bash
# Информация о кластере
kubectl cluster-info
kubectl get nodes

# Получение ресурсов
kubectl get pods
kubectl get pods -n production -o wide
kubectl get deployments
kubectl get services
kubectl get ingress
kubectl get all -n production

# Подробная информация об объекте
kubectl describe pod dotnet-api-7d4b8c6f9-x2k4m
kubectl describe node worker-node-1
```

### Управление деплоями

```bash
# Применение манифестов
kubectl apply -f deployment.yaml
kubectl apply -f ./k8s/ -R          # рекурсивно из директории

# Масштабирование
kubectl scale deployment dotnet-api --replicas=5

# Обновление образа
kubectl set image deployment/dotnet-api api=myregistry/dotnet-api:2.0

# История и откат
kubectl rollout history deployment/dotnet-api
kubectl rollout undo deployment/dotnet-api
kubectl rollout undo deployment/dotnet-api --to-revision=2
kubectl rollout status deployment/dotnet-api
```

### Отладка

```bash
# Логи контейнера
kubectl logs dotnet-api-7d4b8c6f9-x2k4m
kubectl logs -f dotnet-api-7d4b8c6f9-x2k4m          # follow
kubectl logs dotnet-api-7d4b8c6f9-x2k4m --previous   # логи предыдущего контейнера

# Выполнение команд внутри контейнера
kubectl exec -it dotnet-api-7d4b8c6f9-x2k4m -- /bin/bash
kubectl exec dotnet-api-7d4b8c6f9-x2k4m -- printenv

# Проброс порта
kubectl port-forward svc/dotnet-api-svc 8080:80
kubectl port-forward pod/dotnet-api-7d4b8c6f9-x2k4m 8080:8080

# Просмотр событий
kubectl get events --sort-by='.metadata.creationTimestamp'

# Удаление ресурсов
kubectl delete -f deployment.yaml
kubectl delete pod dotnet-api-7d4b8c6f9-x2k4m
```

### Работа с контекстами

```bash
# Просмотр текущего контекста
kubectl config current-context

# Список контекстов
kubectl config get-contexts

# Переключение контекста
kubectl config use-context production-cluster

# Установка namespace по умолчанию
kubectl config set-context --current --namespace=production
```

---

## 11. Dockerfile для .NET приложения

### Multi-stage Build

```dockerfile
# ============================================
# Stage 1: Build
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем файлы проектов для кэширования restore
COPY ["src/MyApi/MyApi.csproj", "src/MyApi/"]
COPY ["src/MyApi.Domain/MyApi.Domain.csproj", "src/MyApi.Domain/"]
COPY ["src/MyApi.Infrastructure/MyApi.Infrastructure.csproj", "src/MyApi.Infrastructure/"]
RUN dotnet restore "src/MyApi/MyApi.csproj"

# Копируем весь исходный код
COPY . .

# Публикация
WORKDIR "/src/src/MyApi"
RUN dotnet publish "MyApi.csproj" -c Release -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ============================================
# Stage 2: Runtime
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Создаём непривилегированного пользователя
RUN adduser --disabled-password --gecos "" appuser

# Копируем опубликованное приложение
COPY --from=build /app/publish .

# Настройка переменных окружения
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0

# Открываем порт
EXPOSE 8080

# Переключаемся на непривилегированного пользователя
USER appuser

# Точка входа
ENTRYPOINT ["dotnet", "MyApi.dll"]
```

### Рекомендации по Dockerfile для .NET

1. **Multi-stage build** — разделяйте сборку и runtime для минимизации размера образа
2. **Кэширование слоёв** — сначала копируйте `.csproj` и делайте `restore`, потом копируйте код
3. **Непривилегированный пользователь** — не запускайте приложение от root
4. **Минимальный базовый образ** — используйте `aspnet` вместо `sdk` для runtime
5. **`.dockerignore`** — исключайте `bin/`, `obj/`, `.git/`, `*.user`

### Пример .dockerignore

```
**/.git
**/.vs
**/bin
**/obj
**/node_modules
**/.env
**/Dockerfile*
**/.dockerignore
```

---

## 12. Kubernetes манифесты для .NET приложения

### Полный набор манифестов

#### Namespace

```yaml
# namespace.yaml
apiVersion: v1
kind: Namespace
metadata:
  name: dotnet-app
  labels:
    app: dotnet-api
```

#### ConfigMap

```yaml
# configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: dotnet-api-config
  namespace: dotnet-app
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  Logging__LogLevel__Default: "Warning"
  Logging__LogLevel__Microsoft.AspNetCore: "Warning"
```

#### Secret

```yaml
# secret.yaml
apiVersion: v1
kind: Secret
metadata:
  name: dotnet-api-secret
  namespace: dotnet-app
type: Opaque
stringData:
  ConnectionStrings__Default: "Server=sql-server;Database=mydb;User=sa;Password=P@ssw0rd;TrustServerCertificate=True"
  JWT__SecretKey: "super_secret_key_123456_that_is_long_enough"
```

#### Deployment

```yaml
# deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: dotnet-api
  namespace: dotnet-app
  labels:
    app: dotnet-api
spec:
  replicas: 3
  selector:
    matchLabels:
      app: dotnet-api
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0
  template:
    metadata:
      labels:
        app: dotnet-api
        version: "2.0"
    spec:
      serviceAccountName: dotnet-api-sa
      containers:
        - name: api
          image: myregistry.azurecr.io/dotnet-api:2.0
          ports:
            - containerPort: 8080
              protocol: TCP
          envFrom:
            - configMapRef:
                name: dotnet-api-config
            - secretRef:
                name: dotnet-api-secret
          resources:
            requests:
              cpu: 250m
              memory: 128Mi
            limits:
              cpu: 500m
              memory: 256Mi
          startupProbe:
            httpGet:
              path: /healthz
              port: 8080
            failureThreshold: 30
            periodSeconds: 10
          livenessProbe:
            httpGet:
              path: /healthz
              port: 8080
            periodSeconds: 10
            failureThreshold: 3
          readinessProbe:
            httpGet:
              path: /ready
              port: 8080
            periodSeconds: 5
            failureThreshold: 3
      restartPolicy: Always
```

#### Service

```yaml
# service.yaml
apiVersion: v1
kind: Service
metadata:
  name: dotnet-api-svc
  namespace: dotnet-app
spec:
  type: ClusterIP
  selector:
    app: dotnet-api
  ports:
    - name: http
      protocol: TCP
      port: 80
      targetPort: 8080
```

#### Ingress

```yaml
# ingress.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: dotnet-api-ingress
  namespace: dotnet-app
  annotations:
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
    nginx.ingress.kubernetes.io/rate-limit: "100"
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
spec:
  ingressClassName: nginx
  tls:
    - hosts:
        - api.example.com
      secretName: api-tls-secret
  rules:
    - host: api.example.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: dotnet-api-svc
                port:
                  number: 80
```

### Применение манифестов

```bash
kubectl apply -f namespace.yaml
kubectl apply -f configmap.yaml
kubectl apply -f secret.yaml
kubectl apply -f deployment.yaml
kubectl apply -f service.yaml
kubectl apply -f ingress.yaml

# или все сразу из директории
kubectl apply -f ./k8s/ -R
```

---

## 13. Resource Limits и Requests

### Описание

- **Requests** — минимальное количество ресурсов, гарантированное Pod-у. Scheduler использует requests для принятия решений о размещении Pod-ов.
- **Limits** — максимально допустимое количество ресурсов. При превышении CPU — throttling, при превышении Memory — OOMKill.

```yaml
resources:
  requests:
    cpu: 250m       # 0.25 ядра CPU
    memory: 128Mi   # 128 мебибайт RAM
  limits:
    cpu: 500m       # 0.5 ядра CPU
    memory: 256Mi   # 256 мебибайт RAM
```

### Единицы измерения

| Ресурс | Единица | Примеры |
|--------|---------|---------|
| CPU | millicores (m) | 100m = 0.1 ядра, 1000m = 1 ядро |
| Memory | байты (Mi, Gi) | 128Mi = 128 MiB, 1Gi = 1 GiB |

### Quality of Service (QoS) классы

| QoS класс | Условие | Приоритет при OOM |
|-----------|---------|-------------------|
| **Guaranteed** | requests == limits для CPU и Memory | Наивысший (последний убивается) |
| **Burstable** | requests < limits | Средний |
| **BestEffort** | Нет requests и limits | Низший (первый убивается) |

### LimitRange для Namespace

```yaml
apiVersion: v1
kind: LimitRange
metadata:
  name: default-limits
  namespace: dotnet-app
spec:
  limits:
    - type: Container
      default:
        cpu: 500m
        memory: 256Mi
      defaultRequest:
        cpu: 250m
        memory: 128Mi
      max:
        cpu: "2"
        memory: 1Gi
      min:
        cpu: 50m
        memory: 32Mi
```

### ResourceQuota для Namespace

```yaml
apiVersion: v1
kind: ResourceQuota
metadata:
  name: namespace-quota
  namespace: dotnet-app
spec:
  hard:
    requests.cpu: "10"
    requests.memory: 20Gi
    limits.cpu: "20"
    limits.memory: 40Gi
    pods: "50"
```

---

## 14. RBAC (Role-Based Access Control)

RBAC управляет доступом пользователей и сервисных аккаунтов к ресурсам Kubernetes.

### Основные объекты RBAC

| Объект | Область | Описание |
|--------|---------|----------|
| **Role** | Namespace | Набор разрешений в рамках одного namespace |
| **ClusterRole** | Кластер | Набор разрешений на уровне кластера |
| **RoleBinding** | Namespace | Привязка Role к субъекту (пользователь, группа, ServiceAccount) |
| **ClusterRoleBinding** | Кластер | Привязка ClusterRole к субъекту |

### ServiceAccount

```yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: dotnet-api-sa
  namespace: dotnet-app
```

### Role и RoleBinding

```yaml
# Role: разрешает чтение ConfigMap и Secret в namespace
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: config-reader
  namespace: dotnet-app
rules:
  - apiGroups: [""]
    resources: ["configmaps", "secrets"]
    verbs: ["get", "list", "watch"]
  - apiGroups: [""]
    resources: ["pods"]
    verbs: ["get", "list"]
---
# RoleBinding: привязка Role к ServiceAccount
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: config-reader-binding
  namespace: dotnet-app
subjects:
  - kind: ServiceAccount
    name: dotnet-api-sa
    namespace: dotnet-app
roleRef:
  kind: Role
  name: config-reader
  apiGroup: rbac.authorization.k8s.io
```

### ClusterRole и ClusterRoleBinding

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: monitoring-reader
rules:
  - apiGroups: [""]
    resources: ["nodes", "pods", "services"]
    verbs: ["get", "list", "watch"]
  - apiGroups: ["metrics.k8s.io"]
    resources: ["pods", "nodes"]
    verbs: ["get", "list"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: monitoring-reader-binding
subjects:
  - kind: ServiceAccount
    name: prometheus-sa
    namespace: monitoring
roleRef:
  kind: ClusterRole
  name: monitoring-reader
  apiGroup: rbac.authorization.k8s.io
```

### Принцип наименьших привилегий

Всегда предоставляйте минимально необходимые разрешения. Используйте Role вместо ClusterRole, если доступ нужен только в одном namespace.

---

## 15. Service Mesh (Istio, Linkerd)

Service Mesh — это инфраструктурный слой, добавляющий возможности наблюдаемости, безопасности и управления трафиком в микросервисной архитектуре без изменения кода приложения.

### Как работает Service Mesh

В каждый Pod внедряется **sidecar-прокси** (например, Envoy), который перехватывает весь входящий и исходящий трафик контейнера.

### Istio

Наиболее функциональный и популярный Service Mesh.

**Возможности:**
- Управление трафиком (traffic routing, canary, fault injection)
- Mutual TLS (mTLS) между сервисами
- Observability (метрики, трассировка, логи)
- Rate limiting, circuit breaking, retry policies

```yaml
# Пример VirtualService для Canary
apiVersion: networking.istio.io/v1beta1
kind: VirtualService
metadata:
  name: dotnet-api-vs
spec:
  hosts:
    - dotnet-api
  http:
    - route:
        - destination:
            host: dotnet-api
            subset: stable
          weight: 90
        - destination:
            host: dotnet-api
            subset: canary
          weight: 10
```

### Linkerd

Более легковесная альтернатива Istio. Меньше потребляет ресурсов, проще в установке.

**Возможности:**
- mTLS из коробки
- Observability (golden metrics: latency, traffic, errors, saturation)
- Retry, timeout policies
- Traffic split

### Сравнение

| Критерий | Istio | Linkerd |
|----------|-------|---------|
| Сложность | Высокая | Низкая |
| Потребление ресурсов | Высокое | Низкое |
| Функциональность | Очень богатая | Достаточная |
| mTLS | Да | Да (по умолчанию) |
| Sidecar-прокси | Envoy | linkerd2-proxy (Rust) |

---

## 16. Мониторинг: Prometheus + Grafana

### Prometheus

Система мониторинга и алертинга с pull-based моделью сбора метрик.

**Ключевые концепции:**
- Собирает метрики по HTTP (endpoint `/metrics`)
- Хранит метрики как time-series данные
- Язык запросов PromQL
- Alertmanager для уведомлений

### Интеграция с .NET

```csharp
// Установка пакета: prometheus-net.AspNetCore
builder.Services.AddHealthChecks();

var app = builder.Build();

// Middleware для экспорта метрик
app.UseHttpMetrics();              // HTTP метрики
app.MapMetrics("/metrics");        // Endpoint для Prometheus

// Пользовательские метрики
var requestCounter = Metrics.CreateCounter(
    "dotnet_api_requests_total",
    "Total number of API requests",
    new CounterConfiguration
    {
        LabelNames = new[] { "method", "endpoint", "status" }
    });
```

### ServiceMonitor для Prometheus Operator

```yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: dotnet-api-monitor
  namespace: monitoring
spec:
  selector:
    matchLabels:
      app: dotnet-api
  endpoints:
    - port: http
      path: /metrics
      interval: 15s
  namespaceSelector:
    matchNames:
      - dotnet-app
```

### Grafana

Платформа визуализации метрик. Подключается к Prometheus как источник данных.

**Типичные дашборды для .NET:**
- HTTP Request Rate / Latency / Errors
- CPU / Memory Usage per Pod
- GC Collections, Heap Size
- Active connections, Thread pool usage

### Пример PromQL-запросов

```promql
# Средний RPS за 5 минут
rate(http_requests_received_total[5m])

# 99-й перцентиль latency
histogram_quantile(0.99, rate(http_request_duration_seconds_bucket[5m]))

# Процент ошибок
sum(rate(http_requests_received_total{code=~"5.."}[5m]))
/
sum(rate(http_requests_received_total[5m])) * 100
```

---

## 17. Логирование: ELK Stack, Fluentd

### ELK Stack

- **Elasticsearch** — хранение и индексация логов
- **Logstash** — обработка и трансформация логов
- **Kibana** — визуализация и поиск по логам

### EFK Stack (альтернатива)

- **Elasticsearch** — хранение
- **Fluentd / Fluent Bit** — сбор и отправка логов
- **Kibana** — визуализация

### Fluentd

DaemonSet, который запускается на каждом узле и собирает логи из stdout/stderr контейнеров.

```yaml
apiVersion: apps/v1
kind: DaemonSet
metadata:
  name: fluentd
  namespace: logging
spec:
  selector:
    matchLabels:
      app: fluentd
  template:
    metadata:
      labels:
        app: fluentd
    spec:
      containers:
        - name: fluentd
          image: fluent/fluentd-kubernetes-daemonset:v1-debian-elasticsearch
          env:
            - name: FLUENT_ELASTICSEARCH_HOST
              value: "elasticsearch.logging.svc.cluster.local"
            - name: FLUENT_ELASTICSEARCH_PORT
              value: "9200"
          volumeMounts:
            - name: varlog
              mountPath: /var/log
            - name: containers
              mountPath: /var/lib/docker/containers
              readOnly: true
      volumes:
        - name: varlog
          hostPath:
            path: /var/log
        - name: containers
          hostPath:
            path: /var/lib/docker/containers
```

### Structured Logging в .NET

```csharp
// Serilog с JSON-форматированием для K8s
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .WriteTo.Console(new JsonFormatter());
});
```

### Лучшие практики логирования в Kubernetes

1. **Пишите логи в stdout/stderr** — Kubernetes собирает их автоматически
2. **Используйте структурированное логирование** (JSON) для удобного парсинга
3. **Добавляйте correlation ID** для трассировки запросов через сервисы
4. **Настройте уровни логирования** через ConfigMap (не требует пересборки образа)
5. **Не храните логи локально в контейнере** — контейнеры эфемерны

---

## 18. Вопросы на собеседовании

### Вопрос 1: Чем отличается Pod от контейнера?

**Ответ:** Pod — это минимальная единица развёртывания в Kubernetes, которая может содержать один или несколько контейнеров. Контейнеры внутри одного Pod разделяют сетевое пространство (IP-адрес, порты), хранилище (volumes) и имеют общее пространство IPC. Pod — это абстракция Kubernetes, а контейнер — это абстракция container runtime (containerd, CRI-O). В большинстве случаев Pod содержит один контейнер, но pattern-ы sidecar, ambassador и adapter используют несколько контейнеров в одном Pod.

### Вопрос 2: Как Kubernetes обеспечивает Self-healing?

**Ответ:** Kubernetes обеспечивает самовосстановление через несколько механизмов:
- **Liveness Probe** — если контейнер не отвечает, kubelet перезапускает его
- **ReplicaSet Controller** — если количество Pod-ов меньше заданного replicas, контроллер создаёт недостающие Pod-ы
- **Node Controller** — если узел становится недоступным, Pod-ы переносятся на другие узлы
- **restartPolicy** — определяет поведение при падении контейнера (Always, OnFailure, Never)

### Вопрос 3: В чём разница между ConfigMap и Secret?

**Ответ:** ConfigMap хранит неконфиденциальные конфигурационные данные в открытом виде. Secret хранит конфиденциальные данные (пароли, токены, сертификаты) в формате base64. Однако base64 — это не шифрование, а кодирование. Для реальной защиты секретов нужно использовать Encryption at Rest в etcd, RBAC для ограничения доступа, или внешние хранилища секретов (HashiCorp Vault, Azure Key Vault, AWS Secrets Manager). Оба могут быть подключены как переменные окружения или как файлы через volume mount.

### Вопрос 4: Объясните разницу между Deployment, StatefulSet и DaemonSet.

**Ответ:**
- **Deployment** — управляет stateless-приложениями. Pod-ы взаимозаменяемы, имеют случайные имена. Поддерживает Rolling Update и Rollback.
- **StatefulSet** — управляет stateful-приложениями. Pod-ы имеют стабильные сетевые имена (pod-0, pod-1), упорядоченное создание/удаление и привязку к PersistentVolume. Используется для БД, Kafka, Elasticsearch.
- **DaemonSet** — гарантирует запуск одного Pod-а на каждом узле (или подмножестве узлов). Используется для мониторинга, сбора логов, сетевых агентов.

### Вопрос 5: Как работает Ingress и зачем он нужен?

**Ответ:** Ingress — это API-объект, определяющий правила маршрутизации внешнего HTTP/HTTPS-трафика к Service-ам внутри кластера. Ingress сам по себе не работает — нужен Ingress Controller (например, NGINX Ingress Controller, Traefik, HAProxy). Ingress позволяет: маршрутизировать по hostname и path, терминировать TLS, объединять несколько сервисов под одним IP. Без Ingress каждому сервису потребовался бы отдельный LoadBalancer, что дорого и неэффективно.

### Вопрос 6: Что такое Horizontal Pod Autoscaler и как он работает?

**Ответ:** HPA автоматически изменяет количество реплик Pod-а на основе наблюдаемых метрик. По умолчанию использует CPU/Memory метрики от Metrics Server, но может работать с custom и external metrics через Prometheus Adapter. HPA проверяет метрики с заданным интервалом (по умолчанию 15 сек), вычисляет желаемое количество реплик по формуле `desiredReplicas = ceil[currentReplicas * (currentMetric / desiredMetric)]` и масштабирует Deployment. Для корректной работы на Pod-ах обязательно должны быть указаны resource requests.

### Вопрос 7: Как обеспечить Zero-Downtime Deployment для .NET приложения в Kubernetes?

**Ответ:** Для Zero-Downtime Deployment необходимо:
1. Использовать стратегию **RollingUpdate** с `maxSurge: 1` и `maxUnavailable: 0`
2. Настроить **Readiness Probe** — новый Pod попадает в Service только после прохождения проверки
3. Настроить **Startup Probe** — для .NET приложений с долгим стартом (warmup, загрузка кэша)
4. Реализовать **Graceful Shutdown** — обработку SIGTERM в приложении (`IHostApplicationLifetime`)
5. Настроить **preStop hook** с небольшой задержкой для корректного удаления из endpoints
6. Обеспечить **обратную совместимость** API между версиями

```yaml
lifecycle:
  preStop:
    exec:
      command: ["sh", "-c", "sleep 5"]
terminationGracePeriodSeconds: 30
```

### Вопрос 8: Что такое Resource Requests и Limits? Что произойдёт при превышении лимитов?

**Ответ:** Requests — минимальное гарантированное количество ресурсов для Pod-а. Scheduler использует requests для размещения Pod-ов на узлах. Limits — максимальное количество ресурсов. При превышении CPU limit контейнер подвергается throttling (замедление). При превышении Memory limit контейнер завершается с OOMKill (Out of Memory Kill). Kubernetes назначает QoS классы: Guaranteed (requests == limits), Burstable (requests < limits), BestEffort (нет requests/limits). При нехватке ресурсов на узле первыми убиваются BestEffort Pod-ы.

### Вопрос 9: Как организовать логирование и мониторинг .NET-приложения в Kubernetes?

**Ответ:** **Логирование:** Приложение должно писать структурированные логи (JSON) в stdout через Serilog или Microsoft.Extensions.Logging. Fluentd/Fluent Bit (DaemonSet) собирает логи со всех узлов и отправляет в Elasticsearch. Kibana используется для визуализации и поиска. **Мониторинг:** Prometheus собирает метрики по HTTP endpoint `/metrics` (библиотека prometheus-net). Grafana визуализирует метрики. Для трассировки используется OpenTelemetry с экспортом в Jaeger или Zipkin. Alertmanager отправляет уведомления при срабатывании правил.

### Вопрос 10: Как работает RBAC в Kubernetes и зачем он нужен?

**Ответ:** RBAC (Role-Based Access Control) — механизм управления доступом к ресурсам Kubernetes. Состоит из четырёх объектов: Role (разрешения в namespace), ClusterRole (разрешения на уровне кластера), RoleBinding и ClusterRoleBinding (привязка ролей к субъектам). Субъекты: User, Group, ServiceAccount. Принцип наименьших привилегий: каждому сервису создаётся свой ServiceAccount с минимально необходимыми разрешениями. RBAC нужен для безопасности: ограничение доступа разработчиков к production namespace, ограничение прав приложений внутри кластера.

### Вопрос 11: Чем отличается Kubernetes Service от Ingress?

**Ответ:** Service работает на L4 (TCP/UDP) и обеспечивает балансировку нагрузки между Pod-ами. Ingress работает на L7 (HTTP/HTTPS) и обеспечивает маршрутизацию по hostname и path, TLS-termination, rate limiting. Service типа LoadBalancer создаёт отдельный внешний IP для каждого сервиса. Ingress позволяет использовать один внешний IP для множества сервисов с маршрутизацией по правилам. В production обычно комбинируют: Ingress для внешнего трафика, ClusterIP Services для внутренней коммуникации.

### Вопрос 12: Что такое Service Mesh и когда его стоит использовать?

**Ответ:** Service Mesh — инфраструктурный слой для управления коммуникацией между сервисами. Реализуется через sidecar-прокси (Envoy в Istio, linkerd2-proxy в Linkerd), внедряемые в каждый Pod. Возможности: mTLS (шифрование между сервисами), observability (метрики, трассировка), traffic management (canary, fault injection, circuit breaking), retries, timeouts. Стоит использовать при большом количестве микросервисов (10+), когда нужна наблюдаемость без изменения кода приложения, когда требуется mTLS. Не стоит использовать для монолитов или 2-3 сервисов — overhead не оправдан.

### Вопрос 13: Как реализовать Canary Deployment в Kubernetes?

**Ответ:** Существует несколько подходов:
1. **Нативный (простой):** Два Deployment с одинаковыми labels, но разным количеством реплик. Service балансирует пропорционально количеству Pod-ов. Недостаток: грубый контроль (1 из 10 = 10%).
2. **Istio VirtualService:** Точное управление весами трафика (weight: 90/10), маршрутизация на основе headers (например, тестовые пользователи).
3. **Argo Rollouts / Flagger:** Автоматизированный canary с анализом метрик и автоматическим rollback при повышении error rate.
Для .NET-приложений рекомендуется подход с Istio или Argo Rollouts для production.

### Вопрос 14: Как масштабировать stateful-приложение (например, SQL Server) в Kubernetes?

**Ответ:** Для stateful-приложений используется StatefulSet вместо Deployment. Ключевые особенности: стабильные сетевые идентификаторы (pod-0, pod-1), упорядоченное создание и удаление, привязка к PersistentVolumeClaim. Для SQL Server: используйте StatefulSet с volumeClaimTemplates, headless Service для DNS-имён каждого Pod-а, Always On Availability Groups для репликации. Важно: горизонтальное масштабирование баз данных сложнее, чем stateless-сервисов. Часто используют managed решения (Azure SQL, Amazon RDS) вместо запуска БД в K8s.

---

## Заключение

Kubernetes — необходимый навык для Senior .NET разработчика в эпоху микросервисов и облачных технологий. Ключевые области знаний:

1. **Архитектура кластера** — понимание Control Plane и Worker Node компонентов
2. **Основные объекты** — Pod, Deployment, Service, Ingress, ConfigMap, Secret
3. **Деплой** — стратегии обновления, Helm Charts, CI/CD интеграция
4. **Надёжность** — Health Checks, Resource Management, HPA
5. **Безопасность** — RBAC, Network Policies, Secret Management
6. **Наблюдаемость** — мониторинг (Prometheus/Grafana), логирование (ELK/EFK), трассировка (OpenTelemetry)
7. **Практические навыки** — Dockerfile для .NET, написание манифестов, работа с kubectl
