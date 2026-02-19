# 06 — CQRS: Command Query Responsibility Segregation

## Содержание

1. [Что такое CQRS](#1-что-такое-cqrs)
2. [Почему разделяем чтение и запись](#2-почему-разделяем-чтение-и-запись)
3. [CQS vs CQRS](#3-cqs-vs-cqrs)
4. [CQRS без Event Sourcing и с Event Sourcing](#4-cqrs-без-event-sourcing-и-с-event-sourcing)
5. [Реализация через MediatR](#5-реализация-через-mediatr)
6. [Pipeline Behaviors](#6-pipeline-behaviors)
7. [Command: создание, валидация, обработка](#7-command-создание-валидация-обработка)
8. [Query: оптимизация чтения, Read Models, Projections](#8-query-оптимизация-чтения-read-models-projections)
9. [Eventual Consistency — проблемы и решения](#9-eventual-consistency--проблемы-и-решения)
10. [Материализованные представления](#10-материализованные-представления-materialized-views)
11. [Когда CQRS оправдан, а когда — оверинжиниринг](#11-когда-cqrs-оправдан-а-когда--оверинжиниринг)
12. [Полный пример: Order Aggregate](#12-полный-пример-order-aggregate-с-commands-и-queries)
13. [Тестирование CQRS](#13-тестирование-cqrs-unit--integration)
14. [Вопросы на собеседовании](#14-вопросы-на-собеседовании-с-ответами)

---

## 1. Что такое CQRS

**CQRS (Command Query Responsibility Segregation)** — архитектурный паттерн, в котором модель записи данных (Command Model) полностью отделена от модели чтения данных (Query Model). Впервые описан Грегом Янгом (Greg Young) как развитие принципа CQS Бертрана Мейера.

Ключевая идея: **операции, изменяющие состояние системы, и операции, возвращающие данные, проходят через разные модели, разные объекты и потенциально разные хранилища**.

```
┌─────────────┐       ┌──────────────────┐       ┌─────────────┐
│   Client     │──────▶│  Command Handler │──────▶│ Write Store │
│  (Write op)  │       └──────────────────┘       └──────┬──────┘
└─────────────┘                                          │ sync / event
                                                         ▼
┌─────────────┐       ┌──────────────────┐       ┌─────────────┐
│   Client     │──────▶│  Query Handler   │──────▶│ Read Store  │
│  (Read op)   │       └──────────────────┘       └─────────────┘
└─────────────┘
```

### Минимальный пример без фреймворков

```csharp
// Command — намерение изменить состояние
public record CreateOrderCommand(Guid CustomerId, List<OrderItemDto> Items);

// Query — запрос данных без побочных эффектов
public record GetOrderByIdQuery(Guid OrderId);

// Command Handler — записывает данные
public class CreateOrderHandler
{
    private readonly IOrderRepository _repo;

    public CreateOrderHandler(IOrderRepository repo) => _repo = repo;

    public async Task<Guid> Handle(CreateOrderCommand cmd)
    {
        var order = Order.Create(cmd.CustomerId, cmd.Items);
        await _repo.AddAsync(order);
        return order.Id;
    }
}

// Query Handler — читает данные
public class GetOrderByIdHandler
{
    private readonly IReadOnlyOrderRepository _readRepo;

    public GetOrderByIdHandler(IReadOnlyOrderRepository readRepo) => _readRepo = readRepo;

    public async Task<OrderDto?> Handle(GetOrderByIdQuery query)
    {
        return await _readRepo.GetByIdAsync(query.OrderId);
    }
}
```

---

## 2. Почему разделяем чтение и запись

### Фундаментальные причины

| Аспект | Запись (Command) | Чтение (Query) |
|--------|-----------------|-----------------|
| **Модель данных** | Нормализованная, с инвариантами | Денормализованная, оптимизированная для UI |
| **Масштабирование** | Вертикальное (сложная бизнес-логика) | Горизонтальное (read-реплики, кэш) |
| **Валидация** | Строгая, бизнес-правила | Минимальная (параметры фильтра) |
| **Транзакции** | ACID, пессимистичные/оптимистичные блокировки | Без блокировок, eventual consistency допустим |
| **Нагрузка** | Обычно 10-20% запросов | Обычно 80-90% запросов |

### Практические преимущества

```csharp
// БЕЗ CQRS: «жирный» сервис, в котором смешаны чтение и запись
public class OrderService
{
    public async Task<OrderDto> GetOrderAsync(Guid id) { /* ... */ }
    public async Task<List<OrderSummaryDto>> GetOrdersForDashboardAsync() { /* ... */ }
    public async Task<Guid> CreateOrderAsync(CreateOrderRequest req) { /* ... */ }
    public async Task CancelOrderAsync(Guid id) { /* ... */ }
    // 30+ методов, 2000 строк — невозможно тестировать и развивать
}

// С CQRS: каждый хендлер — изолированная единица
// Один хендлер = одна ответственность = один тест
public class CancelOrderHandler : IRequestHandler<CancelOrderCommand, Unit>
{
    public async Task<Unit> Handle(CancelOrderCommand cmd, CancellationToken ct)
    {
        // Чистая бизнес-логика, ничего лишнего
        var order = await _repo.GetByIdAsync(cmd.OrderId, ct);
        order.Cancel(cmd.Reason);
        await _repo.SaveAsync(order, ct);
        return Unit.Value;
    }
}
```

---

## 3. CQS vs CQRS

### CQS (Command Query Separation)

Принцип уровня метода, сформулированный Бертраном Мейером:

> Каждый метод должен быть **либо** командой (изменяет состояние, ничего не возвращает), **либо** запросом (возвращает данные, не изменяет состояние). Никогда — и то, и другое.

```csharp
// CQS на уровне класса
public class ShoppingCart
{
    private readonly List<CartItem> _items = new();

    // Query — возвращает данные, не меняет состояние
    public IReadOnlyList<CartItem> GetItems() => _items.AsReadOnly();

    public decimal GetTotal() => _items.Sum(i => i.Price * i.Quantity);

    // Command — меняет состояние, ничего не возвращает (void)
    public void AddItem(CartItem item) => _items.Add(item);

    public void RemoveItem(Guid itemId) => _items.RemoveAll(i => i.Id == itemId);
}
```

### Ключевые различия

| Критерий | CQS | CQRS |
|----------|-----|------|
| Уровень | Метод / класс | Архитектура / система |
| Модель данных | Одна общая модель | Раздельные модели |
| Хранилище | Одно | Может быть несколько |
| Масштабирование | Не предусмотрено | Раздельное масштабирование |
| Сложность | Минимальная | Значительная |

### CQRS — это архитектурное развитие CQS

```csharp
// CQS: один репозиторий, одна модель
public interface IOrderRepository
{
    Task<Order> GetByIdAsync(Guid id);           // Query
    Task AddAsync(Order order);                   // Command
    Task UpdateAsync(Order order);                // Command
}

// CQRS: разные интерфейсы, потенциально разные хранилища
public interface IOrderWriteRepository  // Command Side
{
    Task<Order> GetByIdAsync(Guid id);  // загрузка агрегата для изменения
    Task SaveAsync(Order order);
}

public interface IOrderReadRepository   // Query Side
{
    Task<OrderDto> GetByIdAsync(Guid id);
    Task<PagedResult<OrderSummaryDto>> GetPagedAsync(OrderFilter filter);
}
```

---

## 4. CQRS без Event Sourcing и с Event Sourcing

### CQRS без Event Sourcing (простой вариант)

Запись и чтение работают с одной реляционной базой, но через разные модели.

```csharp
// Write Model — полноценный агрегат с бизнес-логикой
public class Order
{
    public Guid Id { get; private set; }
    public OrderStatus Status { get; private set; }
    private readonly List<OrderLine> _lines = new();

    public void AddLine(Guid productId, int qty, decimal price)
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Нельзя добавлять позиции в подтверждённый заказ");

        _lines.Add(new OrderLine(productId, qty, price));
    }
}

// Read Model — плоский DTO, оптимизированный для отображения
public class OrderSummaryReadModel
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string StatusDisplay { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Query Handler читает напрямую из БД через Dapper (быстро, без ORM overhead)
public class GetOrdersSummaryHandler : IRequestHandler<GetOrdersSummaryQuery, List<OrderSummaryReadModel>>
{
    private readonly IDbConnection _db;

    public GetOrdersSummaryHandler(IDbConnection db) => _db = db;

    public async Task<List<OrderSummaryReadModel>> Handle(
        GetOrdersSummaryQuery query, CancellationToken ct)
    {
        const string sql = """
            SELECT o.Id, c.Name AS CustomerName, o.TotalAmount,
                   o.Status AS StatusDisplay, COUNT(ol.Id) AS ItemCount, o.CreatedAt
            FROM Orders o
            JOIN Customers c ON c.Id = o.CustomerId
            JOIN OrderLines ol ON ol.OrderId = o.Id
            WHERE (@Status IS NULL OR o.Status = @Status)
            GROUP BY o.Id, c.Name, o.TotalAmount, o.Status, o.CreatedAt
            ORDER BY o.CreatedAt DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY
            """;

        var results = await _db.QueryAsync<OrderSummaryReadModel>(sql, new
        {
            query.Status,
            Skip = query.Page * query.PageSize,
            Take = query.PageSize
        });

        return results.ToList();
    }
}
```

### CQRS с Event Sourcing

Состояние агрегата восстанавливается из потока событий. Read-модели строятся проекциями.

```csharp
// Доменные события
public record OrderCreated(Guid OrderId, Guid CustomerId, DateTime CreatedAt);
public record OrderLineAdded(Guid OrderId, Guid ProductId, int Quantity, decimal Price);
public record OrderConfirmed(Guid OrderId, DateTime ConfirmedAt);

// Агрегат восстанавливается из событий
public class Order : AggregateRoot
{
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    private readonly List<OrderLine> _lines = new();

    // Восстановление из истории событий
    public void Apply(OrderCreated e)
    {
        Id = e.OrderId;
        CustomerId = e.CustomerId;
        Status = OrderStatus.Draft;
    }

    public void Apply(OrderLineAdded e)
    {
        _lines.Add(new OrderLine(e.ProductId, e.Quantity, e.Price));
    }

    public void Apply(OrderConfirmed e)
    {
        Status = OrderStatus.Confirmed;
    }

    // Бизнес-операция порождает событие
    public void Confirm()
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Заказ уже подтверждён");
        if (!_lines.Any())
            throw new InvalidOperationException("Нельзя подтвердить пустой заказ");

        RaiseEvent(new OrderConfirmed(Id, DateTime.UtcNow));
    }
}

// Проекция: обработчик событий строит Read Model
public class OrderSummaryProjection :
    IEventHandler<OrderCreated>,
    IEventHandler<OrderConfirmed>
{
    private readonly IOrderReadStore _readStore;

    public OrderSummaryProjection(IOrderReadStore readStore) => _readStore = readStore;

    public async Task Handle(OrderCreated e)
    {
        await _readStore.InsertAsync(new OrderSummaryReadModel
        {
            Id = e.OrderId,
            CustomerId = e.CustomerId,
            Status = "Draft",
            CreatedAt = e.CreatedAt
        });
    }

    public async Task Handle(OrderConfirmed e)
    {
        await _readStore.UpdateStatusAsync(e.OrderId, "Confirmed");
    }
}
```

---

## 5. Реализация через MediatR

### Установка

```bash
dotnet add package MediatR
dotnet add package MediatR.Extensions.Microsoft.DependencyInjection
```

### Регистрация

```csharp
// Program.cs
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
});
```

### IRequest / IRequestHandler — запросы «один-к-одному»

```csharp
// Command — возвращает Id созданного заказа
public record CreateOrderCommand(
    Guid CustomerId,
    List<OrderItemDto> Items
) : IRequest<Guid>;

// Handler
public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Guid>
{
    private readonly IOrderWriteRepository _repo;
    private readonly IUnitOfWork _uow;

    public CreateOrderCommandHandler(IOrderWriteRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<Guid> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        var order = Order.Create(cmd.CustomerId);

        foreach (var item in cmd.Items)
            order.AddLine(item.ProductId, item.Quantity, item.Price);

        await _repo.AddAsync(order, ct);
        await _uow.CommitAsync(ct);

        return order.Id;
    }
}

// Query
public record GetOrderByIdQuery(Guid OrderId) : IRequest<OrderDto?>;

public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderDto?>
{
    private readonly IOrderReadRepository _readRepo;

    public GetOrderByIdQueryHandler(IOrderReadRepository readRepo) => _readRepo = readRepo;

    public async Task<OrderDto?> Handle(GetOrderByIdQuery query, CancellationToken ct)
    {
        return await _readRepo.GetByIdAsync(query.OrderId, ct);
    }
}
```

### INotification / INotificationHandler — события «один-ко-многим»

```csharp
// Notification (доменное событие)
public record OrderCreatedNotification(Guid OrderId, Guid CustomerId) : INotification;

// Несколько обработчиков для одного события
public class SendOrderConfirmationEmail : INotificationHandler<OrderCreatedNotification>
{
    private readonly IEmailService _email;

    public SendOrderConfirmationEmail(IEmailService email) => _email = email;

    public async Task Handle(OrderCreatedNotification n, CancellationToken ct)
    {
        await _email.SendOrderCreatedAsync(n.OrderId, n.CustomerId, ct);
    }
}

public class UpdateInventoryReservation : INotificationHandler<OrderCreatedNotification>
{
    private readonly IInventoryService _inventory;

    public UpdateInventoryReservation(IInventoryService inventory) => _inventory = inventory;

    public async Task Handle(OrderCreatedNotification n, CancellationToken ct)
    {
        await _inventory.ReserveForOrderAsync(n.OrderId, ct);
    }
}

public class UpdateAnalyticsDashboard : INotificationHandler<OrderCreatedNotification>
{
    private readonly IAnalyticsService _analytics;

    public UpdateAnalyticsDashboard(IAnalyticsService analytics) => _analytics = analytics;

    public async Task Handle(OrderCreatedNotification n, CancellationToken ct)
    {
        await _analytics.RecordNewOrderAsync(n.OrderId, ct);
    }
}
```

### Использование в контроллере

```csharp
[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrdersController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<ActionResult<Guid>> Create(
        [FromBody] CreateOrderCommand command, CancellationToken ct)
    {
        var orderId = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = orderId }, orderId);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetById(Guid id, CancellationToken ct)
    {
        var order = await _mediator.Send(new GetOrderByIdQuery(id), ct);
        return order is null ? NotFound() : Ok(order);
    }
}
```

---

## 6. Pipeline Behaviors

Pipeline Behaviors в MediatR работают как middleware: каждый запрос проходит через цепочку поведений до и после хендлера.

```
Request → Logging → Validation → Transaction → Handler → Response
```

### Регистрация

```csharp
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Порядок регистрации = порядок выполнения
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
});
```

### Logging Behavior

```csharp
public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("Handling {RequestName}: {@Request}", requestName, request);

        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        _logger.LogInformation("Handled {RequestName} in {ElapsedMs}ms",
            requestName, sw.ElapsedMilliseconds);

        return response;
    }
}
```

### Validation Behavior (FluentValidation)

```csharp
public class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, ct))))
            .SelectMany(result => result.Errors)
            .Where(error => error is not null)
            .ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next();
    }
}
```

### Transaction Behavior

```csharp
// Маркерный интерфейс для команд, требующих транзакции
public interface ITransactionalCommand { }

public class TransactionBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(IUnitOfWork uow,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (request is not ITransactionalCommand)
            return await next();

        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("Opening transaction for {RequestName}", requestName);

        await using var transaction = await _uow.BeginTransactionAsync(ct);
        try
        {
            var response = await next();
            await _uow.CommitAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Transaction committed for {RequestName}", requestName);
            return response;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            _logger.LogWarning("Transaction rolled back for {RequestName}", requestName);
            throw;
        }
    }
}
```

---

## 7. Command: создание, валидация, обработка

### Структура Command

```csharp
// Команда как immutable record — чистые данные + намерение
public record ConfirmOrderCommand(Guid OrderId) : IRequest<Unit>, ITransactionalCommand;

public record AddOrderLineCommand(
    Guid OrderId,
    Guid ProductId,
    int Quantity,
    decimal UnitPrice
) : IRequest<Guid>, ITransactionalCommand;
```

### Валидация через FluentValidation

```csharp
public class AddOrderLineCommandValidator : AbstractValidator<AddOrderLineCommand>
{
    public AddOrderLineCommandValidator(IProductRepository productRepo)
    {
        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("OrderId обязателен");

        RuleFor(x => x.ProductId)
            .NotEmpty()
            .MustAsync(async (productId, ct) =>
                await productRepo.ExistsAsync(productId, ct))
            .WithMessage("Товар не найден");

        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 1000)
            .WithMessage("Количество должно быть от 1 до 1000");

        RuleFor(x => x.UnitPrice)
            .GreaterThan(0)
            .WithMessage("Цена должна быть положительной");
    }
}
```

### Обработка Command

```csharp
public class AddOrderLineCommandHandler : IRequestHandler<AddOrderLineCommand, Guid>
{
    private readonly IOrderWriteRepository _repo;
    private readonly IMediator _mediator;

    public AddOrderLineCommandHandler(IOrderWriteRepository repo, IMediator mediator)
    {
        _repo = repo;
        _mediator = mediator;
    }

    public async Task<Guid> Handle(AddOrderLineCommand cmd, CancellationToken ct)
    {
        // 1. Загрузить агрегат
        var order = await _repo.GetByIdAsync(cmd.OrderId, ct)
            ?? throw new NotFoundException(nameof(Order), cmd.OrderId);

        // 2. Выполнить бизнес-операцию (инварианты проверяются внутри агрегата)
        var lineId = order.AddLine(cmd.ProductId, cmd.Quantity, cmd.UnitPrice);

        // 3. Сохранить
        await _repo.SaveAsync(order, ct);

        // 4. Опубликовать событие
        await _mediator.Publish(new OrderLineAddedNotification(
            cmd.OrderId, cmd.ProductId, cmd.Quantity), ct);

        return lineId;
    }
}
```

### Обработка ошибок валидации в middleware

```csharp
public class ValidationExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;

    public ValidationExceptionHandlerMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            var errors = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            await context.Response.WriteAsJsonAsync(new
            {
                Title = "Ошибка валидации",
                Status = 400,
                Errors = errors
            });
        }
    }
}
```

---

## 8. Query: оптимизация чтения, Read Models, Projections

### Read Model — плоский DTO, оптимизированный для конкретного экрана

```csharp
// Не доменная сущность, а проекция данных для UI
public class OrderDetailReadModel
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OrderLineReadModel> Lines { get; set; } = new();
}

public class OrderLineReadModel
{
    public Guid Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
```

### Query с фильтрацией и пагинацией

```csharp
public record GetOrdersPagedQuery(
    int Page = 0,
    int PageSize = 20,
    OrderStatus? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? SortBy = "CreatedAt",
    bool Descending = true
) : IRequest<PagedResult<OrderSummaryReadModel>>;

public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNext => Page < TotalPages - 1;
    public bool HasPrevious => Page > 0;
}
```

### Оптимизированный Query Handler через Dapper

```csharp
public class GetOrdersPagedQueryHandler
    : IRequestHandler<GetOrdersPagedQuery, PagedResult<OrderSummaryReadModel>>
{
    private readonly IDbConnection _db;

    public GetOrdersPagedQueryHandler(IDbConnection db) => _db = db;

    public async Task<PagedResult<OrderSummaryReadModel>> Handle(
        GetOrdersPagedQuery q, CancellationToken ct)
    {
        var whereClause = "WHERE 1=1";
        var parameters = new DynamicParameters();

        if (q.Status.HasValue)
        {
            whereClause += " AND o.Status = @Status";
            parameters.Add("Status", q.Status.Value.ToString());
        }
        if (q.FromDate.HasValue)
        {
            whereClause += " AND o.CreatedAt >= @FromDate";
            parameters.Add("FromDate", q.FromDate.Value);
        }
        if (q.ToDate.HasValue)
        {
            whereClause += " AND o.CreatedAt <= @ToDate";
            parameters.Add("ToDate", q.ToDate.Value);
        }

        var orderClause = q.SortBy switch
        {
            "TotalAmount" => "o.TotalAmount",
            "Status" => "o.Status",
            _ => "o.CreatedAt"
        };
        orderClause += q.Descending ? " DESC" : " ASC";

        // Два запроса в одном round-trip
        var sql = $"""
            SELECT COUNT(*) FROM Orders o {whereClause};

            SELECT o.Id, c.Name AS CustomerName, o.TotalAmount,
                   o.Status AS StatusDisplay, o.CreatedAt
            FROM Orders o
            JOIN Customers c ON c.Id = o.CustomerId
            {whereClause}
            ORDER BY {orderClause}
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
            """;

        parameters.Add("Skip", q.Page * q.PageSize);
        parameters.Add("Take", q.PageSize);

        using var multi = await _db.QueryMultipleAsync(sql, parameters);
        var totalCount = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<OrderSummaryReadModel>()).ToList();

        return new PagedResult<OrderSummaryReadModel>(items, totalCount, q.Page, q.PageSize);
    }
}
```

### Projections — построение Read Model из событий

```csharp
// Проекция подписывается на доменные события и обновляет Read Store
public class OrderDashboardProjection :
    INotificationHandler<OrderCreatedNotification>,
    INotificationHandler<OrderConfirmedNotification>,
    INotificationHandler<OrderCancelledNotification>
{
    private readonly IOrderReadStore _store;

    public OrderDashboardProjection(IOrderReadStore store) => _store = store;

    public async Task Handle(OrderCreatedNotification n, CancellationToken ct)
    {
        await _store.UpsertAsync(new OrderSummaryReadModel
        {
            Id = n.OrderId,
            CustomerName = n.CustomerName,
            Status = "Draft",
            TotalAmount = 0,
            CreatedAt = n.CreatedAt
        }, ct);
    }

    public async Task Handle(OrderConfirmedNotification n, CancellationToken ct)
    {
        await _store.UpdateFieldAsync(n.OrderId, x => x.Status, "Confirmed", ct);
    }

    public async Task Handle(OrderCancelledNotification n, CancellationToken ct)
    {
        await _store.UpdateFieldAsync(n.OrderId, x => x.Status, "Cancelled", ct);
    }
}
```

---

## 9. Eventual Consistency — проблемы и решения

При раздельных хранилищах для чтения и записи возникает задержка синхронизации.

### Типичная проблема

```
1. Пользователь создаёт заказ          → Command записывает в Write Store
2. Пользователь сразу открывает список → Query читает из Read Store
3. Read Store ещё не обновился         → Заказа нет в списке!
```

### Решение 1: возвращать результат из Command

```csharp
// Вместо перенаправления на список — возвращаем созданный объект
[HttpPost]
public async Task<ActionResult<OrderDto>> Create(
    [FromBody] CreateOrderCommand command, CancellationToken ct)
{
    var orderId = await _mediator.Send(command, ct);

    // Читаем из Write Model (гарантированно актуально)
    var order = await _mediator.Send(new GetOrderFromWriteStoreQuery(orderId), ct);
    return CreatedAtAction(nameof(GetById), new { id = orderId }, order);
}
```

### Решение 2: синхронное обновление Read Model

```csharp
public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Guid>
{
    private readonly IOrderWriteRepository _writeRepo;
    private readonly IOrderReadStore _readStore;  // обновляем в той же транзакции

    public async Task<Guid> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        var order = Order.Create(cmd.CustomerId);
        await _writeRepo.AddAsync(order, ct);

        // Синхронно обновляем Read Store в той же транзакции
        await _readStore.UpsertAsync(OrderMapper.ToReadModel(order), ct);

        return order.Id;
    }
}
```

### Решение 3: Polling / Pull-based проекция

```csharp
// Background Service периодически синхронизирует Read Store
public class ReadModelSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var syncer = scope.ServiceProvider.GetRequiredService<IReadModelSyncer>();

            var lastPosition = await syncer.GetLastProcessedPositionAsync(ct);
            var events = await syncer.GetEventsAfterAsync(lastPosition, batchSize: 100, ct);

            foreach (var @event in events)
            {
                await syncer.ProjectAsync(@event, ct);
                await syncer.SavePositionAsync(@event.Position, ct);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        }
    }
}
```

### Решение 4: UI-подход (optimistic UI)

```csharp
// На фронтенде: добавляем заказ в локальный стейт сразу после успешного POST
// Через несколько секунд — обновляем список с сервера
```

---

## 10. Материализованные представления (Materialized Views)

Материализованное представление — это предвычисленная, денормализованная копия данных, хранимая в read-оптимизированном виде.

### SQL View vs Materialized View в контексте CQRS

```sql
-- SQL View — вычисляется на каждый запрос (медленно для сложных JOIN)
CREATE VIEW vw_OrderSummary AS
SELECT o.Id, c.Name, SUM(ol.Qty * ol.Price) AS Total
FROM Orders o
JOIN Customers c ON c.Id = o.CustomerId
JOIN OrderLines ol ON ol.OrderId = o.Id
GROUP BY o.Id, c.Name;

-- Materialized View (PostgreSQL) — хранится на диске, обновляется по расписанию
CREATE MATERIALIZED VIEW mv_OrderSummary AS
SELECT o.Id, c.Name, SUM(ol.Qty * ol.Price) AS Total
FROM Orders o
JOIN Customers c ON c.Id = o.CustomerId
JOIN OrderLines ol ON ol.OrderId = o.Id
GROUP BY o.Id, c.Name;

-- Обновление
REFRESH MATERIALIZED VIEW CONCURRENTLY mv_OrderSummary;
```

### Реализация на уровне приложения

```csharp
// Отдельная таблица — Read Model, обновляемая проекциями
public class OrderDashboardView
{
    public Guid OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int LineCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
}

// EF Core конфигурация для Read-таблицы
public class OrderDashboardViewConfiguration : IEntityTypeConfiguration<OrderDashboardView>
{
    public void Configure(EntityTypeBuilder<OrderDashboardView> builder)
    {
        builder.ToTable("OrderDashboardViews"); // Отдельная таблица
        builder.HasKey(x => x.OrderId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.LastModified);
        builder.HasIndex(x => x.CustomerName);
    }
}

// Обновление материализованного представления при изменении заказа
public class RefreshOrderDashboardHandler : INotificationHandler<OrderChangedNotification>
{
    private readonly AppDbContext _db;

    public RefreshOrderDashboardHandler(AppDbContext db) => _db = db;

    public async Task Handle(OrderChangedNotification n, CancellationToken ct)
    {
        var data = await _db.Orders
            .Where(o => o.Id == n.OrderId)
            .Select(o => new OrderDashboardView
            {
                OrderId = o.Id,
                CustomerName = o.Customer.Name,
                TotalAmount = o.Lines.Sum(l => l.Quantity * l.UnitPrice),
                LineCount = o.Lines.Count,
                Status = o.Status.ToString(),
                LastModified = DateTime.UtcNow
            })
            .SingleAsync(ct);

        var existing = await _db.Set<OrderDashboardView>()
            .FindAsync(new object[] { n.OrderId }, ct);

        if (existing is null)
            _db.Set<OrderDashboardView>().Add(data);
        else
            _db.Entry(existing).CurrentValues.SetValues(data);

        await _db.SaveChangesAsync(ct);
    }
}
```

### Использование Redis как материализованного Read Store

```csharp
public class RedisOrderReadStore : IOrderReadStore
{
    private readonly IDatabase _redis;
    private readonly JsonSerializerOptions _jsonOptions;

    public RedisOrderReadStore(IConnectionMultiplexer mux)
    {
        _redis = mux.GetDatabase();
        _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    }

    public async Task UpsertAsync(OrderSummaryReadModel model, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(model, _jsonOptions);
        await _redis.StringSetAsync($"order:{model.Id}", json, TimeSpan.FromHours(24));

        // Индекс для списка заказов клиента
        await _redis.SortedSetAddAsync(
            $"customer:{model.CustomerId}:orders",
            model.Id.ToString(),
            model.CreatedAt.Ticks);
    }

    public async Task<OrderSummaryReadModel?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var json = await _redis.StringGetAsync($"order:{id}");
        return json.IsNullOrEmpty
            ? null
            : JsonSerializer.Deserialize<OrderSummaryReadModel>(json!, _jsonOptions);
    }
}
```

---

## 11. Когда CQRS оправдан, а когда — оверинжиниринг

### CQRS оправдан

| Сценарий | Почему CQRS помогает |
|----------|---------------------|
| Нагрузка на чтение >> нагрузка на запись | Раздельное масштабирование read-реплик |
| Сложная доменная логика | Command-хендлеры изолируют бизнес-правила |
| Разные модели для чтения | Несколько Read Model для разных экранов |
| Событийная архитектура | Естественная интеграция с Event Sourcing |
| Команда > 5 разработчиков | Разные команды работают над read и write |

### CQRS — это оверинжиниринг

| Сценарий | Почему CQRS вредит |
|----------|-------------------|
| CRUD-приложение | Дублирование кода без выгоды |
| Прототип / MVP | Замедляет разработку |
| Простая доменная модель | Один класс-сервис справится лучше |
| Маленькая команда (1-2 человека) | Поддержка двух моделей — двойная работа |
| Строгая консистентность обязательна | Eventual consistency добавляет сложность |

### Градации внедрения

```csharp
// Уровень 0: Нет CQRS — обычный сервис
public class OrderService
{
    public Task<OrderDto> GetByIdAsync(Guid id) { /* ... */ }
    public Task<Guid> CreateAsync(CreateOrderRequest req) { /* ... */ }
}

// Уровень 1: Логическое разделение (одна БД, разные модели)
// — Разные хендлеры для Command и Query
// — Одна БД, одна таблица
// — Самый частый и разумный вариант

// Уровень 2: Раздельные хранилища
// — Write: PostgreSQL с нормализованной схемой
// — Read: Elasticsearch / Redis / denormalized tables
// — Синхронизация через события

// Уровень 3: CQRS + Event Sourcing
// — Write: Event Store
// — Read: Проекции в MongoDB / Elasticsearch
// — Полная история изменений, rebuild проекций
```

---

## 12. Полный пример: Order Aggregate с Commands и Queries

### Доменная модель (Write Side)

```csharp
public class Order : AggregateRoot
{
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }

    private readonly List<OrderLine> _lines = new();
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    public decimal TotalAmount => _lines.Sum(l => l.Total);

    private Order() { } // Для EF Core

    public static Order Create(Guid customerId)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Status = OrderStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
        order.AddDomainEvent(new OrderCreatedDomainEvent(order.Id, customerId));
        return order;
    }

    public Guid AddLine(Guid productId, int quantity, decimal unitPrice)
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException("Нельзя изменять подтверждённый заказ");
        if (quantity <= 0)
            throw new DomainException("Количество должно быть положительным");
        if (unitPrice <= 0)
            throw new DomainException("Цена должна быть положительной");
        if (_lines.Count >= 50)
            throw new DomainException("Максимум 50 позиций в заказе");

        var line = new OrderLine(Guid.NewGuid(), productId, quantity, unitPrice);
        _lines.Add(line);
        AddDomainEvent(new OrderLineAddedDomainEvent(Id, line.Id, productId, quantity));
        return line.Id;
    }

    public void RemoveLine(Guid lineId)
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException("Нельзя изменять подтверждённый заказ");

        var line = _lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new DomainException($"Позиция {lineId} не найдена");

        _lines.Remove(line);
    }

    public void Confirm()
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException("Заказ уже подтверждён или отменён");
        if (!_lines.Any())
            throw new DomainException("Нельзя подтвердить пустой заказ");

        Status = OrderStatus.Confirmed;
        ConfirmedAt = DateTime.UtcNow;
        AddDomainEvent(new OrderConfirmedDomainEvent(Id, TotalAmount));
    }

    public void Cancel(string reason)
    {
        if (Status == OrderStatus.Cancelled)
            throw new DomainException("Заказ уже отменён");

        Status = OrderStatus.Cancelled;
        AddDomainEvent(new OrderCancelledDomainEvent(Id, reason));
    }
}

public class OrderLine
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Total => Quantity * UnitPrice;

    private OrderLine() { }

    public OrderLine(Guid id, Guid productId, int quantity, decimal unitPrice)
    {
        Id = id;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}

public enum OrderStatus { Draft, Confirmed, Cancelled }
```

### Commands

```csharp
// --- Create Order ---
public record CreateOrderCommand(
    Guid CustomerId,
    List<OrderItemDto> Items
) : IRequest<Guid>, ITransactionalCommand;

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty().WithMessage("Заказ не может быть пустым");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.UnitPrice).GreaterThan(0);
        });
    }
}

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Guid>
{
    private readonly IOrderWriteRepository _repo;

    public CreateOrderCommandHandler(IOrderWriteRepository repo) => _repo = repo;

    public async Task<Guid> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        var order = Order.Create(cmd.CustomerId);
        foreach (var item in cmd.Items)
            order.AddLine(item.ProductId, item.Quantity, item.UnitPrice);

        await _repo.AddAsync(order, ct);
        return order.Id;
    }
}

// --- Confirm Order ---
public record ConfirmOrderCommand(Guid OrderId) : IRequest<Unit>, ITransactionalCommand;

public class ConfirmOrderCommandHandler : IRequestHandler<ConfirmOrderCommand, Unit>
{
    private readonly IOrderWriteRepository _repo;

    public ConfirmOrderCommandHandler(IOrderWriteRepository repo) => _repo = repo;

    public async Task<Unit> Handle(ConfirmOrderCommand cmd, CancellationToken ct)
    {
        var order = await _repo.GetByIdAsync(cmd.OrderId, ct)
            ?? throw new NotFoundException(nameof(Order), cmd.OrderId);

        order.Confirm();
        await _repo.SaveAsync(order, ct);
        return Unit.Value;
    }
}

// --- Cancel Order ---
public record CancelOrderCommand(Guid OrderId, string Reason) : IRequest<Unit>, ITransactionalCommand;

public class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, Unit>
{
    private readonly IOrderWriteRepository _repo;

    public CancelOrderCommandHandler(IOrderWriteRepository repo) => _repo = repo;

    public async Task<Unit> Handle(CancelOrderCommand cmd, CancellationToken ct)
    {
        var order = await _repo.GetByIdAsync(cmd.OrderId, ct)
            ?? throw new NotFoundException(nameof(Order), cmd.OrderId);

        order.Cancel(cmd.Reason);
        await _repo.SaveAsync(order, ct);
        return Unit.Value;
    }
}
```

### Queries

```csharp
// --- Get Order By Id ---
public record GetOrderByIdQuery(Guid OrderId) : IRequest<OrderDetailReadModel?>;

public class GetOrderByIdQueryHandler
    : IRequestHandler<GetOrderByIdQuery, OrderDetailReadModel?>
{
    private readonly IDbConnection _db;

    public GetOrderByIdQueryHandler(IDbConnection db) => _db = db;

    public async Task<OrderDetailReadModel?> Handle(
        GetOrderByIdQuery query, CancellationToken ct)
    {
        const string sql = """
            SELECT o.Id, c.Name AS CustomerName, c.Email AS CustomerEmail,
                   o.Status, o.TotalAmount, o.CreatedAt
            FROM Orders o
            JOIN Customers c ON c.Id = o.CustomerId
            WHERE o.Id = @OrderId;

            SELECT ol.Id, p.Name AS ProductName, ol.Quantity, ol.UnitPrice,
                   (ol.Quantity * ol.UnitPrice) AS LineTotal
            FROM OrderLines ol
            JOIN Products p ON p.Id = ol.ProductId
            WHERE ol.OrderId = @OrderId;
            """;

        using var multi = await _db.QueryMultipleAsync(sql, new { query.OrderId });

        var order = await multi.ReadSingleOrDefaultAsync<OrderDetailReadModel>();
        if (order is null) return null;

        order.Lines = (await multi.ReadAsync<OrderLineReadModel>()).ToList();
        return order;
    }
}

// --- Get Orders Paged ---
public record GetOrdersPagedQuery(
    int Page = 0,
    int PageSize = 20,
    string? Status = null
) : IRequest<PagedResult<OrderSummaryReadModel>>;
```

### Контроллер

```csharp
[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrdersController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrderCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderDetailReadModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var order = await _mediator.Send(new GetOrderByIdQuery(id), ct);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ConfirmOrderCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Cancel(
        Guid id, [FromBody] CancelOrderRequest request, CancellationToken ct)
    {
        await _mediator.Send(new CancelOrderCommand(id, request.Reason), ct);
        return NoContent();
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderSummaryReadModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] GetOrdersPagedQuery query, CancellationToken ct)
    {
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }
}
```

---

## 13. Тестирование CQRS (Unit + Integration)

### Unit-тесты домена (агрегата)

```csharp
public class OrderTests
{
    [Fact]
    public void Create_ShouldSetDraftStatus()
    {
        var order = Order.Create(Guid.NewGuid());

        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.NotEqual(Guid.Empty, order.Id);
    }

    [Fact]
    public void AddLine_WhenDraft_ShouldAddLine()
    {
        var order = Order.Create(Guid.NewGuid());

        var lineId = order.AddLine(Guid.NewGuid(), quantity: 2, unitPrice: 100m);

        Assert.Single(order.Lines);
        Assert.Equal(200m, order.TotalAmount);
        Assert.NotEqual(Guid.Empty, lineId);
    }

    [Fact]
    public void AddLine_WhenConfirmed_ShouldThrow()
    {
        var order = Order.Create(Guid.NewGuid());
        order.AddLine(Guid.NewGuid(), 1, 100m);
        order.Confirm();

        var ex = Assert.Throws<DomainException>(() =>
            order.AddLine(Guid.NewGuid(), 1, 50m));
        Assert.Contains("подтверждённый", ex.Message);
    }

    [Fact]
    public void Confirm_WhenEmpty_ShouldThrow()
    {
        var order = Order.Create(Guid.NewGuid());

        Assert.Throws<DomainException>(() => order.Confirm());
    }

    [Fact]
    public void Confirm_ShouldRaiseDomainEvent()
    {
        var order = Order.Create(Guid.NewGuid());
        order.AddLine(Guid.NewGuid(), 1, 100m);

        order.Confirm();

        Assert.Contains(order.DomainEvents,
            e => e is OrderConfirmedDomainEvent);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ShouldThrow()
    {
        var order = Order.Create(Guid.NewGuid());
        order.AddLine(Guid.NewGuid(), 1, 100m);
        order.Cancel("Тест");

        Assert.Throws<DomainException>(() => order.Cancel("Повторно"));
    }
}
```

### Unit-тесты Command Handler

```csharp
public class CreateOrderCommandHandlerTests
{
    private readonly Mock<IOrderWriteRepository> _repoMock = new();
    private readonly CreateOrderCommandHandler _handler;

    public CreateOrderCommandHandlerTests()
    {
        _handler = new CreateOrderCommandHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldCreateOrder()
    {
        // Arrange
        var command = new CreateOrderCommand(
            CustomerId: Guid.NewGuid(),
            Items: new List<OrderItemDto>
            {
                new(Guid.NewGuid(), Quantity: 2, UnitPrice: 100m),
                new(Guid.NewGuid(), Quantity: 1, UnitPrice: 50m)
            });

        _repoMock.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var orderId = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, orderId);
        _repoMock.Verify(r => r.AddAsync(
            It.Is<Order>(o => o.Lines.Count == 2 && o.TotalAmount == 250m),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

### Unit-тесты Validator

```csharp
public class CreateOrderCommandValidatorTests
{
    private readonly CreateOrderCommandValidator _validator = new();

    [Fact]
    public async Task Validate_EmptyCustomerId_ShouldFail()
    {
        var command = new CreateOrderCommand(
            Guid.Empty,
            new List<OrderItemDto> { new(Guid.NewGuid(), 1, 100m) });

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "CustomerId");
    }

    [Fact]
    public async Task Validate_EmptyItems_ShouldFail()
    {
        var command = new CreateOrderCommand(Guid.NewGuid(), new List<OrderItemDto>());

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Items");
    }

    [Fact]
    public async Task Validate_ValidCommand_ShouldPass()
    {
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            new List<OrderItemDto> { new(Guid.NewGuid(), 5, 99.99m) });

        var result = await _validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }
}
```

### Unit-тесты Pipeline Behavior

```csharp
public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_WithValidationErrors_ShouldThrowValidationException()
    {
        // Arrange
        var validator = new InlineValidator<TestCommand>();
        validator.RuleFor(x => x.Name).NotEmpty();

        var behavior = new ValidationBehavior<TestCommand, Unit>(
            new[] { validator });

        var command = new TestCommand { Name = "" };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(command, () => Task.FromResult(Unit.Value), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NoValidators_ShouldCallNext()
    {
        var behavior = new ValidationBehavior<TestCommand, Unit>(
            Enumerable.Empty<IValidator<TestCommand>>());

        var nextCalled = false;
        await behavior.Handle(
            new TestCommand { Name = "test" },
            () => { nextCalled = true; return Task.FromResult(Unit.Value); },
            CancellationToken.None);

        Assert.True(nextCalled);
    }
}
```

### Integration-тесты с WebApplicationFactory

```csharp
public class OrdersIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public OrdersIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Заменяем реальную БД на In-Memory
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(opts =>
                    opts.UseInMemoryDatabase("TestDb_" + Guid.NewGuid()));
            });
        }).CreateClient();
    }

    [Fact]
    public async Task CreateOrder_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            new List<OrderItemDto> { new(Guid.NewGuid(), 1, 100m) });

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", command);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var orderId = await response.Content.ReadFromJsonAsync<Guid>();
        Assert.NotEqual(Guid.Empty, orderId);
    }

    [Fact]
    public async Task CreateOrder_EmptyItems_ReturnsBadRequest()
    {
        var command = new CreateOrderCommand(Guid.NewGuid(), new List<OrderItemDto>());

        var response = await _client.PostAsJsonAsync("/api/orders", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmOrder_ExistingDraftOrder_ReturnsNoContent()
    {
        // Arrange — создаём заказ
        var createCommand = new CreateOrderCommand(
            Guid.NewGuid(),
            new List<OrderItemDto> { new(Guid.NewGuid(), 1, 100m) });
        var createResponse = await _client.PostAsJsonAsync("/api/orders", createCommand);
        var orderId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        // Act — подтверждаем
        var response = await _client.PostAsync($"/api/orders/{orderId}/confirm", null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_AfterCreate_ReturnsOrder()
    {
        // Arrange
        var createCommand = new CreateOrderCommand(
            Guid.NewGuid(),
            new List<OrderItemDto> { new(Guid.NewGuid(), 2, 50m) });
        var createResponse = await _client.PostAsJsonAsync("/api/orders", createCommand);
        var orderId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        // Act
        var response = await _client.GetAsync($"/api/orders/{orderId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderDetailReadModel>();
        Assert.NotNull(order);
        Assert.Equal(100m, order!.TotalAmount);
    }
}
```

---

## 14. Вопросы на собеседовании с ответами

### Вопрос 1: Что такое CQRS и зачем он нужен?

**Ответ:** CQRS (Command Query Responsibility Segregation) — архитектурный паттерн, разделяющий модель записи (Commands) и модель чтения (Queries). Это необходимо, потому что требования к записи (строгая валидация, бизнес-правила, транзакционность) и чтению (скорость, денормализация, различные представления) фундаментально различны. Разделение позволяет оптимизировать каждую сторону независимо, масштабировать чтение горизонтально и упрощать код за счёт изоляции ответственности.

### Вопрос 2: Чем CQS отличается от CQRS?

**Ответ:** CQS — это принцип уровня метода: метод либо возвращает данные (query), либо изменяет состояние (command), но не оба сразу. CQRS — это архитектурный паттерн, который выносит этот принцип на уровень системы: отдельные объекты, модели, потенциально хранилища для чтения и записи. CQS — принцип проектирования, CQRS — архитектурное решение.

### Вопрос 3: Обязательно ли использовать Event Sourcing с CQRS?

**Ответ:** Нет. Это две независимые концепции, которые хорошо работают вместе, но не зависят друг от друга. Самый распространённый вариант CQRS — одна реляционная база с разными моделями для чтения и записи. Event Sourcing добавляет значительную сложность и нужен только когда важна полная история изменений (аудит, финансы, аналитика).

### Вопрос 4: Как MediatR реализует CQRS? Какие абстракции он предоставляет?

**Ответ:** MediatR предоставляет: `IRequest<T>` / `IRequestHandler<TRequest, TResponse>` для паттерна «один запрос — один обработчик» (используется для Commands и Queries); `INotification` / `INotificationHandler<T>` для паттерна «одно событие — много подписчиков» (используется для доменных событий); `IPipelineBehavior<TRequest, TResponse>` для сквозной функциональности (валидация, логирование, транзакции). MediatR убирает прямую зависимость между отправителем и обработчиком через паттерн Mediator.

### Вопрос 5: Что такое Pipeline Behavior и как он связан с CQRS?

**Ответ:** Pipeline Behavior — это декоратор (middleware), который оборачивает выполнение каждого запроса в MediatR. Запрос проходит через цепочку Behavior-ов до и после обработчика. Типичные примеры: `ValidationBehavior` (запускает FluentValidation до хендлера), `LoggingBehavior` (логирует запрос и время выполнения), `TransactionBehavior` (оборачивает команды в транзакцию). Это аналог middleware в ASP.NET, но на уровне CQRS-хендлеров.

### Вопрос 6: Что такое Eventual Consistency и как с ней справляться в CQRS?

**Ответ:** Eventual Consistency — модель, в которой после записи данные не сразу доступны в Read Store. Это неизбежно при раздельных хранилищах. Решения: (1) возвращать результат из команды, чтобы UI не обращался к Read Store сразу; (2) синхронно обновлять Read Store в той же транзакции; (3) Optimistic UI на фронтенде — показывать ожидаемый результат до подтверждения; (4) Subscribe на события и обновлять UI через WebSocket / SignalR когда Read Store готов.

### Вопрос 7: Как организовать валидацию в CQRS-архитектуре?

**Ответ:** Валидация в CQRS разделяется на два уровня. (1) Валидация команды (input validation) — через FluentValidation в Pipeline Behavior: проверка формата, обязательности полей, допустимых диапазонов. Выполняется до хендлера. (2) Доменная валидация (бизнес-правила) — внутри агрегата: «нельзя подтвердить пустой заказ», «нельзя превысить кредитный лимит». Бросает доменные исключения. Первый уровень отвечает за корректность ввода, второй — за бизнес-инварианты.

### Вопрос 8: Что такое Read Model (проекция) и чем она отличается от доменной модели?

**Ответ:** Read Model — это денормализованный DTO, оптимизированный для конкретного сценария чтения (экрана, отчёта). Она не содержит бизнес-логики, не защищает инварианты. Доменная модель (Write Model) — богатый объект с поведением, инкапсуляцией и инвариантами. Для одной доменной модели может быть несколько Read Model: список заказов (OrderSummary), детали заказа (OrderDetail), аналитика (OrderAnalytics). Read Model обновляется проекциями из доменных событий.

### Вопрос 9: Когда CQRS — это оверинжиниринг?

**Ответ:** CQRS не нужен для: (1) CRUD-приложений без сложной бизнес-логики; (2) прототипов и MVP, где важна скорость разработки; (3) маленьких команд (1-2 человека), где поддержка двух моделей — непозволительная роскошь; (4) систем со строгими требованиями к консистентности, где eventual consistency неприемлема; (5) приложений с равномерной нагрузкой на чтение и запись без необходимости раздельного масштабирования.

### Вопрос 10: Как тестировать CQRS-приложение?

**Ответ:** Тестирование идёт на нескольких уровнях. (1) Unit-тесты агрегата — проверка бизнес-правил и инвариантов без зависимостей. (2) Unit-тесты хендлеров — мокаем репозитории, проверяем оркестрацию. (3) Unit-тесты валидаторов — проверяем правила FluentValidation. (4) Unit-тесты Pipeline Behavior — проверяем, что цепочка вызывается корректно. (5) Integration-тесты — через WebApplicationFactory с In-Memory DB проверяем полный цикл: HTTP-запрос -> контроллер -> MediatR -> хендлер -> БД -> ответ.

### Вопрос 11: Можно ли Command возвращать данные? Не нарушает ли это CQS?

**Ответ:** Формально CQS запрещает командам возвращать данные. На практике в CQRS команда часто возвращает идентификатор созданного ресурса (`Task<Guid>`) или результат операции (`Task<Result<T>>`). Это прагматичный компромисс: клиенту нужен Id для редиректа или дальнейших действий. Грег Янг (автор CQRS) также признавал это допустимым. Важно не возвращать из команды сложные модели для отображения — для этого есть Query.

### Вопрос 12: Как обеспечить идемпотентность команд в CQRS?

**Ответ:** Идемпотентность критична для надёжности, особенно при retry-механизмах. Подходы: (1) клиент передаёт уникальный `CommandId` (обычно GUID), сервер хранит список обработанных Id и отклоняет дубликаты; (2) использование optimistic concurrency (version/ETag) — повторная команда не пройдёт из-за конфликта версий; (3) для финансовых операций — идемпотентный ключ (idempotency key) в заголовке HTTP-запроса. Пример:

```csharp
public class IdempotencyBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IIdempotentCommand<TResponse>
{
    private readonly IIdempotencyStore _store;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (await _store.ExistsAsync(request.CommandId, ct))
            return await _store.GetResultAsync<TResponse>(request.CommandId, ct);

        var result = await next();
        await _store.SaveAsync(request.CommandId, result, ct);
        return result;
    }
}
```

### Вопрос 13: Как масштабировать Read и Write стороны независимо?

**Ответ:** Write Store масштабируется вертикально (мощнее сервер) или через шардинг по ключу агрегата. Read Store масштабируется горизонтально: read-реплики SQL, Redis-кэш, Elasticsearch для полнотекстового поиска, CDN для статических проекций. Ключевое преимущество CQRS — нагрузка на чтение (80-90% трафика) не влияет на производительность записи. Можно добавлять read-реплики без изменения write-стороны.

---

## Дополнительные ресурсы

- Greg Young — «CQRS Documents» (оригинальное описание паттерна)
- Martin Fowler — «CQRS» (bliki)
- Microsoft — «CQRS pattern» (Azure Architecture Center)
- Jimmy Bogard — MediatR (автор библиотеки, GitHub)
- Udi Dahan — «Clarified CQRS» (статья)
- Vaughn Vernon — «Implementing Domain-Driven Design» (глава про CQRS)
