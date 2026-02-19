# 10. Базы данных: NoSQL и Шардирование

> Подготовка к собеседованию на позицию Senior .NET Developer

---

## Содержание

1. [SQL vs NoSQL — когда что использовать](#1-sql-vs-nosql--когда-что-использовать)
2. [Типы NoSQL баз данных](#2-типы-nosql-баз-данных)
3. [MongoDB: подробный разбор](#3-mongodb-подробный-разбор)
4. [Redis: подробный разбор](#4-redis-подробный-разбор)
5. [Entity Framework Core](#5-entity-framework-core)
6. [Dapper vs EF Core](#6-dapper-vs-ef-core)
7. [Индексы](#7-индексы)
8. [Query Optimization](#8-query-optimization)
9. [Репликация](#9-репликация)
10. [Шардирование](#10-шардирование)
11. [Партиционирование таблиц](#11-партиционирование-таблиц)
12. [Connection Pooling](#12-connection-pooling)
13. [Database per Service (Microservices)](#13-database-per-service-microservices)
14. [ACID vs BASE](#14-acid-vs-base)
15. [CAP теорема на практике](#15-cap-теорема-на-практике)
16. [Транзакции и Isolation Levels](#16-транзакции-и-isolation-levels)
17. [Distributed Transactions и Saga Pattern](#17-distributed-transactions-и-saga-pattern)
18. [Вопросы на собеседовании с ответами](#18-вопросы-на-собеседовании-с-ответами)

---

## 1. SQL vs NoSQL — когда что использовать

### SQL (реляционные базы данных)

Реляционные базы данных (PostgreSQL, SQL Server, MySQL) основаны на строгой схеме данных, нормализации и языке SQL.

**Когда использовать SQL:**

- Данные имеют чёткую и стабильную структуру (финансы, бухгалтерия, ERP)
- Необходимы сложные JOIN-запросы и агрегации
- Критически важна целостность данных (ACID-транзакции)
- Требуется строгая валидация на уровне схемы
- Объём данных умещается на одном сервере или допускает вертикальное масштабирование

### NoSQL

NoSQL базы данных отказываются от строгой схемы и/или реляционной модели ради горизонтального масштабирования и гибкости.

**Когда использовать NoSQL:**

- Схема данных часто меняется или неоднородна
- Требуется горизонтальное масштабирование (шардирование)
- Высокая нагрузка на запись (write-heavy workloads)
- Данные естественно представимы как документы, графы или key-value пары
- Допустима eventual consistency (BASE)

### Сравнительная таблица

| Критерий | SQL | NoSQL |
|---|---|---|
| Схема | Жёсткая (schema-on-write) | Гибкая (schema-on-read) |
| Масштабирование | Вертикальное | Горизонтальное |
| Транзакции | ACID | BASE (обычно) |
| Запросы | SQL, сложные JOIN | API, ограниченные запросы |
| Консистентность | Строгая | Eventual (обычно) |
| Примеры | PostgreSQL, SQL Server | MongoDB, Redis, Cassandra |

---

## 2. Типы NoSQL баз данных

### 2.1 Document Store (MongoDB, Cosmos DB)

Хранят данные в виде документов (JSON/BSON). Каждый документ может иметь свою структуру.

```json
{
  "_id": "ObjectId('...')",
  "name": "Иван Петров",
  "email": "ivan@example.com",
  "orders": [
    { "product": "Laptop", "price": 85000 },
    { "product": "Mouse", "price": 1500 }
  ]
}
```

**Плюсы:** гибкая схема, вложенные документы, хорошая производительность чтения.
**Минусы:** дублирование данных, ограниченные транзакции (до MongoDB 4.0).

**Azure Cosmos DB** — глобально распределённая multi-model БД от Microsoft. Поддерживает API: SQL (Core), MongoDB, Cassandra, Gremlin, Table. Гарантирует SLA по задержке < 10ms на чтение.

### 2.2 Key-Value Store (Redis, DynamoDB)

Простейшая модель: ключ -> значение. Максимальная скорость на простейших операциях.

```
SET user:1001 '{"name":"Иван","role":"admin"}'
GET user:1001
```

**Плюсы:** минимальная задержка, простота, идеально для кеша.
**Минусы:** нет сложных запросов, нет связей между данными.

### 2.3 Column-Family Store (Cassandra, HBase)

Данные хранятся по столбцам (column families), а не по строкам. Оптимизировано для распределённых систем с огромным объёмом данных.

```
Row Key: user:1001
  Column Family "profile": { name: "Иван", email: "ivan@example.com" }
  Column Family "activity": { last_login: "2025-01-15", sessions: 42 }
```

**Плюсы:** линейное горизонтальное масштабирование, отказоустойчивость (нет single point of failure).
**Минусы:** ограниченные запросы, нет JOIN, сложная модель данных.

**Cassandra** использует CQL (Cassandra Query Language), похожий на SQL, но с ограничениями: нет JOIN, нет подзапросов, запросы должны соответствовать partition key.

### 2.4 Graph Database (Neo4j, Azure Cosmos DB Gremlin)

Хранят данные в виде узлов и рёбер (связей). Идеальны для сильно связанных данных.

```cypher
CREATE (ivan:Person {name: "Иван"})
CREATE (maria:Person {name: "Мария"})
CREATE (ivan)-[:ДРУГ]->(maria)

MATCH (p:Person)-[:ДРУГ]->(friend)
WHERE p.name = "Иван"
RETURN friend.name
```

**Плюсы:** эффективный обход связей, интуитивная модель для соцсетей, рекомендаций.
**Минусы:** плохо масштабируется горизонтально, ограниченная экосистема в .NET.

---

## 3. MongoDB: подробный разбор

### 3.1 Документы и коллекции

MongoDB хранит данные в формате BSON (Binary JSON). Коллекция — аналог таблицы, документ — аналог строки.

```csharp
// Модель документа
public class Product
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; }

    [BsonElement("price")]
    public decimal Price { get; set; }

    [BsonElement("categories")]
    public List<string> Categories { get; set; }

    [BsonElement("specs")]
    public ProductSpecs Specs { get; set; }
}

public class ProductSpecs
{
    public string Cpu { get; set; }
    public int RamGb { get; set; }
    public string Storage { get; set; }
}
```

### 3.2 MongoDB Driver для .NET

```csharp
using MongoDB.Driver;

// Подключение
var client = new MongoClient("mongodb://localhost:27017");
var database = client.GetDatabase("shop");
var collection = database.GetCollection<Product>("products");

// Вставка
var product = new Product
{
    Name = "Ноутбук ASUS",
    Price = 85000,
    Categories = new List<string> { "electronics", "laptops" },
    Specs = new ProductSpecs { Cpu = "i7-12700H", RamGb = 16, Storage = "512GB SSD" }
};
await collection.InsertOneAsync(product);

// Поиск
var filter = Builders<Product>.Filter.Gte(p => p.Price, 50000)
    & Builders<Product>.Filter.AnyEq(p => p.Categories, "laptops");
var laptops = await collection.Find(filter).ToListAsync();

// Обновление
var update = Builders<Product>.Update
    .Set(p => p.Price, 79000)
    .Push(p => p.Categories, "sale");
await collection.UpdateOneAsync(
    Builders<Product>.Filter.Eq(p => p.Id, product.Id),
    update);

// Удаление
await collection.DeleteOneAsync(p => p.Id == product.Id);
```

### 3.3 Индексы в MongoDB

```csharp
// Одиночный индекс
var indexKeys = Builders<Product>.IndexKeys.Ascending(p => p.Name);
await collection.Indexes.CreateOneAsync(
    new CreateIndexModel<Product>(indexKeys));

// Составной индекс
var compositeIndex = Builders<Product>.IndexKeys
    .Ascending(p => p.Categories)
    .Descending(p => p.Price);
await collection.Indexes.CreateOneAsync(
    new CreateIndexModel<Product>(compositeIndex));

// Текстовый индекс
var textIndex = Builders<Product>.IndexKeys.Text(p => p.Name);
await collection.Indexes.CreateOneAsync(
    new CreateIndexModel<Product>(textIndex));

// TTL-индекс (автоматическое удаление документов)
var ttlIndex = Builders<Session>.IndexKeys.Ascending(s => s.CreatedAt);
await sessionCollection.Indexes.CreateOneAsync(
    new CreateIndexModel<Session>(ttlIndex,
        new CreateIndexOptions { ExpireAfter = TimeSpan.FromHours(24) }));
```

### 3.4 Агрегации

```csharp
// Агрегационный пайплайн: средняя цена по категориям
var pipeline = collection.Aggregate()
    .Unwind(p => p.Categories)
    .Group(new BsonDocument
    {
        { "_id", "$categories" },
        { "avgPrice", new BsonDocument("$avg", "$price") },
        { "count", new BsonDocument("$sum", 1) }
    })
    .Sort(new BsonDocument("avgPrice", -1));

var results = await pipeline.ToListAsync();

// Типизированная агрегация с LINQ (MongoDB.Driver.Linq)
var categoryStats = await collection.AsQueryable()
    .SelectMany(p => p.Categories, (p, c) => new { Product = p, Category = c })
    .GroupBy(x => x.Category)
    .Select(g => new
    {
        Category = g.Key,
        AvgPrice = g.Average(x => x.Product.Price),
        Count = g.Count()
    })
    .OrderByDescending(x => x.AvgPrice)
    .ToListAsync();
```

---

## 4. Redis: подробный разбор

### 4.1 Типы данных Redis

| Тип | Описание | Пример использования |
|---|---|---|
| **String** | Строка, число, бинарные данные | Кеш, счётчики |
| **List** | Упорядоченный список строк | Очереди, логи |
| **Set** | Неупорядоченное множество уникальных строк | Теги, уникальные посетители |
| **Sorted Set** | Множество с весом (score) | Рейтинги, лидерборды |
| **Hash** | Словарь (поле -> значение) | Объекты, сессии |
| **Stream** | Append-only лог | Event sourcing, очереди сообщений |
| **HyperLogLog** | Вероятностная структура для подсчёта уникальных | Подсчёт уникальных посещений |

### 4.2 StackExchange.Redis для .NET

```csharp
using StackExchange.Redis;

// Подключение (ConnectionMultiplexer — потокобезопасный синглтон)
var redis = ConnectionMultiplexer.Connect("localhost:6379");
var db = redis.GetDatabase();

// String
await db.StringSetAsync("user:1001:name", "Иван Петров", TimeSpan.FromMinutes(30));
string name = await db.StringGetAsync("user:1001:name");

// Атомарный инкремент
await db.StringIncrementAsync("page:views:home", 1);
long views = (long)await db.StringGetAsync("page:views:home");

// Hash
await db.HashSetAsync("user:1001", new HashEntry[]
{
    new("name", "Иван Петров"),
    new("email", "ivan@example.com"),
    new("role", "admin")
});
var allFields = await db.HashGetAllAsync("user:1001");

// Sorted Set (лидерборд)
await db.SortedSetAddAsync("leaderboard", "player:alice", 2500);
await db.SortedSetAddAsync("leaderboard", "player:bob", 3100);
await db.SortedSetAddAsync("leaderboard", "player:charlie", 1800);

// Топ-3 игрока
var top3 = await db.SortedSetRangeByRankWithScoresAsync(
    "leaderboard", 0, 2, Order.Descending);

// List (очередь)
await db.ListRightPushAsync("task:queue", "task-001");
string task = await db.ListLeftPopAsync("task:queue");
```

### 4.3 Кеширование с Redis

```csharp
public class RedisCacheService
{
    private readonly IDatabase _db;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RedisCacheService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task<T?> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan expiry) where T : class
    {
        var cached = await _db.StringGetAsync(key);
        if (cached.HasValue)
        {
            return JsonSerializer.Deserialize<T>(cached!, _jsonOptions);
        }

        var value = await factory();
        if (value is not null)
        {
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            await _db.StringSetAsync(key, json, expiry);
        }
        return value;
    }

    public async Task InvalidateAsync(string key)
    {
        await _db.KeyDeleteAsync(key);
    }

    public async Task InvalidateByPatternAsync(string pattern)
    {
        var server = _db.Multiplexer.GetServer(
            _db.Multiplexer.GetEndPoints().First());
        var keys = server.Keys(pattern: pattern).ToArray();
        if (keys.Length > 0)
        {
            await _db.KeyDeleteAsync(keys);
        }
    }
}
```

### 4.4 Distributed Lock с Redis (Redlock)

```csharp
public class RedisDistributedLock
{
    private readonly IDatabase _db;

    public RedisDistributedLock(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task<bool> AcquireLockAsync(
        string resource, string lockId, TimeSpan expiry)
    {
        // SET key value NX PX milliseconds
        return await _db.StringSetAsync(
            $"lock:{resource}",
            lockId,
            expiry,
            When.NotExists);
    }

    public async Task<bool> ReleaseLockAsync(string resource, string lockId)
    {
        // Lua-скрипт для атомарного освобождения блокировки:
        // удаляем ключ только если значение совпадает
        var script = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end";

        var result = await _db.ScriptEvaluateAsync(
            script,
            new RedisKey[] { $"lock:{resource}" },
            new RedisValue[] { lockId });

        return (long)result == 1;
    }
}

// Использование
var lockId = Guid.NewGuid().ToString();
var acquired = await lockService.AcquireLockAsync(
    "order:process:1001", lockId, TimeSpan.FromSeconds(30));

if (acquired)
{
    try
    {
        await ProcessOrderAsync(orderId: 1001);
    }
    finally
    {
        await lockService.ReleaseLockAsync("order:process:1001", lockId);
    }
}
```

### 4.5 Pub/Sub

```csharp
var subscriber = redis.GetSubscriber();

// Подписка
await subscriber.SubscribeAsync(
    RedisChannel.Literal("notifications"),
    (channel, message) =>
    {
        Console.WriteLine($"Получено: {message}");
    });

// Публикация
await subscriber.PublishAsync(
    RedisChannel.Literal("notifications"),
    "Новый заказ создан: #1001");
```

---

## 5. Entity Framework Core

### 5.1 Миграции

```bash
# Создание миграции
dotnet ef migrations add InitialCreate

# Применение миграций
dotnet ef database update

# Откат миграции
dotnet ef database update PreviousMigrationName

# Генерация SQL-скрипта
dotnet ef migrations script --idempotent -o migration.sql
```

```csharp
// Пример миграции
public partial class AddOrderTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Orders",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                CustomerId = table.Column<int>(nullable: false),
                Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false,
                    defaultValueSql: "GETUTCDATE()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Orders", x => x.Id);
                table.ForeignKey(
                    name: "FK_Orders_Customers",
                    column: x => x.CustomerId,
                    principalTable: "Customers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_Orders_CustomerId",
            "Orders", "CustomerId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("Orders");
    }
}
```

### 5.2 Оптимизация запросов

```csharp
// Плохо: загружает ВСЕ поля
var orders = await context.Orders.ToListAsync();

// Хорошо: проекция — загружает только нужные поля
var orderSummaries = await context.Orders
    .Select(o => new OrderSummaryDto
    {
        Id = o.Id,
        CustomerName = o.Customer.Name,
        Total = o.Total
    })
    .ToListAsync();

// Пагинация
var page = await context.Orders
    .OrderByDescending(o => o.CreatedAt)
    .Skip((pageNumber - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();

// AsNoTracking — для read-only запросов (экономит память и CPU)
var products = await context.Products
    .AsNoTracking()
    .Where(p => p.IsActive)
    .ToListAsync();

// Compiled Queries — компиляция запроса один раз
private static readonly Func<AppDbContext, int, Task<Order?>> GetOrderById =
    EF.CompileAsyncQuery((AppDbContext ctx, int id) =>
        ctx.Orders
            .Include(o => o.Items)
            .FirstOrDefault(o => o.Id == id));

// Использование
var order = await GetOrderById(context, 42);
```

### 5.3 N+1 Problem

N+1 — одна из самых распространённых проблем производительности ORM.

```csharp
// ПРОБЛЕМА N+1: 1 запрос на заказы + N запросов на клиентов
var orders = await context.Orders.ToListAsync();
foreach (var order in orders)
{
    // Lazy loading: каждый вызов order.Customer — отдельный SQL-запрос!
    Console.WriteLine($"Заказ {order.Id}: {order.Customer.Name}");
}

// РЕШЕНИЕ 1: Eager Loading (Include)
var orders = await context.Orders
    .Include(o => o.Customer)
    .Include(o => o.Items)
        .ThenInclude(i => i.Product)
    .ToListAsync();

// РЕШЕНИЕ 2: Explicit Loading (когда нужна условная загрузка)
var order = await context.Orders.FindAsync(42);
await context.Entry(order)
    .Collection(o => o.Items)
    .Query()
    .Where(i => i.Quantity > 0)
    .LoadAsync();

// РЕШЕНИЕ 3: Split Query (EF Core 5+)
// Избегает Cartesian explosion при множественных Include
var orders = await context.Orders
    .Include(o => o.Items)
    .Include(o => o.Payments)
    .AsSplitQuery()
    .ToListAsync();
```

### 5.4 Change Tracker

```csharp
// Change Tracker отслеживает состояния сущностей
var product = await context.Products.FindAsync(1);
product.Price = 99.99m;

// Посмотреть изменения
var entries = context.ChangeTracker.Entries()
    .Where(e => e.State == EntityState.Modified);

foreach (var entry in entries)
{
    foreach (var prop in entry.Properties.Where(p => p.IsModified))
    {
        Console.WriteLine(
            $"{prop.Metadata.Name}: {prop.OriginalValue} -> {prop.CurrentValue}");
    }
}

await context.SaveChangesAsync();

// Отключение Change Tracker для bulk-операций
context.ChangeTracker.AutoDetectChangesEnabled = false;
try
{
    foreach (var item in largeDataset)
    {
        context.Products.Add(item);
    }
    await context.SaveChangesAsync();
}
finally
{
    context.ChangeTracker.AutoDetectChangesEnabled = true;
}

// Batch update (EF Core 7+) — без загрузки сущностей в память
await context.Products
    .Where(p => p.CategoryId == 5)
    .ExecuteUpdateAsync(setters => setters
        .SetProperty(p => p.IsActive, false)
        .SetProperty(p => p.UpdatedAt, DateTime.UtcNow));

// Batch delete (EF Core 7+)
await context.Products
    .Where(p => p.IsDeleted && p.DeletedAt < DateTime.UtcNow.AddYears(-1))
    .ExecuteDeleteAsync();
```

---

## 6. Dapper vs EF Core

### Когда использовать Dapper

- Высоконагруженные read-сценарии (отчёты, дашборды)
- Сложные SQL-запросы, которые тяжело выразить через LINQ
- Максимальная производительность критична
- Работа с хранимыми процедурами
- Микросервисы с простой моделью данных

### Когда использовать EF Core

- CRUD-операции с богатой доменной моделью
- Необходим Change Tracking
- Миграции БД
- Прототипирование и быстрая разработка
- Сложная объектная модель с навигационными свойствами

### Пример: Dapper

```csharp
using Dapper;
using Microsoft.Data.SqlClient;

public class OrderRepository
{
    private readonly string _connectionString;

    public OrderRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IEnumerable<OrderDto>> GetOrdersWithItemsAsync(int customerId)
    {
        const string sql = @"
            SELECT o.Id, o.CreatedAt, o.Total,
                   c.Name AS CustomerName,
                   i.Id AS ItemId, i.ProductName, i.Quantity, i.Price
            FROM Orders o
            INNER JOIN Customers c ON c.Id = o.CustomerId
            INNER JOIN OrderItems i ON i.OrderId = o.Id
            WHERE o.CustomerId = @CustomerId
            ORDER BY o.CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);

        var orderDict = new Dictionary<int, OrderDto>();

        await connection.QueryAsync<OrderDto, OrderItemDto, OrderDto>(
            sql,
            (order, item) =>
            {
                if (!orderDict.TryGetValue(order.Id, out var existingOrder))
                {
                    existingOrder = order;
                    existingOrder.Items = new List<OrderItemDto>();
                    orderDict.Add(existingOrder.Id, existingOrder);
                }
                existingOrder.Items.Add(item);
                return existingOrder;
            },
            new { CustomerId = customerId },
            splitOn: "ItemId");

        return orderDict.Values;
    }

    // Bulk insert с Dapper
    public async Task BulkInsertAsync(IEnumerable<Product> products)
    {
        const string sql = @"
            INSERT INTO Products (Name, Price, CategoryId)
            VALUES (@Name, @Price, @CategoryId)";

        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(sql, products);
    }
}
```

### Производительность (бенчмарк)

| Операция | EF Core | Dapper | Raw ADO.NET |
|---|---|---|---|
| SELECT 1 строка | ~250 мкс | ~120 мкс | ~100 мкс |
| SELECT 1000 строк | ~5 мс | ~2 мс | ~1.5 мс |
| INSERT 1 строка | ~300 мкс | ~130 мкс | ~110 мкс |
| Сложный JOIN | ~8 мс | ~3 мс | ~2.5 мс |

> Примечание: цифры приблизительные. EF Core 8 значительно улучшил производительность по сравнению с предыдущими версиями.

---

## 7. Индексы

### 7.1 B-Tree индекс

Стандартный тип индекса в большинстве СУБД. Поддерживает точечные запросы, диапазоны и сортировку.

```sql
-- B-Tree индекс (по умолчанию)
CREATE INDEX IX_Orders_CreatedAt ON Orders(CreatedAt);

-- Запросы, использующие B-Tree эффективно:
SELECT * FROM Orders WHERE CreatedAt = '2025-01-15';          -- точное совпадение
SELECT * FROM Orders WHERE CreatedAt > '2025-01-01';          -- диапазон
SELECT * FROM Orders ORDER BY CreatedAt DESC;                  -- сортировка
SELECT * FROM Orders WHERE CreatedAt BETWEEN '2025-01-01' AND '2025-01-31'; -- диапазон
```

**Как работает:** сбалансированное дерево (balanced tree), каждый лист указывает на строку таблицы. Поиск за O(log N). Поддерживает операции `=`, `>`, `<`, `>=`, `<=`, `BETWEEN`, `LIKE 'prefix%'`.

### 7.2 Hash индекс

Используется для точного поиска по равенству. Не поддерживает диапазоны.

```sql
-- Hash индекс (PostgreSQL)
CREATE INDEX IX_Users_Email ON Users USING HASH(Email);

-- Эффективно:
SELECT * FROM Users WHERE Email = 'ivan@example.com';

-- НЕ эффективно (hash не поддерживает):
SELECT * FROM Users WHERE Email > 'a' AND Email < 'z'; -- нет диапазонов
SELECT * FROM Users ORDER BY Email;                      -- нет сортировки
```

### 7.3 Composite (составной) индекс

Индекс на несколько столбцов. Порядок столбцов критически важен.

```sql
-- Составной индекс
CREATE INDEX IX_Orders_Status_Date ON Orders(Status, CreatedAt DESC);

-- Использует индекс (Status — первый столбец):
SELECT * FROM Orders WHERE Status = 'Active';
SELECT * FROM Orders WHERE Status = 'Active' AND CreatedAt > '2025-01-01';
SELECT * FROM Orders WHERE Status = 'Active' ORDER BY CreatedAt DESC;

-- НЕ использует индекс (Status не указан):
SELECT * FROM Orders WHERE CreatedAt > '2025-01-01'; -- skip scan возможен, но неэффективен
```

**Правило leftmost prefix:** составной индекс `(A, B, C)` может использоваться для запросов по `(A)`, `(A, B)`, `(A, B, C)`, но не для `(B)` или `(C)` в одиночку.

### 7.4 Covering индекс

Включает все столбцы, нужные запросу. Запрос выполняется полностью из индекса (Index Only Scan) без обращения к таблице.

```sql
-- Covering index (SQL Server)
CREATE INDEX IX_Orders_Covering ON Orders(CustomerId, Status)
    INCLUDE (Total, CreatedAt);

-- Index Only Scan: все нужные столбцы есть в индексе
SELECT CustomerId, Status, Total, CreatedAt
FROM Orders
WHERE CustomerId = 100 AND Status = 'Active';
```

### 7.5 Когда создавать индексы

**Создавать:**
- Столбцы в WHERE, JOIN ON, ORDER BY
- Foreign Key столбцы (ускоряют JOIN и CASCADE)
- Столбцы с высокой кардинальностью (много уникальных значений)

**Не создавать:**
- Маленькие таблицы (< 1000 строк) — sequential scan быстрее
- Столбцы с низкой кардинальностью (bool, enum с 2-3 значениями)
- Таблицы с преобладанием записи — индексы замедляют INSERT/UPDATE/DELETE
- Слишком много индексов — каждый индекс занимает место и замедляет запись

---

## 8. Query Optimization

### 8.1 Execution Plan

```sql
-- SQL Server: анализ плана выполнения
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

-- Графический план
-- В SSMS: Ctrl+L (estimated), Ctrl+M (actual)

-- Текстовый план
SET SHOWPLAN_TEXT ON;
GO
SELECT * FROM Orders WHERE CustomerId = 100;
GO
SET SHOWPLAN_TEXT OFF;
```

### 8.2 EXPLAIN (PostgreSQL)

```sql
-- Базовый план
EXPLAIN SELECT * FROM Orders WHERE CustomerId = 100;

-- С фактическим выполнением и статистикой
EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT)
SELECT o.Id, c.Name, o.Total
FROM Orders o
JOIN Customers c ON c.Id = o.CustomerId
WHERE o.CreatedAt > '2025-01-01'
  AND o.Status = 'Active'
ORDER BY o.Total DESC
LIMIT 10;

/*
Пример вывода:
Limit  (cost=1234.56..1234.59 rows=10 width=52) (actual time=2.123..2.130 rows=10 loops=1)
  ->  Sort  (cost=1234.56..1256.78 rows=8900 width=52) (actual time=2.121..2.125 rows=10 loops=1)
        Sort Key: o.total DESC
        Sort Method: top-N heapsort  Memory: 26kB
        ->  Hash Join  (cost=100.00..1100.00 rows=8900 width=52) (actual time=0.500..1.800 rows=8900 loops=1)
              Hash Cond: (o.customerid = c.id)
              ->  Index Scan using ix_orders_status_date on orders o  ...
              ->  Hash  (cost=80.00..80.00 rows=1600 width=36)  ...
Planning Time: 0.254 ms
Execution Time: 2.189 ms
*/
```

**На что обращать внимание:**
- **Seq Scan** на большой таблице — возможно, нужен индекс
- **Nested Loop** с большим количеством loops — возможно, нужен Hash/Merge Join
- **Sort** с большим объёмом — возможно, нужен индекс с сортировкой
- **actual rows** значительно больше **rows** — устаревшая статистика, нужен `ANALYZE`

### 8.3 EF Core: логирование SQL

```csharp
// Program.cs — логирование генерируемого SQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString)
           .LogTo(Console.WriteLine, LogLevel.Information)
           .EnableSensitiveDataLogging() // показывает значения параметров
           .EnableDetailedErrors());

// Через тег запроса (EF Core 6+)
var orders = await context.Orders
    .TagWith("GetActiveOrders - Dashboard endpoint")
    .Where(o => o.Status == "Active")
    .ToListAsync();
// SQL будет содержать комментарий: /* GetActiveOrders - Dashboard endpoint */
```

---

## 9. Репликация

### 9.1 Master-Slave (Primary-Replica)

Один Master принимает записи, одна или несколько Replica-серверов обслуживают чтение.

```
[Клиент] --> [Master] --> запись
                |
        +-------+-------+
        |               |
   [Replica 1]     [Replica 2]  --> чтение
```

**Плюсы:** масштабирование чтения, отказоустойчивость (failover).
**Минусы:** replication lag, eventual consistency для чтения.

```csharp
// EF Core: разделение Read/Write подключений
public class AppDbContext : DbContext
{
    private readonly string _writeConnectionString;
    private readonly string _readConnectionString;
    private bool _useReadReplica;

    public AppDbContext(IConfiguration config)
    {
        _writeConnectionString = config.GetConnectionString("Write");
        _readConnectionString = config.GetConnectionString("ReadReplica");
    }

    public AppDbContext AsReadOnly()
    {
        _useReadReplica = true;
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        return this;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        var connectionString = _useReadReplica
            ? _readConnectionString
            : _writeConnectionString;
        options.UseSqlServer(connectionString);
    }
}

// Использование
var orders = await context.AsReadOnly().Orders
    .Where(o => o.Status == "Active")
    .ToListAsync();
```

### 9.2 Master-Master (Multi-Master)

Несколько узлов принимают и записи, и чтения. Требует разрешения конфликтов.

**Плюсы:** масштабирование записи, нет единой точки отказа.
**Минусы:** конфликты при одновременной записи, сложность реализации, сетевые разделения.

**Стратегии разрешения конфликтов:**
- Last Write Wins (LWW) — побеждает последняя запись по таймстампу
- Application-level — бизнес-логика разрешает конфликт
- CRDT (Conflict-free Replicated Data Types) — структуры данных без конфликтов

---

## 10. Шардирование

Шардирование (sharding) — горизонтальное разделение данных между несколькими серверами (шардами). Каждый шард содержит подмножество данных.

### 10.1 Стратегии шардирования

#### Range-based шардирование

```
Shard 1: UserId 1 - 1,000,000
Shard 2: UserId 1,000,001 - 2,000,000
Shard 3: UserId 2,000,001 - 3,000,000
```

**Плюсы:** простота реализации, эффективные range-запросы.
**Минусы:** неравномерное распределение (hotspot), необходимость ребалансировки.

#### Hash-based шардирование

```csharp
// Определение шарда по хешу ключа
int GetShardIndex(string userId, int totalShards)
{
    var hash = MurmurHash3.Hash(userId);
    return Math.Abs(hash % totalShards);
}

// Shard = Hash(UserId) % N
// Hash("user-123") % 3 = 1 --> Shard 1
// Hash("user-456") % 3 = 0 --> Shard 0
// Hash("user-789") % 3 = 2 --> Shard 2
```

**Плюсы:** равномерное распределение данных.
**Минусы:** добавление/удаление шарда требует перераспределения данных, range-запросы неэффективны.

#### Directory-based шардирование

Отдельный lookup-сервис хранит маппинг ключ -> шард.

```csharp
public class ShardDirectory
{
    private readonly Dictionary<string, string> _shardMap;
    private readonly IDatabase _redis;

    public async Task<string> GetShardConnectionAsync(string tenantId)
    {
        var shard = await _redis.HashGetAsync("shard:directory", tenantId);
        return shard.HasValue
            ? shard.ToString()
            : await AssignShardAsync(tenantId);
    }

    private async Task<string> AssignShardAsync(string tenantId)
    {
        // Логика назначения шарда (например, по текущей нагрузке)
        var leastLoadedShard = await FindLeastLoadedShardAsync();
        await _redis.HashSetAsync("shard:directory", tenantId, leastLoadedShard);
        return leastLoadedShard;
    }
}
```

**Плюсы:** гибкость, можно перемещать данные без изменения логики.
**Минусы:** lookup-сервис — single point of failure, дополнительная задержка.

### 10.2 Consistent Hashing

Решает проблему перераспределения данных при добавлении/удалении узлов. Вместо `hash % N` используется кольцо хешей.

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
        for (int i = 0; i < _virtualNodes; i++)
        {
            var hash = GetHash($"{node}-vn{i}");
            _ring[hash] = node;
        }
    }

    public void RemoveNode(T node)
    {
        for (int i = 0; i < _virtualNodes; i++)
        {
            var hash = GetHash($"{node}-vn{i}");
            _ring.Remove(hash);
        }
    }

    public T GetNode(string key)
    {
        if (_ring.Count == 0)
            throw new InvalidOperationException("Кольцо пустое");

        var hash = GetHash(key);

        // Ищем первый узел по часовой стрелке
        foreach (var pair in _ring)
        {
            if (pair.Key >= hash)
                return pair.Value;
        }

        // Если не нашли — возвращаем первый узел (кольцо)
        return _ring.First().Value;
    }

    private int GetHash(string key)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var bytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));
        return BitConverter.ToInt32(bytes, 0);
    }
}

// Использование
var ring = new ConsistentHashRing<string>();
ring.AddNode("shard-1.db.example.com");
ring.AddNode("shard-2.db.example.com");
ring.AddNode("shard-3.db.example.com");

var shard = ring.GetNode("user:1001"); // -> "shard-2.db.example.com"
```

При добавлении нового узла перемещается только ~1/N данных (а не все, как в hash % N).

---

## 11. Партиционирование таблиц

Партиционирование — разделение одной логической таблицы на физические секции на одном сервере. В отличие от шардирования, все секции на одной машине.

```sql
-- PostgreSQL: Range Partitioning
CREATE TABLE orders (
    id         SERIAL,
    created_at TIMESTAMP NOT NULL,
    total      DECIMAL(18,2),
    status     VARCHAR(20)
) PARTITION BY RANGE (created_at);

CREATE TABLE orders_2024 PARTITION OF orders
    FOR VALUES FROM ('2024-01-01') TO ('2025-01-01');
CREATE TABLE orders_2025 PARTITION OF orders
    FOR VALUES FROM ('2025-01-01') TO ('2026-01-01');
CREATE TABLE orders_2026 PARTITION OF orders
    FOR VALUES FROM ('2026-01-01') TO ('2027-01-01');

-- Запрос автоматически использует только нужную партицию (partition pruning)
SELECT * FROM orders WHERE created_at >= '2025-06-01' AND created_at < '2025-07-01';
-- Обращается только к orders_2025

-- SQL Server: Table Partitioning
CREATE PARTITION FUNCTION pf_OrderDate (DATETIME)
    AS RANGE RIGHT FOR VALUES ('2024-01-01', '2025-01-01', '2026-01-01');

CREATE PARTITION SCHEME ps_OrderDate
    AS PARTITION pf_OrderDate
    TO (fg_archive, fg_2024, fg_2025, fg_2026);
```

**Виды партиционирования:**
- **Range** — по диапазону значений (даты, ID)
- **List** — по списку значений (регион, статус)
- **Hash** — по хешу значения

---

## 12. Connection Pooling

Создание TCP-соединения к БД — дорогая операция (handshake, аутентификация). Connection Pool повторно использует существующие соединения.

```csharp
// SQL Server: настройка через строку подключения
var connectionString = new SqlConnectionStringBuilder
{
    DataSource = "localhost",
    InitialCatalog = "MyDatabase",
    IntegratedSecurity = true,
    MinPoolSize = 5,         // минимум соединений в пуле
    MaxPoolSize = 100,       // максимум соединений в пуле (по умолчанию 100)
    ConnectTimeout = 30,     // таймаут получения соединения из пула
    Pooling = true           // включён по умолчанию
}.ToString();

// EF Core: настройка DbContext Pooling
builder.Services.AddDbContextPool<AppDbContext>(options =>
    options.UseSqlServer(connectionString),
    poolSize: 128);  // размер пула DbContext'ов

// Важно: при DbContext Pooling OnConfiguring вызывается один раз,
// а при повторном использовании вызывается OnModelCreating из кеша.
// Не храните состояние в DbContext при использовании пула!
```

**Проблемы:**
- **Connection leak** — забыли вызвать `Dispose()` / не используете `using`
- **Pool exhaustion** — все соединения заняты, новые запросы ждут
- **Stale connections** — соединение разорвано сетью, но пул не знает

```csharp
// Мониторинг пула соединений (SQL Server)
// sys.dm_exec_connections — активные соединения
// sys.dm_exec_sessions — активные сессии

// В .NET: счётчики производительности
// dotnet-counters monitor --process-id <PID> --counters Microsoft.Data.SqlClient.EventSource
```

---

## 13. Database per Service (Microservices)

В микросервисной архитектуре каждый сервис владеет своей базой данных. Другие сервисы не имеют прямого доступа к чужой БД.

```
[Order Service] --> [Orders DB (SQL Server)]
[Product Service] --> [Products DB (MongoDB)]
[Session Service] --> [Sessions (Redis)]
[Analytics Service] --> [Analytics DB (ClickHouse)]
```

**Плюсы:**
- Независимое развёртывание и масштабирование
- Свобода выбора технологии БД под задачу (polyglot persistence)
- Изоляция сбоев

**Минусы:**
- Нет JOIN между сервисами — нужны API-вызовы или денормализация
- Распределённые транзакции — сложно обеспечить согласованность
- Дублирование данных

**Паттерны для работы с данными между сервисами:**
- **API Composition** — агрегирующий сервис вызывает другие API
- **CQRS** — разделение модели чтения и записи
- **Event Sourcing** — хранение событий, не состояний
- **Saga** — распределённые транзакции через последовательность локальных транзакций

---

## 14. ACID vs BASE

### ACID (реляционные БД)

| Свойство | Описание |
|---|---|
| **Atomicity** | Транзакция выполняется полностью или не выполняется вовсе |
| **Consistency** | БД переходит из одного валидного состояния в другое |
| **Isolation** | Параллельные транзакции не влияют друг на друга |
| **Durability** | Закоммиченные данные сохраняются даже при сбое |

### BASE (распределённые NoSQL)

| Свойство | Описание |
|---|---|
| **Basically Available** | Система гарантирует доступность (возможно, с устаревшими данными) |
| **Soft state** | Состояние системы может меняться со временем без внешнего воздействия |
| **Eventually consistent** | Система придёт в согласованное состояние через некоторое время |

```csharp
// ACID: банковский перевод
await using var transaction = await connection.BeginTransactionAsync();
try
{
    await connection.ExecuteAsync(
        "UPDATE Accounts SET Balance = Balance - @Amount WHERE Id = @FromId",
        new { Amount = 1000, FromId = 1 }, transaction);

    await connection.ExecuteAsync(
        "UPDATE Accounts SET Balance = Balance + @Amount WHERE Id = @ToId",
        new { Amount = 1000, ToId = 2 }, transaction);

    await transaction.CommitAsync(); // атомарно
}
catch
{
    await transaction.RollbackAsync(); // откат обеих операций
    throw;
}

// BASE: обновление счётчика просмотров (eventual consistency — допустима)
await redisDb.StringIncrementAsync("article:views:42");
// Периодически (через 5 минут) синхронизируем с SQL
```

---

## 15. CAP теорема на практике

**CAP теорема** утверждает: в распределённой системе невозможно одновременно гарантировать все три свойства:

- **C (Consistency)** — все узлы видят одинаковые данные в один момент времени
- **A (Availability)** — каждый запрос получает ответ (не обязательно актуальный)
- **P (Partition tolerance)** — система работает при разрыве сети между узлами

При сетевом разделении (P — неизбежно в распределённых системах) нужно выбирать между C и A:

| Система | Тип | Описание |
|---|---|---|
| **CP** — MongoDB, Redis (cluster), HBase | Consistency + Partition tolerance | При разделении недоступные узлы не обслуживают запросы |
| **AP** — Cassandra, DynamoDB, CouchDB | Availability + Partition tolerance | Все узлы отвечают, но данные могут быть устаревшими |
| **CA** — PostgreSQL (single node), SQL Server | Consistency + Availability | Нет partition tolerance (один сервер) |

**На практике** CAP — это спектр, а не бинарный выбор. Например, MongoDB позволяет настраивать `writeConcern` и `readPreference`:

```csharp
// MongoDB: настройка уровня консистентности
var client = new MongoClient(new MongoClientSettings
{
    Server = new MongoServerAddress("localhost", 27017),
    WriteConcern = WriteConcern.WMajority,   // запись подтверждена большинством узлов
    ReadPreference = ReadPreference.SecondaryPreferred, // чтение с реплик
    ReadConcern = ReadConcern.Majority       // чтение данных, подтверждённых большинством
});
```

---

## 16. Транзакции и Isolation Levels

### Проблемы параллельного доступа

| Проблема | Описание |
|---|---|
| **Dirty Read** | Чтение незакоммиченных данных другой транзакции |
| **Non-Repeatable Read** | Повторное чтение той же строки даёт другой результат |
| **Phantom Read** | Повторный запрос возвращает строки, которых раньше не было |
| **Lost Update** | Две транзакции перезаписывают данные друг друга |

### Уровни изоляции

| Уровень | Dirty Read | Non-Repeatable Read | Phantom Read | Производительность |
|---|---|---|---|---|
| **Read Uncommitted** | Да | Да | Да | Максимальная |
| **Read Committed** | Нет | Да | Да | Высокая |
| **Repeatable Read** | Нет | Нет | Да | Средняя |
| **Serializable** | Нет | Нет | Нет | Низкая |
| **Snapshot** | Нет | Нет | Нет | Высокая (MVCC) |

```csharp
// Установка уровня изоляции в EF Core
await using var transaction = await context.Database
    .BeginTransactionAsync(IsolationLevel.RepeatableRead);
try
{
    var account = await context.Accounts
        .FirstAsync(a => a.Id == accountId);

    account.Balance -= amount;
    await context.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

// Установка уровня изоляции в ADO.NET / Dapper
await using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();
await using var transaction = connection.BeginTransaction(IsolationLevel.Snapshot);

var balance = await connection.QuerySingleAsync<decimal>(
    "SELECT Balance FROM Accounts WHERE Id = @Id",
    new { Id = accountId },
    transaction);
```

### Snapshot Isolation (MVCC)

SQL Server и PostgreSQL поддерживают Snapshot Isolation через Multi-Version Concurrency Control (MVCC). Каждая транзакция видит снимок данных на момент её начала.

```sql
-- SQL Server: включение Snapshot Isolation
ALTER DATABASE MyDatabase SET ALLOW_SNAPSHOT_ISOLATION ON;
ALTER DATABASE MyDatabase SET READ_COMMITTED_SNAPSHOT ON;
-- Теперь Read Committed использует MVCC вместо блокировок
```

**Преимущество:** читатели не блокируют писателей и наоборот.
**Недостаток:** дополнительное потребление tempdb/WAL для хранения версий.

---

## 17. Distributed Transactions и Saga Pattern

### 17.1 Distributed Transactions (2PC)

Two-Phase Commit (2PC) — протокол координации транзакций между несколькими БД.

```
Фаза 1 (Prepare): Координатор спрашивает все участники: "Готовы?"
Фаза 2 (Commit):  Если все ответили "Да" → Commit. Если кто-то "Нет" → Rollback.
```

**Проблемы 2PC:**
- Блокировка ресурсов на время ожидания
- Координатор — единая точка отказа
- Не работает с NoSQL (обычно)
- Плохо масштабируется

```csharp
// .NET: TransactionScope для распределённых транзакций (DTC)
using var scope = new TransactionScope(
    TransactionScopeOption.Required,
    new TransactionOptions
    {
        IsolationLevel = IsolationLevel.ReadCommitted,
        Timeout = TimeSpan.FromSeconds(30)
    },
    TransactionScopeAsyncFlowOption.Enabled);

// Операция 1: SQL Server
await using var sqlConnection = new SqlConnection(sqlConnectionString);
await sqlConnection.ExecuteAsync(
    "UPDATE Accounts SET Balance = Balance - 1000 WHERE Id = @Id",
    new { Id = 1 });

// Операция 2: другая БД
await using var sqlConnection2 = new SqlConnection(otherConnectionString);
await sqlConnection2.ExecuteAsync(
    "UPDATE Accounts SET Balance = Balance + 1000 WHERE Id = @Id",
    new { Id = 2 });

scope.Complete(); // Commit обеих транзакций
// Если scope.Complete() не вызван — автоматический Rollback
```

### 17.2 Saga Pattern

Saga — альтернатива распределённым транзакциям. Каждый шаг — локальная транзакция с компенсирующим действием.

#### Choreography-based Saga (через события)

```
OrderService: Создать заказ → событие "OrderCreated"
  PaymentService: Списать деньги → событие "PaymentCompleted"
    InventoryService: Зарезервировать товар → событие "InventoryReserved"
      DeliveryService: Создать доставку → событие "DeliveryScheduled"

При ошибке — компенсация в обратном порядке:
  InventoryService: "PaymentFailed" → Снять резерв
  PaymentService: "InventoryFailed" → Вернуть деньги
  OrderService: → Отменить заказ
```

#### Orchestration-based Saga (через оркестратор)

```csharp
// Оркестратор Saga
public class CreateOrderSaga
{
    private readonly IOrderService _orderService;
    private readonly IPaymentService _paymentService;
    private readonly IInventoryService _inventoryService;

    public async Task<SagaResult> ExecuteAsync(CreateOrderCommand command)
    {
        var sagaState = new SagaState();

        try
        {
            // Шаг 1: Создать заказ
            var orderId = await _orderService.CreateOrderAsync(command);
            sagaState.OrderId = orderId;
            sagaState.CompletedSteps.Add("CreateOrder");

            // Шаг 2: Списать деньги
            await _paymentService.ChargeAsync(
                command.CustomerId, command.TotalAmount);
            sagaState.CompletedSteps.Add("ChargePayment");

            // Шаг 3: Зарезервировать товар
            await _inventoryService.ReserveAsync(
                command.Items);
            sagaState.CompletedSteps.Add("ReserveInventory");

            return SagaResult.Success(orderId);
        }
        catch (Exception ex)
        {
            // Компенсация в обратном порядке
            await CompensateAsync(sagaState);
            return SagaResult.Failure(ex.Message);
        }
    }

    private async Task CompensateAsync(SagaState state)
    {
        // Компенсация выполняется в обратном порядке
        foreach (var step in state.CompletedSteps.AsEnumerable().Reverse())
        {
            switch (step)
            {
                case "ReserveInventory":
                    await _inventoryService.ReleaseReservationAsync(
                        state.OrderId);
                    break;
                case "ChargePayment":
                    await _paymentService.RefundAsync(
                        state.OrderId);
                    break;
                case "CreateOrder":
                    await _orderService.CancelOrderAsync(
                        state.OrderId);
                    break;
            }
        }
    }
}

public class SagaState
{
    public int OrderId { get; set; }
    public List<string> CompletedSteps { get; set; } = new();
}
```

**Choreography vs Orchestration:**

| Критерий | Choreography | Orchestration |
|---|---|---|
| Связность | Низкая | Средняя (оркестратор знает все шаги) |
| Сложность | Растёт с количеством шагов | Контролируемая |
| Наблюдаемость | Сложно отследить | Легко отследить (один оркестратор) |
| Единая точка отказа | Нет | Оркестратор |

---

## 18. Вопросы на собеседовании с ответами

### Вопрос 1: Когда вы выберете NoSQL вместо SQL?

**Ответ:** NoSQL выбирается когда: (1) схема данных нестабильна или неоднородна, например, каталог товаров с различными атрибутами; (2) требуется горизонтальное масштабирование для обработки терабайт данных; (3) рабочая нагрузка write-heavy (логи, IoT-данные, аналитика); (4) данные естественно представимы как документы, графы или key-value; (5) допустима eventual consistency. SQL остаётся лучшим выбором для финансовых систем, ERP, и сценариев с сильно связанными данными и сложными транзакциями.

---

### Вопрос 2: Объясните проблему N+1 и как её решить в EF Core.

**Ответ:** N+1 возникает, когда ORM выполняет 1 запрос для получения основных сущностей и затем N дополнительных запросов для загрузки связанных данных (по одному на каждую сущность). Например, загрузка 100 заказов и обращение к `order.Customer` генерирует 101 SQL-запрос. Решения в EF Core: (1) **Eager Loading** с `.Include()` / `.ThenInclude()` — один JOIN-запрос; (2) **Split Query** с `.AsSplitQuery()` — несколько запросов, но без Cartesian explosion; (3) **Explicit Loading** через `context.Entry().Collection().LoadAsync()` для условной загрузки; (4) **Projection** через `.Select()` — загрузка только нужных полей.

---

### Вопрос 3: Чем Snapshot Isolation отличается от Serializable?

**Ответ:** Оба уровня предотвращают dirty read, non-repeatable read и phantom read, но по-разному. **Serializable** использует блокировки (shared/exclusive locks, range locks), что может привести к deadlock-ам и снижению пропускной способности. **Snapshot Isolation** использует MVCC — каждая транзакция работает с версией данных на момент её начала. Читатели не блокируют писателей. Однако Snapshot не является истинно Serializable: возможна аномалия write skew (две транзакции читают одни данные и обновляют разные, создавая нарушение инварианта). Snapshot лучше подходит для read-heavy нагрузки с редкими конфликтами.

---

### Вопрос 4: Как работает Consistent Hashing и зачем он нужен?

**Ответ:** При обычном хешировании (`hash % N`) добавление или удаление узла перераспределяет почти все данные. Consistent Hashing решает эту проблему: узлы и ключи размещаются на кольце хешей (0 .. 2^32). Каждый ключ назначается ближайшему узлу по часовой стрелке. При добавлении узла перемещается только ~1/N данных (от соседнего узла). Виртуальные узлы (каждый физический узел создаёт 100-200 точек на кольце) обеспечивают равномерное распределение. Используется в Redis Cluster, Cassandra, Memcached, CDN.

---

### Вопрос 5: Расскажите о CAP теореме. Какую модель использует MongoDB?

**Ответ:** CAP теорема утверждает, что распределённая система может обеспечить максимум два из трёх свойств: Consistency, Availability, Partition tolerance. Поскольку сетевые разделения (P) неизбежны, реальный выбор — между C и A. MongoDB по умолчанию CP-система: при сетевом разделении выбирает консистентность (недоступный secondary не обслуживает запросы на запись). Однако через `readPreference: secondaryPreferred` и `readConcern: local` MongoDB можно настроить ближе к AP, позволяя чтение с реплик (с возможными устаревшими данными). На практике CAP — это спектр, настраиваемый через writeConcern и readConcern.

---

### Вопрос 6: Когда использовать Dapper вместо EF Core?

**Ответ:** Dapper предпочтителен, когда: (1) производительность запросов критична — Dapper в 2-3 раза быстрее EF Core на маппинге; (2) SQL-запрос сложный (рекурсивные CTE, оконные функции, сложные подзапросы), и его неудобно выражать через LINQ; (3) работа с хранимыми процедурами; (4) микросервис с простой моделью данных, где Change Tracking излишен; (5) bulk-операции чтения (отчёты, дашборды). EF Core предпочтителен для CRUD с богатой доменной моделью, когда нужны миграции, Change Tracking, и скорость разработки важнее максимальной производительности. Часто оба инструмента используются вместе: EF Core для записи, Dapper для чтения.

---

### Вопрос 7: Как работает B-Tree индекс? Когда индекс не поможет?

**Ответ:** B-Tree — сбалансированное дерево, где каждый узел содержит упорядоченные ключи и указатели на дочерние узлы. Листовые узлы содержат указатели на строки таблицы. Поиск, вставка, удаление — O(log N). Поддерживает точечные запросы, диапазоны и сортировку. Индекс **не поможет** когда: (1) запрос читает значительную часть таблицы (>10-20%) — Sequential Scan быстрее; (2) низкая кардинальность столбца (boolean); (3) функция применена к столбцу (`WHERE YEAR(date) = 2025` — нужен функциональный индекс); (4) `LIKE '%suffix'` — B-Tree не поддерживает поиск по суффиксу; (5) составной индекс `(A, B, C)` при фильтрации только по `B` или `C`.

---

### Вопрос 8: Объясните разницу между шардированием и партиционированием.

**Ответ:** **Партиционирование** — разделение таблицы на секции в рамках одного сервера. Управляется СУБД, прозрачно для приложения. Ускоряет запросы через partition pruning и упрощает обслуживание (удаление старых данных). **Шардирование** — распределение данных между несколькими серверами (кластером). Обеспечивает горизонтальное масштабирование: каждый сервер хранит подмножество данных. Требует шардинг-ключа, маршрутизации запросов, и усложняет агрегатные запросы и транзакции. Партиционирование — оптимизация на одном сервере, шардирование — масштабирование за пределы одного сервера.

---

### Вопрос 9: Как реализовать распределённую транзакцию без 2PC?

**Ответ:** Основная альтернатива — **Saga Pattern**. Каждый шаг — локальная ACID-транзакция в одном сервисе. При ошибке выполняются компенсирующие действия в обратном порядке. Два подхода: **Choreography** — сервисы обмениваются событиями через message broker (Kafka, RabbitMQ); простой для 2-3 шагов, но сложен для длинных цепочек. **Orchestration** — центральный оркестратор управляет последовательностью шагов; проще для наблюдения и отладки, но создаёт дополнительный компонент. Также используется паттерн **Outbox** — транзакционно сохраняем событие в ту же БД, что и бизнес-данные, затем отдельный процесс публикует события в broker, гарантируя at-least-once delivery.

---

### Вопрос 10: Как работает Connection Pooling? Какие проблемы могут возникнуть?

**Ответ:** Connection Pool поддерживает набор открытых соединений к БД. При запросе соединения (Open) возвращается существующее из пула, при Dispose — возвращается обратно (не закрывается). Это экономит время на TCP handshake, TLS и аутентификацию. **Проблемы:** (1) **Connection leak** — забыли Dispose, соединение не возвращается; решение: всегда `using`. (2) **Pool exhaustion** — все соединения заняты; диагностика: мониторинг `NumberOfActiveConnections`; решение: увеличить MaxPoolSize или найти утечку. (3) **Stale connections** — TCP-соединение разорвано, но пул считает его живым; решение: `ConnectRetryCount`, `Connection Lifetime`. (4) **Too many pools** — каждая уникальная строка подключения создаёт отдельный пул; решение: стандартизация строк подключения.

---

### Вопрос 11: Что такое Outbox Pattern и зачем он нужен?

**Ответ:** Outbox Pattern решает проблему атомарности «сохранить данные + отправить событие». Без Outbox возможны ситуации, когда данные сохранены, но событие не отправлено (или наоборот). Решение: в одной транзакции с бизнес-данными записываем событие в таблицу Outbox. Отдельный фоновый процесс (Outbox Publisher) читает эту таблицу и публикует события в message broker, помечая их как обработанные.

```csharp
// В одной транзакции
await using var transaction = await context.Database.BeginTransactionAsync();

var order = new Order { CustomerId = 1, Total = 5000 };
context.Orders.Add(order);

context.OutboxMessages.Add(new OutboxMessage
{
    Id = Guid.NewGuid(),
    Type = "OrderCreated",
    Payload = JsonSerializer.Serialize(new OrderCreatedEvent(order.Id, order.Total)),
    CreatedAt = DateTime.UtcNow,
    Processed = false
});

await context.SaveChangesAsync();
await transaction.CommitAsync();
```

---

### Вопрос 12: Как Redis обеспечивает персистентность данных?

**Ответ:** Redis предлагает два механизма: (1) **RDB (snapshotting)** — периодические снимки всех данных на диск (например, каждые 5 минут если изменилось > 100 ключей). Плюс: компактный файл, быстрое восстановление. Минус: потеря данных между снимками. (2) **AOF (Append Only File)** — логирование каждой операции записи. Настраивается fsync: `always` (максимальная надёжность, низкая производительность), `everysec` (компромисс, потеря до 1 секунды), `no` (ОС решает). Redis 7+ поддерживает Multi-Part AOF с автоматической компрессией. На практике используют комбинацию: AOF для минимальных потерь + RDB для быстрого восстановления.

---

### Вопрос 13: Расскажите о стратегиях кеш-инвалидации.

**Ответ:** Основные стратегии: (1) **TTL (Time-To-Live)** — кеш автоматически истекает через заданное время; простейший подход, допускает устаревшие данные в пределах TTL. (2) **Cache-Aside (Lazy Loading)** — приложение проверяет кеш, при промахе загружает из БД и кладёт в кеш; инвалидация при обновлении данных. (3) **Write-Through** — запись одновременно в кеш и БД; консистентность, но двойная запись. (4) **Write-Behind (Write-Back)** — запись сначала в кеш, асинхронная синхронизация с БД; быстрая запись, но риск потери данных. (5) **Event-driven invalidation** — при изменении данных публикуется событие, подписчик удаляет запись из кеша. На практике часто комбинируют TTL + event-driven invalidation.

---

## Дополнительные ресурсы

- [MongoDB .NET Driver Documentation](https://www.mongodb.com/docs/drivers/csharp/)
- [StackExchange.Redis Documentation](https://stackexchange.github.io/StackExchange.Redis/)
- [EF Core Performance](https://learn.microsoft.com/en-us/ef/core/performance/)
- [Designing Data-Intensive Applications — Martin Kleppmann](https://dataintensive.net/)
- [Microsoft: Data Access Architecture Guide](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/architect-microservice-container-applications/data-sovereignty-per-microservice)

---

> **Совет для собеседования:** На позиции Senior важно не только знать теорию, но и уметь аргументировать выбор технологии для конкретного сценария. Будьте готовы обсуждать trade-off'ы: консистентность vs доступность, нормализация vs денормализация, производительность vs простота. Приводите примеры из реального опыта.
