# Domain-Driven Design (DDD) — Подготовка к Senior .NET собеседованию

## Содержание

1. [Введение в DDD](#введение-в-ddd)
2. [Стратегический DDD](#стратегический-ddd)
3. [Тактический DDD](#тактический-ddd)
4. [Domain Events](#domain-events)
5. [Repository паттерн](#repository-паттерн)
6. [Specification паттерн](#specification-паттерн)
7. [Domain Services vs Application Services](#domain-services-vs-application-services)
8. [Anti-Corruption Layer](#anti-corruption-layer)
9. [Shared Kernel](#shared-kernel)
10. [Anemic Domain Model vs Rich Domain Model](#anemic-domain-model-vs-rich-domain-model)
11. [Организация слоёв](#организация-слоёв)
12. [Сравнение архитектур: Clean / Onion / Hexagonal](#сравнение-архитектур)
13. [Практическая реализация в .NET](#практическая-реализация-в-net)
14. [Примеры кода](#примеры-кода)
15. [Ошибки при внедрении DDD](#ошибки-при-внедрении-ddd)
16. [Вопросы на собеседовании с ответами](#вопросы-на-собеседовании-с-ответами)

---

## Введение в DDD

Domain-Driven Design (проектирование на основе предметной области) — это подход к разработке программного обеспечения, предложенный Эриком Эвансом в 2003 году. Ключевая идея: **сложность программы должна отражать сложность бизнес-домена, а не технической инфраструктуры**.

DDD делится на две части:

- **Стратегический DDD** — определяет границы системы, взаимодействие между подсистемами, единый язык.
- **Тактический DDD** — конкретные паттерны реализации внутри ограниченного контекста.

> **Когда применять DDD:** DDD оправдан при высокой сложности бизнес-логики. Для простых CRUD-приложений он избыточен.

---

## Стратегический DDD

### Bounded Context (Ограниченный контекст)

Bounded Context — это явная граница, внутри которой определённая модель домена имеет чёткое и непротиворечивое значение. Один и тот же термин (например, «Клиент») может означать разное в разных контекстах.

```
┌─────────────────────┐    ┌─────────────────────┐
│   Sales Context     │    │   Billing Context    │
│                     │    │                      │
│  Customer:          │    │  Customer:           │
│  - Name             │    │  - Name              │
│  - Preferences      │    │  - BillingAddress    │
│  - ContactInfo      │    │  - PaymentMethod     │
│  - LoyaltyPoints    │    │  - TaxId             │
└─────────────────────┘    └─────────────────────┘
```

```csharp
// Sales Context
namespace Sales.Domain;

public class Customer
{
    public CustomerId Id { get; private set; }
    public string Name { get; private set; }
    public ContactInfo Contact { get; private set; }
    public LoyaltyPoints Loyalty { get; private set; }

    public void AddLoyaltyPoints(int points)
    {
        Loyalty = Loyalty.Add(points);
    }
}

// Billing Context
namespace Billing.Domain;

public class Customer
{
    public CustomerId Id { get; private set; }
    public string Name { get; private set; }
    public BillingAddress Address { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public TaxId TaxId { get; private set; }

    public Invoice GenerateInvoice(Money amount)
    {
        return new Invoice(this, amount, DateTime.UtcNow);
    }
}
```

### Ubiquitous Language (Единый язык)

Ubiquitous Language — это общий словарь терминов, используемый и разработчиками, и бизнес-экспертами. Код должен «говорить» на языке домена.

**Плохо:**

```csharp
public void Process(int id, int status)
{
    var item = _repo.GetById(id);
    item.Status = status;
    _repo.Save(item);
}
```

**Хорошо (Ubiquitous Language):**

```csharp
public void ApproveOrder(OrderId orderId)
{
    var order = _orderRepository.GetById(orderId);
    order.Approve(); // бизнес-термин
    _orderRepository.Save(order);
}
```

### Context Map (Карта контекстов)

Context Map описывает отношения между Bounded Contexts. Основные типы отношений:

| Паттерн | Описание |
|---------|----------|
| **Partnership** | Два контекста развиваются совместно, команды синхронизируются |
| **Shared Kernel** | Общая часть модели, разделяемая между контекстами |
| **Customer-Supplier** | Один контекст (Supplier) предоставляет данные другому (Customer) |
| **Conformist** | Downstream-контекст полностью принимает модель upstream |
| **Anti-Corruption Layer** | Downstream защищает свою модель трансляционным слоем |
| **Open Host Service** | Контекст предоставляет публичный API (протокол) |
| **Published Language** | Документированный формат обмена данными (JSON Schema, Protobuf) |
| **Separate Ways** | Контексты не интегрируются вовсе |

```csharp
// Пример Context Map в коде: Sales (Customer) -> Inventory (Supplier)
namespace Sales.Infrastructure.Adapters;

/// <summary>
/// Anti-Corruption Layer: адаптирует модель Inventory к модели Sales.
/// </summary>
public class InventoryAdapter : IInventoryService
{
    private readonly InventoryApi.Client _client;

    public InventoryAdapter(InventoryApi.Client client)
    {
        _client = client;
    }

    public async Task<StockAvailability> CheckAvailability(ProductId productId)
    {
        // Вызов внешнего контекста
        var dto = await _client.GetStockAsync(productId.Value);

        // Трансляция в модель Sales
        return new StockAvailability(
            productId,
            quantity: dto.AvailableQty,
            isAvailable: dto.AvailableQty > 0
        );
    }
}
```

---

## Тактический DDD

### Entity (Сущность)

Entity — объект, определяемый своей **идентичностью** (Id), а не атрибутами. Два объекта Entity с одинаковыми полями, но разными Id — разные сущности.

```csharp
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    public TId Id { get; protected set; }

    protected Entity(TId id)
    {
        Id = id;
    }

    // Для EF Core
    protected Entity() { }

    public override bool Equals(object? obj)
    {
        return obj is Entity<TId> entity && Equals(entity);
    }

    public bool Equals(Entity<TId>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
        => Equals(left, right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
        => !Equals(left, right);
}
```

### Value Object (Объект-значение)

Value Object — объект без идентичности, определяемый исключительно своими атрибутами. Он **неизменяемый (immutable)** и сравнивается по значению.

```csharp
public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
            return false;

        return Equals((ValueObject)obj);
    }

    public bool Equals(ValueObject? other)
    {
        if (other is null) return false;
        return GetEqualityComponents()
            .SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Aggregate(0, (hash, component) =>
                HashCode.Combine(hash, component?.GetHashCode() ?? 0));
    }

    public static bool operator ==(ValueObject? left, ValueObject? right)
        => Equals(left, right);

    public static bool operator !=(ValueObject? left, ValueObject? right)
        => !Equals(left, right);
}

// Пример: Money
public sealed class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new DomainException("Сумма не может быть отрицательной.");
        if (string.IsNullOrWhiteSpace(currency))
            throw new DomainException("Валюта обязательна.");

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException("Нельзя складывать разные валюты.");
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException("Нельзя вычитать разные валюты.");
        return new Money(Amount - other.Amount, Currency);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Amount} {Currency}";
}
```

### Aggregate и Aggregate Root

**Aggregate** — кластер связанных объектов (Entity и Value Object), рассматриваемый как единое целое для изменений данных. **Aggregate Root** — единственная точка входа в Aggregate.

Правила Aggregates:

1. Внешний код ссылается **только** на Aggregate Root.
2. Aggregate Root обеспечивает **целостность** всех инвариантов.
3. Между Aggregates — только ссылки по Id (а не прямые объектные ссылки).
4. Одна транзакция изменяет **один** Aggregate.

```csharp
public class Order : Entity<OrderId>, IAggregateRoot
{
    private readonly List<OrderLine> _lines = new();
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public CustomerId CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public Money TotalAmount { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Для EF Core
    private Order() : base() { }

    public Order(OrderId id, CustomerId customerId)
        : base(id)
    {
        CustomerId = customerId;
        Status = OrderStatus.Draft;
        TotalAmount = new Money(0, "RUB");
        CreatedAt = DateTime.UtcNow;
    }

    public void AddLine(ProductId productId, string productName, int quantity, Money unitPrice)
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException("Нельзя добавлять позиции в неактивный заказ.");

        if (quantity <= 0)
            throw new DomainException("Количество должно быть положительным.");

        var existingLine = _lines.FirstOrDefault(l => l.ProductId == productId);
        if (existingLine is not null)
        {
            existingLine.IncreaseQuantity(quantity);
        }
        else
        {
            var line = new OrderLine(productId, productName, quantity, unitPrice);
            _lines.Add(line);
        }

        RecalculateTotal();
    }

    public void RemoveLine(ProductId productId)
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException("Нельзя удалять позиции из неактивного заказа.");

        var line = _lines.FirstOrDefault(l => l.ProductId == productId)
            ?? throw new DomainException($"Позиция с продуктом {productId} не найдена.");

        _lines.Remove(line);
        RecalculateTotal();
    }

    public void Submit()
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException("Заказ можно отправить только из состояния Draft.");

        if (!_lines.Any())
            throw new DomainException("Нельзя отправить пустой заказ.");

        Status = OrderStatus.Submitted;
        AddDomainEvent(new OrderSubmittedEvent(Id, CustomerId, TotalAmount));
    }

    public void Confirm()
    {
        if (Status != OrderStatus.Submitted)
            throw new DomainException("Подтвердить можно только отправленный заказ.");

        Status = OrderStatus.Confirmed;
        AddDomainEvent(new OrderConfirmedEvent(Id));
    }

    public void Cancel(string reason)
    {
        if (Status == OrderStatus.Cancelled)
            throw new DomainException("Заказ уже отменён.");

        if (Status == OrderStatus.Shipped)
            throw new DomainException("Нельзя отменить отправленный заказ.");

        Status = OrderStatus.Cancelled;
        AddDomainEvent(new OrderCancelledEvent(Id, reason));
    }

    private void RecalculateTotal()
    {
        var total = _lines.Aggregate(
            new Money(0, "RUB"),
            (sum, line) => sum.Add(line.LineTotal));
        TotalAmount = total;
    }

    private void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}

// OrderLine — Entity внутри Aggregate (НЕ Aggregate Root)
public class OrderLine : Entity<OrderLineId>
{
    public ProductId ProductId { get; private set; }
    public string ProductName { get; private set; }
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; }
    public Money LineTotal => new(UnitPrice.Amount * Quantity, UnitPrice.Currency);

    private OrderLine() : base() { }

    public OrderLine(ProductId productId, string productName, int quantity, Money unitPrice)
        : base(OrderLineId.CreateNew())
    {
        ProductId = productId;
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public void IncreaseQuantity(int additionalQuantity)
    {
        if (additionalQuantity <= 0)
            throw new DomainException("Дополнительное количество должно быть положительным.");
        Quantity += additionalQuantity;
    }
}
```

### Strongly-Typed Id

```csharp
public readonly record struct OrderId(Guid Value)
{
    public static OrderId CreateNew() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct CustomerId(Guid Value)
{
    public static CustomerId CreateNew() => new(Guid.NewGuid());
}

public readonly record struct ProductId(Guid Value)
{
    public static ProductId CreateNew() => new(Guid.NewGuid());
}

public readonly record struct OrderLineId(Guid Value)
{
    public static OrderLineId CreateNew() => new(Guid.NewGuid());
}
```

---

## Domain Events

Domain Events — уведомления о том, что в домене произошло значимое событие. Они выражаются в прошедшем времени и на языке домена.

```csharp
// Базовый интерфейс
public interface IDomainEvent
{
    DateTime OccurredOn { get; }
    Guid EventId { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public Guid EventId { get; } = Guid.NewGuid();
}

// Конкретные события
public record OrderSubmittedEvent(
    OrderId OrderId,
    CustomerId CustomerId,
    Money TotalAmount) : DomainEvent;

public record OrderConfirmedEvent(OrderId OrderId) : DomainEvent;

public record OrderCancelledEvent(
    OrderId OrderId,
    string Reason) : DomainEvent;
```

### Диспетчеризация Domain Events

```csharp
// Интерфейс диспетчера
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default);
}

// Реализация через MediatR
public class MediatRDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IMediator _mediator;

    public MediatRDomainEventDispatcher(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct)
    {
        foreach (var domainEvent in events)
        {
            await _mediator.Publish(domainEvent, ct);
        }
    }
}

// Обработчик события
public class OrderSubmittedEventHandler : INotificationHandler<OrderSubmittedEvent>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<OrderSubmittedEventHandler> _logger;

    public OrderSubmittedEventHandler(
        IEmailService emailService,
        ILogger<OrderSubmittedEventHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(OrderSubmittedEvent notification, CancellationToken ct)
    {
        _logger.LogInformation(
            "Заказ {OrderId} отправлен клиентом {CustomerId} на сумму {Amount}",
            notification.OrderId,
            notification.CustomerId,
            notification.TotalAmount);

        await _emailService.SendOrderConfirmationAsync(
            notification.CustomerId,
            notification.OrderId,
            ct);
    }
}
```

### Публикация событий при сохранении (SaveChanges)

```csharp
public class AppDbContext : DbContext
{
    private readonly IDomainEventDispatcher _dispatcher;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IDomainEventDispatcher dispatcher) : base(options)
    {
        _dispatcher = dispatcher;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Собираем все события перед сохранением
        var aggregatesWithEvents = ChangeTracker.Entries<IAggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = aggregatesWithEvents
            .SelectMany(a => a.DomainEvents)
            .ToList();

        // Очищаем события у агрегатов
        aggregatesWithEvents.ForEach(a => a.ClearDomainEvents());

        // Сохраняем изменения
        var result = await base.SaveChangesAsync(ct);

        // Публикуем события после успешного сохранения
        await _dispatcher.DispatchAsync(domainEvents, ct);

        return result;
    }
}
```

---

## Repository паттерн

### Отличие от Generic Repository

**Generic Repository** (анти-паттерн в DDD):

```csharp
// ПЛОХО: утечка абстракции, нарушает Aggregate-границы
public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    IQueryable<T> Query(); // утечка IQueryable
    Task AddAsync(T entity);
    void Update(T entity);
    void Delete(T entity);
}
```

Проблемы Generic Repository:

1. **IQueryable утекает наружу** — логика запросов размазывается по всему коду.
2. **Нет Aggregate-границ** — можно загрузить любую Entity отдельно, минуя Aggregate Root.
3. **CRUD-мышление** — не отражает бизнес-операции.
4. **Невозможно оптимизировать** — один интерфейс для всех сущностей.

**DDD Repository** (правильный подход):

```csharp
// Репозиторий работает ТОЛЬКО с Aggregate Root
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct = default);
    Task<Order?> GetByIdWithLinesAsync(OrderId id, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetByCustomerIdAsync(CustomerId customerId, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
    void Remove(Order order);
    Task<bool> ExistsAsync(OrderId id, CancellationToken ct = default);
}

// Реализация
public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _context;

    public OrderRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct)
    {
        return await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<Order?> GetByIdWithLinesAsync(OrderId id, CancellationToken ct)
    {
        return await _context.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<IReadOnlyList<Order>> GetByCustomerIdAsync(
        CustomerId customerId, CancellationToken ct)
    {
        return await _context.Orders
            .Where(o => o.CustomerId == customerId)
            .Include(o => o.Lines)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Order order, CancellationToken ct)
    {
        await _context.Orders.AddAsync(order, ct);
    }

    public void Remove(Order order)
    {
        _context.Orders.Remove(order);
    }

    public async Task<bool> ExistsAsync(OrderId id, CancellationToken ct)
    {
        return await _context.Orders.AnyAsync(o => o.Id == id, ct);
    }
}
```

### Unit of Work

```csharp
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

// AppDbContext уже реализует Unit of Work через SaveChangesAsync
public class AppDbContext : DbContext, IUnitOfWork
{
    // SaveChangesAsync — это и есть Unit of Work
}
```

---

## Specification паттерн

Specification инкапсулирует бизнес-правила фильтрации и может комбинироваться.

```csharp
// Базовая спецификация
public abstract class Specification<T>
{
    public abstract Expression<Func<T, bool>> ToExpression();

    public bool IsSatisfiedBy(T entity)
    {
        return ToExpression().Compile()(entity);
    }

    public Specification<T> And(Specification<T> other)
        => new AndSpecification<T>(this, other);

    public Specification<T> Or(Specification<T> other)
        => new OrSpecification<T>(this, other);

    public Specification<T> Not()
        => new NotSpecification<T>(this);
}

// Комбинаторы
public class AndSpecification<T> : Specification<T>
{
    private readonly Specification<T> _left;
    private readonly Specification<T> _right;

    public AndSpecification(Specification<T> left, Specification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpr = _left.ToExpression();
        var rightExpr = _right.ToExpression();

        var parameter = Expression.Parameter(typeof(T));
        var body = Expression.AndAlso(
            Expression.Invoke(leftExpr, parameter),
            Expression.Invoke(rightExpr, parameter));

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

public class OrSpecification<T> : Specification<T>
{
    private readonly Specification<T> _left;
    private readonly Specification<T> _right;

    public OrSpecification(Specification<T> left, Specification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpr = _left.ToExpression();
        var rightExpr = _right.ToExpression();

        var parameter = Expression.Parameter(typeof(T));
        var body = Expression.OrElse(
            Expression.Invoke(leftExpr, parameter),
            Expression.Invoke(rightExpr, parameter));

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

public class NotSpecification<T> : Specification<T>
{
    private readonly Specification<T> _inner;

    public NotSpecification(Specification<T> inner)
    {
        _inner = inner;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var innerExpr = _inner.ToExpression();
        var parameter = Expression.Parameter(typeof(T));
        var body = Expression.Not(Expression.Invoke(innerExpr, parameter));
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

// Конкретные спецификации
public class OrderByCustomerSpec : Specification<Order>
{
    private readonly CustomerId _customerId;

    public OrderByCustomerSpec(CustomerId customerId)
    {
        _customerId = customerId;
    }

    public override Expression<Func<Order, bool>> ToExpression()
        => order => order.CustomerId == _customerId;
}

public class OrderInStatusSpec : Specification<Order>
{
    private readonly OrderStatus _status;

    public OrderInStatusSpec(OrderStatus status)
    {
        _status = status;
    }

    public override Expression<Func<Order, bool>> ToExpression()
        => order => order.Status == _status;
}

public class OrderAboveAmountSpec : Specification<Order>
{
    private readonly decimal _minAmount;

    public OrderAboveAmountSpec(decimal minAmount)
    {
        _minAmount = minAmount;
    }

    public override Expression<Func<Order, bool>> ToExpression()
        => order => order.TotalAmount.Amount >= _minAmount;
}

// Использование
var spec = new OrderByCustomerSpec(customerId)
    .And(new OrderInStatusSpec(OrderStatus.Confirmed))
    .And(new OrderAboveAmountSpec(1000));

var orders = await _context.Orders
    .Where(spec.ToExpression())
    .ToListAsync();
```

---

## Domain Services vs Application Services

### Domain Service

Domain Service содержит бизнес-логику, которая **не принадлежит ни одной сущности**. Работает с доменными объектами и не зависит от инфраструктуры.

```csharp
// Domain Service — чистая бизнес-логика
public class PricingService
{
    public Money CalculateDiscount(Order order, CustomerLevel customerLevel)
    {
        var discountPercentage = customerLevel switch
        {
            CustomerLevel.Bronze => 0.05m,
            CustomerLevel.Silver => 0.10m,
            CustomerLevel.Gold => 0.15m,
            CustomerLevel.Platinum => 0.20m,
            _ => 0m
        };

        var discountAmount = order.TotalAmount.Amount * discountPercentage;
        return new Money(discountAmount, order.TotalAmount.Currency);
    }
}

// Domain Service — перевод денег между счетами
public class TransferService
{
    public void Transfer(Account from, Account to, Money amount)
    {
        if (!from.CanWithdraw(amount))
            throw new DomainException("Недостаточно средств для перевода.");

        from.Withdraw(amount);
        to.Deposit(amount);
    }
}
```

### Application Service

Application Service **оркестрирует** выполнение use case. Не содержит бизнес-логику — делегирует её доменным объектам и Domain Services.

```csharp
// Application Service — оркестрация use case
public class SubmitOrderCommandHandler : IRequestHandler<SubmitOrderCommand, OrderResult>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly PricingService _pricingService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SubmitOrderCommandHandler> _logger;

    public SubmitOrderCommandHandler(
        IOrderRepository orderRepository,
        ICustomerRepository customerRepository,
        PricingService pricingService,
        IUnitOfWork unitOfWork,
        ILogger<SubmitOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
        _pricingService = pricingService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OrderResult> Handle(SubmitOrderCommand command, CancellationToken ct)
    {
        // 1. Загрузка агрегатов
        var order = await _orderRepository.GetByIdWithLinesAsync(command.OrderId, ct)
            ?? throw new NotFoundException($"Заказ {command.OrderId} не найден.");

        var customer = await _customerRepository.GetByIdAsync(order.CustomerId, ct)
            ?? throw new NotFoundException($"Клиент {order.CustomerId} не найден.");

        // 2. Вызов Domain Service для расчёта скидки
        var discount = _pricingService.CalculateDiscount(order, customer.Level);
        order.ApplyDiscount(discount);

        // 3. Вызов бизнес-метода на Aggregate Root
        order.Submit();

        // 4. Сохранение (Unit of Work)
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Заказ {OrderId} успешно отправлен.", order.Id);

        return new OrderResult(order.Id, order.Status, order.TotalAmount);
    }
}
```

### Сравнение

| Критерий | Domain Service | Application Service |
|----------|---------------|-------------------|
| Бизнес-логика | Да | Нет (только оркестрация) |
| Зависимости | Только домен | Домен + инфраструктура |
| Состояние | Нет (stateless) | Нет (stateless) |
| Транзакции | Не управляет | Управляет |
| Пример | Расчёт скидки, валидация | Обработка команды, координация |

---

## Anti-Corruption Layer

Anti-Corruption Layer (ACL) защищает модель домена от «загрязнения» внешними моделями. Используется при интеграции с legacy-системами, внешними API, сторонними сервисами.

```csharp
// Внешняя модель (legacy или сторонний API)
namespace ExternalCrm;

public class CrmCustomerDto
{
    public string CustId { get; set; }      // Внешний формат Id
    public string FullName { get; set; }
    public string CustType { get; set; }     // "P" = physical, "L" = legal
    public string Addr1 { get; set; }
    public string Addr2 { get; set; }
    public decimal BalanceAmt { get; set; }
    public string BalanceCcy { get; set; }   // "840" = USD, "643" = RUB
}

// ACL: адаптер + транслятор
namespace Sales.Infrastructure.Acl;

public interface ICustomerProfileService
{
    Task<CustomerProfile?> GetProfileAsync(CustomerId customerId, CancellationToken ct);
}

public class CrmCustomerProfileAdapter : ICustomerProfileService
{
    private readonly ICrmApiClient _crmClient;
    private readonly CrmCustomerTranslator _translator;

    public CrmCustomerProfileAdapter(
        ICrmApiClient crmClient,
        CrmCustomerTranslator translator)
    {
        _crmClient = crmClient;
        _translator = translator;
    }

    public async Task<CustomerProfile?> GetProfileAsync(CustomerId customerId, CancellationToken ct)
    {
        var crmDto = await _crmClient.GetCustomerAsync(customerId.Value.ToString(), ct);
        if (crmDto is null) return null;

        return _translator.Translate(crmDto);
    }
}

public class CrmCustomerTranslator
{
    private static readonly Dictionary<string, string> CurrencyMap = new()
    {
        ["840"] = "USD",
        ["643"] = "RUB",
        ["978"] = "EUR"
    };

    public CustomerProfile Translate(CrmCustomerDto dto)
    {
        var customerType = dto.CustType switch
        {
            "P" => CustomerType.Individual,
            "L" => CustomerType.LegalEntity,
            _ => throw new InvalidOperationException(
                $"Неизвестный тип клиента CRM: {dto.CustType}")
        };

        var currency = CurrencyMap.GetValueOrDefault(dto.BalanceCcy)
            ?? throw new InvalidOperationException(
                $"Неизвестный код валюты CRM: {dto.BalanceCcy}");

        return new CustomerProfile(
            name: dto.FullName,
            type: customerType,
            address: new Address(dto.Addr1, dto.Addr2),
            balance: new Money(dto.BalanceAmt, currency)
        );
    }
}
```

---

## Shared Kernel

Shared Kernel — общая часть доменной модели, разделяемая между несколькими Bounded Contexts. Изменения в Shared Kernel требуют согласования обеих команд.

```csharp
// SharedKernel — отдельная сборка, используемая несколькими контекстами
namespace SharedKernel;

// Базовые строительные блоки
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    public TId Id { get; protected set; }
    protected Entity(TId id) => Id = id;
    protected Entity() { }

    public override bool Equals(object? obj) =>
        obj is Entity<TId> e && Equals(e);

    public bool Equals(Entity<TId>? other) =>
        other is not null && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override int GetHashCode() => Id.GetHashCode();
}

public abstract class ValueObject { /* ... */ }

public interface IAggregateRoot
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
    Guid EventId { get; }
}

// Общие Value Objects
public sealed class Money : ValueObject { /* ... */ }
public sealed class Email : ValueObject
{
    public string Value { get; }

    public Email(string value)
    {
        if (!IsValid(value))
            throw new DomainException($"Некорректный email: {value}");
        Value = value.ToLowerInvariant();
    }

    private static bool IsValid(string email)
        => !string.IsNullOrWhiteSpace(email) &&
           System.Text.RegularExpressions.Regex.IsMatch(
               email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}

// Структура проекта:
// src/
//   SharedKernel/              <-- Shared Kernel (NuGet-пакет или Project Reference)
//   Sales.Domain/              <-- Зависит от SharedKernel
//   Billing.Domain/            <-- Зависит от SharedKernel
```

> **Правило:** Shared Kernel должен быть минимальным. Чем больше Shared Kernel, тем сильнее связанность контекстов.

---

## Anemic Domain Model vs Rich Domain Model

### Anemic Domain Model (анти-паттерн)

Модель содержит только данные (свойства), вся логика — в сервисах.

```csharp
// ПЛОХО: Anemic Domain Model
public class Order
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Status { get; set; }
    public decimal TotalAmount { get; set; }
    public List<OrderLine> Lines { get; set; } = new();
}

// Вся логика вынесена в сервис — модель «мёртвая»
public class OrderService
{
    public void AddLine(Order order, OrderLine line)
    {
        if (order.Status != "Draft")
            throw new Exception("Cannot add line");
        order.Lines.Add(line);
        order.TotalAmount = order.Lines.Sum(l => l.Quantity * l.UnitPrice);
    }

    public void Submit(Order order)
    {
        if (order.Status != "Draft") throw new Exception("Invalid status");
        if (!order.Lines.Any()) throw new Exception("No lines");
        order.Status = "Submitted";
    }
}
```

Проблемы Anemic Model:

- Бизнес-правила разбросаны по сервисам.
- Инварианты легко нарушить — кто угодно может изменить `Status` напрямую.
- Нарушает принцип инкапсуляции ООП.
- Дублирование валидации в разных сервисах.

### Rich Domain Model (правильный подход)

Логика инкапсулирована в модели. Сеттеры закрыты, изменения — только через бизнес-методы.

```csharp
// ХОРОШО: Rich Domain Model
public class Order : Entity<OrderId>, IAggregateRoot
{
    private readonly List<OrderLine> _lines = new();

    // Публичные свойства — только для чтения
    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();
    public OrderStatus Status { get; private set; }
    public Money TotalAmount { get; private set; }

    // Изменения — только через бизнес-методы с валидацией
    public void AddLine(ProductId productId, string name, int qty, Money price)
    {
        Guard.Against(Status != OrderStatus.Draft,
            "Нельзя добавлять позиции в неактивный заказ.");
        // ... логика добавления
    }

    public void Submit()
    {
        Guard.Against(Status != OrderStatus.Draft,
            "Заказ можно отправить только из состояния Draft.");
        Guard.Against(!_lines.Any(),
            "Нельзя отправить пустой заказ.");
        Status = OrderStatus.Submitted;
    }
}
```

| Критерий | Anemic Model | Rich Model |
|----------|-------------|------------|
| Инкапсуляция | Отсутствует | Строгая |
| Инварианты | Не гарантированы | Гарантированы |
| Тестируемость | Нужно тестировать сервисы + модель | Модель тестируется изолированно |
| Расположение логики | В сервисах | В модели |
| Подходит для | Простой CRUD | Сложная бизнес-логика |

---

## Организация слоёв

### Структура проекта

```
src/
├── MyApp.Domain/                    # Ядро — бизнес-логика
│   ├── Orders/
│   │   ├── Order.cs                 # Aggregate Root
│   │   ├── OrderLine.cs             # Entity
│   │   ├── OrderStatus.cs           # Enum / Value Object
│   │   ├── OrderId.cs               # Strongly-Typed Id
│   │   ├── IOrderRepository.cs      # Интерфейс репозитория
│   │   ├── Events/
│   │   │   ├── OrderSubmittedEvent.cs
│   │   │   └── OrderCancelledEvent.cs
│   │   └── Specifications/
│   │       └── OrderByCustomerSpec.cs
│   ├── Customers/
│   │   ├── Customer.cs
│   │   └── ICustomerRepository.cs
│   ├── Services/
│   │   └── PricingService.cs        # Domain Service
│   ├── Common/
│   │   ├── Entity.cs
│   │   ├── ValueObject.cs
│   │   ├── IAggregateRoot.cs
│   │   └── DomainException.cs
│   └── MyApp.Domain.csproj          # Без зависимостей от инфраструктуры!
│
├── MyApp.Application/               # Use Cases, CQRS
│   ├── Orders/
│   │   ├── Commands/
│   │   │   ├── SubmitOrder/
│   │   │   │   ├── SubmitOrderCommand.cs
│   │   │   │   ├── SubmitOrderCommandHandler.cs
│   │   │   │   └── SubmitOrderCommandValidator.cs
│   │   │   └── CancelOrder/
│   │   │       ├── CancelOrderCommand.cs
│   │   │       └── CancelOrderCommandHandler.cs
│   │   ├── Queries/
│   │   │   ├── GetOrderById/
│   │   │   │   ├── GetOrderByIdQuery.cs
│   │   │   │   ├── GetOrderByIdQueryHandler.cs
│   │   │   │   └── OrderDto.cs
│   │   │   └── GetOrdersByCustomer/
│   │   │       └── ...
│   │   └── EventHandlers/
│   │       └── OrderSubmittedEventHandler.cs
│   ├── Common/
│   │   ├── Behaviors/
│   │   │   ├── ValidationBehavior.cs
│   │   │   └── LoggingBehavior.cs
│   │   └── Interfaces/
│   │       ├── IUnitOfWork.cs
│   │       └── IEmailService.cs
│   └── MyApp.Application.csproj     # Зависит от Domain, MediatR, FluentValidation
│
├── MyApp.Infrastructure/            # Реализации интерфейсов
│   ├── Persistence/
│   │   ├── AppDbContext.cs
│   │   ├── Configurations/
│   │   │   ├── OrderConfiguration.cs
│   │   │   └── CustomerConfiguration.cs
│   │   ├── Repositories/
│   │   │   ├── OrderRepository.cs
│   │   │   └── CustomerRepository.cs
│   │   └── Migrations/
│   ├── Services/
│   │   └── EmailService.cs
│   └── MyApp.Infrastructure.csproj  # Зависит от Application, EF Core
│
├── MyApp.Api/                       # Presentation
│   ├── Controllers/
│   │   └── OrdersController.cs
│   ├── Program.cs
│   └── MyApp.Api.csproj             # Зависит от Application, Infrastructure
```

### Правило зависимостей

```
Presentation → Application → Domain
                    ↑
Infrastructure ─────┘
```

- **Domain** не зависит ни от чего (кроме SharedKernel).
- **Application** зависит от Domain.
- **Infrastructure** зависит от Application (реализует его интерфейсы).
- **Presentation** зависит от Application.

---

## Сравнение архитектур

### Clean Architecture (Чистая архитектура)

Предложена Робертом Мартином (Uncle Bob). Зависимости направлены **к центру**.

```
┌─────────────────────────────────────┐
│         Frameworks & Drivers        │  (API, DB, UI)
│  ┌───────────────────────────────┐  │
│  │         Interface Adapters    │  │  (Controllers, Gateways, Presenters)
│  │  ┌─────────────────────────┐  │  │
│  │  │      Application        │  │  │  (Use Cases)
│  │  │  ┌───────────────────┐  │  │  │
│  │  │  │     Entities      │  │  │  │  (Domain)
│  │  │  └───────────────────┘  │  │  │
│  │  └─────────────────────────┘  │  │
│  └───────────────────────────────┘  │
└─────────────────────────────────────┘
```

### Onion Architecture (Луковая архитектура)

Предложена Джеффри Палермо. Аналогична Clean Architecture, но выделяет Domain Services.

```
┌────────────────────────────────────────┐
│           Infrastructure               │
│  ┌──────────────────────────────────┐  │
│  │        Application Services      │  │
│  │  ┌────────────────────────────┐  │  │
│  │  │     Domain Services        │  │  │
│  │  │  ┌──────────────────────┐  │  │  │
│  │  │  │    Domain Model      │  │  │  │
│  │  │  └──────────────────────┘  │  │  │
│  │  └────────────────────────────┘  │  │
│  └──────────────────────────────────┘  │
└────────────────────────────────────────┘
```

### Hexagonal Architecture (Гексагональная / Ports & Adapters)

Предложена Алистером Кокбёрном. Приложение взаимодействует с внешним миром через **порты** (интерфейсы) и **адаптеры** (реализации).

```
              ┌──────────────┐
    Adapter──►│  Port (in)   │
              │              │
   HTTP ──────┤   Application│
   gRPC ──────┤     Core     ├────── DB Adapter
   CLI  ──────┤              │────── Email Adapter
              │  Port (out)  │────── Queue Adapter
              └──────────────┘
```

```csharp
// Port (входящий) — определён в Application
public interface ISubmitOrderUseCase
{
    Task<OrderResult> ExecuteAsync(SubmitOrderCommand command, CancellationToken ct);
}

// Port (исходящий) — определён в Application
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct);
    Task AddAsync(Order order, CancellationToken ct);
}

// Adapter (входящий) — в Presentation
[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly ISubmitOrderUseCase _submitOrder;

    [HttpPost]
    public async Task<IActionResult> Submit(SubmitOrderRequest request, CancellationToken ct)
    {
        var command = new SubmitOrderCommand(request.OrderId, request.CustomerId);
        var result = await _submitOrder.ExecuteAsync(command, ct);
        return Ok(result);
    }
}

// Adapter (исходящий) — в Infrastructure
public class EfOrderRepository : IOrderRepository
{
    private readonly AppDbContext _context;

    public async Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct)
        => await _context.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task AddAsync(Order order, CancellationToken ct)
        => await _context.Orders.AddAsync(order, ct);
}
```

### Сравнительная таблица

| Критерий | Clean | Onion | Hexagonal |
|----------|-------|-------|-----------|
| Автор | Robert C. Martin | Jeffrey Palermo | Alistair Cockburn |
| Центр | Entities | Domain Model | Application Core |
| Ключевая идея | Правило зависимостей | Слои-луковицы | Порты и адаптеры |
| Структура | Концентрические круги | Концентрические круги | Гексагон с портами |
| Тестируемость | Высокая | Высокая | Высокая |
| Главное отличие | Явные Use Cases | Выделенные Domain Services | Симметрия in/out портов |

> На практике эти три подхода очень похожи и часто комбинируются. Главный принцип общий: **домен в центре, инфраструктура снаружи, зависимости направлены внутрь.**

---

## Практическая реализация в .NET

### Entity Framework: конфигурация Aggregate Root

```csharp
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        // Strongly-Typed Id
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .HasConversion(
                id => id.Value,
                value => new OrderId(value));

        builder.Property(o => o.CustomerId)
            .HasConversion(
                id => id.Value,
                value => new CustomerId(value));

        // Value Object — Owned Type
        builder.OwnsOne(o => o.TotalAmount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("TotalAmount")
                .HasColumnType("decimal(18,2)");
            money.Property(m => m.Currency)
                .HasColumnName("TotalCurrency")
                .HasMaxLength(3);
        });

        // Enum
        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        // Навигация к дочерним Entity внутри Aggregate
        builder.HasMany(o => o.Lines)
            .WithOne()
            .HasForeignKey("OrderId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Order.Lines))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Игнорируем DomainEvents — они не сохраняются в БД
        builder.Ignore(o => o.DomainEvents);

        builder.Property(o => o.CreatedAt);
    }
}

public class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("OrderLines");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasConversion(
                id => id.Value,
                value => new OrderLineId(value));

        builder.Property(l => l.ProductId)
            .HasConversion(
                id => id.Value,
                value => new ProductId(value));

        builder.OwnsOne(l => l.UnitPrice, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("UnitPrice")
                .HasColumnType("decimal(18,2)");
            money.Property(m => m.Currency)
                .HasColumnName("UnitPriceCurrency")
                .HasMaxLength(3);
        });

        builder.Property(l => l.ProductName).HasMaxLength(200);
        builder.Property(l => l.Quantity);

        // LineTotal — вычисляемое, игнорируем
        builder.Ignore(l => l.LineTotal);
    }
}
```

### MediatR: CQRS Pipeline

```csharp
// Команда
public record SubmitOrderCommand(OrderId OrderId) : IRequest<OrderResult>;

// Запрос
public record GetOrderByIdQuery(OrderId OrderId) : IRequest<OrderDto?>;

// Валидация через FluentValidation + MediatR Pipeline
public class SubmitOrderCommandValidator : AbstractValidator<SubmitOrderCommand>
{
    public SubmitOrderCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .Must(id => id.Value != Guid.Empty)
            .WithMessage("OrderId не может быть пустым.");
    }
}

// Pipeline Behavior для валидации
public class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

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
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Any())
            throw new ValidationException(failures);

        return await next();
    }
}

// Pipeline Behavior для логирования
public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("Обработка {RequestName}: {@Request}", requestName, request);

        var stopwatch = Stopwatch.StartNew();
        var response = await next();
        stopwatch.Stop();

        _logger.LogInformation(
            "Завершено {RequestName} за {ElapsedMs}ms",
            requestName, stopwatch.ElapsedMilliseconds);

        return response;
    }
}

// Регистрация в DI
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Domain Services
        services.AddScoped<PricingService>();

        return services;
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>();
        services.AddScoped<IEmailService, EmailService>();

        return services;
    }
}
```

---

## Примеры кода

### Value Object с валидацией: Address

```csharp
public sealed class Address : ValueObject
{
    public string Street { get; }
    public string City { get; }
    public string PostalCode { get; }
    public string Country { get; }

    public Address(string street, string city, string postalCode, string country)
    {
        if (string.IsNullOrWhiteSpace(street))
            throw new DomainException("Улица обязательна.");
        if (string.IsNullOrWhiteSpace(city))
            throw new DomainException("Город обязателен.");
        if (string.IsNullOrWhiteSpace(postalCode))
            throw new DomainException("Почтовый индекс обязателен.");
        if (string.IsNullOrWhiteSpace(country))
            throw new DomainException("Страна обязательна.");
        if (postalCode.Length > 10)
            throw new DomainException("Почтовый индекс не может быть длиннее 10 символов.");

        Street = street.Trim();
        City = city.Trim();
        PostalCode = postalCode.Trim();
        Country = country.Trim();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return PostalCode;
        yield return Country;
    }

    public override string ToString() => $"{Street}, {City}, {PostalCode}, {Country}";
}
```

### Value Object с валидацией: PhoneNumber

```csharp
public sealed class PhoneNumber : ValueObject
{
    public string Value { get; }

    private static readonly Regex PhoneRegex = new(
        @"^\+?[1-9]\d{6,14}$",
        RegexOptions.Compiled);

    public PhoneNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Номер телефона обязателен.");

        var cleaned = value.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

        if (!PhoneRegex.IsMatch(cleaned))
            throw new DomainException($"Некорректный номер телефона: {value}");

        Value = cleaned;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

### Пример: Domain Event с обработкой

```csharp
// Событие: товар добавлен в корзину
public record ProductAddedToCartEvent(
    CartId CartId,
    ProductId ProductId,
    int Quantity,
    Money Price) : DomainEvent;

// Обработчик 1: обновить рекомендации
public class UpdateRecommendationsHandler
    : INotificationHandler<ProductAddedToCartEvent>
{
    private readonly IRecommendationEngine _engine;

    public UpdateRecommendationsHandler(IRecommendationEngine engine)
    {
        _engine = engine;
    }

    public async Task Handle(ProductAddedToCartEvent notification, CancellationToken ct)
    {
        await _engine.TrackUserInterestAsync(
            notification.CartId,
            notification.ProductId,
            ct);
    }
}

// Обработчик 2: проверить наличие на складе
public class CheckStockOnCartAddHandler
    : INotificationHandler<ProductAddedToCartEvent>
{
    private readonly IInventoryService _inventory;
    private readonly ILogger<CheckStockOnCartAddHandler> _logger;

    public CheckStockOnCartAddHandler(
        IInventoryService inventory,
        ILogger<CheckStockOnCartAddHandler> logger)
    {
        _inventory = inventory;
        _logger = logger;
    }

    public async Task Handle(ProductAddedToCartEvent notification, CancellationToken ct)
    {
        var availability = await _inventory.CheckAvailability(
            notification.ProductId, ct);

        if (availability.Quantity < notification.Quantity)
        {
            _logger.LogWarning(
                "Товар {ProductId} может отсутствовать на складе. " +
                "Запрошено: {Requested}, доступно: {Available}",
                notification.ProductId,
                notification.Quantity,
                availability.Quantity);
        }
    }
}
```

### Полный пример: тестирование Aggregate Root

```csharp
public class OrderTests
{
    private readonly OrderId _orderId = OrderId.CreateNew();
    private readonly CustomerId _customerId = CustomerId.CreateNew();
    private readonly ProductId _productId = ProductId.CreateNew();
    private readonly Money _unitPrice = new(100m, "RUB");

    private Order CreateDraftOrder()
    {
        return new Order(_orderId, _customerId);
    }

    [Fact]
    public void NewOrder_ShouldBeDraft()
    {
        var order = CreateDraftOrder();

        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Empty(order.Lines);
        Assert.Equal(new Money(0, "RUB"), order.TotalAmount);
    }

    [Fact]
    public void AddLine_ShouldAddAndRecalculate()
    {
        var order = CreateDraftOrder();

        order.AddLine(_productId, "Товар A", 3, _unitPrice);

        Assert.Single(order.Lines);
        Assert.Equal(new Money(300m, "RUB"), order.TotalAmount);
    }

    [Fact]
    public void AddLine_WhenNotDraft_ShouldThrow()
    {
        var order = CreateDraftOrder();
        order.AddLine(_productId, "Товар A", 1, _unitPrice);
        order.Submit();

        Assert.Throws<DomainException>(() =>
            order.AddLine(ProductId.CreateNew(), "Товар B", 1, _unitPrice));
    }

    [Fact]
    public void Submit_ShouldChangeStatusAndRaiseEvent()
    {
        var order = CreateDraftOrder();
        order.AddLine(_productId, "Товар A", 1, _unitPrice);

        order.Submit();

        Assert.Equal(OrderStatus.Submitted, order.Status);
        Assert.Contains(order.DomainEvents,
            e => e is OrderSubmittedEvent);
    }

    [Fact]
    public void Submit_WhenEmpty_ShouldThrow()
    {
        var order = CreateDraftOrder();

        Assert.Throws<DomainException>(() => order.Submit());
    }

    [Fact]
    public void Cancel_WhenShipped_ShouldThrow()
    {
        var order = CreateDraftOrder();
        order.AddLine(_productId, "Товар A", 1, _unitPrice);
        order.Submit();
        order.Confirm();
        // Допустим, есть метод Ship()

        // Проверяем, что отменить отправленный заказ нельзя
        // order.Ship();
        // Assert.Throws<DomainException>(() => order.Cancel("причина"));
    }

    [Fact]
    public void AddLine_ExistingProduct_ShouldIncreaseQuantity()
    {
        var order = CreateDraftOrder();

        order.AddLine(_productId, "Товар A", 2, _unitPrice);
        order.AddLine(_productId, "Товар A", 3, _unitPrice);

        Assert.Single(order.Lines);
        Assert.Equal(5, order.Lines.First().Quantity);
        Assert.Equal(new Money(500m, "RUB"), order.TotalAmount);
    }
}
```

---

## Ошибки при внедрении DDD

### 1. Применение DDD ко всему проекту

DDD оправдан только для сложных доменов. Для простого CRUD (справочники, настройки) используйте простые подходы.

### 2. Слишком крупные Aggregate

```csharp
// ПЛОХО: гигантский Aggregate
public class Customer : IAggregateRoot
{
    public List<Order> Orders { get; set; }         // тысячи заказов
    public List<Address> Addresses { get; set; }
    public List<PaymentMethod> PaymentMethods { get; set; }
    public ShoppingCart Cart { get; set; }
}

// ХОРОШО: маленькие Aggregates, связь по Id
public class Customer : IAggregateRoot
{
    public CustomerId Id { get; private set; }
    public string Name { get; private set; }
    public Email Email { get; private set; }
    // Orders — отдельный Aggregate, связь через CustomerId
}
```

### 3. Ссылки между Aggregate по объектам вместо Id

```csharp
// ПЛОХО
public class Order
{
    public Customer Customer { get; set; }   // прямая объектная ссылка
}

// ХОРОШО
public class Order
{
    public CustomerId CustomerId { get; private set; }   // ссылка по Id
}
```

### 4. Generic Repository вместо доменного

Смотрите раздел [Repository паттерн](#repository-паттерн).

### 5. Anemic Domain Model

Логика вынесена в сервисы, модель — просто DTO. Смотрите раздел [Anemic vs Rich](#anemic-domain-model-vs-rich-domain-model).

### 6. Пренебрежение Ubiquitous Language

Код использует технические имена вместо бизнес-терминов. `ProcessEntity`, `HandleData` вместо `SubmitOrder`, `ApproveRefund`.

### 7. Изменение нескольких Aggregate в одной транзакции

```csharp
// ПЛОХО: две транзакционные границы в одной операции
public async Task TransferAsync(OrderId orderId, WarehouseId warehouseId)
{
    var order = await _orderRepo.GetByIdAsync(orderId);
    var warehouse = await _warehouseRepo.GetByIdAsync(warehouseId);

    order.Ship();
    warehouse.DeductStock(order.Lines);

    await _unitOfWork.SaveChangesAsync(); // Два Aggregate в одной транзакции!
}

// ХОРОШО: Saga / eventual consistency через Domain Events
public async Task TransferAsync(OrderId orderId)
{
    var order = await _orderRepo.GetByIdAsync(orderId);
    order.Ship(); // Поднимает OrderShippedEvent
    await _unitOfWork.SaveChangesAsync();
    // OrderShippedEvent -> обработчик -> warehouse.DeductStock()
}
```

### 8. Доменные объекты в API-контрактах

```csharp
// ПЛОХО: контроллер возвращает Aggregate Root напрямую
[HttpGet("{id}")]
public async Task<Order> GetOrder(Guid id) // JSON-сериализация сломает инварианты
    => await _repo.GetByIdAsync(new OrderId(id));

// ХОРОШО: DTO / ViewModel
[HttpGet("{id}")]
public async Task<OrderDto> GetOrder(Guid id)
{
    var query = new GetOrderByIdQuery(new OrderId(id));
    return await _mediator.Send(query);
}
```

### 9. Игнорирование Bounded Context

Одна модель `Customer` на всё приложение вместо отдельных моделей в каждом контексте.

### 10. Преждевременная оптимизация архитектуры

Внедрение Event Sourcing, CQRS с раздельными базами, Saga без реальной необходимости.

---

## Вопросы на собеседовании с ответами

### 1. Что такое Bounded Context и зачем он нужен?

**Ответ:** Bounded Context — это явная граница, внутри которой модель домена имеет чёткое и непротиворечивое значение. Один и тот же термин (например, «Продукт») может означать разное в контексте продаж (цена, скидки) и в контексте склада (вес, размеры, ячейка хранения). Bounded Context позволяет каждой команде работать со своей моделью, избегая конфликтов и «размытия» понятий. На практике Bounded Context обычно соответствует отдельному микросервису или модулю.

---

### 2. Чем Entity отличается от Value Object?

**Ответ:** Entity определяется своей идентичностью (Id): два объекта с одинаковыми полями, но разными Id — разные сущности. Value Object определяется своими атрибутами: два объекта с одинаковыми значениями — тождественны. Value Object неизменяемый (immutable) — при изменении создаётся новый экземпляр. Примеры Entity: Order, Customer. Примеры Value Object: Money, Address, Email.

---

### 3. Что такое Aggregate и какие правила к нему применяются?

**Ответ:** Aggregate — это кластер связанных Entity и Value Object, рассматриваемый как единица целостности данных. Правила:
- Внешний код ссылается только на Aggregate Root.
- Aggregate Root гарантирует все инварианты.
- Между разными Aggregates — только ссылки по Id.
- Одна транзакция должна изменять один Aggregate.
- Aggregate должен быть минимальным по размеру — только то, что необходимо для поддержания инвариантов.

---

### 4. Зачем нужны Domain Events?

**Ответ:** Domain Events уведомляют другие части системы о значимых изменениях в домене. Они обеспечивают слабую связанность между Aggregate и между Bounded Context. Позволяют реализовать побочные эффекты (отправка email, обновление проекции, интеграция с внешними системами) без загрязнения основной бизнес-логики. Выражаются в прошедшем времени на языке домена: `OrderSubmitted`, `PaymentReceived`.

---

### 5. В чём разница между Domain Service и Application Service?

**Ответ:** Domain Service содержит бизнес-логику, которая не принадлежит конкретной сущности (например, расчёт скидки на основе данных из нескольких Aggregate). Он не знает об инфраструктуре. Application Service — это оркестратор, который координирует выполнение use case: загружает Aggregates из репозиториев, вызывает Domain Services, сохраняет результат. Application Service не содержит бизнес-логику — он делегирует её доменному слою.

---

### 6. Что такое Anti-Corruption Layer и когда он нужен?

**Ответ:** Anti-Corruption Layer (ACL) — это трансляционный слой между вашим Bounded Context и внешней системой (legacy, сторонний API). Он защищает вашу доменную модель от загрязнения внешними концепциями и форматами данных. ACL реализуется как Adapter + Translator: адаптер вызывает внешнюю систему, транслятор преобразует внешнюю модель в доменную.

---

### 7. Почему Anemic Domain Model считается анти-паттерном в DDD?

**Ответ:** Anemic Domain Model нарушает фундаментальный принцип ООП — инкапсуляцию. Модель содержит только данные (публичные сеттеры), а вся логика — в сервисах. Инварианты не защищены: любой код может установить невалидное состояние. Бизнес-правила дублируются в разных сервисах. Модель не выражает бизнес-язык. При Rich Domain Model логика инкапсулирована в самой модели, сеттеры закрыты, изменения — только через бизнес-методы с валидацией.

---

### 8. Как правильно организовать слои в DDD-приложении?

**Ответ:** Стандартная структура: Domain (ядро, без зависимостей), Application (use cases, зависит от Domain), Infrastructure (реализация интерфейсов, зависит от Application), Presentation (API/UI, зависит от Application). Ключевое правило: зависимости направлены к центру — Domain не знает ни о чём, кроме себя. Infrastructure зависит от Application (а не наоборот) — через Dependency Inversion: Application определяет интерфейсы, Infrastructure их реализует.

---

### 9. Чем Clean Architecture, Onion Architecture и Hexagonal Architecture отличаются друг от друга?

**Ответ:** Все три подхода разделяют общий принцип: домен в центре, инфраструктура снаружи, зависимости направлены внутрь. Различия в акцентах:
- **Clean Architecture** (Uncle Bob) делает акцент на Use Cases как отдельный слой.
- **Onion Architecture** (Palermo) явно выделяет Domain Services.
- **Hexagonal Architecture** (Cockburn) фокусируется на портах (интерфейсах) и адаптерах (реализациях), подчёркивая симметрию входящих и исходящих взаимодействий.

На практике они часто комбинируются, и в .NET-проектах обычно используется гибрид этих подходов.

---

### 10. Как Domain Events доставляются в .NET-проекте? В чём разница между внутридоменными и интеграционными событиями?

**Ответ:** Внутридоменные события (Domain Events) обрабатываются внутри одного процесса, обычно через MediatR. Aggregate Root накапливает события, а при SaveChanges они публикуются. Интеграционные события (Integration Events) передаются между Bounded Contexts / микросервисами через брокер сообщений (RabbitMQ, Kafka, Azure Service Bus). Domain Events — синхронные или eventually consistent внутри одной транзакции. Integration Events — всегда асинхронные, между разными транзакциями и сервисами. Часто Domain Event трансформируется в Integration Event после успешного сохранения.

---

### 11. Когда стоит применять DDD, а когда не стоит?

**Ответ:** DDD оправдан при высокой сложности бизнес-домена: множество бизнес-правил, сложные инварианты, частые изменения требований. Не стоит применять DDD для: простого CRUD, ETL-пайплайнов, проектов с минимальной бизнес-логикой, прототипов. DDD увеличивает начальную сложность разработки — это инвестиция, которая окупается только при достаточной сложности домена.

---

### 12. Что такое Specification паттерн и зачем он нужен?

**Ответ:** Specification инкапсулирует бизнес-правило фильтрации в отдельный объект. Спецификации можно комбинировать через And/Or/Not, переиспользовать и тестировать изолированно. В .NET Specification обычно возвращает `Expression<Func<T, bool>>`, что позволяет использовать её с EF Core для фильтрации на уровне SQL. Это устраняет дублирование логики фильтрации и выносит бизнес-правила из репозиториев.

---

### 13. Объясните разницу между Strongly-Typed Id и примитивным Id. Зачем это нужно?

**Ответ:** Strongly-Typed Id (например, `OrderId`, `CustomerId`) — это обёртка над примитивом (обычно `Guid`), которая обеспечивает типобезопасность. С примитивными Id легко перепутать параметры: `void Process(Guid orderId, Guid customerId)` — компилятор не поймает, если аргументы переставлены. С Strongly-Typed Id: `void Process(OrderId orderId, CustomerId customerId)` — ошибка типов будет обнаружена при компиляции. Реализуется через `readonly record struct` в C# 10+.

---

### 14. Как тестировать доменную логику в DDD?

**Ответ:** Доменная логика тестируется юнит-тестами без моков инфраструктуры — это главное преимущество DDD. Aggregate Root создаётся через конструктор, вызываются бизнес-методы, проверяется результат (состояние, выброшенные исключения, поднятые Domain Events). Тесты читаются на языке домена: «когда клиент отправляет пустой заказ, ожидаем DomainException». Интеграционные тесты проверяют Application Services с реальной БД (in-memory или Testcontainers).

---

### 15. Что такое Shared Kernel? Какие риски с ним связаны?

**Ответ:** Shared Kernel — общая часть доменной модели, разделяемая между несколькими Bounded Contexts (базовые классы Entity, ValueObject, общие Value Objects). Риски: изменение Shared Kernel затрагивает все контексты, требует координации между командами. Shared Kernel должен быть минимальным. Типичное содержимое: базовые абстракции (Entity, ValueObject, IDomainEvent), общие Value Objects (Money, Email). Бизнес-логика конкретного контекста в Shared Kernel не попадает.
