# System Design & Scalability — подготовка к Senior .NET собеседованию

## Содержание

1. [Основы System Design](#1-основы-system-design)
2. [Масштабирование: вертикальное vs горизонтальное](#2-масштабирование-вертикальное-vs-горизонтальное)
3. [Load Balancing](#3-load-balancing)
4. [Caching](#4-caching)
5. [CDN (Content Delivery Network)](#5-cdn-content-delivery-network)
6. [Database Scaling: Replication & Sharding](#6-database-scaling-replication--sharding)
7. [CAP-теорема и PACELC](#7-cap-теорема-и-pacelc)
8. [Consistent Hashing](#8-consistent-hashing)
9. [Rate Limiting & Throttling](#9-rate-limiting--throttling)
10. [Message Queues и Event-Driven Architecture](#10-message-queues-и-event-driven-architecture)
11. [Паттерны проектирования распределённых систем](#11-паттерны-проектирования-распределённых-систем)
12. [Back-of-the-Envelope Estimation](#12-back-of-the-envelope-estimation)
13. [Проектирование URL Shortener](#13-проектирование-url-shortener)
14. [Проектирование Notification System](#14-проектирование-notification-system)
15. [Проектирование Chat/Messaging System](#15-проектирование-chatmessaging-system)
16. [Вопросы на собеседовании с ответами](#16-вопросы-на-собеседовании-с-ответами)

---

## 1. Основы System Design

### Зачем нужен System Design на собеседовании

На позиции Senior и выше вас будут оценивать не только по знанию языка, но и по способности проектировать системы, которые:
- Обрабатывают миллионы запросов
- Остаются доступными при сбоях
- Масштабируются по мере роста нагрузки
- Поддерживаются и развиваются командой

### Фреймворк для ответа на System Design вопросы

1. **Clarify Requirements (2-3 мин)** — уточните функциональные и нефункциональные требования
2. **Back-of-the-Envelope Estimation (3-5 мин)** — оцените нагрузку, хранилище, пропускную способность
3. **High-Level Design (5-10 мин)** — нарисуйте основные компоненты
4. **Detailed Design (10-15 мин)** — углубитесь в критические части
5. **Bottlenecks & Trade-offs (5 мин)** — обсудите узкие места и компромиссы

### Ключевые нефункциональные требования

| Требование | Описание | Метрика |
|---|---|---|
| **Availability** | Доступность системы | 99.9% = 8.76 ч даунтайма/год |
| **Latency** | Задержка ответа | p50, p95, p99 |
| **Throughput** | Пропускная способность | RPS (requests per second) |
| **Durability** | Сохранность данных | Данные не теряются |
| **Consistency** | Согласованность | Strong vs Eventual |
| **Scalability** | Масштабируемость | Линейный рост при добавлении ресурсов |

---

## 2. Масштабирование: вертикальное vs горизонтальное

### Vertical Scaling (Scale Up)

Увеличение ресурсов одного сервера (CPU, RAM, SSD).

**Плюсы:**
- Простота — не нужно менять архитектуру
- Нет проблем с распределённой координацией
- Подходит для баз данных с сильной консистентностью

**Минусы:**
- Есть потолок — нельзя бесконечно увеличивать один сервер
- Single point of failure
- Дорого при высоких конфигурациях

### Horizontal Scaling (Scale Out)

Добавление дополнительных серверов.

**Плюсы:**
- Нет потолка в теории
- Лучшая отказоустойчивость
- Cost-effective на больших масштабах

**Минусы:**
- Сложность: распределённые транзакции, синхронизация
- Требует stateless дизайна или распределённого состояния
- Сложнее дебажить

### Stateless vs Stateful сервисы

```csharp
// Stateless API — легко масштабировать горизонтально
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderRepository _repo;
    private readonly IDistributedCache _cache;

    public OrdersController(IOrderRepository repo, IDistributedCache cache)
    {
        _repo = repo;
        _cache = cache;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrder(Guid id)
    {
        // Состояние хранится в Redis/БД, не в памяти процесса
        var cacheKey = $"order:{id}";
        var cached = await _cache.GetStringAsync(cacheKey);
        if (cached != null)
            return Ok(JsonSerializer.Deserialize<OrderDto>(cached));

        var order = await _repo.GetByIdAsync(id);
        if (order == null) return NotFound();

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(order),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });

        return Ok(order);
    }
}
```

> **Ключевой принцип:** Для горизонтального масштабирования сервисы должны быть stateless — всё состояние вынесено в БД, кэш (Redis) или очереди.

---

## 3. Load Balancing

### Алгоритмы балансировки

| Алгоритм | Описание | Когда использовать |
|---|---|---|
| **Round Robin** | По очереди | Одинаковые серверы, равномерная нагрузка |
| **Weighted Round Robin** | По очереди с весами | Серверы разной мощности |
| **Least Connections** | На наименее загруженный | Запросы с разной длительностью |
| **IP Hash** | По хешу IP клиента | Sticky sessions |
| **Consistent Hashing** | По хешу ключа | Кэширование, шардинг |

### Layer 4 vs Layer 7

- **L4 (Transport)**: Балансирует по IP/порту. Быстро, но не видит содержимое запроса.
- **L7 (Application)**: Видит HTTP-заголовки, URL, cookies. Может маршрутизировать по содержимому.

### Пример: YARP (Yet Another Reverse Proxy) в .NET

```csharp
// Program.cs — настройка YARP reverse proxy
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();
app.MapReverseProxy();
app.Run();
```

```json
// appsettings.json
{
  "ReverseProxy": {
    "Routes": {
      "orders-route": {
        "ClusterId": "orders-cluster",
        "Match": { "Path": "/api/orders/{**catch-all}" }
      }
    },
    "Clusters": {
      "orders-cluster": {
        "LoadBalancingPolicy": "RoundRobin",
        "HealthCheck": {
          "Active": {
            "Enabled": true,
            "Interval": "00:00:10",
            "Path": "/health"
          }
        },
        "Destinations": {
          "instance1": { "Address": "https://orders-1:5001" },
          "instance2": { "Address": "https://orders-2:5002" },
          "instance3": { "Address": "https://orders-3:5003" }
        }
      }
    }
  }
}
```

### Health Checks

```csharp
// Активная проверка здоровья бекенд-серверов
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "database")
    .AddRedis(redisConnection, name: "redis")
    .AddUrlGroup(new Uri("https://external-api.com/health"), name: "external-api");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

---

## 4. Caching

### Стратегии кэширования

#### Cache-Aside (Lazy Loading)

```csharp
public class CacheAsideProductService : IProductService
{
    private readonly IDistributedCache _cache;
    private readonly IProductRepository _repo;

    public async Task<Product?> GetProductAsync(int id)
    {
        var cacheKey = $"product:{id}";

        // 1. Проверяем кэш
        var cached = await _cache.GetStringAsync(cacheKey);
        if (cached != null)
            return JsonSerializer.Deserialize<Product>(cached);

        // 2. Cache miss — идём в БД
        var product = await _repo.GetByIdAsync(id);
        if (product == null) return null;

        // 3. Записываем в кэш
        await _cache.SetStringAsync(cacheKey,
            JsonSerializer.Serialize(product),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

        return product;
    }

    public async Task UpdateProductAsync(Product product)
    {
        await _repo.UpdateAsync(product);
        // Инвалидируем кэш
        await _cache.RemoveAsync($"product:{product.Id}");
    }
}
```

#### Write-Through

```csharp
public class WriteThroughProductService : IProductService
{
    public async Task UpdateProductAsync(Product product)
    {
        // Записываем в БД и кэш одновременно
        await _repo.UpdateAsync(product);
        await _cache.SetStringAsync($"product:{product.Id}",
            JsonSerializer.Serialize(product),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            });
    }
}
```

#### Write-Behind (Write-Back)

```csharp
// Записываем сначала в кэш, потом асинхронно в БД
public class WriteBehindService
{
    private readonly Channel<WriteOperation> _channel =
        Channel.CreateBounded<WriteOperation>(1000);

    public async Task WriteAsync(string key, string value)
    {
        await _cache.SetStringAsync(key, value);
        await _channel.Writer.WriteAsync(new WriteOperation(key, value));
    }

    // Фоновый воркер пишет в БД батчами
    public async Task ProcessWritesAsync(CancellationToken ct)
    {
        var batch = new List<WriteOperation>();
        await foreach (var op in _channel.Reader.ReadAllAsync(ct))
        {
            batch.Add(op);
            if (batch.Count >= 100 || !_channel.Reader.TryPeek(out _))
            {
                await _repo.BulkWriteAsync(batch);
                batch.Clear();
            }
        }
    }
}
```

### Cache Eviction Policies

| Политика | Описание |
|---|---|
| **LRU** (Least Recently Used) | Вытесняет давно не использованные |
| **LFU** (Least Frequently Used) | Вытесняет редко используемые |
| **TTL** (Time To Live) | Удаляет по истечении времени |
| **FIFO** | Вытесняет самые старые |

### Cache Stampede Problem

Когда много запросов одновременно обнаруживают cache miss и идут в БД:

```csharp
// Решение: используем SemaphoreSlim для предотвращения stampede
public class StampedeProtectedCache
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory)
    {
        var cached = await _cache.GetStringAsync(key);
        if (cached != null)
            return JsonSerializer.Deserialize<T>(cached);

        var lockObj = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await lockObj.WaitAsync();
        try
        {
            // Double-check после получения блокировки
            cached = await _cache.GetStringAsync(key);
            if (cached != null)
                return JsonSerializer.Deserialize<T>(cached);

            var result = await factory();
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(result),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                });
            return result;
        }
        finally
        {
            lockObj.Release();
        }
    }
}
```

---

## 5. CDN (Content Delivery Network)

### Принципы работы

CDN — географически распределённая сеть серверов, кэширующих контент ближе к пользователям.

**Типы контента для CDN:**
- Статические файлы: JS, CSS, изображения, видео
- API-ответы (при правильных cache headers)
- HTML-страницы (для SSR/SSG)

### Push vs Pull CDN

| Тип | Описание | Когда использовать |
|---|---|---|
| **Pull** | CDN запрашивает у origin при первом запросе | Высокий трафик, контент часто меняется |
| **Push** | Вы загружаете файлы в CDN | Предсказуемый контент, нечастые обновления |

### Настройка кэш-заголовков в ASP.NET

```csharp
// Настройка Response Caching middleware
builder.Services.AddResponseCaching();

app.UseResponseCaching();

// На контроллере
[HttpGet("{id}")]
[ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "version" })]
public async Task<IActionResult> GetProduct(int id) { ... }

// Для статических файлов
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=31536000,immutable");
    }
});
```

---

## 6. Database Scaling: Replication & Sharding

### Read Replicas

```
         ┌─────────────┐
Writes → │   Primary   │
         └──────┬──────┘
                │ Replication
        ┌───────┼───────┐
        ▼       ▼       ▼
    ┌───────┐┌───────┐┌───────┐
    │Replica││Replica││Replica│ ← Reads
    └───────┘└───────┘└───────┘
```

```csharp
// EF Core: Read/Write splitting через разные контексты
public class WriteDbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer("Server=primary-db;...");
    }
}

public class ReadDbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer("Server=read-replica;...")
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }
}

// Регистрация
builder.Services.AddDbContext<WriteDbContext>();
builder.Services.AddDbContext<ReadDbContext>();

// Использование в сервисе
public class ProductService
{
    private readonly WriteDbContext _writeDb;
    private readonly ReadDbContext _readDb;

    public async Task<Product?> GetAsync(int id) =>
        await _readDb.Products.FindAsync(id);

    public async Task CreateAsync(Product product)
    {
        _writeDb.Products.Add(product);
        await _writeDb.SaveChangesAsync();
    }
}
```

### Sharding (Горизонтальное партиционирование)

Разделение данных между несколькими базами по ключу шардирования.

```csharp
// Простая стратегия шардирования по userId
public class ShardRouter
{
    private readonly string[] _connectionStrings;

    public ShardRouter(string[] connectionStrings)
    {
        _connectionStrings = connectionStrings;
    }

    public string GetConnectionString(Guid userId)
    {
        var shardIndex = Math.Abs(userId.GetHashCode()) % _connectionStrings.Length;
        return _connectionStrings[shardIndex];
    }
}

// Использование
public class ShardedUserRepository
{
    private readonly ShardRouter _router;

    public async Task<User?> GetUserAsync(Guid userId)
    {
        var connString = _router.GetConnectionString(userId);
        await using var connection = new SqlConnection(connString);
        return await connection.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE Id = @Id", new { Id = userId });
    }
}
```

### Выбор ключа шардирования

| Критерий | Хороший ключ | Плохой ключ |
|---|---|---|
| Кардинальность | Высокая (userId, orderId) | Низкая (status, country) |
| Распределение | Равномерное | Скошенное (hot spots) |
| Запросы | Большинство запросов содержат ключ | Частые cross-shard запросы |

---

## 7. CAP-теорема и PACELC

### CAP-теорема

В распределённой системе при сетевом разделении (Partition) можно гарантировать только одно из двух: **Consistency** или **Availability**.

- **CP-системы**: MongoDB (в режиме strong consistency), HBase, ZooKeeper — при partition отклоняют запросы, чтобы сохранить консистентность
- **AP-системы**: Cassandra, DynamoDB, CouchDB — остаются доступными, но данные могут быть eventually consistent

### PACELC

Расширение CAP: если **P**artition → **A**vailability vs **C**onsistency; **E**lse → **L**atency vs **C**onsistency.

| Система | Partition (PA/PC) | Else (EL/EC) |
|---|---|---|
| Cassandra | PA | EL |
| MongoDB | PC | EC |
| DynamoDB | PA | EL |
| PostgreSQL | PC | EC |

### Eventual Consistency в .NET

```csharp
// Пример: событийная модель eventual consistency
public class OrderPlacedEventHandler : INotificationHandler<OrderPlacedEvent>
{
    private readonly IInventoryService _inventory;

    public async Task Handle(OrderPlacedEvent notification, CancellationToken ct)
    {
        // Инвентарь обновится с задержкой — eventual consistency
        await _inventory.ReserveItemsAsync(notification.OrderId, notification.Items, ct);
    }
}

// Компенсирующее действие при ошибке
public class OrderFailedEventHandler : INotificationHandler<OrderFailedEvent>
{
    private readonly IInventoryService _inventory;

    public async Task Handle(OrderFailedEvent notification, CancellationToken ct)
    {
        // Откат резервирования — компенсирующая транзакция
        await _inventory.ReleaseItemsAsync(notification.OrderId, ct);
    }
}
```

---

## 8. Consistent Hashing

### Проблема обычного хеширования

При добавлении/удалении сервера `hash(key) % N` перераспределяет почти все ключи.

### Как работает Consistent Hashing

```
           Node A
          /      \
    Node D        Node B
          \      /
           Node C

Кольцо: 0 ─────────────── 2^32
        |  A  |  B  |  C  |  D  |
```

При добавлении/удалении узла перемещается только ~`1/N` ключей.

### Реализация на C#

```csharp
public class ConsistentHashRing<T>
{
    private readonly SortedDictionary<int, T> _ring = new();
    private readonly int _virtualNodes;

    public ConsistentHashRing(int virtualNodes = 150)
    {
        _virtualNodes = virtualNodes;
    }

    public void AddNode(T node)
    {
        for (var i = 0; i < _virtualNodes; i++)
        {
            var hash = GetHash($"{node}-vn{i}");
            _ring[hash] = node;
        }
    }

    public void RemoveNode(T node)
    {
        for (var i = 0; i < _virtualNodes; i++)
        {
            var hash = GetHash($"{node}-vn{i}");
            _ring.Remove(hash);
        }
    }

    public T GetNode(string key)
    {
        if (_ring.Count == 0)
            throw new InvalidOperationException("Hash ring is empty");

        var hash = GetHash(key);

        // Находим первый узел по часовой стрелке
        foreach (var pair in _ring)
        {
            if (pair.Key >= hash)
                return pair.Value;
        }

        // Если дошли до конца — возвращаем первый (кольцо замкнуто)
        return _ring.First().Value;
    }

    private static int GetHash(string key)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var bytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));
        return BitConverter.ToInt32(bytes, 0) & 0x7FFFFFFF;
    }
}

// Использование
var ring = new ConsistentHashRing<string>();
ring.AddNode("cache-server-1");
ring.AddNode("cache-server-2");
ring.AddNode("cache-server-3");

var server = ring.GetNode("user:12345"); // Всегда один и тот же сервер
```

---

## 9. Rate Limiting & Throttling

### Алгоритмы Rate Limiting

#### Token Bucket

```csharp
public class TokenBucket
{
    private double _tokens;
    private readonly double _maxTokens;
    private readonly double _refillRate; // токенов в секунду
    private DateTime _lastRefill;
    private readonly object _lock = new();

    public TokenBucket(double maxTokens, double refillRate)
    {
        _maxTokens = maxTokens;
        _tokens = maxTokens;
        _refillRate = refillRate;
        _lastRefill = DateTime.UtcNow;
    }

    public bool TryConsume(int tokens = 1)
    {
        lock (_lock)
        {
            Refill();
            if (_tokens >= tokens)
            {
                _tokens -= tokens;
                return true;
            }
            return false;
        }
    }

    private void Refill()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastRefill).TotalSeconds;
        _tokens = Math.Min(_maxTokens, _tokens + elapsed * _refillRate);
        _lastRefill = now;
    }
}
```

#### Sliding Window Counter (Redis)

```csharp
public class RedisSlidingWindowRateLimiter
{
    private readonly IConnectionMultiplexer _redis;

    public async Task<bool> IsAllowedAsync(string clientId, int limit, TimeSpan window)
    {
        var db = _redis.GetDatabase();
        var key = $"rate_limit:{clientId}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowStart = now - (long)window.TotalMilliseconds;

        var transaction = db.CreateTransaction();

        // Удаляем старые записи
        _ = transaction.SortedSetRemoveRangeByScoreAsync(key, 0, windowStart);
        // Добавляем текущий запрос
        _ = transaction.SortedSetAddAsync(key, now.ToString(), now);
        // Считаем запросы в окне
        var countTask = transaction.SortedSetLengthAsync(key);
        // Устанавливаем TTL
        _ = transaction.KeyExpireAsync(key, window);

        await transaction.ExecuteAsync();

        return await countTask <= limit;
    }
}
```

### Rate Limiting в ASP.NET Core 7+

```csharp
// Встроенный Rate Limiting middleware
builder.Services.AddRateLimiter(options =>
{
    // Fixed Window
    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 10;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Sliding Window
    options.AddSlidingWindowLimiter("sliding", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.SegmentsPerWindow = 6; // 10-сек сегменты
    });

    // Token Bucket
    options.AddTokenBucketLimiter("token", opt =>
    {
        opt.TokenLimit = 100;
        opt.ReplenishmentPeriod = TimeSpan.FromSeconds(10);
        opt.TokensPerPeriod = 20;
    });

    // Concurrency Limiter
    options.AddConcurrencyLimiter("concurrency", opt =>
    {
        opt.PermitLimit = 50;
        opt.QueueLimit = 25;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

app.UseRateLimiter();

// Применение к контроллеру
[EnableRateLimiting("sliding")]
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase { }
```

---

## 10. Message Queues и Event-Driven Architecture

### Когда использовать очереди

- **Декаплинг** — отправитель не знает о получателях
- **Буферизация** — сглаживание пиков нагрузки
- **Надёжность** — сообщение не потеряется при падении consumer
- **Масштабирование** — несколько consumer обрабатывают параллельно

### RabbitMQ vs Kafka

| Характеристика | RabbitMQ | Kafka |
|---|---|---|
| Модель | Message broker (push) | Event log (pull) |
| Гарантия доставки | At-least-once, at-most-once | At-least-once, exactly-once |
| Порядок | В рамках очереди | В рамках партиции |
| Хранение | Удаляет после ack | Хранит по retention policy |
| Throughput | Тысячи msg/sec | Миллионы msg/sec |
| Replay | Нет | Да (consumer offset) |
| Применение | Задачи, RPC, routing | Event streaming, event sourcing |

### MassTransit — абстракция над брокерами в .NET

```csharp
// Определяем событие
public record OrderPlaced(Guid OrderId, string CustomerId, decimal Total);

// Consumer
public class OrderPlacedConsumer : IConsumer<OrderPlaced>
{
    private readonly ILogger<OrderPlacedConsumer> _logger;

    public OrderPlacedConsumer(ILogger<OrderPlacedConsumer> logger) => _logger = logger;

    public async Task Consume(ConsumeContext<OrderPlaced> context)
    {
        _logger.LogInformation("Processing order {OrderId}", context.Message.OrderId);
        // Бизнес-логика...
    }
}

// Регистрация
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderPlacedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq://localhost");
        cfg.ConfigureEndpoints(context);
    });
});
```

---

## 11. Паттерны проектирования распределённых систем

### Circuit Breaker

Предотвращает каскадные сбои, прекращая вызовы к недоступному сервису.

```csharp
// Polly v8
builder.Services.AddHttpClient("PaymentService")
    .AddResilienceHandler("payment-pipeline", builder =>
    {
        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 10,
            BreakDuration = TimeSpan.FromSeconds(15),
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .HandleResult(r => !r.IsSuccessStatusCode)
        });

        builder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromMilliseconds(500)
        });

        builder.AddTimeout(TimeSpan.FromSeconds(5));
    });
```

### Saga Pattern

Координирует распределённые транзакции через цепочку локальных транзакций с компенсациями.

```
CreateOrder → ReserveInventory → ProcessPayment → ShipOrder
     ↓               ↓                 ↓
CancelOrder ← ReleaseInventory ← RefundPayment  (компенсации)
```

```csharp
// MassTransit Saga State Machine
public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public State Submitted { get; private set; } = null!;
    public State InventoryReserved { get; private set; } = null!;
    public State PaymentProcessed { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Faulted { get; private set; } = null!;

    public Event<OrderSubmitted> OrderSubmitted { get; private set; } = null!;
    public Event<InventoryReserved> InventoryReserved { get; private set; } = null!;
    public Event<PaymentProcessed> PaymentProcessed { get; private set; } = null!;
    public Event<PaymentFailed> PaymentFailed { get; private set; } = null!;

    public OrderStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderSubmitted, x => x.CorrelateById(c => c.Message.OrderId));
        Event(() => InventoryReserved, x => x.CorrelateById(c => c.Message.OrderId));
        Event(() => PaymentProcessed, x => x.CorrelateById(c => c.Message.OrderId));
        Event(() => PaymentFailed, x => x.CorrelateById(c => c.Message.OrderId));

        Initially(
            When(OrderSubmitted)
                .Then(ctx => ctx.Saga.CustomerId = ctx.Message.CustomerId)
                .Publish(ctx => new ReserveInventory(ctx.Saga.CorrelationId))
                .TransitionTo(Submitted));

        During(Submitted,
            When(InventoryReserved)
                .Publish(ctx => new ProcessPayment(ctx.Saga.CorrelationId, ctx.Saga.Total))
                .TransitionTo(InventoryReserved));

        During(InventoryReserved,
            When(PaymentProcessed)
                .Publish(ctx => new ShipOrder(ctx.Saga.CorrelationId))
                .TransitionTo(Completed)
                .Finalize(),
            When(PaymentFailed)
                // Компенсация: освобождаем инвентарь
                .Publish(ctx => new ReleaseInventory(ctx.Saga.CorrelationId))
                .TransitionTo(Faulted));
    }
}
```

### Bulkhead Pattern

Изолирует ресурсы, чтобы сбой одного компонента не исчерпал ресурсы для всех.

```csharp
// Polly Bulkhead
builder.Services.AddResiliencePipeline("bulkhead", builder =>
{
    builder.AddConcurrencyLimiter(new ConcurrencyLimiterOptions
    {
        PermitLimit = 25,
        QueueLimit = 50
    });
});
```

### Outbox Pattern

Гарантирует атомарность записи в БД и отправки события.

```csharp
// Сохраняем сообщение в outbox-таблицу в той же транзакции
public class OutboxRepository
{
    private readonly DbContext _context;

    public async Task SaveWithOutboxAsync(Order order, OrderPlacedEvent @event)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        _context.Orders.Add(order);

        _context.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = nameof(OrderPlacedEvent),
            Payload = JsonSerializer.Serialize(@event),
            CreatedAt = DateTime.UtcNow,
            Processed = false
        });

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}

// Фоновый сервис публикует из outbox
public class OutboxProcessor : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var messages = await _repo.GetUnprocessedAsync(batchSize: 50);
            foreach (var msg in messages)
            {
                await _bus.Publish(msg.Deserialize());
                msg.Processed = true;
            }
            await _repo.SaveChangesAsync();
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
    }
}
```

---

## 12. Back-of-the-Envelope Estimation

### Ключевые числа для запоминания

| Операция | Время |
|---|---|
| L1 cache reference | 0.5 ns |
| L2 cache reference | 7 ns |
| RAM reference | 100 ns |
| SSD random read | 150 µs |
| HDD seek | 10 ms |
| Network round trip (same datacenter) | 0.5 ms |
| Network round trip (cross-continent) | 150 ms |

### Единицы хранения и пропускной способности

| Единица | Значение |
|---|---|
| 1 KB | 1,000 bytes |
| 1 MB | 1,000 KB |
| 1 GB | 1,000 MB |
| 1 TB | 1,000 GB |
| QPS для web-сервера | ~1,000-10,000 |
| QPS для БД | ~1,000-5,000 |
| QPS для кэша (Redis) | ~100,000+ |

### Пример расчёта

**Задача:** Спроектировать систему для 10 млн DAU, где каждый пользователь делает ~20 запросов/день.

```
Запросы/день:  10M × 20 = 200M
Запросы/сек:   200M / 86400 ≈ 2,300 QPS
Пиковый QPS:   2,300 × 3 ≈ 7,000 QPS (правило x3 для пиков)

Если каждый запрос = 500 bytes:
Трафик/день:   200M × 500B = 100 GB/день
Трафик/сек:    100 GB / 86400 ≈ 1.2 MB/s

Хранение за год: 100 GB × 365 = 36.5 TB
```

---

## 13. Проектирование URL Shortener

### Requirements

**Функциональные:**
- Генерация короткого URL для длинного
- Редирект по короткому URL
- Кастомные алиасы (опционально)
- TTL для ссылок

**Нефункциональные:**
- 100M URL/день создание, 10:1 read/write ratio → 1B reads/день
- Низкая латентность: < 50ms для редиректа
- Высокая доступность

### High-Level Design

```
Client → Load Balancer → API Servers → Cache (Redis)
                              ↓               ↓
                         DB (Write)     DB (Read Replicas)
```

### Генерация коротких ID

```csharp
public class UrlShortenerService
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private readonly IDistributedIdGenerator _idGenerator;
    private readonly IUrlRepository _repo;
    private readonly IDistributedCache _cache;

    // Подход 1: Base62 от auto-increment ID (Snowflake)
    public async Task<string> ShortenAsync(string longUrl)
    {
        var id = await _idGenerator.NextIdAsync(); // Snowflake ID
        var shortCode = ToBase62(id);

        await _repo.SaveAsync(new UrlMapping
        {
            ShortCode = shortCode,
            LongUrl = longUrl,
            CreatedAt = DateTime.UtcNow
        });

        await _cache.SetStringAsync($"url:{shortCode}", longUrl,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) });

        return shortCode;
    }

    public async Task<string?> ResolveAsync(string shortCode)
    {
        // Сначала кэш
        var cached = await _cache.GetStringAsync($"url:{shortCode}");
        if (cached != null) return cached;

        // Потом БД
        var mapping = await _repo.GetByShortCodeAsync(shortCode);
        if (mapping == null) return null;

        await _cache.SetStringAsync($"url:{shortCode}", mapping.LongUrl,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) });

        return mapping.LongUrl;
    }

    private static string ToBase62(long number)
    {
        if (number == 0) return "0";
        var result = new StringBuilder();
        while (number > 0)
        {
            result.Insert(0, Alphabet[(int)(number % 62)]);
            number /= 62;
        }
        return result.ToString();
    }
}
```

### Масштабирование

- **Шардинг** по первому символу short code или consistent hashing
- **Read replicas** для чтения (read-heavy система)
- **Redis кластер** для кэширования горячих URL
- **Rate limiting** чтобы защитить от abuse

---

## 14. Проектирование Notification System

### Requirements

- Push-уведомления (mobile), SMS, Email
- 10M пользователей, 100M уведомлений/день
- Приоритеты: urgent, normal, low
- Шаблоны уведомлений
- Настройки пользователя (opt-in/opt-out по каналам)

### Архитектура

```
API → Notification Service → Priority Queue → Workers → Channels
              ↓                                            ├── Email (SendGrid)
        User Preferences DB                                ├── SMS (Twilio)
        Template Engine                                    ├── Push (FCM/APNs)
        Rate Limiter                                       └── In-App (WebSocket)
```

```csharp
// Модель уведомления
public class NotificationRequest
{
    public Guid UserId { get; init; }
    public string TemplateId { get; init; } = string.Empty;
    public Dictionary<string, string> Parameters { get; init; } = new();
    public NotificationPriority Priority { get; init; }
    public NotificationChannel[] Channels { get; init; } = Array.Empty<NotificationChannel>();
}

// Диспетчер
public class NotificationDispatcher : IConsumer<NotificationRequest>
{
    private readonly IUserPreferencesService _preferences;
    private readonly ITemplateEngine _templates;
    private readonly IEnumerable<INotificationChannel> _channels;

    public async Task Consume(ConsumeContext<NotificationRequest> context)
    {
        var request = context.Message;

        // Проверяем настройки пользователя
        var prefs = await _preferences.GetAsync(request.UserId);

        // Рендерим шаблон
        var content = await _templates.RenderAsync(request.TemplateId, request.Parameters);

        // Отправляем по разрешённым каналам
        foreach (var channel in _channels)
        {
            if (request.Channels.Contains(channel.Type) && prefs.IsEnabled(channel.Type))
            {
                await channel.SendAsync(request.UserId, content);
            }
        }
    }
}
```

---

## 15. Проектирование Chat/Messaging System

### Requirements

- 1-to-1 и групповые чаты
- Онлайн-статус
- Доставка сообщений: sent, delivered, read
- Хранение истории
- 50M DAU

### Ключевые решения

**Протокол:** WebSocket для real-time, HTTP fallback для ненадёжных сетей.

**Хранение сообщений:** Write-heavy → Cassandra или ScyllaDB.

```csharp
// WebSocket Hub в SignalR
public class ChatHub : Hub
{
    private readonly IMessageRepository _messages;
    private readonly IPresenceService _presence;

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier!;
        await _presence.SetOnlineAsync(userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier!;
        await _presence.SetOfflineAsync(userId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(string chatId, string content)
    {
        var userId = Context.UserIdentifier!;
        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatId = chatId,
            SenderId = userId,
            Content = content,
            SentAt = DateTime.UtcNow,
            Status = MessageStatus.Sent
        };

        await _messages.SaveAsync(message);

        // Отправляем всем участникам чата
        await Clients.Group(chatId).SendAsync("ReceiveMessage", message);
    }

    public async Task MarkAsRead(string chatId, Guid messageId)
    {
        var userId = Context.UserIdentifier!;
        await _messages.MarkAsReadAsync(messageId, userId);
        await Clients.Group(chatId).SendAsync("MessageRead", messageId, userId);
    }

    public async Task JoinChat(string chatId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, chatId);
    }
}
```

### Масштабирование WebSocket

```
Client → Load Balancer (L4, sticky sessions)
              ↓
    ┌─────────┼─────────┐
    ▼         ▼         ▼
  Server1  Server2  Server3
    └─────────┼─────────┘
              ↓
         Redis Backplane (для SignalR)
```

```csharp
// SignalR с Redis Backplane для масштабирования
builder.Services.AddSignalR()
    .AddStackExchangeRedis("redis-connection-string", options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("ChatApp");
    });
```

---

## 16. Вопросы на собеседовании с ответами

### Q1: Как бы вы масштабировали систему, которая обрабатывает 1M RPS?

**Ответ:**
1. **Горизонтальное масштабирование** API-серверов за load balancer
2. **CDN** для статики
3. **Многоуровневый кэш** (L1 in-memory → L2 Redis кластер)
4. **Database sharding** для распределения нагрузки
5. **Read replicas** для read-heavy запросов
6. **Message queues** для async-обработки тяжёлых операций
7. **Rate limiting** для защиты
8. **Connection pooling** для БД

### Q2: Объясните разницу между strong consistency и eventual consistency. Когда что выбрать?

**Ответ:**
- **Strong consistency**: каждое чтение возвращает последнюю запись. Нужна для финансовых транзакций, инвентаря с ограниченным запасом. Цена — повышенная латентность и сниженная доступность.
- **Eventual consistency**: данные синхронизируются через время. Подходит для лайки, счётчики просмотров, рекомендации. Выигрыш — высокая доступность и низкая латентность.

Выбор зависит от бизнес-требований. В одной системе могут сосуществовать оба подхода для разных доменов.

### Q3: Как предотвратить каскадные сбои в микросервисной архитектуре?

**Ответ:**
- **Circuit Breaker** — прекращаем вызовы к недоступному сервису
- **Bulkhead** — изолируем ресурсы (пулы потоков, подключений)
- **Timeout** — ограничиваем время ожидания ответа
- **Retry с exponential backoff** — повторяем с нарастающей задержкой
- **Fallback** — возвращаем кэшированные/дефолтные данные
- **Rate limiting** — защищаем от перегрузки
- **Health checks** — быстро выводим нездоровые инстансы из ротации

### Q4: Спроектируйте систему аналитики в реальном времени (подсчёт уникальных посетителей)

**Ответ:**
- **HyperLogLog** для подсчёта уникальных с минимальной памятью (~12 KB для 10^9 элементов)
- Redis `PFADD / PFCOUNT` для реализации
- **Kafka** для стриминга событий
- **Pre-aggregation** для различных временных окон (1 мин, 5 мин, 1 час)
- **Bloom Filter** для проверки «видели ли мы этот элемент»

### Q5: Что такое идемпотентность и почему она важна в распределённых системах?

**Ответ:**
Идемпотентность — свойство операции давать одинаковый результат при повторном выполнении. Критически важна, т.к.:
- Сетевые сбои вызывают retry
- Message brokers гарантируют at-least-once delivery
- Без идемпотентности повтор может дублировать платеж, создать дубль заказа

Реализация: idempotency key в заголовке запроса + проверка в БД/кэше, что операция уже выполнена.

### Q6: Как обеспечить exactly-once delivery в распределённой системе?

**Ответ:**
«Настоящий» exactly-once невозможен в общем случае (Two Generals' Problem). На практике используют **at-least-once delivery + idempotent consumers**:
- **Outbox Pattern** — сообщение и бизнес-данные в одной транзакции
- **Inbox Pattern** — consumer сохраняет ID обработанных сообщений
- **Kafka Transactions** — producer transactions + consumer `read_committed`

> **Совет на собеседовании:** Всегда показывайте, что понимаете компромиссы. Нет «правильного» решения — есть решение, подходящее под конкретные требования. Задавайте уточняющие вопросы перед тем, как проектировать.
