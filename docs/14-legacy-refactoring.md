# Legacy Code Refactoring — Подготовка к Senior .NET собеседованию

## Содержание

1. [Что такое Legacy Code](#что-такое-legacy-code)
2. [Working Effectively with Legacy Code — ключевые идеи](#working-effectively-with-legacy-code)
3. [Стратегии рефакторинга](#стратегии-рефакторинга)
4. [Characterization Tests](#characterization-tests)
5. [Seams — швы в коде](#seams--швы-в-коде)
6. [Техники рефакторинга](#техники-рефакторинга)
7. [SOLID принципы в контексте рефакторинга](#solid-принципы-в-контексте-рефакторинга)
8. [Dependency Injection в Legacy Code](#dependency-injection-в-legacy-code)
9. [Миграция с .NET Framework на .NET 6/7/8](#миграция-с-net-framework-на-net-678)
10. [Покрытие тестами: с чего начать](#покрытие-тестами-с-чего-начать)
11. [Рефакторинг базы данных](#рефакторинг-базы-данных)
12. [Работа с хранимыми процедурами](#работа-с-хранимыми-процедурами)
13. [Code Smells](#code-smells)
14. [Метрики качества](#метрики-качества)
15. [Инструменты](#инструменты)
16. [Вопросы на собеседовании](#вопросы-на-собеседовании)

---

## Что такое Legacy Code

### Определение Michael Feathers

> **Legacy code — это код без тестов.**
> — Michael Feathers, *Working Effectively with Legacy Code*

Это определение намеренно провокационно. Feathers не говорит о возрасте кода или используемых технологиях. Код, написанный вчера без тестов, уже является legacy. Ключевая проблема legacy-кода — **невозможность безопасно вносить изменения**, потому что нет автоматической проверки корректности поведения.

Расширенные признаки legacy-кода:

- **Отсутствие тестов** — главный признак по Feathers
- **Высокая связанность (coupling)** — изменение одного модуля ломает другие
- **Нет документации** — единственная документация — сам код
- **Страх изменений** — разработчики боятся трогать работающий код
- **Устаревший стек** — .NET Framework 4.x, WCF, Web Forms, Entity Framework 6
- **Знание сосредоточено у одного человека** — "bus factor = 1"

---

## Working Effectively with Legacy Code

### Ключевые идеи книги Michael Feathers

**1. Edit and Pray vs. Cover and Modify**

Два подхода к работе с legacy:

- **Edit and Pray** — вносим изменения и надеемся, что ничего не сломали. Типичный подход в командах без тестов.
- **Cover and Modify** — сначала покрываем код тестами, потом безопасно меняем. Это целевой подход.

**2. The Legacy Code Dilemma**

> Чтобы написать тесты, нужно изменить код. Чтобы безопасно изменить код, нужны тесты.

Выход — искать **швы (seams)** в коде, позволяющие подставить тестовые зависимости без изменения поведения.

**3. Алгоритм работы с legacy:**

1. Определить точки изменения (change points)
2. Найти точки тестирования (test points)
3. Разорвать зависимости (break dependencies)
4. Написать тесты
5. Внести изменения и провести рефакторинг

---

## Стратегии рефакторинга

### Strangler Fig Pattern

Паттерн назван в честь тропического дерева-душителя, которое постепенно обвивает и замещает дерево-хозяина. Аналогично новый код постепенно замещает старый.

```
Этапы:
1. Новая функциональность пишется в новой системе
2. Старая функциональность постепенно мигрирует
3. Фасад (proxy/gateway) перенаправляет трафик
4. Старая система отключается
```

Пример реализации через middleware в ASP.NET:

```csharp
// Фасад, перенаправляющий запросы между старой и новой системой
public class StranglerFacadeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IFeatureFlagService _featureFlags;

    public StranglerFacadeMiddleware(RequestDelegate next, IFeatureFlagService featureFlags)
    {
        _next = next;
        _featureFlags = featureFlags;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;

        if (_featureFlags.IsNewSystemEnabled(path))
        {
            // Новая система обрабатывает запрос
            await _next(context);
        }
        else
        {
            // Проксируем в старую систему
            await ProxyToLegacySystem(context);
        }
    }

    private async Task ProxyToLegacySystem(HttpContext context)
    {
        using var httpClient = new HttpClient();
        var legacyUrl = $"http://legacy-system{context.Request.Path}{context.Request.QueryString}";
        var response = await httpClient.GetAsync(legacyUrl);
        var content = await response.Content.ReadAsByteArrayAsync();
        context.Response.StatusCode = (int)response.StatusCode;
        await context.Response.Body.WriteAsync(content);
    }
}
```

### Branch by Abstraction

Паттерн позволяет постепенно заменять реализацию **внутри одного приложения** без ветвления в VCS.

```csharp
// Шаг 1: Выделяем абстракцию
public interface INotificationService
{
    Task SendAsync(string recipient, string message);
}

// Шаг 2: Оборачиваем старый код
public class LegacyNotificationService : INotificationService
{
    private readonly SmtpClient _smtpClient; // старый код

    public async Task SendAsync(string recipient, string message)
    {
        // Старая логика отправки через SMTP напрямую
        var mailMessage = new MailMessage("noreply@company.com", recipient, "Notification", message);
        await _smtpClient.SendMailAsync(mailMessage);
    }
}

// Шаг 3: Новая реализация
public class ModernNotificationService : INotificationService
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<ModernNotificationService> _logger;

    public ModernNotificationService(IEmailSender emailSender, ILogger<ModernNotificationService> logger)
    {
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task SendAsync(string recipient, string message)
    {
        _logger.LogInformation("Sending notification to {Recipient}", recipient);
        await _emailSender.SendEmailAsync(recipient, "Notification", message);
    }
}

// Шаг 4: Переключение через feature flag
public class SwitchableNotificationService : INotificationService
{
    private readonly LegacyNotificationService _legacy;
    private readonly ModernNotificationService _modern;
    private readonly IFeatureFlagService _flags;

    public async Task SendAsync(string recipient, string message)
    {
        if (_flags.IsEnabled("UseModernNotifications"))
            await _modern.SendAsync(recipient, message);
        else
            await _legacy.SendAsync(recipient, message);
    }
}
```

### Parallel Run

Оба варианта (старый и новый) выполняются одновременно, результаты сравниваются. Это позволяет убедиться в корректности новой реализации в продакшене.

```csharp
public class ParallelRunPricingService
{
    private readonly LegacyPricingEngine _legacy;
    private readonly NewPricingEngine _modern;
    private readonly ILogger<ParallelRunPricingService> _logger;
    private readonly IMetricsCollector _metrics;

    public async Task<decimal> CalculatePriceAsync(Order order)
    {
        // Старый результат — всегда является основным
        var legacyResult = await _legacy.CalculateAsync(order);

        // Новый результат — вычисляем параллельно, ошибки не влияют на пользователя
        _ = Task.Run(async () =>
        {
            try
            {
                var modernResult = await _modern.CalculateAsync(order);
                if (legacyResult != modernResult)
                {
                    _logger.LogWarning(
                        "Price mismatch for Order {OrderId}: Legacy={Legacy}, Modern={Modern}",
                        order.Id, legacyResult, modernResult);
                    _metrics.IncrementCounter("pricing.mismatch");
                }
                else
                {
                    _metrics.IncrementCounter("pricing.match");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Modern pricing engine failed for Order {OrderId}", order.Id);
            }
        });

        return legacyResult; // Всегда возвращаем результат старой системы
    }
}
```

---

## Characterization Tests

**Characterization Test (тест-характеристика)** фиксирует текущее поведение системы. Он не проверяет, правильно ли работает код — он документирует, **как код работает сейчас**.

### Алгоритм создания:

1. Вызвать метод с известными входными данными
2. Записать фактический результат
3. Сделать этот результат ожидаемым значением в тесте

```csharp
// Legacy-код: никто не знает, что именно делает этот метод
public class TaxCalculator
{
    public decimal Calculate(decimal amount, string region, bool isExempt)
    {
        if (isExempt) return 0;
        decimal rate = region switch
        {
            "US-CA" => 0.0725m,
            "US-NY" => 0.08m,
            "US-TX" => 0.0625m,
            _ => 0.05m
        };
        var result = Math.Round(amount * rate, 2);
        if (amount > 10000) result *= 0.95m; // скидка для крупных сумм?!
        return result;
    }
}

// Characterization tests — фиксируем текущее поведение
[TestFixture]
public class TaxCalculatorCharacterizationTests
{
    private TaxCalculator _calculator;

    [SetUp]
    public void SetUp() => _calculator = new TaxCalculator();

    [Test]
    public void California_StandardAmount_Returns7Point25Percent()
    {
        var result = _calculator.Calculate(1000m, "US-CA", false);
        Assert.That(result, Is.EqualTo(72.50m));
    }

    [Test]
    public void California_LargeAmount_AppliesDiscount()
    {
        var result = _calculator.Calculate(20000m, "US-CA", false);
        // 20000 * 0.0725 = 1450, * 0.95 = 1377.50
        Assert.That(result, Is.EqualTo(1377.50m));
    }

    [Test]
    public void Exempt_ReturnsZero()
    {
        var result = _calculator.Calculate(5000m, "US-CA", true);
        Assert.That(result, Is.EqualTo(0m));
    }

    [Test]
    public void UnknownRegion_UsesDefaultRate()
    {
        var result = _calculator.Calculate(1000m, "DE", false);
        Assert.That(result, Is.EqualTo(50.00m));
    }
}
```

---

## Seams -- швы в коде

**Seam (шов)** — место в коде, где можно изменить поведение без редактирования самого кода. Feathers выделяет три типа.

### Object Seam

Самый распространённый и предпочтительный тип. Используется через наследование или реализацию интерфейса.

```csharp
// До: жёсткая зависимость, невозможно тестировать
public class OrderProcessor
{
    public void Process(Order order)
    {
        // Прямой вызов — нет шва
        var emailService = new SmtpEmailService();
        emailService.Send(order.CustomerEmail, "Order confirmed");

        var db = new SqlConnection("Server=prod;Database=Orders;");
        // сохраняем в БД...
    }
}

// После: Object Seam через выделение зависимостей
public class OrderProcessor
{
    private readonly IEmailService _emailService;
    private readonly IOrderRepository _orderRepository;

    // Шов — через конструктор можно подставить любую реализацию
    public OrderProcessor(IEmailService emailService, IOrderRepository orderRepository)
    {
        _emailService = emailService;
        _orderRepository = orderRepository;
    }

    public void Process(Order order)
    {
        _emailService.Send(order.CustomerEmail, "Order confirmed");
        _orderRepository.Save(order);
    }
}

// Тест с использованием шва
[Test]
public void Process_SendsEmailToCustomer()
{
    var mockEmail = new Mock<IEmailService>();
    var mockRepo = new Mock<IOrderRepository>();
    var processor = new OrderProcessor(mockEmail.Object, mockRepo.Object);

    processor.Process(new Order { CustomerEmail = "test@test.com" });

    mockEmail.Verify(e => e.Send("test@test.com", "Order confirmed"), Times.Once);
}
```

### Preprocessing Seam

В C#/.NET редко используется напрямую, но аналогом служат директивы условной компиляции.

```csharp
public class PaymentGateway
{
    public PaymentResult Charge(CreditCard card, decimal amount)
    {
#if TESTING
        // Preprocessing seam: в тестовом режиме не обращаемся к реальному шлюзу
        return new PaymentResult { Success = true, TransactionId = "TEST-001" };
#else
        // Реальный вызов платёжного шлюза
        var client = new RealPaymentClient();
        return client.ProcessPayment(card, amount);
#endif
    }
}
```

### Link Seam

В .NET реализуется через подмену сборок или использование Assembly Binding Redirect. На практике чаще используется подход с конфигурацией DI-контейнера.

```xml
<!-- app.config / web.config — подмена сборки -->
<runtime>
  <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
    <dependentAssembly>
      <assemblyIdentity name="PaymentLib" publicKeyToken="abc123" />
      <bindingRedirect oldVersion="1.0.0.0" newVersion="2.0.0.0" />
    </dependentAssembly>
  </assemblyBinding>
</runtime>
```

---

## Техники рефакторинга

### Extract Method

```csharp
// ДО: Монолитный метод с перемешанной логикой
public class InvoiceService
{
    public decimal ProcessInvoice(Invoice invoice)
    {
        // Валидация
        if (invoice == null) throw new ArgumentNullException(nameof(invoice));
        if (invoice.Items == null || invoice.Items.Count == 0)
            throw new InvalidOperationException("Invoice has no items");
        if (invoice.CustomerId <= 0)
            throw new InvalidOperationException("Invalid customer");

        // Расчёт суммы
        decimal subtotal = 0;
        foreach (var item in invoice.Items)
        {
            decimal itemTotal = item.Quantity * item.UnitPrice;
            if (item.Discount > 0)
                itemTotal -= itemTotal * item.Discount / 100;
            subtotal += itemTotal;
        }

        // Применение налога
        decimal taxRate = 0.2m;
        if (invoice.Region == "EU") taxRate = 0.21m;
        if (invoice.Region == "US") taxRate = 0.07m;
        decimal tax = subtotal * taxRate;

        decimal total = subtotal + tax;
        return total;
    }
}

// ПОСЛЕ: Логика разбита на читаемые методы
public class InvoiceService
{
    public decimal ProcessInvoice(Invoice invoice)
    {
        ValidateInvoice(invoice);
        var subtotal = CalculateSubtotal(invoice.Items);
        var tax = CalculateTax(subtotal, invoice.Region);
        return subtotal + tax;
    }

    private void ValidateInvoice(Invoice invoice)
    {
        if (invoice == null) throw new ArgumentNullException(nameof(invoice));
        if (invoice.Items == null || invoice.Items.Count == 0)
            throw new InvalidOperationException("Invoice has no items");
        if (invoice.CustomerId <= 0)
            throw new InvalidOperationException("Invalid customer");
    }

    private decimal CalculateSubtotal(IReadOnlyList<InvoiceItem> items)
    {
        return items.Sum(item => CalculateItemTotal(item));
    }

    private decimal CalculateItemTotal(InvoiceItem item)
    {
        var total = item.Quantity * item.UnitPrice;
        if (item.Discount > 0)
            total -= total * item.Discount / 100;
        return total;
    }

    private decimal CalculateTax(decimal subtotal, string region)
    {
        var taxRate = region switch
        {
            "EU" => 0.21m,
            "US" => 0.07m,
            _ => 0.2m
        };
        return subtotal * taxRate;
    }
}
```

### Extract Class

```csharp
// ДО: God Class — один класс делает всё
public class UserManager
{
    public void Register(string email, string password)
    {
        // Валидация email
        if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            throw new ArgumentException("Invalid email");

        // Хэширование пароля
        using var sha = SHA256.Create();
        var hash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(password)));

        // Сохранение в БД
        using var conn = new SqlConnection("...");
        conn.Open();
        using var cmd = new SqlCommand("INSERT INTO Users (Email, PasswordHash) VALUES (@e, @p)", conn);
        cmd.Parameters.AddWithValue("@e", email);
        cmd.Parameters.AddWithValue("@p", hash);
        cmd.ExecuteNonQuery();

        // Отправка приветственного письма
        var smtp = new SmtpClient("smtp.company.com");
        smtp.Send("noreply@company.com", email, "Welcome!", "Thank you for registering.");
    }
}

// ПОСЛЕ: Ответственности распределены по классам
public class UserRegistrationService
{
    private readonly IEmailValidator _emailValidator;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserRepository _userRepository;
    private readonly IEmailSender _emailSender;

    public UserRegistrationService(
        IEmailValidator emailValidator,
        IPasswordHasher passwordHasher,
        IUserRepository userRepository,
        IEmailSender emailSender)
    {
        _emailValidator = emailValidator;
        _passwordHasher = passwordHasher;
        _userRepository = userRepository;
        _emailSender = emailSender;
    }

    public async Task RegisterAsync(string email, string password)
    {
        _emailValidator.Validate(email);
        var hash = _passwordHasher.Hash(password);
        await _userRepository.CreateAsync(new User { Email = email, PasswordHash = hash });
        await _emailSender.SendWelcomeEmailAsync(email);
    }
}
```

### Replace Conditional with Polymorphism

```csharp
// ДО: Цепочка условий, которая растёт с каждой новой скидкой
public class DiscountCalculator
{
    public decimal Calculate(Customer customer, decimal orderTotal)
    {
        if (customer.Type == "Gold")
            return orderTotal * 0.15m;
        else if (customer.Type == "Silver")
            return orderTotal * 0.10m;
        else if (customer.Type == "Bronze")
            return orderTotal * 0.05m;
        else if (customer.Type == "Employee")
            return orderTotal * 0.30m;
        else
            return 0m;
    }
}

// ПОСЛЕ: Полиморфизм — каждая стратегия отдельно
public interface IDiscountStrategy
{
    decimal Calculate(decimal orderTotal);
}

public class GoldDiscount : IDiscountStrategy
{
    public decimal Calculate(decimal orderTotal) => orderTotal * 0.15m;
}

public class SilverDiscount : IDiscountStrategy
{
    public decimal Calculate(decimal orderTotal) => orderTotal * 0.10m;
}

public class BronzeDiscount : IDiscountStrategy
{
    public decimal Calculate(decimal orderTotal) => orderTotal * 0.05m;
}

public class EmployeeDiscount : IDiscountStrategy
{
    public decimal Calculate(decimal orderTotal) => orderTotal * 0.30m;
}

public class NoDiscount : IDiscountStrategy
{
    public decimal Calculate(decimal orderTotal) => 0m;
}

public class DiscountStrategyFactory
{
    private readonly Dictionary<string, IDiscountStrategy> _strategies = new()
    {
        ["Gold"] = new GoldDiscount(),
        ["Silver"] = new SilverDiscount(),
        ["Bronze"] = new BronzeDiscount(),
        ["Employee"] = new EmployeeDiscount()
    };

    public IDiscountStrategy GetStrategy(string customerType)
    {
        return _strategies.GetValueOrDefault(customerType, new NoDiscount());
    }
}
```

### Introduce Parameter Object

```csharp
// ДО: Метод с большим количеством параметров
public List<Order> SearchOrders(
    DateTime? startDate, DateTime? endDate,
    string customerName, string status,
    decimal? minAmount, decimal? maxAmount,
    int page, int pageSize,
    string sortBy, bool sortDescending)
{
    // ...
}

// ПОСЛЕ: Параметры инкапсулированы в объект
public record OrderSearchCriteria
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? CustomerName { get; init; }
    public string? Status { get; init; }
    public decimal? MinAmount { get; init; }
    public decimal? MaxAmount { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string SortBy { get; init; } = "CreatedDate";
    public bool SortDescending { get; init; } = true;
}

public List<Order> SearchOrders(OrderSearchCriteria criteria)
{
    // ...
}
```

---

## SOLID принципы в контексте рефакторинга

### Single Responsibility Principle (SRP)

> Класс должен иметь одну и только одну причину для изменения.

При рефакторинге legacy SRP — первый принцип, который нарушен. God-классы с тысячами строк нарушают SRP максимально.

```csharp
// НАРУШЕНИЕ SRP: класс отвечает за всё — бизнес-логику, доступ к данным, логирование
public class ReportService
{
    public byte[] GenerateMonthlyReport(int month, int year)
    {
        // 1. Получение данных из БД
        using var conn = new SqlConnection("...");
        var data = conn.Query<SalesData>("SELECT * FROM Sales WHERE ...");

        // 2. Бизнес-логика расчётов
        var totals = data.GroupBy(d => d.Category).Select(g => new { g.Key, Sum = g.Sum(x => x.Amount) });

        // 3. Генерация PDF
        var pdf = new PdfDocument();
        // ... 200 строк работы с PDF ...

        // 4. Логирование
        File.AppendAllText("log.txt", $"Report generated at {DateTime.Now}");

        // 5. Отправка по email
        new SmtpClient("smtp.company.com").Send("...", "...", "Report", "...");

        return pdf.ToBytes();
    }
}

// ПРИМЕНЕНИЕ SRP: каждый класс отвечает за одну задачу
public class MonthlyReportGenerator
{
    private readonly ISalesRepository _salesRepo;
    private readonly ISalesAggregator _aggregator;
    private readonly IPdfRenderer _pdfRenderer;

    public byte[] Generate(int month, int year)
    {
        var data = _salesRepo.GetSalesData(month, year);
        var summary = _aggregator.Aggregate(data);
        return _pdfRenderer.Render(summary);
    }
}
```

### Open/Closed Principle (OCP)

> Классы должны быть открыты для расширения, но закрыты для модификации.

```csharp
// НАРУШЕНИЕ: при добавлении нового формата экспорта нужно менять существующий код
public class ReportExporter
{
    public byte[] Export(Report report, string format)
    {
        switch (format)
        {
            case "PDF": return ExportToPdf(report);
            case "Excel": return ExportToExcel(report);
            case "CSV": return ExportToCsv(report);
            default: throw new NotSupportedException();
        }
    }
}

// ПРИМЕНЕНИЕ OCP: новый формат = новый класс, без изменения существующего кода
public interface IReportExporter
{
    string Format { get; }
    byte[] Export(Report report);
}

public class PdfReportExporter : IReportExporter
{
    public string Format => "PDF";
    public byte[] Export(Report report) { /* ... */ }
}

public class ExcelReportExporter : IReportExporter
{
    public string Format => "Excel";
    public byte[] Export(Report report) { /* ... */ }
}

// Регистрация — просто добавляем новую реализацию в DI
public class ReportExportService
{
    private readonly Dictionary<string, IReportExporter> _exporters;

    public ReportExportService(IEnumerable<IReportExporter> exporters)
    {
        _exporters = exporters.ToDictionary(e => e.Format);
    }

    public byte[] Export(Report report, string format)
    {
        if (!_exporters.TryGetValue(format, out var exporter))
            throw new NotSupportedException($"Format '{format}' is not supported");
        return exporter.Export(report);
    }
}
```

### Liskov Substitution Principle (LSP)

> Объекты подклассов должны быть заменяемы объектами суперкласса без нарушения корректности программы.

```csharp
// НАРУШЕНИЕ LSP: ReadOnlyRepository бросает исключение при вызове метода базового класса
public class Repository<T>
{
    public virtual void Add(T entity) { /* ... */ }
    public virtual T GetById(int id) { /* ... */ }
}

public class ReadOnlyRepository<T> : Repository<T>
{
    public override void Add(T entity)
    {
        throw new NotSupportedException("Read-only repository"); // нарушение LSP
    }
}

// ИСПРАВЛЕНИЕ: разделяем интерфейсы
public interface IReadRepository<T>
{
    T GetById(int id);
    IEnumerable<T> GetAll();
}

public interface IWriteRepository<T>
{
    void Add(T entity);
    void Update(T entity);
    void Delete(int id);
}

public interface IRepository<T> : IReadRepository<T>, IWriteRepository<T> { }
```

### Interface Segregation Principle (ISP)

> Клиенты не должны зависеть от интерфейсов, которые они не используют.

```csharp
// НАРУШЕНИЕ ISP: раздутый интерфейс
public interface IUserService
{
    User GetById(int id);
    List<User> GetAll();
    void Create(User user);
    void Update(User user);
    void Delete(int id);
    void SendEmail(int userId, string message);
    void ResetPassword(int userId);
    byte[] ExportToExcel();
    void ImportFromCsv(Stream csvStream);
    UserStatistics GetStatistics();
}

// ПРИМЕНЕНИЕ ISP: узкие специализированные интерфейсы
public interface IUserReader
{
    User GetById(int id);
    List<User> GetAll();
}

public interface IUserWriter
{
    void Create(User user);
    void Update(User user);
    void Delete(int id);
}

public interface IUserNotifier
{
    void SendEmail(int userId, string message);
    void ResetPassword(int userId);
}

public interface IUserDataExchange
{
    byte[] ExportToExcel();
    void ImportFromCsv(Stream csvStream);
}
```

### Dependency Inversion Principle (DIP)

> Модули верхнего уровня не должны зависеть от модулей нижнего уровня. Оба должны зависеть от абстракций.

```csharp
// НАРУШЕНИЕ DIP: бизнес-логика напрямую зависит от инфраструктуры
public class OrderService
{
    private readonly SqlOrderRepository _repository = new(); // конкретный класс
    private readonly SmtpEmailService _emailService = new(); // конкретный класс

    public void PlaceOrder(Order order)
    {
        _repository.Save(order);
        _emailService.SendConfirmation(order);
    }
}

// ПРИМЕНЕНИЕ DIP: зависимость от абстракций
public class OrderService
{
    private readonly IOrderRepository _repository;
    private readonly IEmailService _emailService;

    public OrderService(IOrderRepository repository, IEmailService emailService)
    {
        _repository = repository;
        _emailService = emailService;
    }

    public void PlaceOrder(Order order)
    {
        _repository.Save(order);
        _emailService.SendConfirmation(order);
    }
}
```

---

## Dependency Injection в Legacy Code

В legacy-коде DI часто полностью отсутствует. Зависимости создаются через `new` внутри методов. Внедрение DI — один из первых шагов при рефакторинге.

### Шаг 1: Extract and Override (минимальные изменения)

```csharp
// Legacy код
public class ReportGenerator
{
    public Report Generate()
    {
        var db = new DatabaseConnection("Server=prod;...");
        var data = db.ExecuteQuery("SELECT * FROM Sales");
        // ...
    }
}

// Шаг 1: выделяем создание зависимости в виртуальный метод
public class ReportGenerator
{
    public Report Generate()
    {
        var db = CreateDatabaseConnection();
        var data = db.ExecuteQuery("SELECT * FROM Sales");
        // ...
    }

    protected virtual IDatabaseConnection CreateDatabaseConnection()
    {
        return new DatabaseConnection("Server=prod;...");
    }
}

// Теперь можем тестировать через наследование
public class TestableReportGenerator : ReportGenerator
{
    protected override IDatabaseConnection CreateDatabaseConnection()
    {
        return new FakeDatabaseConnection();
    }
}
```

### Шаг 2: Introduce Constructor Injection (постепенно)

```csharp
// Добавляем конструктор с зависимостью, сохраняя обратную совместимость
public class ReportGenerator
{
    private readonly IDatabaseConnection _db;

    // Старый конструктор — для совместимости с существующим кодом
    public ReportGenerator()
        : this(new DatabaseConnection("Server=prod;..."))
    {
    }

    // Новый конструктор — для DI и тестов
    public ReportGenerator(IDatabaseConnection db)
    {
        _db = db;
    }

    public Report Generate()
    {
        var data = _db.ExecuteQuery("SELECT * FROM Sales");
        // ...
    }
}
```

### Шаг 3: Полноценный DI-контейнер

```csharp
// Program.cs / Startup.cs
builder.Services.AddScoped<IDatabaseConnection>(sp =>
    new DatabaseConnection(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<ReportGenerator>();
```

---

## Миграция с .NET Framework на .NET 6/7/8

### Пошаговый план

```
1. Аудит зависимостей
   - Проверить совместимость NuGet-пакетов с .NET 6+
   - Инструмент: .NET Upgrade Assistant, try-convert
   - Выявить несовместимые API (WCF, AppDomain, Remoting)

2. Переход на .NET Standard 2.0
   - Перевести общие библиотеки на .NET Standard 2.0
   - Это позволяет использовать их в обоих мирах

3. Замена несовместимых технологий
   - WCF Server -> gRPC или ASP.NET Core Web API
   - Web Forms -> Blazor или Razor Pages
   - WPF/WinForms -> остаются (поддерживаются в .NET 6+)
   - Entity Framework 6 -> Entity Framework Core
   - System.Web -> Microsoft.AspNetCore.*

4. Миграция проектов (.csproj)
   - Старый формат -> SDK-style csproj
   - Удаление packages.config -> PackageReference

5. Тестирование
   - Запуск characterization tests на каждом шаге
   - Параллельный запуск (Parallel Run) для критических путей
```

### Пример миграции csproj

```xml
<!-- ДО: Старый формат .NET Framework -->
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" />
  <PropertyGroup>
    <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
    <OutputType>Library</OutputType>
    <RootNamespace>MyApp.Core</RootNamespace>
    <AssemblyName>MyApp.Core</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="System" />
    <Reference Include="System.Core" />
    <Reference Include="Newtonsoft.Json, Version=13.0.0.0, ...">
      <HintPath>..\packages\Newtonsoft.Json.13.0.1\lib\net45\Newtonsoft.Json.dll</HintPath>
    </Reference>
  </ItemGroup>
  <ItemGroup>
    <Compile Include="Services\OrderService.cs" />
    <Compile Include="Models\Order.cs" />
    <!-- 200+ файлов перечислены вручную -->
  </ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>

<!-- ПОСЛЕ: SDK-style .NET 8 -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
```

---

## Покрытие тестами: с чего начать

### Golden Master (Approval Tests)

**Golden Master** — техника, при которой записывается «эталонный» вывод программы. При каждом следующем запуске результат сравнивается с эталоном.

```csharp
// Используем библиотеку ApprovalTests
[Test]
public void LegacyReport_GoldenMaster()
{
    var report = new LegacyReportGenerator();
    var result = report.GenerateHtmlReport(
        month: 1,
        year: 2025,
        department: "Sales");

    // Первый запуск: файл .approved.txt создаётся вручную из .received.txt
    // Последующие запуски: сравнение с .approved.txt
    Approvals.VerifyHtml(result);
}

// Для сложных объектов — сериализуем в JSON
[Test]
public void OrderProcessing_GoldenMaster()
{
    var processor = new LegacyOrderProcessor();
    var orders = LoadTestOrders();

    var results = orders.Select(o => processor.Process(o)).ToList();

    Approvals.VerifyJson(JsonConvert.SerializeObject(results, Formatting.Indented));
}
```

### С чего начинать покрытие тестами

```
Приоритеты покрытия (от высшего к низшему):

1. Критический бизнес-путь
   - Обработка платежей, расчёт цен, формирование отчётов
   - Любой баг здесь = прямые финансовые потери

2. Код, который часто меняется
   - git log --format=format: --name-only | sort | uniq -c | sort -rn | head -20
   - Самые часто изменяемые файлы — наибольший риск регрессии

3. Код с известными багами
   - Если модуль регулярно ломается — это первый кандидат на тесты

4. Новый код, добавляемый в legacy
   - Всегда писать тесты для нового кода — не допускать роста legacy
```

---

## Рефакторинг базы данных

### Стратегия рефакторинга схемы

```sql
-- Шаг 1: Добавить новый столбец (не удаляя старый)
ALTER TABLE Customers ADD FullName NVARCHAR(200);

-- Шаг 2: Миграция данных
UPDATE Customers SET FullName = FirstName + ' ' + LastName;

-- Шаг 3: Код переходит на новый столбец (deploy)
-- Шаг 4: Через N дней, после проверки, удаляем старые столбцы
ALTER TABLE Customers DROP COLUMN FirstName;
ALTER TABLE Customers DROP COLUMN LastName;
```

### Expand-Contract Pattern

```
Phase 1 (Expand):    Добавляем новую структуру, сохраняя старую
Phase 2 (Migrate):   Переносим данные, обновляем код для записи в обе структуры
Phase 3 (Contract):  Удаляем старую структуру после полного перехода
```

Пример с Entity Framework Core миграциями:

```csharp
// Миграция Phase 1: Expand
public partial class AddFullNameColumn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "FullName",
            table: "Customers",
            type: "nvarchar(200)",
            nullable: true);

        migrationBuilder.Sql(
            "UPDATE Customers SET FullName = FirstName + ' ' + LastName");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "FullName", table: "Customers");
    }
}

// Миграция Phase 3: Contract (через несколько спринтов)
public partial class RemoveSplitNameColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "FirstName", table: "Customers");
        migrationBuilder.DropColumn(name: "LastName", table: "Customers");
        migrationBuilder.AlterColumn<string>(
            name: "FullName",
            table: "Customers",
            nullable: false);
    }
}
```

---

## Работа с хранимыми процедурами

### Проблемы хранимых процедур в legacy

- Бизнес-логика размазана между C# и SQL
- Сложно тестировать — нужна реальная БД
- Нет рефакторинга, нет статического анализа
- Контроль версий затруднён

### Миграция хранимой процедуры в код

```sql
-- Legacy: хранимая процедура с бизнес-логикой
CREATE PROCEDURE sp_CalculateOrderTotal
    @OrderId INT,
    @Total DECIMAL(18,2) OUTPUT
AS
BEGIN
    DECLARE @Subtotal DECIMAL(18,2)
    DECLARE @DiscountPercent DECIMAL(5,2)
    DECLARE @CustomerType NVARCHAR(50)

    SELECT @Subtotal = SUM(Quantity * UnitPrice)
    FROM OrderItems WHERE OrderId = @OrderId

    SELECT @CustomerType = c.CustomerType
    FROM Customers c
    INNER JOIN Orders o ON o.CustomerId = c.Id
    WHERE o.Id = @OrderId

    SET @DiscountPercent = CASE @CustomerType
        WHEN 'Gold' THEN 15.0
        WHEN 'Silver' THEN 10.0
        WHEN 'Bronze' THEN 5.0
        ELSE 0.0
    END

    SET @Total = @Subtotal * (1 - @DiscountPercent / 100.0)

    UPDATE Orders SET TotalAmount = @Total WHERE Id = @OrderId
END
```

```csharp
// ПОСЛЕ: бизнес-логика в C# коде — тестируема, поддерживаема
public class OrderTotalCalculator
{
    private readonly IOrderRepository _orderRepo;
    private readonly ICustomerRepository _customerRepo;
    private readonly IDiscountStrategy _discountStrategy;

    public OrderTotalCalculator(
        IOrderRepository orderRepo,
        ICustomerRepository customerRepo,
        IDiscountStrategy discountStrategy)
    {
        _orderRepo = orderRepo;
        _customerRepo = customerRepo;
        _discountStrategy = discountStrategy;
    }

    public async Task<decimal> CalculateAndUpdateAsync(int orderId)
    {
        var order = await _orderRepo.GetWithItemsAsync(orderId);
        var customer = await _customerRepo.GetByIdAsync(order.CustomerId);

        var subtotal = order.Items.Sum(i => i.Quantity * i.UnitPrice);
        var discount = _discountStrategy.Calculate(customer.Type, subtotal);
        var total = subtotal - discount;

        order.TotalAmount = total;
        await _orderRepo.UpdateAsync(order);

        return total;
    }
}

// Тест — без базы данных
[Test]
public async Task CalculateTotal_GoldCustomer_Applies15PercentDiscount()
{
    var orderRepo = new Mock<IOrderRepository>();
    orderRepo.Setup(r => r.GetWithItemsAsync(1))
        .ReturnsAsync(new Order
        {
            Id = 1,
            CustomerId = 100,
            Items = new List<OrderItem>
            {
                new() { Quantity = 2, UnitPrice = 50m },
                new() { Quantity = 1, UnitPrice = 100m }
            }
        });

    var customerRepo = new Mock<ICustomerRepository>();
    customerRepo.Setup(r => r.GetByIdAsync(100))
        .ReturnsAsync(new Customer { Id = 100, Type = "Gold" });

    var calculator = new OrderTotalCalculator(
        orderRepo.Object,
        customerRepo.Object,
        new StandardDiscountStrategy());

    var total = await calculator.CalculateAndUpdateAsync(1);

    Assert.That(total, Is.EqualTo(170m)); // (100 + 100) * 0.85
}
```

---

## Code Smells

### God Class

Класс, который знает слишком много и делает слишком многое. Типичные признаки: 1000+ строк, 50+ методов, 20+ полей.

**Решение:** Extract Class, применение SRP.

### Long Method

Метод длиной более 20-30 строк, выполняющий несколько логических операций.

**Решение:** Extract Method, Compose Method.

### Feature Envy

Метод больше обращается к данным другого класса, чем к своим собственным.

```csharp
// Feature Envy: метод OrderValidator слишком много знает о Customer
public class OrderValidator
{
    public bool CanPlaceOrder(Customer customer)
    {
        return customer.IsActive
            && customer.CreditLimit > customer.CurrentBalance
            && customer.RegistrationDate < DateTime.Now.AddDays(-30)
            && !customer.IsBlocked
            && customer.EmailConfirmed;
    }
}

// Исправление: логика переносится в Customer
public class Customer
{
    public bool CanPlaceOrders()
    {
        return IsActive
            && CreditLimit > CurrentBalance
            && RegistrationDate < DateTime.Now.AddDays(-30)
            && !IsBlocked
            && EmailConfirmed;
    }
}
```

### Shotgun Surgery

Одно изменение требует правок во множестве классов. Например, добавление нового поля клиента требует изменений в DTO, маппере, валидаторе, репозитории, контроллере, представлении.

**Решение:** Move Method/Field, объединение связанных обязанностей, паттерны MediatR/CQRS.

---

## Метрики качества

### Cyclomatic Complexity

Количество независимых путей через код. Каждый `if`, `while`, `for`, `case`, `&&`, `||` увеличивает метрику.

```
Значение  | Уровень риска
----------|--------------
1-10      | Простой, низкий риск
11-20     | Умеренная сложность
21-50     | Сложный, высокий риск
51+       | Нетестируемый, нуждается в рефакторинге
```

### Code Coverage

Процент строк/ветвей кода, покрытых тестами.

```
Рекомендации:
- Ниже 60%  — высокий риск регрессии
- 60-80%    — приемлемо для большинства проектов
- 80%+      — хороший уровень (не нужно стремиться к 100%)
- 100%      — часто нерентабельно, может привести к хрупким тестам
```

Важно: покрытие само по себе не гарантирует качество тестов. Тест может покрывать строку, но не проверять её результат.

### Technical Debt

Метафора Ward Cunningham: технический долг — это разница между текущим состоянием кода и его идеальным состоянием.

```
Типы технического долга:
- Осознанный (deliberate)    — "Знаем, что некрасиво, но deadline"
- Неосознанный (inadvertent) — "Не знали лучшего подхода"
- Устаревший (bit rot)       — Код устарел из-за изменения требований

Метрики:
- SQALE Rating (SonarQube): A-E
- Debt Ratio: отношение времени исправления к времени разработки
- Remediation Cost: оценка в часах/днях для устранения
```

---

## Инструменты

### SonarQube

Платформа непрерывного анализа качества кода.

```yaml
# sonar-project.properties
sonar.projectKey=my-legacy-app
sonar.sources=src/
sonar.tests=tests/
sonar.cs.opencover.reportsPaths=coverage.xml
sonar.cs.vstest.reportsPaths=results.trx

# Quality Gate — пороги качества:
# - Code Coverage > 80%
# - Duplicated Lines < 3%
# - Maintainability Rating = A
# - Reliability Rating = A
# - Security Rating = A
# - No new blocker/critical issues
```

Ключевые возможности:
- Обнаружение багов, уязвимостей, code smells
- Отслеживание технического долга
- Визуализация зависимостей
- Интеграция с CI/CD (Azure DevOps, Jenkins, GitHub Actions)

### NDepend

Специализированный инструмент для .NET с глубоким статическим анализом.

```csharp
// Пример CQLinq-правила NDepend
// Найти все методы с Cyclomatic Complexity > 15
warnif count > 0
from m in Application.Methods
where m.CyclomaticComplexity > 15
orderby m.CyclomaticComplexity descending
select new { m, m.CyclomaticComplexity, m.NbLinesOfCode }

// Найти классы с чрезмерными зависимостями
warnif count > 0
from t in Application.Types
where t.NbTypesUsed > 30
select new { t, t.NbTypesUsed, t.NbTypesUsingMe }
```

Ключевые возможности:
- Dependency Graph и Dependency Matrix
- Code Diff с предыдущей версией
- Правила качества на языке CQLinq
- Trend Monitoring — отслеживание динамики

### ReSharper / Rider

Инструменты JetBrains для автоматизированного рефакторинга.

```
Ключевые рефакторинги:
- Extract Method/Class/Interface  (Ctrl+R, Ctrl+M)
- Inline Variable/Method          (Ctrl+R, Ctrl+I)
- Rename                          (Ctrl+R, Ctrl+R)
- Move Type to File               (Ctrl+R, Ctrl+O)
- Safe Delete                     (Alt+Delete)
- Change Signature                (Ctrl+R, Ctrl+S)
- Pull Members Up / Push Down     (Ctrl+R, Ctrl+U / Ctrl+R, Ctrl+D)

Анализ:
- Solution-wide analysis — подсветка ошибок во всём решении
- Code Inspections — сотни встроенных проверок
- Type Dependency Diagram — визуализация зависимостей
```

---

## Вопросы на собеседовании

### 1. Что такое legacy code по определению Michael Feathers?

**Ответ:** Legacy code — это код без тестов. Feathers намеренно даёт такое определение, чтобы подчеркнуть: главная проблема legacy-кода — невозможность безопасно вносить изменения. Без тестов каждое изменение — это рулетка. Возраст кода и используемые технологии вторичны. Код на .NET 8, написанный вчера без тестов, уже является legacy, потому что его опасно менять.

### 2. Как вы подходите к рефакторингу legacy-системы? Опишите вашу стратегию.

**Ответ:** Я следую подходу "Cover and Modify":
1. Анализирую систему: определяю критические пути и области с наибольшим техническим долгом
2. Пишу characterization tests для фиксации текущего поведения
3. Определяю швы (seams) для внедрения зависимостей
4. Применяю рефакторинг малыми шагами: Extract Method, Extract Class
5. Внедряю DI постепенно (через конструктор с обратной совместимостью)
6. Применяю Strangler Fig для постепенной замены модулей
7. На каждом шаге проверяю, что characterization tests проходят

### 3. Что такое Strangler Fig Pattern и когда его применять?

**Ответ:** Strangler Fig — стратегия постепенной замены legacy-системы новой. Вместо рискованного "big bang rewrite" новая функциональность строится рядом со старой, и трафик постепенно перенаправляется через фасад (proxy). Паттерн подходит для крупных систем, где полная переписка занимает месяцы/годы. Риски минимальны, так как можно откатить перенаправление трафика в любой момент.

### 4. Что такое Seam? Какие типы швов вы знаете?

**Ответ:** Seam (шов) — место в коде, где можно изменить поведение без редактирования самого кода. Три типа:
- **Object Seam** — наиболее используемый. Замена зависимости через наследование или интерфейс. Позволяет подставить mock/fake в тестах.
- **Preprocessing Seam** — использование директив компиляции (`#if TESTING`). В .NET применяется редко.
- **Link Seam** — подмена сборки/DLL. В .NET реализуется через Assembly Binding Redirect или конфигурацию DI.

### 5. Объясните разницу между Golden Master и Characterization Tests.

**Ответ:** Characterization Test — общий подход к фиксации текущего поведения кода. Golden Master — конкретная реализация этого подхода, при которой сохраняется полный вывод программы (snapshot). Characterization test может проверять конкретные аспекты поведения (как unit test), а Golden Master фиксирует весь вывод целиком. Оба подхода не проверяют корректность — они проверяют неизменность поведения. На практике Golden Master хорош для начала (быстро покрыть большой объём кода), а затем заменяется на точечные unit-тесты.

### 6. Как внедрить Dependency Injection в legacy-код, который создаёт зависимости через `new`?

**Ответ:** Поэтапно:
1. **Extract and Override**: выделить создание зависимости в `virtual` метод, переопределить в тестах через наследование
2. **Параметризация конструктора**: добавить конструктор с параметром-зависимостью, сохранив старый конструктор по умолчанию (Bastard Injection) для обратной совместимости
3. **Интерфейс**: выделить интерфейс из конкретного класса зависимости
4. **DI-контейнер**: зарегистрировать зависимости в контейнере и убрать конструктор по умолчанию

Важно делать это **постепенно**, не пытаясь внедрить DI сразу во всей системе.

### 7. С какими основными проблемами вы сталкивались при миграции с .NET Framework на .NET 6+?

**Ответ:**
- **WCF Server** не поддерживается — замена на gRPC или REST API
- **System.Web** зависимости — требуют переписки на ASP.NET Core middleware
- **AppDomain** изоляция — заменяется на AssemblyLoadContext
- **Global.asax** — заменяется на Program.cs и Startup.cs
- **web.config** — миграция на appsettings.json
- **NuGet-пакеты**, не поддерживающие .NET Standard 2.0+
- **.NET Remoting** — полностью удалён, замена на gRPC
- **Windows-специфичные API** (Registry, WMI) — требуют условной компиляции или альтернатив

Инструмент **.NET Upgrade Assistant** автоматизирует часть миграции.

### 8. Какие Code Smells указывают на необходимость рефакторинга?

**Ответ:**
- **God Class** — класс с тысячами строк и десятками обязанностей. Решение: Extract Class, SRP.
- **Long Method** — метод длиннее 20-30 строк. Решение: Extract Method.
- **Feature Envy** — метод обращается к данным чужого класса чаще, чем к своим. Решение: Move Method.
- **Shotgun Surgery** — одно изменение задевает много классов. Решение: Move Method/Field, объединение.
- **Primitive Obsession** — использование примитивов вместо доменных типов. Решение: Value Object.
- **Data Clumps** — одни и те же группы параметров повторяются. Решение: Introduce Parameter Object.
- **Divergent Change** — один класс меняется по разным причинам (антипод SRP). Решение: Extract Class.

### 9. Как вы оцениваете качество legacy-кода? Какие метрики используете?

**Ответ:**
- **Cyclomatic Complexity** — количество путей выполнения. Выше 15 для метода — красный флаг.
- **Code Coverage** — покрытие тестами. Ниже 60% — высокий риск. Целевой уровень — 80%.
- **Coupling (Ca/Ce)** — афферентная и эфферентная связанность. Высокая связанность = хрупкий код.
- **Lines of Code per method/class** — простая, но эффективная метрика.
- **SQALE Rating** (SonarQube) — интегральная оценка поддерживаемости.
- **Churn rate** — частота изменений файла (из git). Высокий churn + низкое покрытие = приоритет для рефакторинга.
- **Debt Ratio** — процент технического долга относительно общего времени разработки.

### 10. Расскажите о паттерне Parallel Run. Когда его стоит применять?

**Ответ:** Parallel Run — стратегия, при которой старая и новая реализация выполняются одновременно, а результаты сравниваются. Основной ответ всегда отдаётся старой системой (для безопасности). Расхождения логируются и анализируются. Паттерн применяется, когда: цена ошибки высока (финансовые расчёты, биллинг), полное покрытие тестами невозможно, или нужна уверенность в идентичности поведения на реальных данных. Недостаток — двойная нагрузка на инфраструктуру и усложнённый код.

### 11. Как рефакторить базу данных с минимальным риском?

**Ответ:** Применяю **Expand-Contract Pattern**:
1. **Expand** — добавляю новую структуру параллельно со старой, мигрирую данные
2. **Migrate** — код пишет в обе структуры, читает из новой
3. **Contract** — через несколько спринтов, после подтверждения стабильности, удаляю старую структуру

Принципы: backward-compatible миграции, каждая миграция отдельно деплоится, откат должен быть всегда возможен. Использую EF Core Migrations или FluentMigrator.

### 12. Почему не стоит выполнять полную переписку legacy-системы (Big Bang Rewrite)?

**Ответ:** По нескольким причинам:
- **Второй системный эффект** (Brooks) — тенденция перепроектировать вторую систему
- Бизнес не может ждать месяцы/годы без новой функциональности
- Знание бизнес-правил теряется — оригинальные разработчики ушли, документации нет
- Новая система будет содержать новые баги при попытке воспроизвести все нюансы
- Оценка объёма работ всегда занижена в 2-3 раза
- Проект рискует быть отменённым до завершения

Предпочтительнее **инкрементальный подход** через Strangler Fig, постепенно заменяя части системы.

### 13. Как вы работаете с хранимыми процедурами при рефакторинге?

**Ответ:** Стратегия зависит от ситуации:
- Если процедура содержит **чистый SQL** (SELECT/INSERT без логики) — оставляю как есть или заменяю на LINQ/EF Core
- Если содержит **бизнес-логику** (условия, циклы, расчёты) — вытаскиваю логику в C# код, покрываю тестами
- Применяю Parallel Run: новый код и старая процедура работают параллельно, результаты сравниваются
- После подтверждения корректности — удаляю процедуру

Ключевой принцип: бизнес-логика должна быть в одном месте — в коде приложения, где её можно тестировать и отлаживать.

---

## Резюме

Рефакторинг legacy-кода — это **марафон, а не спринт**. Ключевые принципы:

1. **Никогда не рефакторить без тестов** — сначала cover, потом modify
2. **Малые шаги** — каждый шаг рефакторинга должен оставлять систему в рабочем состоянии
3. **Boy Scout Rule** — оставляй код чище, чем нашёл
4. **Прагматизм** — не весь legacy-код нуждается в рефакторинге; рефакторим то, что меняем
5. **Измеряй прогресс** — используй метрики (SonarQube, NDepend) для отслеживания улучшений
