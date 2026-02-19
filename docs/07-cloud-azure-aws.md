# 07. Cloud: Azure и AWS для Senior .NET-разработчика

## Содержание

1. [Модели облачных сервисов: IaaS, PaaS, SaaS, FaaS](#модели-облачных-сервисов)
2. [Azure (основной фокус)](#azure)
   - [Azure App Service](#azure-app-service)
   - [Azure Functions (Serverless)](#azure-functions)
   - [Службы обмена сообщениями: Service Bus, Event Hub, Event Grid](#службы-обмена-сообщениями)
   - [Azure Storage: Blob, Queue, Table](#azure-storage)
   - [Azure SQL vs Cosmos DB](#azure-sql-vs-cosmos-db)
   - [Azure Key Vault](#azure-key-vault)
   - [Azure Container Apps и AKS](#azure-container-apps-и-aks)
   - [Azure DevOps Pipelines](#azure-devops-pipelines)
   - [Azure Active Directory (Entra ID) и Managed Identity](#azure-ad-и-managed-identity)
   - [Azure Cache for Redis](#azure-cache-for-redis)
   - [Application Insights](#application-insights)
3. [AWS (обзор для сравнения)](#aws)
   - [EC2, Lambda, ECS/EKS](#aws-compute)
   - [SQS vs SNS vs EventBridge](#aws-messaging)
   - [S3, DynamoDB, RDS](#aws-storage)
   - [API Gateway](#aws-api-gateway)
   - [CloudWatch](#aws-cloudwatch)
4. [Cloud Design Patterns](#cloud-design-patterns)
5. [12-Factor App](#12-factor-app)
6. [Оптимизация затрат (Cost Optimization)](#оптимизация-затрат)
7. [Сравнительная таблица Azure vs AWS](#сравнительная-таблица)
8. [Вопросы и ответы для собеседования](#вопросы-для-собеседования)

---

## Модели облачных сервисов

| Модель | Описание | Примеры Azure | Примеры AWS |
|--------|----------|---------------|-------------|
| **IaaS** | Инфраструктура как сервис. Вы управляете ОС, рантаймом, приложением | Azure VMs | EC2 |
| **PaaS** | Платформа как сервис. Провайдер управляет ОС и рантаймом | Azure App Service | Elastic Beanstalk |
| **SaaS** | Программное обеспечение как сервис. Готовое приложение | Microsoft 365, Dynamics | Amazon WorkMail |
| **FaaS** | Функции как сервис (Serverless). Оплата за вызов | Azure Functions | AWS Lambda |

**Ключевой принцип**: чем выше уровень абстракции, тем меньше ответственность за инфраструктуру, но меньше контроля.

### Shared Responsibility Model

```
IaaS:  Провайдер → физика, сеть, виртуализация
       Вы       → ОС, рантайм, данные, приложение

PaaS:  Провайдер → физика, сеть, виртуализация, ОС, рантайм
       Вы       → данные, приложение

SaaS:  Провайдер → всё
       Вы       → данные (частично), конфигурация

FaaS:  Провайдер → физика, сеть, виртуализация, ОС, рантайм, масштабирование
       Вы       → код функции, данные
```

---

## Azure

### Azure App Service

Azure App Service — это PaaS-платформа для хостинга веб-приложений, REST API и мобильных бэкендов.

**Ключевые возможности:**
- Автоматическое масштабирование (scale-up и scale-out)
- Deployment slots (слоты развёртывания) для blue-green деплоя
- Встроенная аутентификация (Easy Auth)
- Поддержка custom domains и SSL
- Интеграция с CI/CD

**Тарифные планы (App Service Plans):**
- **Free/Shared** — для разработки и тестирования
- **Basic** — без автомасштабирования
- **Standard** — автомасштабирование, слоты развёртывания
- **Premium** — повышенная производительность, VNet Integration
- **Isolated** — полная изоляция сети (ASE)

**Пример конфигурации в C# (Program.cs):**

```csharp
var builder = WebApplication.CreateBuilder(args);

// Конфигурация для Azure App Service
builder.Configuration
    .AddAzureKeyVault(
        new Uri(builder.Configuration["KeyVault:Url"]!),
        new DefaultAzureCredential());

// Health checks для Azure Load Balancer
builder.Services.AddHealthChecks()
    .AddAzureBlobStorage(builder.Configuration.GetConnectionString("BlobStorage")!)
    .AddSqlServer(builder.Configuration.GetConnectionString("SqlDatabase")!);

var app = builder.Build();

app.MapHealthChecks("/health");
app.Run();
```

**Deployment Slots — пример использования:**

```csharp
// Слоты позволяют деплоить в staging и переключать трафик без простоя
// В appsettings.json можно использовать slot-specific настройки:
// Настройка помечается как "Slot Setting" в Azure Portal,
// и она НЕ переносится при swap.

// Пример: проверка текущего слота в коде
public class SlotInfo
{
    public static string GetCurrentSlot()
    {
        // Azure устанавливает эту переменную окружения
        return Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME") ?? "production";
    }
}
```

---

### Azure Functions

Azure Functions — serverless-платформа (FaaS) для выполнения кода по событиям.

**Триггеры и привязки (Triggers & Bindings):**
- HTTP Trigger
- Timer Trigger (CRON)
- Queue Trigger (Storage Queue, Service Bus)
- Blob Trigger
- Event Hub Trigger
- Cosmos DB Trigger (Change Feed)

**Модели хостинга:**
- **Consumption Plan** — оплата за выполнение, холодный старт
- **Premium Plan** — предварительно прогретые экземпляры, VNet
- **Dedicated (App Service Plan)** — фиксированные ресурсы
- **Container Apps** — контейнерный хостинг функций

**Пример Azure Function на C# (.NET 8 Isolated Worker):**

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

public class OrderFunctions
{
    private readonly ILogger<OrderFunctions> _logger;
    private readonly IOrderService _orderService;

    public OrderFunctions(ILogger<OrderFunctions> logger, IOrderService orderService)
    {
        _logger = logger;
        _orderService = orderService;
    }

    // HTTP-триггер: создание заказа
    [Function("CreateOrder")]
    public async Task<HttpResponseData> CreateOrder(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders")]
        HttpRequestData req)
    {
        var order = await req.ReadFromJsonAsync<CreateOrderRequest>();
        var result = await _orderService.CreateAsync(order!);

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(result);
        return response;
    }

    // Queue-триггер: обработка заказа из очереди
    [Function("ProcessOrder")]
    public async Task ProcessOrder(
        [ServiceBusTrigger("orders-queue", Connection = "ServiceBusConnection")]
        OrderMessage message)
    {
        _logger.LogInformation("Обработка заказа {OrderId}", message.OrderId);
        await _orderService.ProcessAsync(message.OrderId);
    }

    // Timer-триггер: ежедневная очистка (каждый день в 02:00 UTC)
    [Function("DailyCleanup")]
    public async Task DailyCleanup(
        [TimerTrigger("0 0 2 * * *")] TimerInfo timerInfo)
    {
        _logger.LogInformation("Запуск ежедневной очистки в {Time}", DateTime.UtcNow);
        await _orderService.CleanupExpiredOrdersAsync();
    }

    // Cosmos DB Change Feed-триггер
    [Function("OnOrderChanged")]
    public void OnOrderChanged(
        [CosmosDBTrigger(
            databaseName: "shop-db",
            containerName: "orders",
            Connection = "CosmosDBConnection",
            LeaseContainerName = "leases",
            CreateLeaseContainerIfNotExists = true)]
        IReadOnlyList<OrderDocument> changes)
    {
        foreach (var doc in changes)
        {
            _logger.LogInformation("Заказ изменён: {OrderId}, статус: {Status}",
                doc.Id, doc.Status);
        }
    }
}
```

**Регистрация DI в Azure Functions Isolated Worker:**

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddScoped<IOrderService, OrderService>();
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(context.Configuration.GetConnectionString("SqlDb")));
        services.AddApplicationInsightsTelemetryWorkerService();
    })
    .Build();

host.Run();
```

---

### Службы обмена сообщениями

#### Azure Service Bus vs Event Hub vs Event Grid

| Характеристика | Service Bus | Event Hub | Event Grid |
|----------------|-------------|-----------|------------|
| **Тип** | Message Broker | Event Streaming | Event Routing |
| **Паттерн** | Queue / Pub-Sub | Streaming (append-only log) | Reactive (push) |
| **Доставка** | At-least-once, at-most-once | At-least-once | At-least-once |
| **Порядок** | FIFO (Sessions) | В рамках partition | Не гарантирован |
| **Размер сообщения** | До 256 KB (Standard), 100 MB (Premium) | До 1 MB | До 1 MB |
| **Хранение** | До 14 дней | До 90 дней (retention) | 24 часа (retry) |
| **Пропускная способность** | Умеренная | Очень высокая (миллионы/сек) | Высокая |
| **Сценарии** | Надёжная бизнес-логика, транзакции, заказы | Телеметрия, IoT, логирование | Реактивные события (blob создан, ресурс изменён) |

**Azure Service Bus — пример отправки и получения:**

```csharp
// Отправка сообщения в очередь
public class OrderPublisher
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;

    public OrderPublisher(ServiceBusClient client)
    {
        _client = client;
        _sender = _client.CreateSender("orders-queue");
    }

    public async Task PublishOrderAsync(Order order)
    {
        var message = new ServiceBusMessage(JsonSerializer.SerializeToUtf8Bytes(order))
        {
            ContentType = "application/json",
            Subject = "OrderCreated",
            MessageId = order.Id.ToString(),
            // Отложенная доставка
            ScheduledEnqueueTime = DateTimeOffset.UtcNow.AddMinutes(5),
            // Time-to-live
            TimeToLive = TimeSpan.FromHours(24),
            // Свойства для фильтрации в подписках (Topics)
            ApplicationProperties =
            {
                ["OrderType"] = order.Type,
                ["Region"] = order.Region
            }
        };

        await _sender.SendMessageAsync(message);
    }

    // Пакетная отправка
    public async Task PublishBatchAsync(IEnumerable<Order> orders)
    {
        using ServiceBusMessageBatch batch = await _sender.CreateMessageBatchAsync();

        foreach (var order in orders)
        {
            var message = new ServiceBusMessage(
                JsonSerializer.SerializeToUtf8Bytes(order));

            if (!batch.TryAddMessage(message))
            {
                // Пакет полон — отправляем и создаём новый
                await _sender.SendMessagesAsync(batch);
                batch.Dispose();
                // Повторно добавляем текущее сообщение в новый пакет
            }
        }

        if (batch.Count > 0)
            await _sender.SendMessagesAsync(batch);
    }
}

// Обработка сообщений
public class OrderProcessor : BackgroundService
{
    private readonly ServiceBusProcessor _processor;
    private readonly IServiceProvider _serviceProvider;

    public OrderProcessor(ServiceBusClient client, IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _processor = client.CreateProcessor("orders-queue", new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 10,
            AutoCompleteMessages = false,
            PrefetchCount = 20
        });

        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _processor.StartProcessingAsync(stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IOrderHandler>();

        var order = args.Message.Body.ToObjectFromJson<Order>();
        await handler.HandleAsync(order);

        // Явное подтверждение обработки
        await args.CompleteMessageAsync(args.Message);
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        // Логирование ошибок. После максимума попыток → Dead Letter Queue
        return Task.CompletedTask;
    }
}
```

**Service Bus Topics (Pub/Sub) с фильтрацией:**

```csharp
// Создание подписки с SQL-фильтром
// (обычно делается через IaC, но можно и программно)
var adminClient = new ServiceBusAdministrationClient(connectionString);

await adminClient.CreateSubscriptionAsync(
    new CreateSubscriptionOptions("orders-topic", "high-priority-orders")
    {
        MaxDeliveryCount = 5,
        LockDuration = TimeSpan.FromMinutes(2),
        DefaultMessageTimeToLive = TimeSpan.FromDays(1)
    });

await adminClient.CreateRuleAsync("orders-topic", "high-priority-orders",
    new CreateRuleOptions("HighPriorityFilter",
        new SqlRuleFilter("OrderType = 'Premium' AND Region = 'EU'")));
```

---

### Azure Storage

#### Blob Storage

```csharp
public class BlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;

    public BlobStorageService(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

    // Загрузка файла с метаданными
    public async Task<string> UploadFileAsync(
        string containerName, Stream content, string fileName, string contentType)
    {
        var container = _blobServiceClient.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.None);

        var blobClient = container.GetBlobClient(fileName);

        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            Metadata = new Dictionary<string, string>
            {
                ["uploadedAt"] = DateTime.UtcNow.ToString("O"),
                ["originalName"] = fileName
            },
            // Уровень доступа
            AccessTier = AccessTier.Hot
        };

        await blobClient.UploadAsync(content, options);
        return blobClient.Uri.ToString();
    }

    // Генерация SAS-токена для временного доступа
    public Uri GenerateSasUri(string containerName, string blobName, TimeSpan expiry)
    {
        var blobClient = _blobServiceClient
            .GetBlobContainerClient(containerName)
            .GetBlobClient(blobName);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = blobName,
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiry),
            Protocol = SasProtocol.Https
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return blobClient.GenerateSasUri(sasBuilder);
    }
}
```

#### Queue Storage vs Service Bus Queue

| Характеристика | Storage Queue | Service Bus Queue |
|----------------|---------------|-------------------|
| Размер очереди | До 500 TB | До 80 GB |
| Размер сообщения | До 64 KB | До 256 KB (Standard) |
| Гарантия доставки | At-least-once | At-least-once / At-most-once |
| FIFO | Не гарантирован | Гарантирован (Sessions) |
| Dead Letter Queue | Нет | Да |
| Транзакции | Нет | Да |
| Стоимость | Ниже | Выше |

#### Table Storage

```csharp
// Azure Table Storage — простое NoSQL-хранилище (key-value)
public class AuditLogEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!; // UserId
    public string RowKey { get; set; } = default!;       // Timestamp (ticks)
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Action { get; set; } = default!;
    public string Details { get; set; } = default!;
}

public class AuditLogRepository
{
    private readonly TableClient _tableClient;

    public AuditLogRepository(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient("AuditLogs");
        _tableClient.CreateIfNotExists();
    }

    public async Task LogAsync(string userId, string action, string details)
    {
        var entity = new AuditLogEntity
        {
            PartitionKey = userId,
            RowKey = DateTime.UtcNow.Ticks.ToString("D19"),
            Action = action,
            Details = details
        };

        await _tableClient.AddEntityAsync(entity);
    }

    // Запрос по PartitionKey (эффективный) + фильтр
    public async IAsyncEnumerable<AuditLogEntity> GetUserLogsAsync(
        string userId, DateTime since)
    {
        var sinceRowKey = since.Ticks.ToString("D19");

        var query = _tableClient.QueryAsync<AuditLogEntity>(
            filter: $"PartitionKey eq '{userId}' and RowKey ge '{sinceRowKey}'",
            maxPerPage: 100);

        await foreach (var entity in query)
        {
            yield return entity;
        }
    }
}
```

---

### Azure SQL vs Cosmos DB

| Характеристика | Azure SQL | Cosmos DB |
|----------------|-----------|-----------|
| **Тип** | Реляционная СУБД | Мульти-модельная NoSQL |
| **Модель данных** | Таблицы, строки | Документы (JSON), графы, колонки |
| **Запросы** | T-SQL | SQL-подобный, MongoDB API, Gremlin |
| **Масштабирование** | Вертикальное (+ read replicas) | Горизонтальное (partitioning) |
| **Консистентность** | Strong (ACID) | 5 уровней (от Strong до Eventual) |
| **Latency** | Миллисекунды | < 10 мс (SLA) |
| **Стоимость** | DTU или vCore | RU/s (Request Units) |
| **Когда использовать** | Сложные JOIN, транзакции, отчёты | Высокая нагрузка, глобальное распределение, гибкая схема |

**Cosmos DB — пример работы:**

```csharp
public class ProductRepository
{
    private readonly Container _container;

    public ProductRepository(CosmosClient cosmosClient)
    {
        _container = cosmosClient.GetContainer("shop-db", "products");
    }

    public async Task<Product> GetByIdAsync(string id, string categoryId)
    {
        // categoryId — partition key для эффективного поиска
        var response = await _container.ReadItemAsync<Product>(
            id, new PartitionKey(categoryId));

        // Логирование потреблённых RU
        Console.WriteLine($"Request Charge: {response.RequestCharge} RU");

        return response.Resource;
    }

    public async Task<IEnumerable<Product>> SearchAsync(string category, decimal maxPrice)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.categoryId = @category AND c.price <= @maxPrice")
            .WithParameter("@category", category)
            .WithParameter("@maxPrice", maxPrice);

        var iterator = _container.GetItemQueryIterator<Product>(query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(category),
                MaxItemCount = 50
            });

        var results = new List<Product>();
        double totalRu = 0;

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            totalRu += response.RequestCharge;
            results.AddRange(response);
        }

        Console.WriteLine($"Общее потребление RU: {totalRu}");
        return results;
    }

    // Оптимистичная конкуренция через ETag
    public async Task UpdateAsync(Product product)
    {
        await _container.ReplaceItemAsync(product, product.Id,
            new PartitionKey(product.CategoryId),
            new ItemRequestOptions { IfMatchEtag = product.ETag });
    }

    // Transactional Batch (в рамках одного partition key)
    public async Task CreateOrderWithItemsAsync(Order order, List<OrderItem> items)
    {
        var batch = _container.CreateTransactionalBatch(
            new PartitionKey(order.CustomerId));

        batch.CreateItem(order);
        foreach (var item in items)
            batch.CreateItem(item);

        var response = await batch.ExecuteAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Транзакция не удалась: {response.StatusCode}");
    }
}
```

**Уровни консистентности Cosmos DB:**
1. **Strong** — линеаризуемость, как в реляционных БД
2. **Bounded Staleness** — гарантированное отставание не более N операций или T секунд
3. **Session** — консистентность в рамках сессии клиента (по умолчанию)
4. **Consistent Prefix** — порядок записей сохраняется, но возможно отставание
5. **Eventual** — максимальная производительность, минимальная гарантия

---

### Azure Key Vault

Azure Key Vault — централизованное хранилище секретов, ключей шифрования и сертификатов.

```csharp
// Интеграция Key Vault с конфигурацией .NET
var builder = WebApplication.CreateBuilder(args);

// Использование Managed Identity для доступа к Key Vault (без секретов в коде!)
builder.Configuration.AddAzureKeyVault(
    new Uri("https://myapp-keyvault.vault.azure.net/"),
    new DefaultAzureCredential());

// Секрет "Database--ConnectionString" в Key Vault
// автоматически маппится в Configuration["Database:ConnectionString"]

// Прямой доступ к Key Vault через SDK
public class SecretService
{
    private readonly SecretClient _secretClient;

    public SecretService(SecretClient secretClient)
    {
        _secretClient = secretClient;
    }

    public async Task<string> GetSecretAsync(string secretName)
    {
        KeyVaultSecret secret = await _secretClient.GetSecretAsync(secretName);
        return secret.Value;
    }

    // Ротация секретов
    public async Task RotateSecretAsync(string secretName, string newValue)
    {
        await _secretClient.SetSecretAsync(secretName, newValue);
    }
}

// Регистрация в DI
builder.Services.AddSingleton(new SecretClient(
    new Uri("https://myapp-keyvault.vault.azure.net/"),
    new DefaultAzureCredential()));
```

---

### Azure Container Apps и AKS

#### Azure Container Apps

Serverless-платформа для контейнеров. Построена поверх Kubernetes (KEDA, Dapr, Envoy).

**Когда использовать:**
- Микросервисы без необходимости управлять Kubernetes
- HTTP API, фоновые задачи, event-driven приложения
- Автоматическое масштабирование от 0 до N

#### AKS (Azure Kubernetes Service)

Полноценный управляемый Kubernetes.

**Когда использовать:**
- Полный контроль над кластером
- Сложная оркестрация, custom operators
- Существующий опыт с Kubernetes

```yaml
# Пример Kubernetes Deployment для .NET-приложения в AKS
apiVersion: apps/v1
kind: Deployment
metadata:
  name: order-api
spec:
  replicas: 3
  selector:
    matchLabels:
      app: order-api
  template:
    metadata:
      labels:
        app: order-api
    spec:
      containers:
      - name: order-api
        image: myregistry.azurecr.io/order-api:latest
        ports:
        - containerPort: 8080
        resources:
          requests:
            cpu: "250m"
            memory: "256Mi"
          limits:
            cpu: "500m"
            memory: "512Mi"
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        # Секреты из Azure Key Vault через CSI Driver
        volumeMounts:
        - name: secrets-store
          mountPath: "/mnt/secrets"
          readOnly: true
        livenessProbe:
          httpGet:
            path: /health/live
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 15
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 10
      volumes:
      - name: secrets-store
        csi:
          driver: secrets-store.csi.k8s.io
          readOnly: true
          volumeAttributes:
            secretProviderClass: "azure-keyvault"
```

---

### Azure DevOps Pipelines

```yaml
# azure-pipelines.yml — пример CI/CD для .NET-приложения
trigger:
  branches:
    include:
      - main
      - release/*

pool:
  vmImage: 'ubuntu-latest'

variables:
  buildConfiguration: 'Release'
  dotnetVersion: '8.0.x'

stages:
- stage: Build
  jobs:
  - job: BuildAndTest
    steps:
    - task: UseDotNet@2
      inputs:
        packageType: 'sdk'
        version: $(dotnetVersion)

    - script: dotnet restore
      displayName: 'Восстановление зависимостей'

    - script: dotnet build --configuration $(buildConfiguration) --no-restore
      displayName: 'Сборка проекта'

    - script: dotnet test --configuration $(buildConfiguration) --no-build --collect:"XPlat Code Coverage" --results-directory $(Agent.TempDirectory)
      displayName: 'Запуск тестов'

    - task: PublishCodeCoverageResults@2
      inputs:
        summaryFileLocation: '$(Agent.TempDirectory)/**/coverage.cobertura.xml'

    - script: dotnet publish -c $(buildConfiguration) -o $(Build.ArtifactStagingDirectory)
      displayName: 'Публикация артефактов'

    - task: PublishBuildArtifacts@1
      inputs:
        pathToPublish: $(Build.ArtifactStagingDirectory)
        artifactName: 'drop'

- stage: DeployStaging
  dependsOn: Build
  condition: succeeded()
  jobs:
  - deployment: DeployToStaging
    environment: 'staging'
    strategy:
      runOnce:
        deploy:
          steps:
          - task: AzureWebApp@1
            inputs:
              azureSubscription: 'MyAzureConnection'
              appType: 'webApp'
              appName: 'myapp-staging'
              package: '$(Pipeline.Workspace)/drop/**/*.zip'
              slotName: 'staging'

- stage: DeployProduction
  dependsOn: DeployStaging
  condition: succeeded()
  jobs:
  - deployment: DeployToProd
    environment: 'production'
    strategy:
      runOnce:
        deploy:
          steps:
          # Swap staging → production (zero-downtime)
          - task: AzureAppServiceManage@0
            inputs:
              azureSubscription: 'MyAzureConnection'
              action: 'Swap Slots'
              webAppName: 'myapp-staging'
              sourceSlot: 'staging'
              targetSlot: 'production'
```

---

### Azure AD и Managed Identity

**Azure Active Directory (Entra ID)** — сервис идентификации и управления доступом.

**Managed Identity** — позволяет приложениям аутентифицироваться в Azure-сервисах без хранения секретов.

```csharp
// DefaultAzureCredential автоматически выбирает способ аутентификации:
// 1. Environment Variables
// 2. Managed Identity (в Azure)
// 3. Visual Studio / Azure CLI / Azure PowerShell (локально)
var credential = new DefaultAzureCredential();

// Использование Managed Identity для доступа к Azure SQL
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connection = new SqlConnection(
        builder.Configuration.GetConnectionString("SqlDb"));

    // Получаем токен через Managed Identity
    var tokenCredential = new DefaultAzureCredential();
    var token = tokenCredential.GetToken(
        new TokenRequestContext(["https://database.windows.net/.default"]));

    connection.AccessToken = token.Token;
    options.UseSqlServer(connection);
});

// Защита API через Azure AD (JWT Bearer)
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

// appsettings.json
// {
//   "AzureAd": {
//     "Instance": "https://login.microsoftonline.com/",
//     "TenantId": "your-tenant-id",
//     "ClientId": "your-client-id",
//     "Audience": "api://your-api-id"
//   }
// }

// Авторизация по ролям из Azure AD
[Authorize(Roles = "Admin,Manager")]
[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    [HttpGet("users")]
    [RequiredScope("Users.Read")]
    public async Task<IActionResult> GetUsers()
    {
        // Доступ к claims пользователя
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value);
        // ...
        return Ok();
    }
}
```

---

### Azure Cache for Redis

```csharp
// Регистрация Redis в DI
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "myapp:";
});

// Distributed Cache — простой интерфейс
public class CachedProductService : IProductService
{
    private readonly IDistributedCache _cache;
    private readonly IProductRepository _repository;

    public CachedProductService(IDistributedCache cache, IProductRepository repository)
    {
        _cache = cache;
        _repository = repository;
    }

    public async Task<Product?> GetByIdAsync(string id)
    {
        var cacheKey = $"product:{id}";

        // Попытка получить из кеша
        var cached = await _cache.GetStringAsync(cacheKey);
        if (cached is not null)
            return JsonSerializer.Deserialize<Product>(cached);

        // Загрузка из БД
        var product = await _repository.GetByIdAsync(id);
        if (product is null) return null;

        // Сохранение в кеш
        await _cache.SetStringAsync(cacheKey,
            JsonSerializer.Serialize(product),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                SlidingExpiration = TimeSpan.FromMinutes(10)
            });

        return product;
    }

    // Инвалидация кеша при обновлении
    public async Task UpdateAsync(Product product)
    {
        await _repository.UpdateAsync(product);
        await _cache.RemoveAsync($"product:{product.Id}");
    }
}

// Прямой доступ через ConnectionMultiplexer (для продвинутых сценариев)
public class RedisLockService
{
    private readonly IConnectionMultiplexer _redis;

    public RedisLockService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    // Распределённая блокировка
    public async Task<bool> AcquireLockAsync(string resource, TimeSpan expiry)
    {
        var db = _redis.GetDatabase();
        var lockKey = $"lock:{resource}";
        var lockValue = Guid.NewGuid().ToString();

        return await db.StringSetAsync(lockKey, lockValue, expiry, When.NotExists);
    }
}
```

---

### Application Insights

```csharp
// Подключение Application Insights
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    options.EnableAdaptiveSampling = true;
});

// Кастомная телеметрия
public class OrderService
{
    private readonly TelemetryClient _telemetry;

    public OrderService(TelemetryClient telemetry)
    {
        _telemetry = telemetry;
    }

    public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
    {
        using var operation = _telemetry.StartOperation<RequestTelemetry>("CreateOrder");

        try
        {
            // Кастомные метрики
            _telemetry.GetMetric("OrdersCreated").TrackValue(1);

            // Кастомные события
            _telemetry.TrackEvent("OrderCreated", new Dictionary<string, string>
            {
                ["OrderType"] = request.Type,
                ["ItemCount"] = request.Items.Count.ToString(),
                ["Region"] = request.Region
            });

            var order = await ProcessOrderInternalAsync(request);

            operation.Telemetry.Success = true;
            return order;
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            _telemetry.TrackException(ex, new Dictionary<string, string>
            {
                ["OrderType"] = request.Type
            });
            throw;
        }
    }
}

// KQL-запрос для Application Insights (Kusto Query Language)
// requests
// | where timestamp > ago(1h)
// | where success == false
// | summarize failedCount = count() by operation_Name
// | order by failedCount desc
// | take 10
```

---

## AWS

### AWS Compute

| Сервис | Аналог Azure | Описание |
|--------|-------------|----------|
| **EC2** | Azure VMs | Виртуальные машины (IaaS) |
| **Lambda** | Azure Functions | Serverless-функции (FaaS) |
| **ECS** | Azure Container Instances | Контейнерная оркестрация (проприетарная) |
| **EKS** | AKS | Управляемый Kubernetes |
| **Fargate** | Azure Container Apps | Serverless-контейнеры |
| **Elastic Beanstalk** | Azure App Service | PaaS для веб-приложений |

**AWS Lambda — пример на C#:**

```csharp
// AWS Lambda с .NET
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

public class OrderFunction
{
    private readonly IOrderService _orderService;

    public OrderFunction()
    {
        // DI через конструктор (или через Amazon.Lambda.Annotations)
        var services = new ServiceCollection();
        services.AddScoped<IOrderService, OrderService>();
        var provider = services.BuildServiceProvider();
        _orderService = provider.GetRequiredService<IOrderService>();
    }

    public async Task<APIGatewayProxyResponse> CreateOrder(
        APIGatewayProxyRequest request, ILambdaContext context)
    {
        context.Logger.LogInformation($"Request: {request.Body}");

        var order = JsonSerializer.Deserialize<CreateOrderRequest>(request.Body);
        var result = await _orderService.CreateAsync(order!);

        return new APIGatewayProxyResponse
        {
            StatusCode = 201,
            Body = JsonSerializer.Serialize(result),
            Headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json"
            }
        };
    }
}
```

---

### AWS Messaging

| Сервис | Аналог Azure | Описание |
|--------|-------------|----------|
| **SQS** | Storage Queue / Service Bus Queue | Очередь сообщений |
| **SNS** | Event Grid / Service Bus Topics | Pub/Sub-уведомления |
| **EventBridge** | Event Grid | Event Bus для событийной архитектуры |
| **Kinesis** | Event Hub | Потоковая обработка данных |

**SQS + SNS — пример на C#:**

```csharp
// Отправка сообщения в SQS
using Amazon.SQS;
using Amazon.SQS.Model;

public class SqsPublisher
{
    private readonly IAmazonSQS _sqs;
    private readonly string _queueUrl;

    public SqsPublisher(IAmazonSQS sqs, IConfiguration config)
    {
        _sqs = sqs;
        _queueUrl = config["AWS:SQS:OrdersQueueUrl"]!;
    }

    public async Task SendAsync(Order order)
    {
        await _sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = _queueUrl,
            MessageBody = JsonSerializer.Serialize(order),
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["OrderType"] = new()
                {
                    DataType = "String",
                    StringValue = order.Type
                }
            },
            // Задержка доставки (до 15 минут)
            DelaySeconds = 0
        });
    }
}

// Публикация в SNS Topic
using Amazon.SimpleNotificationService;

public class SnsPublisher
{
    private readonly IAmazonSimpleNotificationService _sns;

    public async Task PublishAsync(string topicArn, OrderEvent orderEvent)
    {
        await _sns.PublishAsync(new PublishRequest
        {
            TopicArn = topicArn,
            Message = JsonSerializer.Serialize(orderEvent),
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["EventType"] = new()
                {
                    DataType = "String",
                    StringValue = orderEvent.Type
                }
            }
        });
    }
}
```

---

### AWS Storage

| Сервис | Аналог Azure | Описание |
|--------|-------------|----------|
| **S3** | Azure Blob Storage | Объектное хранилище |
| **DynamoDB** | Cosmos DB / Table Storage | NoSQL (key-value + document) |
| **RDS** | Azure SQL | Управляемые реляционные БД |
| **ElastiCache** | Azure Cache for Redis | Кеширование (Redis/Memcached) |

```csharp
// AWS S3 — пример работы
using Amazon.S3;
using Amazon.S3.Model;

public class S3StorageService
{
    private readonly IAmazonS3 _s3;

    public async Task<string> UploadAsync(string bucketName, string key, Stream content)
    {
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = content,
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
        });

        return $"https://{bucketName}.s3.amazonaws.com/{key}";
    }

    // Pre-signed URL (аналог SAS-токена Azure)
    public string GeneratePresignedUrl(string bucket, string key, TimeSpan expiry)
    {
        return _s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = key,
            Expires = DateTime.UtcNow.Add(expiry),
            Verb = HttpVerb.GET
        });
    }
}
```

---

### AWS API Gateway

API Gateway — управляемый сервис для создания, публикации и защиты REST/WebSocket API.

| Тип | Описание |
|-----|----------|
| **REST API** | Полнофункциональный, кеширование, авторизация, throttling |
| **HTTP API** | Облегчённый, дешевле, быстрее, для Lambda-прокси |
| **WebSocket API** | Двусторонняя связь в реальном времени |

Аналог в Azure: **Azure API Management (APIM)**.

---

### AWS CloudWatch

Аналог **Azure Application Insights + Azure Monitor**.

| Возможность | CloudWatch | Azure |
|-------------|-----------|-------|
| Метрики | CloudWatch Metrics | Azure Monitor Metrics |
| Логи | CloudWatch Logs | Log Analytics |
| Трейсинг | AWS X-Ray | Application Insights |
| Алерты | CloudWatch Alarms | Azure Monitor Alerts |
| Дашборды | CloudWatch Dashboards | Azure Dashboards |

---

## Cloud Design Patterns

### 1. Retry Pattern (Повтор при сбое)

```csharp
// Использование Microsoft.Extensions.Http.Resilience (рекомендуется для .NET 8+)
builder.Services.AddHttpClient("PaymentApi", client =>
{
    client.BaseAddress = new Uri("https://payment-api.example.com");
})
.AddStandardResilienceHandler();

// Или через Polly напрямую для тонкой настройки
builder.Services.AddHttpClient("ExternalApi")
.AddResilienceHandler("retry-pipeline", builder =>
{
    builder.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromMilliseconds(500),
        BackoffType = DelayBackoffType.Exponential, // 500ms, 1s, 2s
        UseJitter = true,                           // Рандомизация для избежания thundering herd
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests
                            || r.StatusCode >= HttpStatusCode.InternalServerError)
    });
});
```

### 2. Circuit Breaker Pattern (Предохранитель)

```csharp
// Circuit Breaker — предотвращение каскадных сбоев
builder.Services.AddHttpClient("CatalogApi")
.AddResilienceHandler("circuit-breaker", builder =>
{
    builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
    {
        // Сколько ошибок для открытия circuit breaker
        FailureRatio = 0.5,                // 50% ошибок в окне выборки
        SamplingDuration = TimeSpan.FromSeconds(30),
        MinimumThroughput = 10,            // Минимум 10 запросов для оценки
        BreakDuration = TimeSpan.FromSeconds(30),  // Время "отдыха" в открытом состоянии
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .HandleResult(r => (int)r.StatusCode >= 500)
    });
});

// Состояния Circuit Breaker:
// Closed  → нормальная работа, запросы проходят
// Open    → все запросы немедленно отклоняются (fail-fast)
// HalfOpen → пропускается один пробный запрос для проверки восстановления
```

### 3. Queue-Based Load Leveling (Выравнивание нагрузки через очередь)

```
Проблема: пиковая нагрузка перегружает сервис

[Клиенты] → [API] → [Очередь] → [Worker (масштабируется)]
                                        ↓
                                   [База данных]

Вместо прямого вызова "тяжёлого" сервиса, мы кладём задачу в очередь.
Worker обрабатывает задачи с контролируемой скоростью.
```

```csharp
// API кладёт задачу в очередь
[HttpPost("reports")]
public async Task<IActionResult> GenerateReport([FromBody] ReportRequest request)
{
    var message = new ServiceBusMessage(JsonSerializer.SerializeToUtf8Bytes(request))
    {
        MessageId = Guid.NewGuid().ToString(),
        Subject = "GenerateReport"
    };

    await _sender.SendMessageAsync(message);

    // Возвращаем 202 Accepted — отчёт будет создан асинхронно
    return Accepted(new { TrackingId = message.MessageId });
}
```

### 4. Competing Consumers (Конкурирующие потребители)

```
Несколько экземпляров Worker обрабатывают одну очередь параллельно.
Каждое сообщение доставляется только одному потребителю.

[Очередь] → [Worker 1]
          → [Worker 2]
          → [Worker 3]

Масштабирование: увеличиваем количество Worker-ов.
В Azure Service Bus это обеспечивается lock-ом сообщения (PeekLock).
```

### 5. Другие важные паттерны

| Паттерн | Описание |
|---------|----------|
| **Bulkhead** | Изоляция ресурсов (пулы потоков/соединений) для предотвращения каскадного сбоя |
| **Saga** | Распределённые транзакции через последовательность локальных транзакций с компенсацией |
| **CQRS** | Разделение моделей чтения и записи |
| **Event Sourcing** | Хранение всех изменений как последовательности событий |
| **Gateway Aggregation** | Агрегация нескольких вызовов микросервисов в один ответ |
| **Sidecar** | Вспомогательный контейнер рядом с основным (логирование, прокси) |
| **Ambassador** | Прокси-контейнер для управления соединениями |
| **Strangler Fig** | Постепенная миграция монолита на микросервисы |

---

## 12-Factor App

Методология для создания облачных приложений:

| # | Фактор | Описание | Пример в .NET |
|---|--------|----------|---------------|
| 1 | **Codebase** | Один репозиторий — одно приложение | Git-репозиторий |
| 2 | **Dependencies** | Явное объявление зависимостей | NuGet, `*.csproj` |
| 3 | **Config** | Конфигурация через переменные окружения | `IConfiguration`, Azure App Settings |
| 4 | **Backing Services** | Внешние сервисы как подключаемые ресурсы | Connection strings для БД, Redis, MQ |
| 5 | **Build, Release, Run** | Строгое разделение стадий | CI/CD pipeline, Docker |
| 6 | **Processes** | Stateless-процессы | Не хранить состояние в памяти; использовать Redis/БД |
| 7 | **Port Binding** | Привязка к порту | Kestrel: `app.Run("http://0.0.0.0:8080")` |
| 8 | **Concurrency** | Масштабирование через процессы | Горизонтальное масштабирование (replicas) |
| 9 | **Disposability** | Быстрый старт и корректное завершение | `IHostedService`, graceful shutdown |
| 10 | **Dev/Prod Parity** | Минимальные различия между средами | Docker, IaC (Terraform/Bicep) |
| 11 | **Logs** | Логи как поток событий | `stdout` → собирается платформой |
| 12 | **Admin Processes** | Административные задачи как одноразовые процессы | Миграции БД, seed-скрипты |

```csharp
// Пример реализации принципов 12-Factor в .NET

// Фактор 3: Config — всё через переменные окружения
var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddEnvironmentVariables(prefix: "MYAPP_")
    .AddAzureKeyVault(/* ... */);

// Фактор 6: Stateless — сессии в Redis, а не в памяти
builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = builder.Configuration["Redis:ConnectionString"]);

// Фактор 9: Disposability — graceful shutdown
builder.Services.AddHostedService<OrderProcessorService>();

public class OrderProcessorService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessNextBatchAsync(stoppingToken);
        }
        // При получении SIGTERM — корректное завершение
    }
}

// Фактор 11: Logs — структурированный вывод в stdout
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});
```

---

## Оптимизация затрат

### Общие принципы

1. **Right-sizing** — выбор подходящего размера ресурсов (не переплачивать за неиспользуемые мощности)
2. **Reserved Instances** — резервирование на 1-3 года (экономия до 72%)
3. **Spot/Preemptible Instances** — для batch-задач (экономия до 90%)
4. **Auto-scaling** — масштабирование по нагрузке
5. **Serverless** — оплата только за использование (Functions, Container Apps)
6. **Правильный Storage tier** — Hot, Cool, Cold, Archive
7. **Мониторинг** — Azure Cost Management, AWS Cost Explorer

### Azure-специфичные рекомендации

```
| Подход                          | Экономия      |
|---------------------------------|---------------|
| Azure Reservations (1-3 года)   | 30-72%        |
| Azure Spot VMs                  | До 90%        |
| Azure Hybrid Benefit            | До 40%        |
| Auto-shutdown Dev/Test VMs      | До 60%        |
| Cosmos DB Autoscale vs Manual   | До 50%        |
| Azure Functions Consumption     | Первые 1M вызовов бесплатно |
| Blob Storage Lifecycle Policies | Автоперенос в Cool/Archive |
```

```csharp
// Cosmos DB: оптимизация RU
// 1. Выбор правильного partition key (избежание hot partitions)
// 2. Использование point reads вместо запросов
// 3. Проецирование — запрашивать только нужные поля

var query = new QueryDefinition(
    "SELECT c.id, c.name, c.price FROM c WHERE c.categoryId = @cat")
    .WithParameter("@cat", categoryId);

// 4. Использование Autoscale вместо фиксированных RU/s
// В Terraform / Bicep:
// throughput_settings {
//   autoscale {
//     max_throughput = 4000  // Min = max/10 = 400 RU/s
//   }
// }
```

---

## Сравнительная таблица

| Категория | Azure | AWS |
|-----------|-------|-----|
| **Compute (VM)** | Azure VMs | EC2 |
| **Serverless** | Azure Functions | Lambda |
| **PaaS Web** | App Service | Elastic Beanstalk |
| **Containers** | Container Apps, AKS | ECS, EKS, Fargate |
| **Object Storage** | Blob Storage | S3 |
| **Queue** | Service Bus, Storage Queue | SQS |
| **Pub/Sub** | Service Bus Topics, Event Grid | SNS, EventBridge |
| **Streaming** | Event Hub | Kinesis |
| **SQL** | Azure SQL | RDS |
| **NoSQL** | Cosmos DB | DynamoDB |
| **Cache** | Azure Cache for Redis | ElastiCache |
| **Secrets** | Key Vault | Secrets Manager, Parameter Store |
| **IAM** | Entra ID, Managed Identity | IAM, Cognito |
| **Monitoring** | Application Insights, Monitor | CloudWatch, X-Ray |
| **CDN** | Azure CDN / Front Door | CloudFront |
| **DNS** | Azure DNS | Route 53 |
| **API Gateway** | API Management | API Gateway |
| **CI/CD** | Azure DevOps, GitHub Actions | CodePipeline, CodeBuild |
| **IaC** | Bicep, ARM Templates | CloudFormation, CDK |

---

## Вопросы для собеседования

### Вопрос 1: Чем отличается Azure Service Bus от Event Hub? Когда что использовать?

**Ответ:**

**Service Bus** — это полноценный message broker для бизнес-логики:
- Гарантирует доставку и порядок (FIFO через Sessions)
- Поддерживает Dead Letter Queue, отложенные сообщения, транзакции
- Паттерны: Queue (point-to-point) и Topic/Subscription (pub/sub)
- Сценарии: обработка заказов, платежей, межсервисное взаимодействие

**Event Hub** — это платформа для потоковой обработки:
- Оптимизирована для высокой пропускной способности (миллионы событий/сек)
- Append-only log (аналог Kafka)
- Consumer Groups для параллельной обработки
- Retention до 90 дней
- Сценарии: телеметрия, IoT, аналитика, логирование

**Правило**: если нужна надёжная обработка каждого сообщения — Service Bus. Если нужна высокая пропускная способность для потока событий — Event Hub.

---

### Вопрос 2: Что такое Managed Identity и зачем она нужна?

**Ответ:**

Managed Identity — механизм Azure AD, позволяющий приложениям аутентифицироваться в Azure-сервисах без секретов (паролей, ключей, connection strings).

Два типа:
- **System-assigned** — создаётся вместе с ресурсом, привязана к его жизненному циклу
- **User-assigned** — создаётся отдельно, может быть присвоена нескольким ресурсам

Преимущества:
- Нет секретов в коде или конфигурации
- Автоматическая ротация токенов
- Нет риска утечки credentials
- Принцип наименьших привилегий через RBAC

В коде используется `DefaultAzureCredential`, которая автоматически определяет способ аутентификации (Managed Identity в Azure, Azure CLI локально).

---

### Вопрос 3: Как выбрать уровень консистентности в Cosmos DB?

**Ответ:**

Cosmos DB предлагает 5 уровней (по убыванию строгости):

1. **Strong** — все читатели видят последнюю подтверждённую запись. Используется когда критична точность данных (финансы). Снижает производительность, недоступна для multi-region writes.

2. **Bounded Staleness** — отставание ограничено N операциями или T секундами. Подходит для сценариев, где нужен баланс между строгостью и производительностью.

3. **Session** (по умолчанию) — гарантия "read your own writes" в рамках клиентской сессии. Оптимальный выбор для большинства приложений.

4. **Consistent Prefix** — чтения могут отставать, но порядок записей сохраняется. Подходит для отображения ленты событий.

5. **Eventual** — минимальная гарантия, максимальная производительность. Для некритичных данных (лайки, просмотры).

**Рекомендация**: начинайте с Session. Перед выбором Strong или Bounded Staleness оцените влияние на RU и latency.

---

### Вопрос 4: Как реализовать Circuit Breaker в .NET-приложении?

**Ответ:**

Circuit Breaker предотвращает каскадные сбои при недоступности внешнего сервиса.

Три состояния:
- **Closed**: запросы проходят нормально; ошибки подсчитываются
- **Open**: все запросы немедленно отклоняются (fail-fast) на заданное время
- **Half-Open**: пропускается пробный запрос; при успехе — переход в Closed, при ошибке — обратно в Open

В .NET 8+ рекомендуется использовать `Microsoft.Extensions.Resilience` (обёртка над Polly v8):

```csharp
builder.Services.AddHttpClient("ExternalApi")
    .AddResilienceHandler("pipeline", builder =>
    {
        builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 10,
            BreakDuration = TimeSpan.FromSeconds(30)
        });
        builder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential
        });
    });
```

---

### Вопрос 5: Что такое 12-Factor App и какие факторы наиболее важны для облака?

**Ответ:**

12-Factor App — методология разработки SaaS-приложений. Наиболее важные для облака:

- **Config (3)**: конфигурация через переменные окружения, а не в коде. В Azure — App Settings, Key Vault.
- **Statelessness (6)**: процессы не хранят состояние. Сессии — в Redis/БД. Это позволяет горизонтально масштабировать.
- **Disposability (9)**: быстрый старт и корректное завершение. Критично для auto-scaling и контейнеров.
- **Dev/Prod Parity (10)**: одинаковые среды. Docker + IaC (Terraform/Bicep) обеспечивают это.
- **Logs (11)**: логи как поток в stdout. Платформа (Azure Monitor, CloudWatch) собирает и агрегирует.

---

### Вопрос 6: Как работает автомасштабирование в Azure App Service?

**Ответ:**

Azure App Service поддерживает два типа масштабирования:

**Scale-up (вертикальное)**: переход на более мощный тарифный план (больше CPU, RAM). Требует перезапуска.

**Scale-out (горизонтальное)**: увеличение числа экземпляров.
- **Rule-based**: на основе метрик (CPU > 70% → добавить экземпляр)
- **Schedule-based**: по расписанию (рабочие часы → 5 экземпляров, ночь → 2)
- **Automatic (Preview)**: платформа сама принимает решения

Важные настройки:
- Minimum/Maximum instances — границы масштабирования
- Cool-down period — время ожидания между масштабированиями (избежание oscillation)
- Правила уменьшения (scale-in) — обычно с бОльшим cool-down

Приложение должно быть stateless для корректной работы auto-scaling.

---

### Вопрос 7: В чём разница между Azure SQL и Cosmos DB? Когда какой выбрать?

**Ответ:**

**Azure SQL** — когда:
- Структурированные данные с жёсткой схемой
- Сложные JOIN и аналитические запросы
- ACID-транзакции между таблицами
- Существующее приложение на EF Core / SQL Server
- Отчётность и BI

**Cosmos DB** — когда:
- Глобальное распределение с multi-region writes
- Гарантия latency < 10 мс на уровне SLA
- Гибкая или эволюционирующая схема данных
- Очень высокая пропускная способность (горизонтальное масштабирование)
- Разные модели данных (документы, графы, колонки, key-value)
- Event sourcing (Change Feed)

**Гибридный подход**: многие системы используют оба — SQL для транзакционных данных и Cosmos DB для каталогов, сессий, событий.

---

### Вопрос 8: Как безопасно хранить секреты в облачном .NET-приложении?

**Ответ:**

**Никогда** не хранить секреты:
- В коде (hardcoded strings)
- В `appsettings.json` под source control
- В переменных окружения без шифрования (допустимо для некритичных настроек)

**Правильный подход:**

1. **Azure Key Vault** — централизованное хранилище секретов
2. **Managed Identity** — доступ к Key Vault без секретов
3. **DefaultAzureCredential** — единый код для локальной разработки и production

```csharp
// Production: Managed Identity → Key Vault
// Development: Azure CLI / Visual Studio credentials → Key Vault
builder.Configuration.AddAzureKeyVault(
    new Uri(builder.Configuration["KeyVault:Url"]!),
    new DefaultAzureCredential());
```

4. **User Secrets** — для локальной разработки (`dotnet user-secrets`)
5. **Ротация секретов** — Key Vault поддерживает версионирование и автоматическую ротацию

---

### Вопрос 9: Объясните паттерн Queue-Based Load Leveling. Когда он полезен?

**Ответ:**

Паттерн помещает задачи в очередь между отправителем и обработчиком, чтобы:

1. **Выровнять нагрузку**: пиковые запросы буферизуются в очереди, обработчик работает с постоянной скоростью
2. **Защитить бэкенд**: база данных или внешний сервис не перегружаются
3. **Обеспечить отказоустойчивость**: при падении обработчика сообщения остаются в очереди

**Реализация в Azure:**
- Очередь: Azure Service Bus или Storage Queue
- Обработчик: Azure Functions (триггер по очереди) или Background Worker
- API возвращает `202 Accepted` с tracking ID

**Когда использовать:**
- Пиковая нагрузка значительно превышает среднюю
- Тяжёлые операции (генерация отчётов, отправка email, обработка изображений)
- Интеграция с внешними API с ограничением rate limit

**Важно**: клиент должен уметь работать асинхронно (polling, webhook, SignalR).

---

### Вопрос 10: Как организовать мониторинг и алертинг для .NET-микросервисов в Azure?

**Ответ:**

**Стек мониторинга Azure:**

1. **Application Insights** — APM (Application Performance Monitoring):
   - Автоматический сбор HTTP-запросов, зависимостей, исключений
   - Distributed tracing (сквозная трассировка между микросервисами)
   - Live Metrics, Application Map
   - Кастомные метрики и события

2. **Azure Monitor** — инфраструктурные метрики:
   - CPU, память, сеть
   - Auto-scale rules на основе метрик
   - Log Analytics workspace (KQL-запросы)

3. **Azure Monitor Alerts**:
   - Metric alerts (CPU > 80%)
   - Log alerts (количество 5xx ошибок за 5 минут)
   - Smart detection (аномалии)

**Практические рекомендации:**

```csharp
// Structured logging
logger.LogWarning("Заказ {OrderId} обработан за {Duration}ms, " +
    "превышение порога {Threshold}ms",
    orderId, duration, threshold);

// Health checks
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "sql", tags: ["ready"])
    .AddRedis(redisConnection, name: "redis", tags: ["ready"])
    .AddAzureServiceBusQueue(sbConnection, "orders", name: "servicebus");

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false  // Только проверка, что процесс жив
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

---

### Вопрос 11: Чем Azure Functions отличается от Azure Container Apps? Когда что выбрать?

**Ответ:**

| Критерий | Azure Functions | Container Apps |
|----------|----------------|----------------|
| Модель | Функции (event-driven) | Контейнеры |
| Масштабирование | До 0 (Consumption) | До 0 (serverless) |
| Длительность | До 10 мин (Consumption) | Без ограничений |
| Состояние | Stateless (кроме Durable Functions) | Stateless/Stateful |
| Зависимости | Ограничены SDK | Любые (Docker) |
| Сеть | Ограничена (VNet в Premium) | Полная VNet-интеграция |

**Functions** — для: коротких event-driven задач, триггеров, лёгких API, автоматизации.

**Container Apps** — для: микросервисов, долгоживущих процессов, приложений с особыми зависимостями, Dapr-интеграции.

---

### Вопрос 12: Как реализовать Retry с экспоненциальным backoff и jitter? Зачем нужен jitter?

**Ответ:**

**Экспоненциальный backoff**: задержка между повторами увеличивается экспоненциально (1с, 2с, 4с, 8с...), чтобы дать сервису время восстановиться.

**Jitter (случайное отклонение)**: добавляет случайную составляющую к задержке. Без jitter все клиенты будут повторять запросы одновременно (thundering herd), создавая повторные пики нагрузки.

```csharp
// С jitter: вместо ровно 2с → случайное значение около 2с
// Это распределяет повторные запросы во времени

builder.Services.AddHttpClient("api")
    .AddResilienceHandler("retry", b =>
    {
        b.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 4,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true  // Критически важно для распределённых систем
        });
    });
```

---

### Вопрос 13: Как работает Change Feed в Cosmos DB и для чего его использовать?

**Ответ:**

**Change Feed** — это упорядоченный поток изменений (inserts и updates) в контейнере Cosmos DB.

Сценарии:
- Реактивная обработка: отправка уведомлений при изменении данных
- Материализация представлений: обновление read-моделей (CQRS)
- Event sourcing: аудит всех изменений
- Синхронизация с другими хранилищами (Elasticsearch, Redis)
- Триггер Azure Functions

```csharp
// Change Feed Processor (SDK)
var processor = _container
    .GetChangeFeedProcessorBuilder<OrderDocument>(
        "orderProcessor",
        HandleChangesAsync)
    .WithInstanceName(Environment.MachineName)
    .WithLeaseContainer(_leaseContainer)
    .WithStartTime(DateTime.UtcNow.AddHours(-1))
    .Build();

await processor.StartAsync();

static async Task HandleChangesAsync(
    ChangeFeedProcessorContext context,
    IReadOnlyCollection<OrderDocument> changes,
    CancellationToken cancellationToken)
{
    foreach (var doc in changes)
    {
        // Обновление read-модели, отправка уведомления и т.д.
        await ProcessChangeAsync(doc);
    }
}
```

**Важно**: Change Feed не содержит удалений. Для отслеживания удалений используйте soft-delete (поле `isDeleted`).

---

### Бонус: Краткая шпаргалка для собеседования

```
Выбор compute:
  VM / EC2         → полный контроль, legacy-приложения
  App Service      → PaaS для веб-приложений, быстрый деплой
  Functions/Lambda → event-driven, короткие задачи, оплата за выполнение
  Container Apps   → микросервисы без управления K8s
  AKS/EKS         → полноценный Kubernetes

Выбор базы данных:
  Azure SQL / RDS  → реляционные данные, ACID, сложные запросы
  Cosmos DB        → глобальное распределение, < 10ms latency, гибкая схема
  DynamoDB         → key-value, высокая пропускная способность
  Table Storage    → простой key-value, низкая стоимость

Выбор очереди:
  Service Bus      → надёжная бизнес-логика, FIFO, DLQ, транзакции
  Event Hub        → потоковая обработка, высокая пропускная способность
  Event Grid       → реактивные события, push-модель
  Storage Queue    → простая очередь, низкая стоимость

Безопасность:
  Managed Identity → аутентификация без секретов
  Key Vault        → централизованное хранение секретов
  DefaultAzureCredential → единый код для dev и prod

Паттерны:
  Retry + Jitter          → устойчивость к временным сбоям
  Circuit Breaker         → защита от каскадных сбоев
  Queue-Based Load Leveling → выравнивание нагрузки
  Competing Consumers     → параллельная обработка очереди
```
