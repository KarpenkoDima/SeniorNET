# 15. Performance Profiling и оптимизация в .NET

## Содержание

1. [Подход к оптимизации](#подход-к-оптимизации)
2. [Инструменты профилирования](#инструменты-профилирования)
3. [Garbage Collector в .NET](#garbage-collector-в-net)
4. [Memory Leaks: причины и диагностика](#memory-leaks-причины-и-диагностика)
5. [Zero-allocation паттерны](#zero-allocation-паттерны)
6. [Оптимизация строк](#оптимизация-строк)
7. [Value Types vs Reference Types](#value-types-vs-reference-types)
8. [struct vs class](#struct-vs-class)
9. [Async performance](#async-performance)
10. [EF Core performance](#ef-core-performance)
11. [Caching](#caching)
12. [HTTP performance](#http-performance)
13. [Вопросы на собеседовании](#вопросы-на-собеседовании)

---

## Подход к оптимизации

Золотое правило оптимизации — **никогда не оптимизируйте без измерений**. Интуиция разработчика о «узких местах» ошибается в большинстве случаев.

### Цикл Measure -> Analyze -> Optimize -> Verify

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Measure   │────>│   Analyze   │────>│  Optimize   │────>│   Verify    │
│ (Профилир.) │     │ (Найти узкое│     │ (Изменить   │     │ (Проверить  │
│             │     │  место)     │     │  код)       │     │  улучшение) │
└─────────────┘     └─────────────┘     └─────────────┘     └──────┬──────┘
       ^                                                           │
       └───────────────────────────────────────────────────────────┘
                        Повторить при необходимости
```

**1. Measure (Измерение):** Соберите baseline-метрики. Используйте профилировщики, бенчмарки, логи. Без baseline невозможно оценить эффект оптимизации.

**2. Analyze (Анализ):** Определите реальное узкое место. Применяйте правило Парето — 20% кода отвечают за 80% проблем с производительностью.

**3. Optimize (Оптимизация):** Вносите **одно** изменение за раз. Это позволяет точно определить, какая именно оптимизация дала эффект.

**4. Verify (Проверка):** Повторите измерение. Если прирост незначителен — откатите изменение. Не усложняйте код ради 1% улучшения.

```csharp
// Антипаттерн: «Мне кажется, что Dictionary медленный, заменю на массив»
// Правильный подход: измерить, доказать, оптимизировать, проверить

// Пример использования Stopwatch для быстрого замера
public class QuickBenchmark
{
    public static void MeasureAction(string name, Action action, int iterations = 1000)
    {
        // Прогрев (JIT-компиляция)
        action();

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            action();
        }
        sw.Stop();

        Console.WriteLine($"{name}: {sw.ElapsedMilliseconds}ms " +
                          $"({sw.ElapsedMilliseconds / (double)iterations:F3}ms/op)");
    }
}
```

> **Важно:** `Stopwatch` подходит для грубой оценки. Для точных микробенчмарков используйте BenchmarkDotNet.

---

## Инструменты профилирования

### dotTrace (JetBrains) — CPU Profiling

dotTrace позволяет анализировать, на что тратится процессорное время. Поддерживает несколько режимов профилирования:

- **Sampling** — минимальное влияние на производительность, снимает «снимки» стека вызовов с заданной частотой. Подходит для первичного анализа.
- **Tracing** — точное измерение каждого вызова метода. Значительно замедляет приложение, но даёт точные данные.
- **Line-by-line** — самый детальный, но и самый медленный режим. Показывает время выполнения каждой строки кода.
- **Timeline** — показывает поведение приложения во времени, включая потоки, GC-паузы, I/O ожидания.

```
Типичный workflow с dotTrace:
1. Запустить профилирование в режиме Timeline
2. Воспроизвести сценарий (нагрузка, запрос)
3. Найти Hot Spots — методы с наибольшим временем выполнения
4. Углубиться в Call Tree для понимания контекста вызова
5. Оптимизировать и перепрофилировать
```

### dotMemory (JetBrains) — Memory Profiling

dotMemory анализирует использование памяти и помогает выявлять утечки:

- **Snapshot comparison** — сравнение двух снимков для нахождения растущих объектов
- **Object retention graph** — цепочка ссылок, удерживающих объект в памяти
- **Dominators** — объекты, удаление которых освободит наибольший объём памяти
- **Traffic analysis** — какие типы создаются и собираются чаще всего

### Visual Studio Diagnostic Tools

Встроенные инструменты Visual Studio доступны без дополнительных лицензий:

- **CPU Usage** — показывает потребление CPU по методам
- **Memory Usage** — снимки кучи с анализом объектов
- **.NET Counters** — метрики рантайма (GC, ThreadPool, Exception rate)
- **Events viewer** — ETW-события для диагностики
- **Database tool** — анализ запросов к БД (время, количество)

```csharp
// Включение Diagnostic Tools программно через DiagnosticSource
using System.Diagnostics;

// Пользовательские EventSource для мониторинга
[EventSource(Name = "MyApp.Performance")]
public sealed class AppEventSource : EventSource
{
    public static readonly AppEventSource Instance = new();

    [Event(1, Level = EventLevel.Informational)]
    public void RequestStarted(string endpoint) => WriteEvent(1, endpoint);

    [Event(2, Level = EventLevel.Informational)]
    public void RequestCompleted(string endpoint, long elapsedMs)
        => WriteEvent(2, endpoint, elapsedMs);
}
```

### PerfView

PerfView — бесплатный инструмент от Microsoft для глубокого анализа производительности .NET-приложений. Работает на основе ETW (Event Tracing for Windows).

Ключевые возможности:
- Анализ CPU (стеки вызовов, flame graphs)
- Анализ GC (аллокации, паузы, поколения)
- Анализ исключений и потоков
- JIT-компиляция
- Работа с dump-файлами

```bash
# Сбор данных CPU и GC
PerfView.exe collect /GCCollectOnly /AcceptEULA

# Анализ аллокаций
PerfView.exe collect /GCOnly /AcceptEULA

# Профилирование CPU в течение 30 секунд
PerfView.exe collect /CircularMB:500 /MaxCollectSec:30 /AcceptEULA
```

### BenchmarkDotNet — микробенчмарки

BenchmarkDotNet — стандарт де-факто для микробенчмарков в .NET. Автоматически учитывает JIT-прогрев, статистическую достоверность и влияние GC.

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

[MemoryDiagnoser]          // Показывает аллокации
[RankColumn]               // Ранжирование результатов
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class StringConcatBenchmark
{
    private readonly string[] _items = Enumerable.Range(0, 100)
        .Select(i => i.ToString())
        .ToArray();

    [Benchmark(Baseline = true)]
    public string ConcatWithPlus()
    {
        string result = "";
        foreach (var item in _items)
            result += item;       // Каждый + создаёт новую строку
        return result;
    }

    [Benchmark]
    public string ConcatWithStringBuilder()
    {
        var sb = new StringBuilder();
        foreach (var item in _items)
            sb.Append(item);
        return sb.ToString();
    }

    [Benchmark]
    public string ConcatWithJoin()
    {
        return string.Join("", _items);
    }
}

// Результат (пример):
// |              Method |       Mean | Ratio |   Gen0 | Allocated |
// |-------------------- |-----------:|------:|-------:|----------:|
// | ConcatWithJoin      |   812.5 ns |  0.08 | 0.0534 |     336 B |
// | ConcatWithSB        |   965.3 ns |  0.10 | 0.0801 |     504 B |
// | ConcatWithPlus      | 9,847.1 ns |  1.00 | 3.8147 |  24,016 B |
```

---

## Garbage Collector в .NET

### Поколения (Generations)

GC в .NET использует **generational** (поколенческий) подход, основанный на гипотезе: большинство объектов живут недолго.

```
┌──────────────────────────────────────────────────────────────┐
│                     Managed Heap                             │
├──────────┬──────────┬──────────────┬──────────┬─────────────┤
│  Gen 0   │  Gen 1   │    Gen 2     │   LOH    │    POH      │
│ (Молодые)│(Средние) │  (Долгожив.) │ (>=85KB) │ (Pinned)    │
│ ~256 KB  │ ~2 MB    │  Без лимита  │          │ .NET 5+     │
├──────────┴──────────┴──────────────┴──────────┴─────────────┤
│  Частые     Средние      Редкие        Редкие     Редкие     │
│  сборки     сборки       сборки        (с Gen2)   сборки     │
└──────────────────────────────────────────────────────────────┘
```

**Gen 0:** Новые объекты. Сборка происходит чаще всего, но она самая быстрая (обычно < 1ms). Выжившие объекты переходят в Gen 1.

**Gen 1:** Буфер между короткоживущими и долгоживущими объектами. Сборка реже, чем Gen 0.

**Gen 2:** Долгоживущие объекты (статические данные, кэши, синглтоны). Полная сборка (Full GC) самая дорогая операция — может приводить к заметным паузам.

**LOH (Large Object Heap):** Объекты размером >= 85 000 байт. Собирается вместе с Gen 2. По умолчанию не уплотняется (no compaction), что может приводить к фрагментации.

**POH (Pinned Object Heap):** Появился в .NET 5. Для объектов, которые «закреплены» (pinned) и не должны перемещаться в памяти. Уменьшает фрагментацию основной кучи.

```csharp
// Информация о GC
Console.WriteLine($"Gen 0 collections: {GC.CollectionCount(0)}");
Console.WriteLine($"Gen 1 collections: {GC.CollectionCount(1)}");
Console.WriteLine($"Gen 2 collections: {GC.CollectionCount(2)}");
Console.WriteLine($"Total memory: {GC.GetTotalMemory(false) / 1024} KB");

// GC Info (.NET 6+)
var gcInfo = GC.GetGCMemoryInfo();
Console.WriteLine($"Heap size: {gcInfo.HeapSizeBytes / 1024 / 1024} MB");
Console.WriteLine($"Fragmented: {gcInfo.FragmentedBytes / 1024} KB");
Console.WriteLine($"Pinned objects: {gcInfo.PinnedObjectsCount}");
```

### Workstation GC vs Server GC

| Характеристика        | Workstation GC           | Server GC                    |
|------------------------|--------------------------|------------------------------|
| Число куч              | 1                        | По одной на логический CPU   |
| Потоки GC              | 1 (с низким приоритетом) | По одному на кучу (высокий)  |
| Размер сегментов       | Меньше                   | Больше                       |
| Пропускная способность | Ниже                     | Выше                         |
| Латентность            | Ниже                     | Выше (дольше паузы)          |
| Назначение             | Desktop/CLI приложения   | Серверные приложения (Web)   |

```xml
<!-- Включение Server GC в .csproj -->
<PropertyGroup>
    <ServerGarbageCollection>true</ServerGarbageCollection>
</PropertyGroup>
```

```json
// Или в runtimeconfig.json
{
  "runtimeOptions": {
    "configProperties": {
      "System.GC.Server": true,
      "System.GC.Concurrent": true
    }
  }
}
```

### Concurrent vs Background GC

**Concurrent GC (устаревший):** Выполнял часть сборки Gen 2 параллельно с потоками приложения. Заменён на Background GC.

**Background GC:** Эволюция Concurrent GC. Позволяет выполнять сборки Gen 0/Gen 1, пока идёт фоновая сборка Gen 2. Значительно уменьшает паузы.

```
Без Background GC:
  Приложение: ████████░░░░░░░████████████████
  GC Gen2:              ███████

С Background GC:
  Приложение: ████████████████████████████████
  GC Gen2 BG:     ░░░░░░░░░░░░░░░░░░░   (фоновый)
  GC Gen0/1:              ██  (эфемерные сборки разрешены)
```

### GC.Collect() — почему НЕ вызывать

```csharp
// АНТИПАТТЕРН: Принудительный вызов GC
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
```

Почему это плохо:

1. **Разрушает поколенческую модель** — объекты преждевременно продвигаются в старшие поколения, увеличивая стоимость будущих сборок.
2. **Приводит к лишним Full GC** — самая дорогая операция, вызывающая Stop-the-World паузы.
3. **GC настраивается автоматически** — рантайм адаптирует пороги и частоту сборок под реальный паттерн работы приложения.
4. **Ложное чувство контроля** — вызов `GC.Collect()` не гарантирует освобождения конкретных объектов.

Допустимые случаи использования (крайне редко):
- Бенчмарки (очистка между итерациями)
- Уникальные сценарии с гарантированным освобождением большого объёма памяти
- Диагностика утечек при разработке

### Финализаторы и IDisposable

```csharp
// Правильная реализация паттерна Dispose
public class ResourceHolder : IDisposable
{
    private IntPtr _unmanagedResource;
    private ManagedStream _managedStream;
    private bool _disposed;

    ~ResourceHolder()
    {
        // Финализатор вызывается GC
        // Освобождаем ТОЛЬКО неуправляемые ресурсы
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        // Подавляем финализацию — ресурсы уже освобождены
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Освобождаем управляемые ресурсы
            _managedStream?.Dispose();
        }

        // Освобождаем неуправляемые ресурсы
        if (_unmanagedResource != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_unmanagedResource);
            _unmanagedResource = IntPtr.Zero;
        }

        _disposed = true;
    }
}
```

Проблемы с финализаторами:
- Объект с финализатором **живёт минимум на одну сборку GC дольше** (попадает в Finalization Queue, затем в F-Reachable Queue).
- Финализатор работает в **отдельном потоке** — нет гарантий порядка и времени выполнения.
- Если финализатор бросает исключение — процесс завершается.

---

## Memory Leaks: причины и диагностика

В managed-среде «утечка памяти» — это ситуация, когда объекты не могут быть собраны GC из-за удерживающих ссылок, хотя логически они больше не нужны.

### Event Handler Leaks

Самая распространённая причина утечек в .NET:

```csharp
// УТЕЧКА: publisher удерживает ссылку на subscriber через event
public class EventPublisher
{
    public event EventHandler DataChanged;
}

public class EventSubscriber
{
    private readonly EventPublisher _publisher;

    public EventSubscriber(EventPublisher publisher)
    {
        _publisher = publisher;
        _publisher.DataChanged += OnDataChanged; // Создаёт ссылку publisher -> subscriber
    }

    private void OnDataChanged(object? sender, EventArgs e) { /* ... */ }

    // Если subscriber не отписался, он не будет собран GC,
    // пока жив publisher
}

// РЕШЕНИЕ 1: Отписка в Dispose
public class FixedSubscriber : IDisposable
{
    private readonly EventPublisher _publisher;

    public FixedSubscriber(EventPublisher publisher)
    {
        _publisher = publisher;
        _publisher.DataChanged += OnDataChanged;
    }

    private void OnDataChanged(object? sender, EventArgs e) { /* ... */ }

    public void Dispose()
    {
        _publisher.DataChanged -= OnDataChanged;
    }
}

// РЕШЕНИЕ 2: Weak Event Pattern
public class WeakEventSubscriber
{
    public WeakEventSubscriber(EventPublisher publisher)
    {
        WeakEventManager<EventPublisher, EventArgs>
            .AddHandler(publisher, nameof(EventPublisher.DataChanged), OnDataChanged);
    }

    private void OnDataChanged(object? sender, EventArgs e) { /* ... */ }
}
```

### Static References

```csharp
// УТЕЧКА: статический список бесконечно накапливает данные
public static class AuditLog
{
    // Эта коллекция живёт всё время работы приложения!
    private static readonly List<AuditEntry> _entries = new();

    public static void Log(AuditEntry entry)
    {
        _entries.Add(entry); // Память растёт бесконечно
    }
}

// РЕШЕНИЕ: Ограниченный буфер или запись в внешнее хранилище
public static class FixedAuditLog
{
    private static readonly ConcurrentQueue<AuditEntry> _entries = new();
    private const int MaxEntries = 10_000;

    public static void Log(AuditEntry entry)
    {
        _entries.Enqueue(entry);
        while (_entries.Count > MaxEntries)
            _entries.TryDequeue(out _);
    }
}
```

### Closure Captures

```csharp
// УТЕЧКА: лямбда захватывает весь объект, хотя нужно только одно поле
public class DataProcessor
{
    private readonly byte[] _largeBuffer = new byte[10_000_000]; // 10 MB
    private readonly string _name;

    public Func<string> GetNameProvider()
    {
        // Лямбда захватывает this (а значит и _largeBuffer!)
        return () => _name;
    }
}

// РЕШЕНИЕ: Захватить только нужное значение в локальную переменную
public class FixedDataProcessor
{
    private readonly byte[] _largeBuffer = new byte[10_000_000];
    private readonly string _name;

    public Func<string> GetNameProvider()
    {
        var name = _name; // Копируем в локальную переменную
        return () => name; // Захватывает только name, не this
    }
}
```

### Unmanaged Resources

```csharp
// УТЕЧКА: неуправляемый ресурс не освобождается
public void ProcessImage()
{
    IntPtr handle = NativeMethods.CreateImageHandle();
    // Если здесь произойдёт исключение — handle не освободится
    ProcessHandle(handle);
    NativeMethods.ReleaseImageHandle(handle);
}

// РЕШЕНИЕ: SafeHandle или try/finally
public void FixedProcessImage()
{
    using var handle = new SafeImageHandle(NativeMethods.CreateImageHandle());
    ProcessHandle(handle.DangerousGetHandle());
    // SafeHandle автоматически освободит ресурс
}

// Или с помощью оператора using
public void AlsoFixedProcessImage()
{
    var bitmap = new Bitmap(1920, 1080);
    try
    {
        ProcessBitmap(bitmap);
    }
    finally
    {
        bitmap.Dispose();
    }
}
```

---

## Zero-allocation паттерны

### Span\<T\> и Memory\<T\>

`Span<T>` — это stack-only структура, которая представляет непрерывный участок памяти без аллокаций в куче.

```csharp
// Пример: парсинг строки без аллокаций
public static (int year, int month, int day) ParseDate(ReadOnlySpan<char> input)
{
    // input = "2025-01-15"
    // Никаких аллокаций — работаем с «окнами» в исходной строке
    var year = int.Parse(input[..4]);
    var month = int.Parse(input[5..7]);
    var day = int.Parse(input[8..10]);
    return (year, month, day);
}

// Сравнение с классическим подходом
public static (int year, int month, int day) ParseDateOld(string input)
{
    // Каждый Substring создаёт новую строку в куче
    var year = int.Parse(input.Substring(0, 4));   // аллокация!
    var month = int.Parse(input.Substring(5, 2));   // аллокация!
    var day = int.Parse(input.Substring(8, 2));     // аллокация!
    return (year, month, day);
}
```

`Memory<T>` — аналог `Span<T>`, но может храниться в куче (поля класса, async-методы):

```csharp
public class DataBuffer
{
    private Memory<byte> _buffer;  // OK — Memory<T> можно хранить в куче

    // private Span<byte> _span;  // ОШИБКА КОМПИЛЯЦИИ — Span<T> нельзя

    public async Task ProcessAsync(Memory<byte> data)
    {
        // Memory<T> можно использовать в async-методах
        await SomeOperationAsync(data);

        // Преобразование в Span для синхронной работы
        Span<byte> span = data.Span;
        ProcessSync(span);
    }

    private void ProcessSync(Span<byte> span)
    {
        for (int i = 0; i < span.Length; i++)
            span[i] = (byte)(span[i] ^ 0xFF);
    }
}
```

### ArrayPool\<T\>

`ArrayPool<T>` позволяет переиспользовать массивы, избегая частых аллокаций и давления на GC:

```csharp
// БЕЗ пула: каждый вызов аллоцирует новый массив
public byte[] ProcessWithoutPool(int size)
{
    byte[] buffer = new byte[size]; // Аллокация в куче
    FillBuffer(buffer);
    return buffer;
}

// С пулом: массивы переиспользуются
public void ProcessWithPool(int size)
{
    byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
    try
    {
        // ВНИМАНИЕ: Rent может вернуть массив больше запрошенного
        // Используйте size, а не buffer.Length
        FillBuffer(buffer.AsSpan(0, size));
        ProcessData(buffer.AsSpan(0, size));
    }
    finally
    {
        // ОБЯЗАТЕЛЬНО вернуть массив в пул!
        ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
    }
}

// Бенчмарк: ArrayPool vs new byte[]
[MemoryDiagnoser]
public class ArrayPoolBenchmark
{
    [Params(1024, 65536, 1048576)]
    public int Size { get; set; }

    [Benchmark(Baseline = true)]
    public byte[] NewArray()
    {
        return new byte[Size];
    }

    [Benchmark]
    public void RentAndReturn()
    {
        var buffer = ArrayPool<byte>.Shared.Rent(Size);
        ArrayPool<byte>.Shared.Return(buffer);
    }
}

// Результат (пример):
// |        Method |    Size |         Mean | Ratio |  Allocated |
// |-------------- |-------- |-------------:|------:|-----------:|
// |      NewArray |    1024 |    45.672 ns |  1.00 |    1,048 B |
// | RentAndReturn |    1024 |     9.213 ns |  0.20 |        0 B |
// |      NewArray |   65536 | 1,823.456 ns |  1.00 |   65,560 B |
// | RentAndReturn |   65536 |    10.567 ns |  0.01 |        0 B |
// |      NewArray | 1048576 | 9,234.123 ns |  1.00 | 1,048,600 B|
// | RentAndReturn | 1048576 |    11.234 ns |  0.00 |        0 B |
```

---

## Оптимизация строк

### StringBuilder

```csharp
// Правило: если конкатенация в цикле — используйте StringBuilder
public string BuildReport(IEnumerable<ReportLine> lines)
{
    // Указание начальной ёмкости уменьшает реаллокации
    var sb = new StringBuilder(lines.Count() * 80);

    foreach (var line in lines)
    {
        sb.Append(line.Date.ToString("yyyy-MM-dd"))
          .Append(" | ")
          .Append(line.Category.PadRight(20))
          .Append(" | ")
          .AppendLine(line.Amount.ToString("F2"));
    }

    return sb.ToString();
}
```

### string.Create

`string.Create` позволяет создать строку заданной длины и заполнить её содержимое без промежуточных аллокаций:

```csharp
// Создание строки без промежуточных аллокаций
public static string FormatHexBytes(byte[] bytes)
{
    return string.Create(bytes.Length * 2, bytes, (span, state) =>
    {
        for (int i = 0; i < state.Length; i++)
        {
            var hex = state[i].ToString("X2");
            span[i * 2] = hex[0];
            span[i * 2 + 1] = hex[1];
        }
    });
}

// Высокоэффективная версия с использованием lookup-таблицы
public static string FormatHexBytesFast(byte[] bytes)
{
    return string.Create(bytes.Length * 2, bytes, (span, state) =>
    {
        ReadOnlySpan<char> hexChars = "0123456789ABCDEF";
        for (int i = 0; i < state.Length; i++)
        {
            span[i * 2] = hexChars[state[i] >> 4];
            span[i * 2 + 1] = hexChars[state[i] & 0x0F];
        }
    });
}
```

### StringPool / String Interning

```csharp
// String interning — повторное использование идентичных строк
public class StringPoolExample
{
    // Стандартный intern-пул .NET
    public void InternExample()
    {
        string s1 = "hello";                          // В intern-пуле (литералы автоматически)
        string s2 = string.Intern(new string("hello")); // Принудительно интернировать
        Console.WriteLine(ReferenceEquals(s1, s2));   // True
    }

    // Пользовательский пул строк для сценариев с повторяющимися значениями
    private readonly ConcurrentDictionary<string, string> _pool = new();

    public string Deduplicate(string value)
    {
        return _pool.GetOrAdd(value, value);
    }
}

// Пример: При чтении CSV с повторяющимися значениями (страна, валюта и т.д.)
// дедупликация экономит значительный объём памяти
```

---

## Value Types vs Reference Types

### Влияние на производительность

```csharp
// Reference type — аллокация в куче, GC давление
public class PointClass
{
    public double X { get; set; }
    public double Y { get; set; }
}

// Value type — аллокация на стеке (если локальная переменная), без GC
public struct PointStruct
{
    public double X { get; set; }
    public double Y { get; set; }
}

[MemoryDiagnoser]
public class ValueVsRefBenchmark
{
    [Benchmark]
    public double SumWithClass()
    {
        double sum = 0;
        for (int i = 0; i < 10_000; i++)
        {
            var p = new PointClass { X = i, Y = i };  // 10000 аллокаций в куче
            sum += p.X + p.Y;
        }
        return sum;
    }

    [Benchmark]
    public double SumWithStruct()
    {
        double sum = 0;
        for (int i = 0; i < 10_000; i++)
        {
            var p = new PointStruct { X = i, Y = i }; // На стеке, 0 аллокаций
            sum += p.X + p.Y;
        }
        return sum;
    }
}

// Результат:
// |        Method |      Mean |  Allocated |
// |-------------- |----------:|-----------:|
// | SumWithStruct |  18.45 us |        0 B |
// | SumWithClass  | 112.34 us |  240,000 B |
```

### Boxing / Unboxing

```csharp
// Boxing: value type -> object (аллокация в куче)
int x = 42;
object boxed = x;          // Boxing! Копия значения помещается в кучу

// Unboxing: object -> value type
int y = (int)boxed;        // Unboxing! Копирование из кучи обратно

// Скрытый boxing через интерфейсы
IComparable comparable = x; // Boxing! struct приводится к интерфейсу

// Избежание boxing: generic constraints
public static T Max<T>(T a, T b) where T : IComparable<T>
{
    // Нет boxing — вызов через constrained call
    return a.CompareTo(b) > 0 ? a : b;
}
```

---

## struct vs class

### Когда использовать struct

Используйте `struct`, когда **все** условия выполняются:
1. Тип логически представляет одно значение (Point, DateTime, Color)
2. Размер экземпляра **не более 16 байт** (оптимально)
3. Тип неизменяемый (immutable)
4. Не требуется частое boxing

```csharp
// Хороший struct: маленький, immutable, представляет значение
public readonly struct Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Currency mismatch");
        return new Money(Amount + other.Amount, Currency);
    }
}

// Плохой struct: слишком большой, изменяемый
public struct BadLargeStruct  // НЕ делайте так
{
    public string Name;           // reference (8 bytes на x64)
    public string Description;    // reference (8 bytes)
    public decimal Price;         // 16 bytes
    public DateTime Created;      // 8 bytes
    public DateTime Modified;     // 8 bytes
    public int[] Tags;            // reference (8 bytes)
    // Итого > 16 байт, содержит ссылки — лучше class
}

// readonly struct — гарантия иммутабельности на уровне компилятора
public readonly struct Vector3
{
    public float X { get; }
    public float Y { get; }
    public float Z { get; }

    public Vector3(float x, float y, float z) => (X, Y, Z) = (x, y, z);

    // Все методы возвращают новый экземпляр
    public Vector3 Add(Vector3 other) =>
        new(X + other.X, Y + other.Y, Z + other.Z);

    public float Length() => MathF.Sqrt(X * X + Y * Y + Z * Z);
}

// ref struct — не может быть размещён в куче (как Span<T>)
public ref struct StackOnlyBuffer
{
    private Span<byte> _buffer;

    public StackOnlyBuffer(Span<byte> buffer) => _buffer = buffer;

    public void Fill(byte value) => _buffer.Fill(value);
}
```

---

## Async performance

### ValueTask vs Task

```csharp
// Task<T> всегда аллоцирует объект в куче
public async Task<int> GetValueAlwaysAllocates()
{
    if (_cache.TryGetValue("key", out int value))
        return value; // Всё равно аллокация Task<int>

    return await LoadFromDbAsync();
}

// ValueTask<T> — не аллоцирует, если результат доступен синхронно
public ValueTask<int> GetValueOptimized()
{
    if (_cache.TryGetValue("key", out int value))
        return new ValueTask<int>(value); // НЕТ аллокации — struct

    return new ValueTask<int>(LoadFromDbAsync());
}

// ВАЖНО: Ограничения ValueTask
// 1. Нельзя await несколько раз
// 2. Нельзя вызывать .Result/.GetAwaiter() одновременно
// 3. Нельзя использовать с Task.WhenAll / Task.WhenAny напрямую
```

### Pooling State Machines

В .NET 6+ включён пулинг async state machine для `ValueTask`-методов:

```csharp
// Включение пулинга state machine через атрибут (экспериментально)
[AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
public async ValueTask<string> GetDataPooled()
{
    // State machine берётся из пула вместо аллокации
    var data = await _httpClient.GetStringAsync("https://api.example.com/data");
    return data;
}

// ConfigureAwait(false) — избежание захвата SynchronizationContext
public async Task<string> ProcessAsync()
{
    // В библиотечном коде ВСЕГДА используйте ConfigureAwait(false)
    var data = await _httpClient.GetStringAsync(url).ConfigureAwait(false);
    var processed = await TransformAsync(data).ConfigureAwait(false);
    return processed;
}
```

### Избегание лишних async/await

```csharp
// ИЗБЫТОЧНО: async/await не нужен, если просто пробрасываем Task
public async Task<User> GetUserAsync(int id)
{
    return await _repository.GetByIdAsync(id); // Лишняя state machine
}

// ОПТИМАЛЬНО: возвращаем Task напрямую
public Task<User> GetUserAsync(int id)
{
    return _repository.GetByIdAsync(id); // Без state machine
}

// НО! Если есть using/try-catch — async/await НЕОБХОДИМ
public async Task<User> GetUserSafeAsync(int id)
{
    await using var connection = await _factory.CreateConnectionAsync();
    return await _repository.GetByIdAsync(id, connection);
    // Без async/await connection будет disposed до завершения запроса!
}
```

---

## EF Core performance

### AsNoTracking

```csharp
// По умолчанию EF Core отслеживает все загруженные сущности (Change Tracker)
// Это стоит памяти и CPU

// Для read-only запросов — используйте AsNoTracking
public async Task<List<Product>> GetProductsReadOnly()
{
    return await _context.Products
        .AsNoTracking()          // Без отслеживания изменений
        .Where(p => p.IsActive)
        .ToListAsync();
}

// AsNoTrackingWithIdentityResolution — без трекинга, но с дедупликацией
public async Task<List<Order>> GetOrdersWithProducts()
{
    return await _context.Orders
        .AsNoTrackingWithIdentityResolution() // Один Product == один объект
        .Include(o => o.Product)
        .ToListAsync();
}

// Глобальная настройка для read-heavy приложений
public class ReadOnlyDbContext : DbContext
{
    public ReadOnlyDbContext(DbContextOptions options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }
}
```

### Split Queries

```csharp
// Проблема: Cartesian Explosion при множественных Include
// Один запрос с JOIN может вернуть огромный результат

// ОДИН запрос с JOIN (по умолчанию)
var orders = await _context.Orders
    .Include(o => o.OrderItems)         // JOIN
    .Include(o => o.ShippingDetails)    // ещё JOIN = Cartesian product!
    .ToListAsync();

// SPLIT QUERY: Несколько отдельных запросов
var orders = await _context.Orders
    .Include(o => o.OrderItems)
    .Include(o => o.ShippingDetails)
    .AsSplitQuery()                     // 3 отдельных SQL-запроса
    .ToListAsync();

// Глобальная настройка
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    optionsBuilder.UseSqlServer(connectionString,
        o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
}
```

### Compiled Queries

```csharp
// Скомпилированные запросы кешируют план выполнения LINQ
// Полезно для часто вызываемых запросов

public class ProductRepository
{
    // Запрос компилируется один раз и переиспользуется
    private static readonly Func<AppDbContext, decimal, IAsyncEnumerable<Product>>
        _getExpensiveProducts = EF.CompileAsyncQuery(
            (AppDbContext ctx, decimal minPrice) =>
                ctx.Products.Where(p => p.Price >= minPrice));

    public IAsyncEnumerable<Product> GetExpensiveProducts(decimal minPrice)
    {
        return _getExpensiveProducts(_context, minPrice);
    }
}
```

### Projections (Select)

```csharp
// ПЛОХО: Загрузка всей сущности, когда нужны только 2 поля
var products = await _context.Products
    .Where(p => p.IsActive)
    .ToListAsync(); // Загружает ВСЕ столбцы

// ХОРОШО: Проекция — загружаем только нужные данные
var products = await _context.Products
    .Where(p => p.IsActive)
    .Select(p => new ProductDto
    {
        Id = p.Id,
        Name = p.Name
        // Не загружаем Description, Image, и другие тяжёлые столбцы
    })
    .ToListAsync();

// Бонус: Проекция автоматически включает AsNoTracking для анонимных типов
var stats = await _context.Orders
    .GroupBy(o => o.CustomerId)
    .Select(g => new
    {
        CustomerId = g.Key,
        TotalOrders = g.Count(),
        TotalAmount = g.Sum(o => o.Total)
    })
    .ToListAsync();
```

---

## Caching

### MemoryCache

```csharp
public class ProductService
{
    private readonly IMemoryCache _cache;
    private readonly IProductRepository _repository;

    public ProductService(IMemoryCache cache, IProductRepository repository)
    {
        _cache = cache;
        _repository = repository;
    }

    public async Task<Product?> GetProductAsync(int id)
    {
        var cacheKey = $"product:{id}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            entry.SlidingExpiration = TimeSpan.FromMinutes(2);
            entry.Size = 1; // Для ограничения размера кэша
            entry.Priority = CacheItemPriority.Normal;

            return await _repository.GetByIdAsync(id);
        });
    }

    public void InvalidateProduct(int id)
    {
        _cache.Remove($"product:{id}");
    }
}

// Регистрация с ограничением размера
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1000; // Максимум 1000 элементов
});
```

### IDistributedCache

```csharp
// Распределённый кэш для масштабируемых приложений
public class DistributedProductService
{
    private readonly IDistributedCache _cache;
    private readonly IProductRepository _repository;

    public async Task<Product?> GetProductAsync(int id)
    {
        var cacheKey = $"product:{id}";

        var cached = await _cache.GetStringAsync(cacheKey);
        if (cached is not null)
            return JsonSerializer.Deserialize<Product>(cached);

        var product = await _repository.GetByIdAsync(id);
        if (product is not null)
        {
            await _cache.SetStringAsync(cacheKey,
                JsonSerializer.Serialize(product),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                });
        }

        return product;
    }
}

// Регистрация Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
    options.InstanceName = "myapp:";
});
```

### Response Caching

```csharp
// Middleware для кэширования HTTP-ответов
builder.Services.AddResponseCaching();
app.UseResponseCaching();

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    [HttpGet]
    [ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "category", "page" })]
    public async Task<IActionResult> GetProducts(string? category, int page = 1)
    {
        // Ответ кэшируется на 60 секунд
        var products = await _service.GetProductsAsync(category, page);
        return Ok(products);
    }

    // Output Caching (.NET 7+) — более мощная альтернатива
    [HttpGet("{id}")]
    [OutputCache(Duration = 120, Tags = new[] { "products" })]
    public async Task<IActionResult> GetProduct(int id)
    {
        var product = await _service.GetProductAsync(id);
        return product is null ? NotFound() : Ok(product);
    }
}

// Инвалидация Output Cache по тегу
app.MapPost("api/products", async (Product product, IOutputCacheStore store) =>
{
    await SaveProduct(product);
    await store.EvictByTagAsync("products", default); // Инвалидировать все продукты
});
```

---

## HTTP performance

### HTTP/2

```csharp
// Kestrel с HTTP/2
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5001, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
        listenOptions.UseHttps();
    });
});

// HttpClient с HTTP/2
var handler = new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(15) // Переиспользование соединений
};

var client = new HttpClient(handler)
{
    DefaultRequestVersion = HttpVersion.Version20,
    DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
};
```

Преимущества HTTP/2:
- **Мультиплексирование** — несколько запросов через одно TCP-соединение
- **Header compression** (HPACK) — меньше трафика
- **Server Push** — сервер отправляет ресурсы до запроса клиента
- **Binary framing** — эффективнее текстового HTTP/1.1

### gRPC

```csharp
// gRPC: высокопроизводительный RPC на базе HTTP/2 и Protocol Buffers

// proto файл
// syntax = "proto3";
// service ProductService {
//   rpc GetProduct (GetProductRequest) returns (ProductReply);
//   rpc ListProducts (ListProductsRequest) returns (stream ProductReply);
// }

// Сервер
public class ProductGrpcService : ProductService.ProductServiceBase
{
    public override async Task<ProductReply> GetProduct(
        GetProductRequest request, ServerCallContext context)
    {
        var product = await _repository.GetByIdAsync(request.Id);
        return new ProductReply
        {
            Id = product.Id,
            Name = product.Name,
            Price = (double)product.Price
        };
    }

    // Server streaming — эффективная передача больших коллекций
    public override async Task ListProducts(
        ListProductsRequest request,
        IServerStreamWriter<ProductReply> responseStream,
        ServerCallContext context)
    {
        await foreach (var product in _repository.GetAllAsync())
        {
            await responseStream.WriteAsync(new ProductReply
            {
                Id = product.Id,
                Name = product.Name
            });
        }
    }
}
```

### Compression

```csharp
// Response Compression Middleware
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true; // Осторожно с BREACH-атаками
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/json", "application/xml" });
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Optimal; // Баланс скорость/сжатие
});

app.UseResponseCompression(); // Должен быть ДО UseStaticFiles

// Сжатие в HttpClient (запросы к внешним API)
var handler = new HttpClientHandler
{
    AutomaticDecompression = DecompressionMethods.All
};
```

---

## Вопросы на собеседовании

### 1. Что такое поколения GC и зачем они нужны?

**Ответ:** GC в .NET использует поколенческий подход на основе гипотезы о «смертности младенцев» — большинство объектов живут очень недолго. Heap разделён на поколения: Gen 0 (новые объекты, частые быстрые сборки), Gen 1 (буфер), Gen 2 (долгоживущие объекты, редкие дорогие сборки), LOH (объекты >= 85KB), POH (pinned-объекты, .NET 5+). Это позволяет собирать мусор эффективно — чаще проверять маленькие области памяти, где находятся короткоживущие объекты.

### 2. Почему нельзя вызывать GC.Collect() в production-коде?

**Ответ:** `GC.Collect()` вызывает полную сборку мусора (Full GC), которая является Stop-the-World операцией. Это приводит к паузам, нарушает автоматическую настройку порогов GC, преждевременно продвигает объекты в старшие поколения (увеличивая стоимость будущих сборок). GC самостоятельно оптимизирует свою работу под паттерн приложения. Допустимое использование — бенчмарки и диагностика при разработке.

### 3. В чём разница между Span\<T\> и Memory\<T\>?

**Ответ:** `Span<T>` — это `ref struct`, который существует только на стеке. Он не может быть полем класса, элементом массива, использоваться в async-методах или замыканиях. `Memory<T>` — обычная структура, которая может храниться в куче и использоваться в async-контексте. `Memory<T>` имеет свойство `.Span` для получения `Span<T>` при синхронной работе. Выбор: используйте `Span<T>` для синхронных операций, `Memory<T>` — когда нужна асинхронность или хранение в куче.

### 4. Когда ValueTask лучше Task?

**Ответ:** `ValueTask<T>` выгоден, когда метод **часто** возвращает результат синхронно (например, из кэша). В этом случае `ValueTask<T>` — struct, который не аллоцирует в куче. `Task<T>` всегда создаёт объект в куче. Однако `ValueTask<T>` имеет ограничения: нельзя await дважды, нельзя использовать `.Result` конкурентно, нельзя напрямую передавать в `Task.WhenAll`. Если метод всегда асинхронный — `Task<T>` проще и безопаснее.

### 5. Как найти утечку памяти в .NET-приложении?

**Ответ:** Алгоритм диагностики: (1) Обнаружение — мониторинг потребления памяти, метрики GC, OOM-исключения. (2) Сбор данных — снять два memory snapshot с интервалом в dotMemory или Visual Studio. (3) Сравнение — найти типы, количество экземпляров которых растёт. (4) Анализ retention path — определить цепочку ссылок, удерживающих объект. Типичные причины: неотписанные event handler'ы, статические коллекции, замыкания, захватывающие большие объекты, неосвобождённые unmanaged-ресурсы.

### 6. Чем AsNoTracking отличается от обычного запроса в EF Core?

**Ответ:** По умолчанию EF Core отслеживает все загруженные сущности в Change Tracker — хранит копию оригинальных значений, проверяет изменения при `SaveChanges()`. `AsNoTracking()` отключает отслеживание: снижается потребление памяти (нет копий), ускоряется материализация, но нельзя вызвать `SaveChanges()` для обновления. `AsNoTrackingWithIdentityResolution()` — компромисс: без трекинга, но с гарантией identity (один объект в БД = один объект в памяти).

### 7. Когда struct лучше class?

**Ответ:** struct предпочтителен, когда тип: (1) представляет единичное значение (координата, деньги, цвет), (2) имеет размер не более 16 байт, (3) является immutable, (4) не требует частого boxing. Struct копируется при передаче, поэтому большие изменяемые struct — антипаттерн. В высоконагруженных сценариях struct избавляет от аллокаций в куче и давления на GC. `readonly struct` гарантирует иммутабельность, `ref struct` (как Span\<T\>) ограничен стеком.

### 8. Как оптимизировать работу со строками в hot path?

**Ответ:** (1) `StringBuilder` для конкатенации в циклах, с указанием начальной ёмкости. (2) `string.Create` для формирования строки заданной длины без промежуточных аллокаций. (3) `Span<char>` / `ReadOnlySpan<char>` для парсинга без Substring. (4) `stackalloc` для небольших буферов. (5) Интернирование (`string.Intern`) для часто повторяющихся строк. (6) `StringPool` / `ConcurrentDictionary<string, string>` для дедупликации. (7) Использование `ISpanFormattable` и interpolated string handlers (.NET 6+).

### 9. В чём разница между Workstation GC и Server GC?

**Ответ:** Workstation GC использует одну кучу и один поток GC с низким приоритетом — минимальное влияние на UI, но ниже пропускная способность. Server GC создаёт по одной куче на каждый логический CPU с выделенными потоками GC с высоким приоритетом — максимальная пропускная способность, но длиннее паузы GC. Для веб-приложений (ASP.NET Core) по умолчанию включён Server GC. Для десктопных и CLI-приложений — Workstation GC.

### 10. Что такое Compiled Queries в EF Core и когда их использовать?

**Ответ:** `EF.CompileQuery` / `EF.CompileAsyncQuery` создаёт предварительно скомпилированный делегат, который пропускает этапы построения expression tree и трансляции в SQL при каждом вызове. Полезно для «горячих» запросов, вызываемых тысячи раз (например, поиск по ID). Обычные LINQ-запросы EF Core кэширует автоматически, но Compiled Queries исключают даже overhead поиска в кэше. Ограничение: нельзя динамически менять структуру запроса (только параметры).

### 11. Как ArrayPool\<T\> помогает производительности?

**Ответ:** `ArrayPool<T>.Shared` поддерживает пул массивов, которые можно переиспользовать вместо создания новых. Это уменьшает количество аллокаций и давление на GC, особенно для больших массивов (которые попадают в LOH). Важно: `Rent()` может вернуть массив больше запрошенного, `Return()` обязателен (иначе пул истощится), `clearArray: true` обнуляет массив перед возвратом в пул (безопасность данных).

### 12. Какие проблемы производительности решает Split Query в EF Core?

**Ответ:** При множественных `Include()` EF Core по умолчанию генерирует один SQL-запрос с JOIN'ами. Это приводит к Cartesian Explosion — если Order имеет 10 OrderItems и 5 ShippingDetails, результат содержит 50 строк вместо 15. `AsSplitQuery()` разбивает запрос на несколько отдельных SQL-запросов (по одному на каждую коллекцию), что уменьшает объём передаваемых данных. Компромисс: больше round-trip'ов к БД, нет атомарности (данные могут измениться между запросами).

### 13. Какие подходы к кэшированию существуют в ASP.NET Core?

**Ответ:** (1) **In-Memory Cache** (`IMemoryCache`) — быстрый, но локальный для процесса. (2) **Distributed Cache** (`IDistributedCache`) — Redis, SQL Server — разделяемый между инстансами. (3) **Response Caching** — HTTP-кэширование с заголовками `Cache-Control`. (4) **Output Caching** (.NET 7+) — серверное кэширование ответов с поддержкой тегов и политик инвалидации. Выбор зависит от сценария: In-Memory для горячих данных одного инстанса, Distributed для масштабируемых приложений, Output/Response для HTTP-ответов.

### 14. Как избежать аллокаций в async-методах?

**Ответ:** (1) Использовать `ValueTask<T>` вместо `Task<T>` для методов с частым синхронным результатом. (2) В .NET 6+ — `PoolingAsyncValueTaskMethodBuilder` для пулинга state machine. (3) Кэшировать часто используемые `Task<T>` через `Task.FromResult()`. (4) Не использовать `async/await`, если метод просто пробрасывает Task (но осторожно: необходим async при наличии using/try-catch). (5) `ConfigureAwait(false)` в библиотечном коде для избежания захвата SynchronizationContext.

---

## Резюме

| Область                     | Ключевые инструменты / техники                        |
|-----------------------------|-------------------------------------------------------|
| CPU Profiling               | dotTrace, PerfView, VS Diagnostic Tools               |
| Memory Profiling            | dotMemory, PerfView, VS Memory Usage                  |
| Микробенчмарки              | BenchmarkDotNet                                       |
| Zero-allocation             | Span\<T\>, Memory\<T\>, ArrayPool\<T\>, stackalloc   |
| Строки                      | StringBuilder, string.Create, string interning        |
| Типы данных                 | readonly struct, ref struct, boxing avoidance          |
| Async                       | ValueTask, PoolingAsyncValueTaskMethodBuilder          |
| EF Core                     | AsNoTracking, Split Queries, Compiled Queries, Select |
| Кэширование                 | MemoryCache, Redis, Output Caching                    |
| HTTP                        | HTTP/2, gRPC, Brotli/Gzip compression                 |

> **Главный принцип:** Всегда измеряйте перед оптимизацией. Преждевременная оптимизация — корень всех зол (Дональд Кнут). Код должен быть сначала правильным, затем понятным, и только потом быстрым.
