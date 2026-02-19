# Микросервисная архитектура — подготовка к Senior .NET собеседованию

## Содержание

1. [Монолит vs Микросервисы](#1-монолит-vs-микросервисы--когда-что-выбрать)
2. [Принципы декомпозиции](#2-принципы-декомпозиции)
3. [Коммуникация между сервисами](#3-коммуникация-между-сервисами)
4. [API Gateway](#4-api-gateway-паттерн)
5. [Service Discovery](#5-service-discovery)
6. [Message Brokers: RabbitMQ vs Kafka](#6-message-brokers-rabbitmq-vs-kafka)
7. [Паттерны отказоустойчивости и координации](#7-паттерны-отказоустойчивости-и-координации)
8. [Event-Driven Architecture и Event Sourcing](#8-event-driven-architecture-и-event-sourcing)
9. [Distributed Transactions](#9-distributed-transactions--проблемы-и-решения)
10. [Data Management: Database per Service](#10-data-management-database-per-service)
11. [Observability: Health Checks, Logging, Tracing](#11-observability-health-checks-logging-distributed-tracing)
12. [Идемпотентность](#12-идемпотентность)
13. [Versioning API](#13-versioning-api)
14. [.NET реализации: MassTransit, Rebus, Wolverine, Dapr](#14-net-реализации)
15. [Docker и Docker Compose](#15-docker--docker-compose-для-микросервисов)
16. [Вопросы на собеседовании с ответами](#16-вопросы-на-собеседовании-с-ответами)

---

## 1. Монолит vs Микросервисы — когда что выбрать

### Монолитная архитектура

Все компоненты приложения развёрнуты как единый процесс, разделяющий общую кодовую базу и базу данных.

**Преимущества монолита:**
- Простота разработки и отладки на начальном этапе
- Простой деплой — один артефакт
- Нет проблем с распределёнными транзакциями
- Низкий порог входа для новых разработчиков

**Недостатки монолита:**
- Масштабирование только целиком (vertical scaling)
- Длительный цикл сборки и деплоя при росте кодовой базы
- Технологический lock-in — один стек на всё
- Одна ошибка может положить всё приложение

### Микросервисная архитектура

Система разбита на небольшие автономные сервисы, каждый из которых отвечает за конкретную бизнес-функцию.

**Преимущества микросервисов:**
- Независимое масштабирование отдельных сервисов
- Независимые деплои
- Технологическая гетерогенность — каждый сервис может использовать свой стек
- Изоляция отказов (fault isolation)
- Лучше подходит для больших команд

**Недостатки микросервисов:**
- Сложность операционной инфраструктуры
- Распределённые транзакции
- Сетевые задержки и необходимость обработки сбоев сети
- Сложность отладки и трейсинга
- Data consistency — eventual consistency вместо strong consistency

### Когда что выбирать

| Критерий | Монолит | Микросервисы |
|---|---|---|
| Размер команды | < 10 человек | > 10 человек, несколько команд |
| Стадия продукта | MVP, стартап | Зрелый продукт с понятными доменами |
| Нагрузка | Равномерная | Неравномерная, нужно точечное масштабирование |
| Домен | Плохо изученный | Хорошо понятный, чёткие bounded contexts |
| DevOps-зрелость | Низкая | Высокая (CI/CD, контейнеризация, оркестрация) |

> **Совет на собеседовании:** Не говорите «микросервисы всегда лучше». Покажите, что понимаете компромиссы. Упоминайте паттерн «Modular Monolith» как промежуточное решение — монолит с чёткими модульными границами, который проще потом разбить на микросервисы.

---

## 2. Принципы декомпозиции

### Декомпозиция по бизнес-доменам

Каждый микросервис соответствует бизнес-возможности (business capability): `OrderService`, `PaymentService`, `InventoryService`, `NotificationService`.

### Декомпозиция по Bounded Context (DDD)

Bounded Context — это граница, внутри которой определённая модель имеет конкретное значение. Один и тот же термин «Product» может значить разное в контексте каталога, склада и заказов.

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Catalog BC    │    │  Warehouse BC   │    │    Order BC     │
│                 │    │                 │    │                 │
│  Product:       │    │  Product:       │    │  Product:       │
│  - Name         │    │  - SKU          │    │  - ProductId    │
│  - Description  │    │  - Location     │    │  - Quantity     │
│  - Price        │    │  - Quantity     │    │  - UnitPrice    │
│  - Images       │    │  - Weight       │    │                 │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

### Правила декомпозиции

1. **Single Responsibility** — один сервис = одна бизнес-функция
2. **High Cohesion** — всё, что связано с доменом, внутри одного сервиса
3. **Loose Coupling** — минимум зависимостей между сервисами
4. **Автономность данных** — у каждого сервиса своя база данных
5. **Размер** — сервис должен быть достаточно маленьким, чтобы одна команда (2-pizza team) могла его поддерживать

### Пример структуры проекта

```
src/
├── Services/
│   ├── Catalog/
│   │   ├── Catalog.API/
│   │   ├── Catalog.Domain/
│   │   ├── Catalog.Infrastructure/
│   │   └── Catalog.Application/
│   ├── Ordering/
│   │   ├── Ordering.API/
│   │   ├── Ordering.Domain/
│   │   ├── Ordering.Infrastructure/
│   │   └── Ordering.Application/
│   └── Payment/
│       ├── Payment.API/
│       ├── Payment.Domain/
│       ├── Payment.Infrastructure/
│       └── Payment.Application/
├── ApiGateways/
│   └── Web.Gateway/
├── BuildingBlocks/
│   ├── EventBus/
│   └── Common/
└── docker-compose.yml
```

---

## 3. Коммуникация между сервисами

### Синхронная коммуникация

#### REST (HTTP)

Самый распространённый способ. Прост в реализации, широко поддерживается.

```csharp
// Вызов другого сервиса через HttpClient (с Typed Client)
public class OrderService
{
    private readonly HttpClient _httpClient;

    public OrderService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("CatalogService");
    }

    public async Task<ProductDto?> GetProductAsync(int productId)
    {
        var response = await _httpClient.GetAsync($"/api/products/{productId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductDto>();
    }
}

// Регистрация в DI
builder.Services.AddHttpClient("CatalogService", client =>
{
    client.BaseAddress = new Uri("https://catalog-service:5001");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy());
```

#### gRPC

Бинарный протокол на основе HTTP/2 и Protocol Buffers. Высокая производительность, строгая типизация.

```protobuf
// catalog.proto
syntax = "proto3";

option csharp_namespace = "Catalog.Grpc";

service CatalogService {
  rpc GetProduct (GetProductRequest) returns (ProductResponse);
  rpc GetProducts (GetProductsRequest) returns (stream ProductResponse);
}

message GetProductRequest {
  int32 product_id = 1;
}

message ProductResponse {
  int32 id = 1;
  string name = 2;
  double price = 3;
  int32 stock = 4;
}
```

```csharp
// gRPC-клиент в .NET
public class CatalogGrpcClient
{
    private readonly CatalogService.CatalogServiceClient _client;

    public CatalogGrpcClient(CatalogService.CatalogServiceClient client)
    {
        _client = client;
    }

    public async Task<ProductResponse> GetProductAsync(int productId)
    {
        var request = new GetProductRequest { ProductId = productId };
        return await _client.GetProductAsync(request);
    }
}

// Регистрация
builder.Services.AddGrpcClient<CatalogService.CatalogServiceClient>(options =>
{
    options.Address = new Uri("https://catalog-service:5001");
});
```

**REST vs gRPC:**

| Критерий | REST | gRPC |
|---|---|---|
| Протокол | HTTP/1.1 или HTTP/2 | HTTP/2 |
| Формат данных | JSON (текстовый) | Protobuf (бинарный) |
| Производительность | Ниже | Выше (в 5-10 раз) |
| Streaming | Ограничен | Bidirectional streaming |
| Browser-поддержка | Полная | Через gRPC-Web |
| Контракт | OpenAPI/Swagger (опционально) | .proto файл (обязательно) |

### Асинхронная коммуникация (Message Broker)

Сервисы общаются через очередь сообщений. Отправитель не ждёт ответа.

```csharp
// Публикация события через MassTransit + RabbitMQ
public class OrderCreatedEvent
{
    public Guid OrderId { get; init; }
    public string CustomerId { get; init; } = default!;
    public decimal TotalAmount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public class OrderController : ControllerBase
{
    private readonly IPublishEndpoint _publishEndpoint;

    public OrderController(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
    {
        // ... создание заказа в БД ...

        await _publishEndpoint.Publish(new OrderCreatedEvent
        {
            OrderId = order.Id,
            CustomerId = request.CustomerId,
            TotalAmount = order.TotalAmount,
            CreatedAt = DateTime.UtcNow
        });

        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }
}
```

```csharp
// Подписчик (Consumer) в другом сервисе
public class OrderCreatedConsumer : IConsumer<OrderCreatedEvent>
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<OrderCreatedConsumer> _logger;

    public OrderCreatedConsumer(
        IPaymentService paymentService,
        ILogger<OrderCreatedConsumer> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        _logger.LogInformation(
            "Processing payment for order {OrderId}", context.Message.OrderId);

        await _paymentService.ProcessPaymentAsync(
            context.Message.OrderId,
            context.Message.TotalAmount);
    }
}
```

---

## 4. API Gateway паттерн

API Gateway — единая точка входа для клиентов. Выполняет маршрутизацию, аутентификацию, rate limiting, агрегацию запросов.

### Ocelot

```json
// ocelot.json
{
  "Routes": [
    {
      "DownstreamPathTemplate": "/api/products/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        { "Host": "catalog-service", "Port": 80 }
      ],
      "UpstreamPathTemplate": "/catalog/{everything}",
      "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE" ],
      "AuthenticationOptions": {
        "AuthenticationProviderKey": "Bearer"
      },
      "RateLimitOptions": {
        "EnableRateLimiting": true,
        "Period": "1m",
        "Limit": 100
      }
    },
    {
      "DownstreamPathTemplate": "/api/orders/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        { "Host": "order-service", "Port": 80 }
      ],
      "UpstreamPathTemplate": "/orders/{everything}",
      "UpstreamHttpMethod": [ "GET", "POST" ]
    }
  ],
  "GlobalConfiguration": {
    "BaseUrl": "https://api.myshop.com"
  }
}
```

```csharp
// Program.cs для Ocelot Gateway
var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
builder.Services.AddOcelot();

var app = builder.Build();
await app.UseOcelot();
app.Run();
```

### YARP (Yet Another Reverse Proxy)

Более современная альтернатива от Microsoft. Лучшая производительность и гибкость.

```json
// appsettings.json
{
  "ReverseProxy": {
    "Routes": {
      "catalog-route": {
        "ClusterId": "catalog-cluster",
        "Match": {
          "Path": "/catalog/{**catch-all}"
        },
        "Transforms": [
          { "PathRemovePrefix": "/catalog" }
        ]
      },
      "order-route": {
        "ClusterId": "order-cluster",
        "Match": {
          "Path": "/orders/{**catch-all}"
        },
        "Transforms": [
          { "PathRemovePrefix": "/orders" }
        ]
      }
    },
    "Clusters": {
      "catalog-cluster": {
        "Destinations": {
          "destination1": {
            "Address": "http://catalog-service:80"
          }
        },
        "HealthCheck": {
          "Active": {
            "Enabled": true,
            "Interval": "00:00:30",
            "Path": "/health"
          }
        }
      },
      "order-cluster": {
        "LoadBalancingPolicy": "RoundRobin",
        "Destinations": {
          "destination1": { "Address": "http://order-service-1:80" },
          "destination2": { "Address": "http://order-service-2:80" }
        }
      }
    }
  }
}
```

```csharp
// Program.cs для YARP
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();
app.MapReverseProxy();
app.Run();
```

> **YARP vs Ocelot:** YARP предпочтительнее для новых проектов — выше производительность, разработка Microsoft, активная поддержка, встроенная интеграция с ASP.NET Core middleware pipeline. Ocelot — зрелый проект с большим community, но развивается медленнее.

---

## 5. Service Discovery

В микросервисной архитектуре адреса сервисов могут меняться динамически (контейнеры, автоскейлинг). Service Discovery решает проблему поиска инстансов.

### Consul

```csharp
// Регистрация сервиса в Consul при старте
public static class ConsulRegistrationExtensions
{
    public static IServiceCollection AddConsulRegistration(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IConsulClient, ConsulClient>(p =>
            new ConsulClient(cfg =>
            {
                cfg.Address = new Uri(configuration["Consul:Address"]!);
            }));

        return services;
    }

    public static IApplicationBuilder UseConsulRegistration(
        this IApplicationBuilder app)
    {
        var consulClient = app.ApplicationServices.GetRequiredService<IConsulClient>();
        var lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
        var configuration = app.ApplicationServices.GetRequiredService<IConfiguration>();

        var registration = new AgentServiceRegistration
        {
            ID = $"{configuration["Service:Name"]}-{Guid.NewGuid()}",
            Name = configuration["Service:Name"],
            Address = configuration["Service:Host"],
            Port = int.Parse(configuration["Service:Port"]!),
            Check = new AgentServiceCheck
            {
                HTTP = $"http://{configuration["Service:Host"]}:" +
                       $"{configuration["Service:Port"]}/health",
                Interval = TimeSpan.FromSeconds(10),
                DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1)
            }
        };

        consulClient.Agent.ServiceRegister(registration).Wait();

        lifetime.ApplicationStopping.Register(() =>
        {
            consulClient.Agent.ServiceDeregister(registration.ID).Wait();
        });

        return app;
    }
}
```

### Механизмы Service Discovery

1. **Client-side discovery** — клиент запрашивает реестр и сам выбирает инстанс (Consul, Eureka)
2. **Server-side discovery** — запрос идёт через балансировщик, который обращается к реестру (Kubernetes Service, AWS ELB)
3. **DNS-based** — Kubernetes внутренний DNS, Consul DNS interface

В Kubernetes Service Discovery встроен: каждый `Service` получает DNS-имя вида `catalog-service.default.svc.cluster.local`.

---

## 6. Message Brokers: RabbitMQ vs Kafka

### RabbitMQ

Классический message broker, реализующий протокол AMQP. Модель: Producer -> Exchange -> Queue -> Consumer.

```csharp
// Настройка MassTransit с RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>();
    x.AddConsumer<PaymentCompletedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq://rabbitmq-host", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ReceiveEndpoint("order-created-queue", e =>
        {
            e.ConfigureConsumer<OrderCreatedConsumer>(context);
            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
        });

        cfg.ConfigureEndpoints(context);
    });
});
```

### Kafka

Распределённая платформа для потоковой обработки событий. Модель: Producer -> Topic (Partitions) -> Consumer Group.

```csharp
// Настройка MassTransit с Kafka (Confluent)
builder.Services.AddMassTransit(x =>
{
    x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));

    x.AddRider(rider =>
    {
        rider.AddConsumer<OrderEventConsumer>();
        rider.AddProducer<OrderCreatedEvent>("order-events");

        rider.UsingKafka((context, k) =>
        {
            k.Host("kafka-broker:9092");

            k.TopicEndpoint<OrderCreatedEvent>("order-events", "order-group", e =>
            {
                e.ConfigureConsumer<OrderEventConsumer>(context);
                e.AutoOffsetReset = AutoOffsetReset.Earliest;
            });
        });
    });
});
```

### Сравнение RabbitMQ и Kafka

| Критерий | RabbitMQ | Kafka |
|---|---|---|
| Модель | Message Queue (push) | Event Log (pull) |
| Хранение сообщений | Удаляет после подтверждения | Хранит заданное время (retention) |
| Порядок | В рамках одной очереди | В рамках partition |
| Replay | Нет (сообщение удалено) | Да (consumer может перечитать) |
| Throughput | Тысячи msg/sec | Миллионы msg/sec |
| Routing | Гибкий (exchanges, routing keys) | По topic и partition key |
| Протокол | AMQP | Свой бинарный протокол |
| Когда использовать | Task queue, RPC, routing | Event streaming, audit log, высокий throughput |

> **Правило:** RabbitMQ — для command-стиля общения (сделай X). Kafka — для event-стиля (произошло Y), event sourcing и аналитики в реальном времени.

---

## 7. Паттерны отказоустойчивости и координации

### Saga

Saga — способ управления распределёнными транзакциями через последовательность локальных транзакций с компенсациями.

#### Orchestration (централизованный координатор)

```csharp
// Saga Orchestrator через MassTransit State Machine
public class OrderSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = default!;
    public Guid OrderId { get; set; }
    public decimal TotalAmount { get; set; }
    public string CustomerId { get; set; } = default!;
}

public class OrderSaga : MassTransitStateMachine<OrderSagaState>
{
    public State PaymentPending { get; private set; } = null!;
    public State InventoryReserved { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    public Event<OrderCreatedEvent> OrderCreated { get; private set; } = null!;
    public Event<PaymentCompletedEvent> PaymentCompleted { get; private set; } = null!;
    public Event<PaymentFailedEvent> PaymentFailed { get; private set; } = null!;
    public Event<InventoryReservedEvent> InventoryReserved { get; private set; } = null!;

    public OrderSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderCreated, x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => PaymentCompleted, x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => PaymentFailed, x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => InventoryReserved, x => x.CorrelateById(ctx => ctx.Message.OrderId));

        Initially(
            When(OrderCreated)
                .Then(ctx =>
                {
                    ctx.Saga.OrderId = ctx.Message.OrderId;
                    ctx.Saga.TotalAmount = ctx.Message.TotalAmount;
                    ctx.Saga.CustomerId = ctx.Message.CustomerId;
                })
                .Publish(ctx => new ProcessPaymentCommand
                {
                    OrderId = ctx.Saga.OrderId,
                    Amount = ctx.Saga.TotalAmount
                })
                .TransitionTo(PaymentPending));

        During(PaymentPending,
            When(PaymentCompleted)
                .Publish(ctx => new ReserveInventoryCommand
                {
                    OrderId = ctx.Saga.OrderId
                })
                .TransitionTo(InventoryReserved),
            When(PaymentFailed)
                .Publish(ctx => new CancelOrderCommand
                {
                    OrderId = ctx.Saga.OrderId,
                    Reason = "Payment failed"
                })
                .TransitionTo(Failed)
                .Finalize());

        During(InventoryReserved,
            When(InventoryReserved)
                .Publish(ctx => new CompleteOrderCommand
                {
                    OrderId = ctx.Saga.OrderId
                })
                .TransitionTo(Completed)
                .Finalize());
    }
}
```

#### Choreography (децентрализованный, на событиях)

Каждый сервис слушает события и принимает решение самостоятельно. Нет центрального координатора.

```
OrderService          PaymentService          InventoryService
    │                       │                        │
    ├──OrderCreated────────>│                        │
    │                       ├──PaymentCompleted──────>│
    │                       │                        ├──InventoryReserved──>
    │<──────────────────────┼────────────────────────┤
    │              (OrderCompleted)                   │
```

**Orchestration vs Choreography:**
- Orchestration: проще отслеживать поток, легче дебажить, но есть единая точка отказа (оркестратор)
- Choreography: лучше decoupling, нет единой точки отказа, но сложнее отслеживать и дебажить

### Circuit Breaker (Polly)

Предотвращает каскадные отказы, «размыкая цепь» при повторяющихся ошибках.

```csharp
// Polly v8 (современный API через Resilience Pipelines)
builder.Services.AddHttpClient("CatalogService")
    .AddResilienceHandler("catalog-pipeline", builder =>
    {
        // Retry
        builder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(500),
            BackoffType = DelayBackoffType.Exponential,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .HandleResult(r => r.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        });

        // Circuit Breaker
        builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            SamplingDuration = TimeSpan.FromSeconds(30),
            FailureRatio = 0.5,
            MinimumThroughput = 10,
            BreakDuration = TimeSpan.FromSeconds(15),
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .HandleResult(r => !r.IsSuccessStatusCode)
        });

        // Timeout
        builder.AddTimeout(TimeSpan.FromSeconds(5));
    });
```

### Bulkhead

Изолирует ресурсы, чтобы проблема в одном компоненте не исчерпала ресурсы всей системы.

```csharp
// Bulkhead через Polly — ограничение параллельных вызовов
builder.AddConcurrencyLimiter(new ConcurrencyLimiterOptions
{
    PermitLimit = 25,          // максимум 25 параллельных вызовов
    QueueLimit = 50            // максимум 50 в очереди
});
```

### Outbox Pattern

Гарантирует атомарность записи в БД и публикации события. Событие сначала сохраняется в таблицу Outbox той же транзакцией, а затем отдельный процесс публикует его в брокер.

```csharp
// Outbox — сохранение события в той же транзакции
public class OrderRepository
{
    private readonly OrderDbContext _context;

    public OrderRepository(OrderDbContext context)
    {
        _context = context;
    }

    public async Task CreateOrderAsync(Order order, OrderCreatedEvent @event)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Orders.Add(order);

            _context.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = nameof(OrderCreatedEvent),
                Payload = JsonSerializer.Serialize(@event),
                CreatedAt = DateTime.UtcNow,
                ProcessedAt = null
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}

// Background Service для публикации из Outbox
public class OutboxPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPublishEndpoint _publishEndpoint;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

            var messages = await context.OutboxMessages
                .Where(m => m.ProcessedAt == null)
                .OrderBy(m => m.CreatedAt)
                .Take(50)
                .ToListAsync(stoppingToken);

            foreach (var message in messages)
            {
                // Десериализация и публикация
                var @event = JsonSerializer.Deserialize<OrderCreatedEvent>(message.Payload);
                await _publishEndpoint.Publish(@event!, stoppingToken);

                message.ProcessedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
```

> **MassTransit** поддерживает Outbox из коробки через `UseEntityFrameworkCoreOutbox()`.

---

## 8. Event-Driven Architecture и Event Sourcing

### Event-Driven Architecture (EDA)

Сервисы взаимодействуют через события. Событие — факт, который произошёл (`OrderPlaced`, `PaymentReceived`).

Типы событий:
- **Domain Events** — внутри одного bounded context (`OrderItemAdded`)
- **Integration Events** — между сервисами (`OrderCreatedIntegrationEvent`)

```csharp
// Интеграционное событие
public record OrderCreatedIntegrationEvent(
    Guid OrderId,
    string CustomerId,
    List<OrderItemDto> Items,
    decimal TotalAmount,
    DateTime OccurredAt) : IntegrationEvent;

// Базовый класс
public record IntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
```

### Event Sourcing

Вместо хранения текущего состояния сущности хранятся все события, которые привели к этому состоянию.

```csharp
// Агрегат с Event Sourcing
public class Order
{
    public Guid Id { get; private set; }
    public OrderStatus Status { get; private set; }
    public List<OrderItem> Items { get; private set; } = new();
    public decimal TotalAmount { get; private set; }

    private readonly List<IDomainEvent> _uncommittedEvents = new();
    public IReadOnlyList<IDomainEvent> UncommittedEvents => _uncommittedEvents;

    // Восстановление из событий
    public static Order FromHistory(IEnumerable<IDomainEvent> events)
    {
        var order = new Order();
        foreach (var @event in events)
        {
            order.Apply(@event);
        }
        return order;
    }

    // Команда — создание заказа
    public static Order Create(Guid id, string customerId)
    {
        var order = new Order();
        order.RaiseEvent(new OrderCreatedDomainEvent(id, customerId, DateTime.UtcNow));
        return order;
    }

    public void AddItem(string productId, int quantity, decimal price)
    {
        RaiseEvent(new OrderItemAddedEvent(Id, productId, quantity, price));
    }

    public void Confirm()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Only pending orders can be confirmed.");

        RaiseEvent(new OrderConfirmedEvent(Id, DateTime.UtcNow));
    }

    // Применение событий
    private void Apply(IDomainEvent @event)
    {
        switch (@event)
        {
            case OrderCreatedDomainEvent e:
                Id = e.OrderId;
                Status = OrderStatus.Pending;
                break;

            case OrderItemAddedEvent e:
                Items.Add(new OrderItem(e.ProductId, e.Quantity, e.Price));
                TotalAmount = Items.Sum(i => i.Quantity * i.Price);
                break;

            case OrderConfirmedEvent:
                Status = OrderStatus.Confirmed;
                break;
        }
    }

    private void RaiseEvent(IDomainEvent @event)
    {
        Apply(@event);
        _uncommittedEvents.Add(@event);
    }
}
```

```csharp
// Event Store — сохранение и восстановление
public class EventStoreRepository
{
    private readonly EventStoreClient _client;

    public async Task SaveAsync(Order order)
    {
        var events = order.UncommittedEvents
            .Select(e => new EventData(
                Uuid.NewUuid(),
                e.GetType().Name,
                JsonSerializer.SerializeToUtf8Bytes(e, e.GetType())))
            .ToArray();

        await _client.AppendToStreamAsync(
            $"order-{order.Id}",
            StreamState.Any,
            events);
    }

    public async Task<Order> LoadAsync(Guid orderId)
    {
        var result = _client.ReadStreamAsync(
            Direction.Forwards,
            $"order-{orderId}",
            StreamPosition.Start);

        var events = new List<IDomainEvent>();
        await foreach (var @event in result)
        {
            var type = Type.GetType(@event.Event.EventType)!;
            var data = JsonSerializer.Deserialize(@event.Event.Data.Span, type);
            events.Add((IDomainEvent)data!);
        }

        return Order.FromHistory(events);
    }
}
```

> **CQRS + Event Sourcing** часто используются вместе: write-модель строится из событий, а read-модель (проекция) создаётся отдельно для оптимизации запросов.

---

## 9. Distributed Transactions — проблемы и решения

### Проблема

В монолите: одна БД, одна транзакция, ACID. В микросервисах: каждый сервис имеет свою БД, классический `BEGIN TRANSACTION ... COMMIT` невозможен.

### Two-Phase Commit (2PC)

Координатор опрашивает участников (Prepare), затем фиксирует (Commit). **Не рекомендуется** для микросервисов — блокирующий, слабо масштабируется, единая точка отказа.

### Рекомендуемые подходы

1. **Saga Pattern** — последовательность локальных транзакций с компенсирующими действиями (см. раздел 7)
2. **Outbox Pattern** — атомарная запись в БД + публикация события (см. раздел 7)
3. **Eventual Consistency** — принятие того, что данные будут согласованы не мгновенно, а через некоторое время
4. **Idempotent Consumers** — потребители должны корректно обрабатывать повторные сообщения

```
Проблема:                          Решение:
┌─────────┐                        ┌─────────┐
│ Service  │ -- Distributed TX --> │  Saga   │
│    A     │     (не работает)     │ Pattern │
│   DB_A   │                       │         │
└─────────┘                        └─────────┘
     │                                  │
     ▼                                  ▼
┌─────────┐                        ┌─────────┐
│ Service  │                       │ Outbox  │
│    B     │                       │ Pattern │
│   DB_B   │                       │         │
└─────────┘                        └─────────┘
```

---

## 10. Data Management: Database per Service

### Принцип

Каждый микросервис владеет своей базой данных. Другие сервисы **не имеют прямого доступа** к чужой БД.

### Polyglot Persistence

Каждый сервис выбирает оптимальную БД:

```
┌──────────────────┐   ┌──────────────────┐   ┌──────────────────┐
│  Catalog Service │   │  Order Service   │   │  Search Service  │
│   PostgreSQL     │   │   SQL Server     │   │  Elasticsearch   │
└──────────────────┘   └──────────────────┘   └──────────────────┘

┌──────────────────┐   ┌──────────────────┐   ┌──────────────────┐
│  Cart Service    │   │  User Service    │   │ Analytics Service│
│     Redis        │   │   PostgreSQL     │   │     ClickHouse   │
└──────────────────┘   └──────────────────┘   └──────────────────┘
```

### Проблемы и решения

| Проблема | Решение |
|---|---|
| JOIN между сервисами | API Composition, CQRS с денормализованной read-моделью |
| Согласованность данных | Eventual consistency через события |
| Отчёты по нескольким доменам | Отдельный аналитический сервис, подписанный на события |
| Дублирование данных | Допустимо — каждый сервис хранит нужную ему проекцию |

```csharp
// API Composition — агрегирование данных из нескольких сервисов
public class OrderDetailsAggregator
{
    private readonly IOrderServiceClient _orderClient;
    private readonly ICatalogServiceClient _catalogClient;
    private readonly ICustomerServiceClient _customerClient;

    public async Task<OrderDetailsDto> GetOrderDetailsAsync(Guid orderId)
    {
        var order = await _orderClient.GetOrderAsync(orderId);

        // Параллельные запросы к другим сервисам
        var customerTask = _customerClient.GetCustomerAsync(order.CustomerId);
        var productTasks = order.Items
            .Select(item => _catalogClient.GetProductAsync(item.ProductId));

        await Task.WhenAll(
            customerTask,
            Task.WhenAll(productTasks));

        return new OrderDetailsDto
        {
            OrderId = order.Id,
            Customer = await customerTask,
            Items = (await Task.WhenAll(productTasks))
                .Zip(order.Items, (product, item) => new OrderItemDetailsDto
                {
                    ProductName = product.Name,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                }).ToList()
        };
    }
}
```

---

## 11. Observability: Health Checks, Logging, Distributed Tracing

### Health Checks

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddSqlServer(
        connectionString: builder.Configuration.GetConnectionString("OrderDb")!,
        name: "sql-server",
        tags: new[] { "db", "sql" })
    .AddRabbitMQ(
        rabbitConnectionString: builder.Configuration["RabbitMQ:ConnectionString"]!,
        name: "rabbitmq",
        tags: new[] { "messaging" })
    .AddRedis(
        redisConnectionString: builder.Configuration["Redis:ConnectionString"]!,
        name: "redis",
        tags: new[] { "cache" })
    .AddUrlGroup(
        new Uri("http://catalog-service/health"),
        name: "catalog-service",
        tags: new[] { "service" });

var app = builder.Build();

// Liveness — жив ли процесс
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false // Не проверяем зависимости, только сам процесс
});

// Readiness — готов ли обрабатывать запросы
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("db") || check.Tags.Contains("messaging")
});

// Детальный health check (для мониторинга)
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

### Structured Logging (Serilog + ELK/Seq)

```csharp
// Program.cs — настройка Serilog
builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithProperty("Service", "OrderService")
        .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
        .WriteTo.Console(new JsonFormatter())
        .WriteTo.Seq("http://seq:5341")
        .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(
            new Uri("http://elasticsearch:9200"))
        {
            IndexFormat = "order-service-{0:yyyy.MM.dd}",
            AutoRegisterTemplate = true,
            AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7
        });
});

// Использование структурированного логирования
public class OrderService
{
    private readonly ILogger<OrderService> _logger;

    public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
    {
        _logger.LogInformation(
            "Creating order for customer {CustomerId} with {ItemCount} items, " +
            "total amount {TotalAmount:C}",
            request.CustomerId,
            request.Items.Count,
            request.TotalAmount);

        // ... логика ...

        _logger.LogInformation(
            "Order {OrderId} created successfully for customer {CustomerId}",
            order.Id,
            request.CustomerId);

        return order;
    }
}
```

### Correlation ID (middleware)

```csharp
// Middleware для propagation Correlation ID между сервисами
public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
```

### Distributed Tracing (OpenTelemetry + Jaeger)

```csharp
// Program.cs — настройка OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "OrderService", serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation(options =>
        {
            options.SetDbStatementForText = true;
            options.RecordException = true;
        })
        .AddSource("MassTransit")
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://jaeger:4317");
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());
```

---

## 12. Идемпотентность

Идемпотентность — свойство операции давать одинаковый результат при повторном вызове. Критически важна в микросервисах, где сообщения могут дублироваться.

### Реализация через Idempotency Key

```csharp
// Middleware для обеспечения идемпотентности
public class IdempotencyFilter : IEndpointFilter
{
    private readonly IDistributedCache _cache;

    public IdempotencyFilter(IDistributedCache cache) => _cache = cache;

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var idempotencyKey = context.HttpContext.Request
            .Headers["Idempotency-Key"].FirstOrDefault();

        if (string.IsNullOrEmpty(idempotencyKey))
            return Results.BadRequest("Idempotency-Key header is required");

        var cacheKey = $"idempotency:{idempotencyKey}";
        var cached = await _cache.GetStringAsync(cacheKey);

        if (cached is not null)
        {
            return JsonSerializer.Deserialize<object>(cached);
        }

        var result = await next(context);

        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(result),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            });

        return result;
    }
}

// Применение
app.MapPost("/api/orders", CreateOrder)
    .AddEndpointFilter<IdempotencyFilter>();
```

### Идемпотентный Consumer

```csharp
// Consumer с защитой от повторной обработки
public class PaymentConsumer : IConsumer<ProcessPaymentCommand>
{
    private readonly PaymentDbContext _context;

    public async Task Consume(ConsumeContext<ProcessPaymentCommand> context)
    {
        var messageId = context.MessageId?.ToString()
            ?? context.Message.OrderId.ToString();

        // Проверяем, обрабатывали ли мы уже это сообщение
        var alreadyProcessed = await _context.ProcessedMessages
            .AnyAsync(m => m.MessageId == messageId);

        if (alreadyProcessed)
        {
            // Уже обработано — пропускаем
            return;
        }

        // Обработка платежа
        var payment = new Payment
        {
            OrderId = context.Message.OrderId,
            Amount = context.Message.Amount,
            Status = PaymentStatus.Completed
        };

        _context.Payments.Add(payment);
        _context.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId = messageId,
            ProcessedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
    }
}
```

---

## 13. Versioning API

### Подходы к версионированию

```csharp
// Установка пакета
// dotnet add package Asp.Versioning.Http

// Program.cs
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;

    // Способы указания версии:
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),          // /api/v1/orders
        new HeaderApiVersionReader("X-Api-Version"), // Header: X-Api-Version: 1.0
        new QueryStringApiVersionReader("api-version") // ?api-version=1.0
    );
});
```

```csharp
// Версионирование через URL-сегмент (наиболее распространённый)
var v1 = app.NewVersionedApi().MapGroup("/api/v{version:apiVersion}");

v1.MapGet("/orders", GetOrdersV1)
    .HasApiVersion(1.0);

v1.MapGet("/orders", GetOrdersV2)
    .HasApiVersion(2.0);

// V1 — оригинальный ответ
async Task<IResult> GetOrdersV1(OrderDbContext db)
{
    var orders = await db.Orders
        .Select(o => new OrderV1Dto(o.Id, o.CustomerName, o.Total))
        .ToListAsync();
    return Results.Ok(orders);
}

// V2 — расширенный ответ с разбивкой на страницы
async Task<IResult> GetOrdersV2(
    OrderDbContext db,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
{
    var orders = await db.Orders
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(o => new OrderV2Dto(
            o.Id, o.CustomerName, o.Total, o.Status, o.CreatedAt))
        .ToListAsync();

    return Results.Ok(new PagedResult<OrderV2Dto>(orders, page, pageSize));
}
```

### Стратегии эволюции API

1. **Расширение** — добавление новых полей (backward compatible)
2. **Deprecation** — помечаем старую версию как устаревшую, даём время на миграцию
3. **Параллельная работа** — две версии работают одновременно
4. **Consumer-Driven Contracts** — потребители определяют, что им нужно (Pact-тесты)

---

## 14. .NET реализации

### MassTransit

Наиболее популярный фреймворк для работы с message broker в .NET. Поддерживает RabbitMQ, Kafka, Azure Service Bus, Amazon SQS.

```csharp
// Полная настройка MassTransit
builder.Services.AddMassTransit(x =>
{
    // Автоматическая регистрация всех Consumers из сборки
    x.AddConsumers(typeof(Program).Assembly);

    // Saga с EF Core persistence
    x.AddSagaStateMachine<OrderSaga, OrderSagaState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Pessimistic;
            r.AddDbContext<DbContext, OrderSagaDbContext>((provider, optionsBuilder) =>
            {
                optionsBuilder.UseNpgsql(
                    provider.GetRequiredService<IConfiguration>()
                        .GetConnectionString("SagaDb"));
            });
        });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        // Outbox для гарантии доставки
        cfg.UseEntityFrameworkCoreOutbox<OrderDbContext>(context);

        cfg.ConfigureEndpoints(context);
    });
});
```

### Rebus

Легковесная альтернатива MassTransit. Простой API, хорошая документация.

```csharp
// Настройка Rebus
builder.Services.AddRebus(configure => configure
    .Transport(t => t.UseRabbitMq(
        "amqp://guest:guest@rabbitmq",
        "order-service-queue"))
    .Routing(r => r.TypeBased()
        .Map<ProcessPaymentCommand>("payment-service-queue")
        .Map<ReserveInventoryCommand>("inventory-service-queue"))
    .Sagas(s => s.StoreInSqlServer(
        connectionString,
        "Sagas",
        "SagaIndexes"))
    .Options(o =>
    {
        o.SetMaxParallelism(10);
        o.EnableCompression();
    })
    .Logging(l => l.Serilog()));

// Обработчик
public class OrderCreatedHandler : IHandleMessages<OrderCreatedEvent>
{
    public async Task Handle(OrderCreatedEvent message)
    {
        // обработка
    }
}
```

### Wolverine

Новый фреймворк от Jeremy Miller (автор Marten, Lamar). Минимальный boilerplate, convention-based.

```csharp
// Program.cs — Wolverine
builder.Host.UseWolverine(opts =>
{
    opts.UseRabbitMq(rabbit =>
    {
        rabbit.HostName = "rabbitmq";
    }).AutoProvision();

    opts.PublishMessage<OrderCreatedEvent>()
        .ToRabbitExchange("order-events");

    opts.ListenToRabbitQueue("order-events")
        .UseDurableInbox();

    // Встроенный Outbox
    opts.Policies.UseDurableLocalQueues();
    opts.Policies.AutoApplyTransactions();

    opts.PersistMessagesWithPostgresql(connectionString, "wolverine");
});

// Обработчик — чистый C# метод, без интерфейсов
public static class OrderCreatedHandler
{
    // Wolverine находит обработчик по конвенции
    public static async Task Handle(
        OrderCreatedEvent @event,
        IPaymentService paymentService,
        ILogger logger)
    {
        logger.LogInformation("Processing order {OrderId}", @event.OrderId);
        await paymentService.ProcessPaymentAsync(@event.OrderId, @event.TotalAmount);
    }
}
```

### Dapr (Distributed Application Runtime)

Sidecar-архитектура для микросервисов. Абстрагирует инфраструктуру: pub/sub, state management, service invocation.

```csharp
// Program.cs — Dapr
builder.Services.AddDaprClient();

var app = builder.Build();
app.UseCloudEvents();          // Обработка CloudEvents формата
app.MapSubscribeHandler();     // Регистрация подписок

// Публикация события через Dapr
app.MapPost("/api/orders", async (
    CreateOrderRequest request,
    DaprClient daprClient) =>
{
    var order = new Order(request);

    // Сохранение состояния через Dapr State Store
    await daprClient.SaveStateAsync("statestore", order.Id.ToString(), order);

    // Публикация события через Dapr Pub/Sub
    await daprClient.PublishEventAsync("pubsub", "order-created", new OrderCreatedEvent
    {
        OrderId = order.Id,
        TotalAmount = order.TotalAmount
    });

    return Results.Created($"/api/orders/{order.Id}", order);
});

// Подписка на событие
[Topic("pubsub", "order-created")]
app.MapPost("/events/order-created", async (OrderCreatedEvent @event) =>
{
    // Обработка
    return Results.Ok();
});
```

```yaml
# dapr/components/pubsub.yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: pubsub
spec:
  type: pubsub.rabbitmq
  version: v1
  metadata:
    - name: host
      value: "amqp://guest:guest@rabbitmq:5672"
    - name: durable
      value: "true"
```

---

## 15. Docker + Docker Compose для микросервисов

### Dockerfile для .NET микросервиса

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем csproj и восстанавливаем зависимости (кешируется)
COPY ["Services/Ordering/Ordering.API/Ordering.API.csproj", "Services/Ordering/Ordering.API/"]
COPY ["Services/Ordering/Ordering.Domain/Ordering.Domain.csproj", "Services/Ordering/Ordering.Domain/"]
COPY ["Services/Ordering/Ordering.Infrastructure/Ordering.Infrastructure.csproj", "Services/Ordering/Ordering.Infrastructure/"]
COPY ["BuildingBlocks/EventBus/EventBus.csproj", "BuildingBlocks/EventBus/"]
RUN dotnet restore "Services/Ordering/Ordering.API/Ordering.API.csproj"

# Копируем всё остальное и собираем
COPY . .
WORKDIR "/src/Services/Ordering/Ordering.API"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Создаём non-root пользователя
RUN adduser --disabled-password --gecos "" appuser
USER appuser

HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
  CMD curl -f http://localhost:80/health/live || exit 1

ENTRYPOINT ["dotnet", "Ordering.API.dll"]
```

### Docker Compose для всей системы

```yaml
# docker-compose.yml
version: '3.8'

services:
  # ─── API Gateway ───
  api-gateway:
    build:
      context: .
      dockerfile: ApiGateways/Web.Gateway/Dockerfile
    ports:
      - "5000:80"
    depends_on:
      catalog-service:
        condition: service_healthy
      order-service:
        condition: service_healthy
    environment:
      - ASPNETCORE_ENVIRONMENT=Development

  # ─── Catalog Service ───
  catalog-service:
    build:
      context: .
      dockerfile: Services/Catalog/Catalog.API/Dockerfile
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__CatalogDb=Host=catalog-db;Database=catalog;Username=postgres;Password=postgres
      - RabbitMQ__Host=rabbitmq
    depends_on:
      catalog-db:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:80/health/live"]
      interval: 10s
      timeout: 5s
      retries: 5

  catalog-db:
    image: postgres:16-alpine
    environment:
      - POSTGRES_DB=catalog
      - POSTGRES_USER=postgres
      - POSTGRES_PASSWORD=postgres
    volumes:
      - catalog-db-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 5s
      timeout: 5s
      retries: 5

  # ─── Order Service ───
  order-service:
    build:
      context: .
      dockerfile: Services/Ordering/Ordering.API/Dockerfile
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__OrderDb=Server=order-db;Database=orders;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True
      - RabbitMQ__Host=rabbitmq
    depends_on:
      order-db:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:80/health/live"]
      interval: 10s
      timeout: 5s
      retries: 5

  order-db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourStrong!Passw0rd
    volumes:
      - order-db-data:/var/opt/mssql
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P YourStrong!Passw0rd -C -Q 'SELECT 1' || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5

  # ─── Infrastructure ───
  rabbitmq:
    image: rabbitmq:3-management-alpine
    ports:
      - "5672:5672"
      - "15672:15672"
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "-q", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"

  # ─── Observability ───
  seq:
    image: datalust/seq
    ports:
      - "5341:5341"
      - "8081:80"
    environment:
      - ACCEPT_EULA=Y

  jaeger:
    image: jaegertracing/all-in-one:latest
    ports:
      - "16686:16686"   # UI
      - "4317:4317"     # OTLP gRPC
      - "4318:4318"     # OTLP HTTP

volumes:
  catalog-db-data:
  order-db-data:
```

```bash
# Команды для работы
docker compose up -d                    # Запуск всех сервисов
docker compose up -d --build            # Пересборка и запуск
docker compose logs -f order-service    # Логи конкретного сервиса
docker compose ps                       # Статус всех контейнеров
docker compose down -v                  # Остановка + удаление volumes
```

---

## 16. Вопросы на собеседовании с ответами

### Вопрос 1: Когда стоит переходить с монолита на микросервисы?

**Ответ:** Переход оправдан, когда: (1) команда выросла и разные группы начинают мешать друг другу при деплое; (2) отдельные части системы имеют принципиально разные требования к масштабированию; (3) домен хорошо изучен и границы между bounded contexts чёткие; (4) компания обладает достаточной DevOps-зрелостью (CI/CD, контейнеры, мониторинг). Не стоит переходить ради «хайпа» — premature decomposition хуже монолита. Рекомендую начинать с модульного монолита (Modular Monolith) с чёткими границами модулей, а затем выделять сервисы по мере необходимости (Strangler Fig Pattern).

---

### Вопрос 2: Как обеспечить согласованность данных между микросервисами?

**Ответ:** Принять eventual consistency как данность. Использовать: (1) **Saga Pattern** для координации распределённых бизнес-процессов с компенсирующими транзакциями; (2) **Outbox Pattern** для атомарной записи данных и публикации событий; (3) **Idempotent Consumers** для защиты от дублирования; (4) **Correlation ID** для трассировки. Two-Phase Commit (2PC) не рекомендуется из-за блокировок и слабой масштабируемости.

---

### Вопрос 3: Какая разница между Orchestration и Choreography Saga?

**Ответ:**
- **Orchestration:** централизованный координатор (оркестратор) управляет шагами саги, отправляя команды сервисам и получая ответы. Плюсы: явный поток, легче отлаживать, проще добавлять шаги. Минусы: оркестратор — единая точка отказа и coupling point.
- **Choreography:** каждый сервис подписывается на события и самостоятельно решает, что делать. Плюсы: полная decoupling. Минусы: сложно отследить весь поток, риск циклических зависимостей, труднее дебажить.

Для простых саг (2-3 шага) подходит choreography. Для сложных бизнес-процессов (5+ шагов, условные ветвления) лучше orchestration.

---

### Вопрос 4: Зачем нужен Outbox Pattern и как он работает?

**Ответ:** Outbox решает проблему dual write: нужно одновременно записать данные в БД и опубликовать событие, но они не в одной транзакции. Решение: в той же транзакции, где сохраняются данные, событие записывается в таблицу `OutboxMessages`. Отдельный фоновый процесс (или CDC — Change Data Capture) читает эту таблицу и публикует события в брокер. Это гарантирует at-least-once delivery. Потребитель должен быть идемпотентным.

---

### Вопрос 5: Чем RabbitMQ отличается от Kafka? Когда что использовать?

**Ответ:**
- **RabbitMQ** — традиционный message broker (smart broker, dumb consumer). Сообщение удаляется после acknowledge. Гибкий routing через exchanges. Хорош для task queues, RPC-сценариев, когда нужна гарантия обработки каждого сообщения ровно одним consumer.
- **Kafka** — распределённый event log (dumb broker, smart consumer). Сообщения хранятся заданное время, consumer управляет своим offset. Высочайший throughput (миллионы msg/sec). Хорош для event streaming, event sourcing, аналитики реального времени, когда нужен replay и множественные consumers.

Правило: если нужна очередь задач — RabbitMQ. Если нужен журнал событий — Kafka.

---

### Вопрос 6: Что такое Circuit Breaker и как его настроить в .NET?

**Ответ:** Circuit Breaker — паттерн, предотвращающий каскадные отказы. Три состояния: Closed (норма), Open (все вызовы отклоняются), Half-Open (пробные вызовы). При превышении порога ошибок цепь «размыкается», давая зависимому сервису время на восстановление. В .NET реализуется через Polly (часть Microsoft.Extensions.Http.Resilience). Ключевые параметры: `FailureRatio`, `SamplingDuration`, `MinimumThroughput`, `BreakDuration`. Важно настроить fallback — что возвращать клиенту, пока цепь разомкнута (кэшированные данные, дефолтное значение, 503).

---

### Вопрос 7: Как организовать distributed tracing в микросервисах?

**Ответ:** Использовать OpenTelemetry — стандарт для сбора traces, metrics и logs. Каждый запрос получает `TraceId`, а каждая операция внутри запроса — `SpanId`. Эти ID пробрасываются между сервисами через заголовки (W3C Trace Context). В .NET подключаем `OpenTelemetry.Extensions.Hosting`, добавляем инструментации (`AspNetCore`, `HttpClient`, `SqlClient`, `MassTransit`), и отправляем данные в collector (Jaeger, Zipkin, Azure Monitor). Это позволяет визуализировать полный путь запроса через все сервисы, находить узкие места и отлаживать проблемы.

---

### Вопрос 8: Что такое идемпотентность и почему она важна в микросервисах?

**Ответ:** Идемпотентность — свойство операции давать один и тот же результат при повторном вызове. В микросервисах сообщения могут дублироваться (at-least-once delivery, retry-политики, сетевые сбои). Если consumer не идемпотентен, повторное сообщение приведёт к дублированию данных (два платежа, два заказа). Реализация: хранить ID обработанных сообщений (в БД или Redis), проверять перед обработкой. На стороне API — принимать Idempotency-Key от клиента.

---

### Вопрос 9: Как версионировать API в микросервисах?

**Ответ:** Основные подходы: (1) **URL-сегмент** `/api/v1/orders` — самый прозрачный; (2) **Query parameter** `?api-version=1.0`; (3) **HTTP-заголовок** `X-Api-Version: 1.0`; (4) **Content negotiation** (Accept header). В .NET используется пакет `Asp.Versioning.Http`. Стратегия: делать backward-compatible изменения (добавление полей) без смены версии. Новую версию — только при breaking changes. Устаревшую версию помечать deprecated и давать потребителям время (deprecation policy).

---

### Вопрос 10: Что такое Bulkhead Pattern и зачем он нужен?

**Ответ:** Bulkhead (переборка) — паттерн изоляции ресурсов, аналогия с переборками на корабле. Если один отсек затоплен, остальные не пострадают. В микросервисах: если один внешний сервис тормозит, он не должен исчерпать все потоки/соединения приложения. Реализация: выделение отдельных пулов потоков или ограничение параллелизма (concurrency limiter) для каждого внешнего вызова. В Polly используется `AddConcurrencyLimiter`. Также можно изолировать HttpClient instances через отдельные пулы соединений.

---

### Вопрос 11: Как реализовать Database per Service и решить проблему запросов, охватывающих несколько сервисов?

**Ответ:** Каждый сервис владеет своей БД, доступ только через API сервиса. Проблема «JOIN между сервисами» решается: (1) **API Composition** — агрегатор запрашивает данные из нескольких сервисов и объединяет (хорош для UI); (2) **CQRS** — отдельная read-модель, денормализованная, обновляемая через подписку на события; (3) **Materialized View** — отдельный сервис строит проекцию из событий нескольких доменов. Polyglot persistence позволяет каждому сервису использовать оптимальную БД (SQL, NoSQL, Graph, Search).

---

### Вопрос 12: Объясните разницу между MassTransit, Rebus, Wolverine и Dapr.

**Ответ:**
- **MassTransit** — зрелый, полнофункциональный фреймворк. Поддержка Saga (State Machine), Outbox, несколько транспортов (RabbitMQ, Kafka, Azure SB, SQS). Большое community. Подходит для большинства проектов.
- **Rebus** — легковесный, простой API. Хорош для небольших проектов, быстрый старт.
- **Wolverine** — современный, convention-based, минимальный boilerplate. Встроенный Outbox, интеграция с Marten (Event Sourcing + PostgreSQL). Набирает популярность.
- **Dapr** — sidecar-подход, абстрагирует инфраструктуру. Не только messaging, но и state management, service invocation, secrets. Удобен в Kubernetes-среде.

Для enterprise-проектов чаще всего выбирают MassTransit. Для новых проектов стоит рассмотреть Wolverine. Dapr — если нужна cloud-agnostic абстракция и уже используется Kubernetes.

---

### Вопрос 13: Как правильно разбить монолит на микросервисы?

**Ответ:** Поэтапный подход: (1) Провести Event Storming или Domain Storytelling для выявления bounded contexts; (2) Внутри монолита выделить модули с чёткими границами (Modular Monolith); (3) Применить **Strangler Fig Pattern** — новая функциональность пишется как микросервис, старая постепенно переносится; (4) Начинать с наименее связанного домена; (5) Вводить Anti-Corruption Layer между монолитом и новыми сервисами; (6) Выносить общие данные через события (event-driven), а не через общую БД.

---

### Вопрос 14: Какие минусы у микросервисов часто забывают упомянуть?

**Ответ:** (1) **Operational complexity** — нужны CI/CD, мониторинг, alerting для каждого сервиса; (2) **Debugging hell** — отладка запроса, проходящего через 5 сервисов; (3) **Data consistency** — eventual consistency непривычна и сложна в реализации; (4) **Integration testing** — тестирование взаимодействия сервисов значительно сложнее; (5) **Latency** — сетевые вызовы вместо in-process; (6) **Организационная зрелость** — нужны DevOps-практики, platform team, observability stack; (7) **Distributed monolith** — антипаттерн, когда сервисы сильно связаны и деплоятся только вместе.
