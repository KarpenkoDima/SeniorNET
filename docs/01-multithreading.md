# Многопоточность в C# — Подготовка к Senior .NET собеседованию

## Содержание

1. [Thread, ThreadPool — основы](#1-thread-threadpool--основы)
2. [Task, Task\<T\>, ValueTask](#2-task-taskt-valuetask)
3. [async/await — как работает под капотом](#3-asyncawait--как-работает-под-капотом)
4. [SynchronizationContext и ConfigureAwait](#4-synchronizationcontext-и-configureawait)
5. [Параллелизм: Parallel.For, Parallel.ForEach, PLINQ](#5-параллелизм-parallelfor-parallelforeach-plinq)
6. [Concurrent коллекции](#6-concurrent-коллекции)
7. [Примитивы синхронизации](#7-примитивы-синхронизации)
8. [Deadlock, Race Condition, Livelock](#8-deadlock-race-condition-livelock)
9. [Channels (System.Threading.Channels)](#9-channels)
10. [IAsyncEnumerable](#10-iasyncenumerable)
11. [Cancellation (CancellationToken)](#11-cancellation-cancellationtoken)
12. [Thread Safety паттерны](#12-thread-safety-паттерны)
13. [Вопросы на собеседовании с ответами](#13-вопросы-на-собеседовании-с-ответами)

---

## 1. Thread, ThreadPool — основы

### Thread

`Thread` — это низкоуровневый класс для создания и управления потоками в .NET. Каждый объект `Thread` представляет отдельный поток ОС. Создание потока — дорогая операция (выделяется ~1 МБ стека, происходит системный вызов), поэтому для кратковременных задач лучше использовать `ThreadPool` или `Task`.

```csharp
// Создание и запуск потока
var thread = new Thread(() =>
{
    Console.WriteLine($"Поток {Thread.CurrentThread.ManagedThreadId} выполняется");
    Thread.Sleep(1000);
    Console.WriteLine("Поток завершён");
});

thread.IsBackground = true; // фоновый поток — не препятствует завершению приложения
thread.Priority = ThreadPriority.Normal;
thread.Start();
thread.Join(); // ожидание завершения потока
```

**Foreground vs Background потоки:**

- **Foreground** — приложение не завершится, пока работает хотя бы один foreground-поток.
- **Background** — автоматически завершаются при завершении всех foreground-потоков.

```csharp
// Передача параметра в поток
var thread = new Thread(param =>
{
    string message = (string)param!;
    Console.WriteLine(message);
});
thread.Start("Привет из потока");
```

### ThreadPool

`ThreadPool` — это пул заранее созданных потоков, управляемый CLR. Потоки переиспользуются, что значительно снижает накладные расходы на создание и уничтожение потоков.

```csharp
// Постановка работы в пул потоков
ThreadPool.QueueUserWorkItem(state =>
{
    Console.WriteLine($"Выполняется в потоке пула: {Thread.CurrentThread.ManagedThreadId}");
});

// Настройка пула
ThreadPool.SetMinThreads(workerThreads: 4, completionPortThreads: 4);
ThreadPool.SetMaxThreads(workerThreads: 100, completionPortThreads: 100);

// Получение информации о пуле
ThreadPool.GetAvailableThreads(out int workerAvail, out int ioAvail);
Console.WriteLine($"Доступные worker-потоки: {workerAvail}, IO-потоки: {ioAvail}");
```

**Важно:** Потоки пула всегда являются background-потоками. Не стоит ставить длительные блокирующие операции в `ThreadPool` — это может привести к истощению пула (thread pool starvation).

---

## 2. Task, Task\<T\>, ValueTask

### Task и Task\<T\>

`Task` — это абстракция поверх `ThreadPool`, представляющая асинхронную операцию. `Task<T>` — аналогична, но возвращает результат типа `T`.

```csharp
// Запуск задачи через Task.Run (предпочтительный способ)
Task task = Task.Run(() =>
{
    Console.WriteLine("Выполняю работу...");
});
await task;

// Задача с результатом
Task<int> taskWithResult = Task.Run(() =>
{
    return Enumerable.Range(1, 100).Sum();
});
int result = await taskWithResult;
Console.WriteLine($"Сумма: {result}");

// Комбинаторы задач
Task<int> task1 = Task.Run(() => ComputeA());
Task<int> task2 = Task.Run(() => ComputeB());

// Ожидание всех задач
await Task.WhenAll(task1, task2);

// Ожидание первой завершившейся
Task<int> completed = await Task.WhenAny(task1, task2);
int firstResult = await completed;
```

### Продолжения (Continuations)

```csharp
Task<string> fetchTask = Task.Run(() => FetchData());

// ContinueWith — низкоуровневый способ (предпочитайте async/await)
fetchTask.ContinueWith(antecedent =>
{
    if (antecedent.IsCompletedSuccessfully)
        Console.WriteLine($"Данные: {antecedent.Result}");
    else if (antecedent.IsFaulted)
        Console.WriteLine($"Ошибка: {antecedent.Exception?.InnerException?.Message}");
}, TaskContinuationOptions.ExecuteSynchronously);
```

### ValueTask и ValueTask\<T\>

`ValueTask<T>` — это структура (`struct`), позволяющая избежать аллокации объекта `Task<T>` в куче, когда результат часто доступен синхронно (например, из кэша).

```csharp
// Типичное использование: метод с кэшем
private readonly ConcurrentDictionary<string, Data> _cache = new();

public ValueTask<Data> GetDataAsync(string key)
{
    // Синхронный путь — нет аллокации в куче
    if (_cache.TryGetValue(key, out var cached))
        return new ValueTask<Data>(cached);

    // Асинхронный путь — создаётся Task
    return new ValueTask<Data>(LoadDataAsync(key));
}

private async Task<Data> LoadDataAsync(string key)
{
    var data = await _repository.LoadAsync(key);
    _cache.TryAdd(key, data);
    return data;
}
```

**Ограничения ValueTask:**

- Нельзя `await` более одного раза.
- Нельзя одновременно ожидать из нескольких потоков.
- Нельзя вызывать `.GetAwaiter().GetResult()`, если задача не завершена.
- Если нужно нарушить эти ограничения, используйте `.AsTask()`.

```csharp
// Если нужно await несколько раз — преобразуйте в Task
ValueTask<int> vt = SomeMethodAsync();
Task<int> task = vt.AsTask(); // теперь можно await многократно
```

---

## 3. async/await — как работает под капотом

### State Machine (конечный автомат)

Когда компилятор встречает `async`-метод, он переписывает его в конечный автомат (state machine). Каждый `await` — это точка, где выполнение может быть приостановлено и возобновлено.

```csharp
// Исходный код
public async Task<string> GetDataAsync()
{
    var httpClient = new HttpClient();
    string raw = await httpClient.GetStringAsync("https://api.example.com/data");
    string processed = Process(raw);
    int length = await SaveAsync(processed);
    return $"Сохранено {length} байт";
}
```

Компилятор генерирует примерно следующее:

```csharp
// Упрощённая версия того, что генерирует компилятор
[CompilerGenerated]
private sealed class <GetDataAsync>d__0 : IAsyncStateMachine
{
    public int <>1__state; // текущее состояние: -1 (начало), 0, 1, ...
    public AsyncTaskMethodBuilder<string> <>t__builder;

    // Локальные переменные метода становятся полями
    private HttpClient httpClient;
    private string raw;
    private string processed;
    private int length;

    // Awaiters для каждого await
    private TaskAwaiter<string> <>u__1;
    private TaskAwaiter<int> <>u__2;

    public void MoveNext()
    {
        int num = <>1__state;
        string result;
        try
        {
            TaskAwaiter<string> awaiter;
            TaskAwaiter<int> awaiter2;

            switch (num)
            {
                case 0:
                    goto Label_GetStringCompleted;
                case 1:
                    goto Label_SaveCompleted;
            }

            // state == -1: начало метода
            httpClient = new HttpClient();
            awaiter = httpClient.GetStringAsync("https://api.example.com/data").GetAwaiter();

            if (!awaiter.IsCompleted)
            {
                <>1__state = 0;
                <>u__1 = awaiter;
                <>t__builder.AwaitUnsafeOnCompleted(ref awaiter, ref this);
                return; // выход из метода — поток свободен
            }

            Label_GetStringCompleted:
            raw = <>u__1.GetResult();
            processed = Process(raw);
            awaiter2 = SaveAsync(processed).GetAwaiter();

            if (!awaiter2.IsCompleted)
            {
                <>1__state = 1;
                <>u__2 = awaiter2;
                <>t__builder.AwaitUnsafeOnCompleted(ref awaiter2, ref this);
                return;
            }

            Label_SaveCompleted:
            length = <>u__2.GetResult();
            result = $"Сохранено {length} байт";
        }
        catch (Exception ex)
        {
            <>t__builder.SetException(ex);
            return;
        }
        <>t__builder.SetResult(result);
    }
}
```

**Ключевые моменты:**

1. Каждый `await` — точка возможной приостановки.
2. Если задача уже завершена (`IsCompleted == true`), приостановки не происходит — метод продолжает синхронно.
3. Локальные переменные «поднимаются» в поля структуры/класса state machine.
4. `AsyncTaskMethodBuilder` управляет жизненным циклом `Task`, который возвращается вызывающему коду.
5. При приостановке callback регистрируется через `SynchronizationContext` или `TaskScheduler`.

### Async void — почему это опасно

```csharp
// ПЛОХО: исключения невозможно поймать извне
public async void HandleClick(object sender, EventArgs e)
{
    await DoWorkAsync(); // если здесь исключение — оно уйдёт в SynchronizationContext
}

// ХОРОШО: используйте async Task
public async Task HandleClickAsync()
{
    await DoWorkAsync();
}
```

`async void` допустим только для обработчиков событий. Исключения из `async void` бросаются напрямую в `SynchronizationContext` и могут привести к аварийному завершению приложения.

---

## 4. SynchronizationContext и ConfigureAwait

### SynchronizationContext

`SynchronizationContext` определяет, в каком контексте (потоке) будет продолжено выполнение после `await`. Разные платформы имеют свои реализации:

| Среда | SynchronizationContext | Поведение |
|---|---|---|
| WPF | `DispatcherSynchronizationContext` | Маршалит на UI-поток |
| WinForms | `WindowsFormsSynchronizationContext` | Маршалит на UI-поток |
| ASP.NET (classic) | `AspNetSynchronizationContext` | Маршалит на поток с HttpContext |
| ASP.NET Core | **null** | Нет контекста, продолжение в пуле потоков |
| Console App | **null** | Нет контекста, продолжение в пуле потоков |

```csharp
// Пример: после await выполнение продолжается в UI-потоке (WPF/WinForms)
public async Task UpdateUiAsync()
{
    string data = await FetchDataAsync(); // уходим из UI-потока
    // SynchronizationContext маршалит обратно на UI-поток
    labelStatus.Text = data; // безопасно обновляем UI
}
```

### ConfigureAwait(false)

`ConfigureAwait(false)` говорит: «Мне не нужно возвращаться в исходный `SynchronizationContext`; продолжай выполнение в потоке пула».

```csharp
// В библиотечном коде ВСЕГДА используйте ConfigureAwait(false)
public async Task<byte[]> DownloadAsync(string url)
{
    using var client = new HttpClient();
    // Не захватываем контекст — библиотеке не нужен UI-поток
    var response = await client.GetAsync(url).ConfigureAwait(false);
    return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
}
```

**Правила:**

- В **библиотечном коде** — всегда `ConfigureAwait(false)`.
- В **прикладном коде ASP.NET Core** — не обязательно (контекста нет), но и не навредит.
- В **UI-приложениях** — на верхнем уровне (обработчики событий) — **не используйте**, чтобы продолжить на UI-потоке.

```csharp
// Проблема без ConfigureAwait(false) в библиотеке + UI-контекст = deadlock
public void Button_Click(object sender, EventArgs e)
{
    // DEADLOCK! .Result блокирует UI-поток,
    // а продолжение после await пытается вернуться на UI-поток
    var result = GetDataAsync().Result;
}

public async Task<string> GetDataAsync()
{
    // Без ConfigureAwait(false) — пытается вернуться на UI-поток
    return await httpClient.GetStringAsync("https://api.example.com");
}

// Решение: ConfigureAwait(false) в библиотечном коде
public async Task<string> GetDataAsync()
{
    return await httpClient.GetStringAsync("https://api.example.com")
        .ConfigureAwait(false); // продолжение пойдёт в потоке пула
}
```

---

## 5. Параллелизм: Parallel.For, Parallel.ForEach, PLINQ

### Parallel.For и Parallel.ForEach

Выполняют итерации параллельно, распределяя работу по потокам пула. Подходят для CPU-bound задач.

```csharp
// Parallel.For
Parallel.For(0, 1000, i =>
{
    // Каждая итерация может выполняться в своём потоке
    ProcessItem(i);
});

// Parallel.ForEach
var items = Enumerable.Range(0, 10000).ToList();
Parallel.ForEach(items, item =>
{
    ProcessItem(item);
});

// С параметрами: ограничение степени параллелизма
var options = new ParallelOptions
{
    MaxDegreeOfParallelism = Environment.ProcessorCount,
    CancellationToken = cts.Token
};

Parallel.ForEach(items, options, item =>
{
    ProcessItem(item);
});
```

### Parallel.ForEach с локальным состоянием потока

```csharp
// Потокобезопасное вычисление суммы без lock
long totalSum = 0;

Parallel.ForEach(
    source: items,
    localInit: () => 0L,                         // инициализация локальной суммы для потока
    body: (item, state, localSum) =>
    {
        return localSum + ComputeValue(item);     // аккумулируем в локальной переменной
    },
    localFinally: localSum =>
    {
        Interlocked.Add(ref totalSum, localSum);  // атомарно складываем в общую сумму
    }
);
```

### Parallel.ForEachAsync (.NET 6+)

```csharp
// Асинхронная параллельная обработка
await Parallel.ForEachAsync(urls, new ParallelOptions
{
    MaxDegreeOfParallelism = 10,
    CancellationToken = cancellationToken
},
async (url, ct) =>
{
    var content = await httpClient.GetStringAsync(url, ct);
    await ProcessContentAsync(content, ct);
});
```

### PLINQ (Parallel LINQ)

```csharp
// Параллельная обработка LINQ-запроса
var results = items
    .AsParallel()                        // включаем параллелизм
    .WithDegreeOfParallelism(4)          // ограничиваем количество потоков
    .WithCancellation(cts.Token)         // поддержка отмены
    .Where(x => x.IsValid)
    .Select(x => Transform(x))
    .ToList();

// AsOrdered — сохранение порядка (за счёт производительности)
var ordered = items
    .AsParallel()
    .AsOrdered()
    .Select(x => ExpensiveTransform(x))
    .ToList();

// ForAll — выполняет действие без сбора результатов (максимальная производительность)
items
    .AsParallel()
    .Where(x => x.NeedsProcessing)
    .ForAll(x => Process(x));
```

**Когда использовать PLINQ:**

- Большой объём данных.
- CPU-bound операции в `Select`/`Where`.
- Порядок не важен (или допустимы накладные расходы `AsOrdered`).

**Когда НЕ использовать:**

- Малый объём данных (накладные расходы на параллелизм перевесят выигрыш).
- I/O-bound операции (лучше `async/await`).
- Операции с побочными эффектами, требующие синхронизации.

---

## 6. Concurrent коллекции

Все concurrent коллекции находятся в `System.Collections.Concurrent` и обеспечивают потокобезопасность без внешней блокировки.

### ConcurrentDictionary\<TKey, TValue\>

```csharp
var dict = new ConcurrentDictionary<string, int>();

// Добавление или обновление
dict.AddOrUpdate(
    key: "counter",
    addValue: 1,
    updateValueFactory: (key, oldValue) => oldValue + 1
);

// GetOrAdd — атомарно получить или добавить
int value = dict.GetOrAdd("total", key => ComputeInitialValue(key));

// ВНИМАНИЕ: фабричные делегаты могут вызываться несколько раз при конкурентном доступе.
// Гарантируется только то, что в словарь попадёт одно значение.
int val = dict.GetOrAdd("expensive", key =>
{
    // Этот делегат может быть вызван несколько раз!
    // Но только один результат будет добавлен.
    return ExpensiveComputation(key);
});

// Если вычисление дорогое, используйте Lazy<T>
var lazyDict = new ConcurrentDictionary<string, Lazy<Data>>();
var data = lazyDict.GetOrAdd("key", k => new Lazy<Data>(() => LoadData(k))).Value;
```

### ConcurrentQueue\<T\>

Потокобезопасная FIFO-очередь. Реализована на базе lock-free алгоритма (связный список сегментов с CAS-операциями).

```csharp
var queue = new ConcurrentQueue<WorkItem>();

// Производитель
queue.Enqueue(new WorkItem("task1"));
queue.Enqueue(new WorkItem("task2"));

// Потребитель
if (queue.TryDequeue(out WorkItem? item))
{
    Process(item);
}

// Проверка без извлечения
if (queue.TryPeek(out WorkItem? peeked))
{
    Console.WriteLine($"Следующий элемент: {peeked}");
}
```

### ConcurrentBag\<T\>

Неупорядоченная потокобезопасная коллекция. Оптимизирована для сценариев, где один и тот же поток добавляет и извлекает элементы (thread-local storage).

```csharp
var bag = new ConcurrentBag<Result>();

Parallel.ForEach(items, item =>
{
    var result = Process(item);
    bag.Add(result);       // каждый поток добавляет в своё локальное хранилище
});

// Извлечение (порядок не гарантирован)
while (bag.TryTake(out Result? result))
{
    Console.WriteLine(result);
}
```

### ConcurrentStack\<T\>

Потокобезопасный LIFO-стек.

```csharp
var stack = new ConcurrentStack<int>();

stack.Push(1);
stack.Push(2);
stack.Push(3);

if (stack.TryPop(out int top))
    Console.WriteLine(top); // 3

// Пакетное извлечение
int[] buffer = new int[2];
int count = stack.TryPopRange(buffer);
```

### BlockingCollection\<T\>

Обёртка над `IProducerConsumerCollection<T>` (по умолчанию — `ConcurrentQueue<T>`), добавляющая блокирующее ожидание и ограничение ёмкости.

```csharp
// Ограниченная коллекция (bounded buffer)
using var collection = new BlockingCollection<WorkItem>(boundedCapacity: 100);

// Производитель
Task producer = Task.Run(() =>
{
    for (int i = 0; i < 1000; i++)
    {
        // Блокируется, если коллекция заполнена
        collection.Add(new WorkItem(i));
    }
    collection.CompleteAdding(); // сигнал о завершении
});

// Потребитель
Task consumer = Task.Run(() =>
{
    // GetConsumingEnumerable блокируется, если коллекция пуста,
    // и завершается, когда вызван CompleteAdding() и коллекция опустела
    foreach (var item in collection.GetConsumingEnumerable())
    {
        Process(item);
    }
});

await Task.WhenAll(producer, consumer);
```

### Сравнительная таблица

| Коллекция | Структура | Сценарий использования |
|---|---|---|
| `ConcurrentDictionary` | Хеш-таблица с сегментной блокировкой | Кэши, lookup |
| `ConcurrentQueue` | Lock-free FIFO | Очередь задач, pipeline |
| `ConcurrentStack` | Lock-free LIFO | Обратный порядок обработки |
| `ConcurrentBag` | Thread-local списки | Параллельные вычисления (add/take одним потоком) |
| `BlockingCollection` | Обёртка + блокирующее ожидание | Producer-Consumer с bounded buffer |

---

## 7. Примитивы синхронизации

### lock (Monitor)

`lock` — синтаксический сахар над `Monitor.Enter`/`Monitor.Exit`. Обеспечивает взаимное исключение (mutual exclusion).

```csharp
private readonly object _syncRoot = new();
private int _counter;

public void Increment()
{
    lock (_syncRoot)
    {
        _counter++;
    }
}

// Что делает компилятор:
public void IncrementExpanded()
{
    bool lockTaken = false;
    try
    {
        Monitor.Enter(_syncRoot, ref lockTaken);
        _counter++;
    }
    finally
    {
        if (lockTaken)
            Monitor.Exit(_syncRoot);
    }
}

// Monitor.TryEnter — попытка захвата с таймаутом
public bool TryIncrement()
{
    bool lockTaken = false;
    try
    {
        Monitor.TryEnter(_syncRoot, TimeSpan.FromSeconds(1), ref lockTaken);
        if (lockTaken)
        {
            _counter++;
            return true;
        }
        return false;
    }
    finally
    {
        if (lockTaken)
            Monitor.Exit(_syncRoot);
    }
}

// Monitor.Wait / Pulse — условная синхронизация
private readonly Queue<WorkItem> _queue = new();

public void Produce(WorkItem item)
{
    lock (_syncRoot)
    {
        _queue.Enqueue(item);
        Monitor.Pulse(_syncRoot); // разбудить одного ожидающего
    }
}

public WorkItem Consume()
{
    lock (_syncRoot)
    {
        while (_queue.Count == 0)
            Monitor.Wait(_syncRoot); // отпустить lock и ждать Pulse
        return _queue.Dequeue();
    }
}
```

### Mutex

`Mutex` — именованный примитив синхронизации на уровне ОС. Работает между процессами.

```csharp
// Единственный экземпляр приложения
using var mutex = new Mutex(initiallyOwned: false, name: "Global\\MyAppSingleInstance");

if (!mutex.WaitOne(TimeSpan.Zero))
{
    Console.WriteLine("Приложение уже запущено!");
    return;
}

try
{
    RunApplication();
}
finally
{
    mutex.ReleaseMutex();
}
```

### Semaphore и SemaphoreSlim

`Semaphore` — ограничивает количество потоков, одновременно обращающихся к ресурсу. `SemaphoreSlim` — облегчённая версия без обращений к ОС.

```csharp
// Ограничение одновременных HTTP-запросов
private readonly SemaphoreSlim _throttler = new(initialCount: 10, maxCount: 10);

public async Task<string> FetchWithThrottleAsync(string url)
{
    await _throttler.WaitAsync(); // ожидание «слота»
    try
    {
        return await _httpClient.GetStringAsync(url);
    }
    finally
    {
        _throttler.Release();
    }
}

// Пакетная обработка с ограничением параллелизма
public async Task ProcessBatchAsync(IEnumerable<string> urls)
{
    var semaphore = new SemaphoreSlim(5); // максимум 5 одновременно

    var tasks = urls.Select(async url =>
    {
        await semaphore.WaitAsync();
        try
        {
            return await FetchAsync(url);
        }
        finally
        {
            semaphore.Release();
        }
    });

    await Task.WhenAll(tasks);
}
```

### ReaderWriterLockSlim

Разделяет доступ на чтение (множество одновременных читателей) и запись (эксклюзивный доступ).

```csharp
private readonly ReaderWriterLockSlim _rwLock = new();
private readonly Dictionary<string, string> _data = new();

public string? Read(string key)
{
    _rwLock.EnterReadLock();
    try
    {
        return _data.TryGetValue(key, out var value) ? value : null;
    }
    finally
    {
        _rwLock.ExitReadLock();
    }
}

public void Write(string key, string value)
{
    _rwLock.EnterWriteLock();
    try
    {
        _data[key] = value;
    }
    finally
    {
        _rwLock.ExitWriteLock();
    }
}

// Повышение блокировки: чтение -> запись
public void UpdateIfExists(string key, Func<string, string> transform)
{
    _rwLock.EnterUpgradeableReadLock();
    try
    {
        if (_data.TryGetValue(key, out var existing))
        {
            _rwLock.EnterWriteLock();
            try
            {
                _data[key] = transform(existing);
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }
    }
    finally
    {
        _rwLock.ExitUpgradeableReadLock();
    }
}
```

### SpinLock

`SpinLock` — блокировка с активным ожиданием (spinning). Подходит для очень коротких критических секций, где ожидание блокировки будет меньше, чем стоимость переключения контекста.

```csharp
private SpinLock _spinLock = new(enableThreadOwnerTracking: false);

public void CriticalSection()
{
    bool lockTaken = false;
    try
    {
        _spinLock.Enter(ref lockTaken);
        // Очень короткая операция
        _sharedValue++;
    }
    finally
    {
        if (lockTaken)
            _spinLock.Exit(useMemoryBarrier: false);
    }
}
```

**Предупреждение:** `SpinLock` — это struct. Не передавайте его по значению (по ссылке — `ref`), иначе будет создана копия.

### Interlocked

Атомарные операции без блокировок. Самый быстрый способ синхронизации для простых операций.

```csharp
private long _counter;
private int _flag;

public void AtomicOperations()
{
    // Атомарный инкремент
    Interlocked.Increment(ref _counter);

    // Атомарный декремент
    Interlocked.Decrement(ref _counter);

    // Атомарное сложение
    Interlocked.Add(ref _counter, 42);

    // Атомарная замена — возвращает старое значение
    long old = Interlocked.Exchange(ref _counter, 100);

    // Compare-And-Swap (CAS) — основа lock-free алгоритмов
    // Заменяет значение, только если оно равно ожидаемому
    long original = Interlocked.CompareExchange(
        location1: ref _counter,
        value: 200,       // новое значение
        comparand: 100    // ожидаемое текущее
    );
    // original == 100 → замена произошла
    // original != 100 → замена не произошла
}

// Паттерн: lock-free обновление
public void SafeIncrement()
{
    long initial, computed;
    do
    {
        initial = Interlocked.Read(ref _counter);
        computed = initial + 1;
    }
    while (Interlocked.CompareExchange(ref _counter, computed, initial) != initial);
}
```

### ManualResetEventSlim и AutoResetEvent

```csharp
// ManualResetEventSlim — сигнализация: после Set() все ожидающие проходят
var mre = new ManualResetEventSlim(initialState: false);

Task.Run(() =>
{
    Console.WriteLine("Ожидание сигнала...");
    mre.Wait();  // блокирует до вызова Set()
    Console.WriteLine("Сигнал получен!");
});

Thread.Sleep(2000);
mre.Set();   // все ожидающие потоки продолжат выполнение
// mre.Reset(); // сброс в несигнальное состояние

// AutoResetEvent — пропускает ровно один ожидающий поток
var are = new AutoResetEvent(initialState: false);
are.Set();      // один поток проходит
// автоматически сбрасывается после пропуска одного потока
```

---

## 8. Deadlock, Race Condition, Livelock

### Deadlock (Взаимная блокировка)

Deadlock возникает, когда два или более потока ждут ресурсы, захваченные друг другом.

```csharp
// Пример deadlock
private static readonly object LockA = new();
private static readonly object LockB = new();

// Поток 1
Task.Run(() =>
{
    lock (LockA)
    {
        Thread.Sleep(100); // имитация работы
        lock (LockB) // ждёт LockB, который захвачен потоком 2
        {
            Console.WriteLine("Поток 1: захватил оба замка");
        }
    }
});

// Поток 2
Task.Run(() =>
{
    lock (LockB)
    {
        Thread.Sleep(100);
        lock (LockA) // ждёт LockA, который захвачен потоком 1
        {
            Console.WriteLine("Поток 2: захватил оба замка");
        }
    }
});
// Программа зависнет — оба потока ждут друг друга бесконечно
```

**Как избежать deadlock:**

1. **Фиксированный порядок захвата** — всегда захватывайте блокировки в одном и том же порядке.
2. **Таймаут** — используйте `Monitor.TryEnter` с таймаутом.
3. **Избегайте вложенных блокировок** — минимизируйте количество одновременно удерживаемых блокировок.
4. **Используйте `ConfigureAwait(false)`** — для предотвращения async deadlock.

```csharp
// Решение: фиксированный порядок захвата
void SafeMethod()
{
    // Всегда сначала LockA, потом LockB
    lock (LockA)
    {
        lock (LockB)
        {
            // безопасная работа
        }
    }
}

// Решение: таймаут
void SafeMethodWithTimeout()
{
    bool lockATaken = false;
    bool lockBTaken = false;
    try
    {
        Monitor.TryEnter(LockA, TimeSpan.FromSeconds(1), ref lockATaken);
        if (lockATaken)
        {
            Monitor.TryEnter(LockB, TimeSpan.FromSeconds(1), ref lockBTaken);
            if (lockBTaken)
            {
                // работа с ресурсами
            }
        }
    }
    finally
    {
        if (lockBTaken) Monitor.Exit(LockB);
        if (lockATaken) Monitor.Exit(LockA);
    }
}
```

### Async Deadlock

```csharp
// Классический async deadlock в UI/ASP.NET (не Core)
public ActionResult Index()
{
    // DEADLOCK: .Result блокирует поток с SynchronizationContext,
    // а продолжение await пытается вернуться на этот же поток
    var data = GetDataAsync().Result;
    return View(data);
}

private async Task<string> GetDataAsync()
{
    var result = await httpClient.GetStringAsync("https://api.example.com");
    return result; // пытается вернуться на заблокированный поток
}

// Решения:
// 1. async all the way — не блокируйте асинхронный код
public async Task<ActionResult> Index()
{
    var data = await GetDataAsync();
    return View(data);
}

// 2. ConfigureAwait(false)
private async Task<string> GetDataAsync()
{
    var result = await httpClient.GetStringAsync("https://api.example.com")
        .ConfigureAwait(false);
    return result;
}
```

### Race Condition (Состояние гонки)

Возникает, когда результат зависит от порядка выполнения потоков, и этот порядок не контролируется.

```csharp
// Пример race condition
private int _balance = 1000;

public void Withdraw(int amount)
{
    // Проверка и изменение не атомарны — между ними может вклиниться другой поток
    if (_balance >= amount)       // Поток 1 читает: 1000 >= 800 ✓
    {                              // Поток 2 читает: 1000 >= 500 ✓
        Thread.Sleep(1);           // имитация работы
        _balance -= amount;        // Поток 1: 1000 - 800 = 200
    }                              // Поток 2: 200 - 500 = -300 (!!!)
}

// Решение 1: lock
private readonly object _balanceLock = new();

public void WithdrawSafe(int amount)
{
    lock (_balanceLock)
    {
        if (_balance >= amount)
        {
            _balance -= amount;
        }
    }
}

// Решение 2: Interlocked (lock-free)
public bool WithdrawLockFree(int amount)
{
    int initial, newBalance;
    do
    {
        initial = _balance;
        if (initial < amount)
            return false;
        newBalance = initial - amount;
    }
    while (Interlocked.CompareExchange(ref _balance, newBalance, initial) != initial);
    return true;
}
```

### Livelock (Активная блокировка)

Потоки не заблокированы, но постоянно реагируют на действия друг друга и не могут продвинуться.

```csharp
// Пример livelock: два потока «уступают» друг другу бесконечно
private int _resource1Owner = 0; // 0 = свободен, 1 = поток 1, 2 = поток 2
private int _resource2Owner = 0;

void Thread1Work()
{
    while (true)
    {
        Interlocked.CompareExchange(ref _resource1Owner, 1, 0);
        if (_resource1Owner == 1)
        {
            if (Interlocked.CompareExchange(ref _resource2Owner, 1, 0) == 0)
            {
                // Захватили оба ресурса — работаем
                DoWork();
                Interlocked.Exchange(ref _resource1Owner, 0);
                Interlocked.Exchange(ref _resource2Owner, 0);
                return;
            }
            // Не удалось захватить второй — «вежливо» отпускаем первый
            Interlocked.Exchange(ref _resource1Owner, 0);
            // Поток 2 делает то же самое — и так бесконечно
        }
    }
}

// Решение: случайная задержка перед повторной попыткой
void Thread1WorkFixed()
{
    var rng = new Random();
    while (true)
    {
        Interlocked.CompareExchange(ref _resource1Owner, 1, 0);
        if (_resource1Owner == 1)
        {
            if (Interlocked.CompareExchange(ref _resource2Owner, 1, 0) == 0)
            {
                DoWork();
                Interlocked.Exchange(ref _resource1Owner, 0);
                Interlocked.Exchange(ref _resource2Owner, 0);
                return;
            }
            Interlocked.Exchange(ref _resource1Owner, 0);
            // Случайная задержка разрывает «симметрию»
            Thread.Sleep(rng.Next(1, 50));
        }
    }
}
```

---

## 9. Channels

`System.Threading.Channels` — высокопроизводительная реализация паттерна Producer-Consumer для асинхронного кода. Рекомендуется вместо `BlockingCollection` в async-сценариях.

### Bounded Channel (ограниченный буфер)

```csharp
// Создание канала с ограниченной ёмкостью
var channel = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(capacity: 100)
{
    FullMode = BoundedChannelFullMode.Wait, // ожидать, если канал полон
    SingleReader = false,
    SingleWriter = false
});

// Производитель
async Task ProduceAsync(ChannelWriter<WorkItem> writer)
{
    for (int i = 0; i < 1000; i++)
    {
        await writer.WriteAsync(new WorkItem(i));
    }
    writer.Complete(); // сигнал о завершении
}

// Потребитель
async Task ConsumeAsync(ChannelReader<WorkItem> reader)
{
    await foreach (var item in reader.ReadAllAsync())
    {
        await ProcessAsync(item);
    }
}

// Запуск
var producer = ProduceAsync(channel.Writer);
var consumer = ConsumeAsync(channel.Reader);
await Task.WhenAll(producer, consumer);
```

### Unbounded Channel (неограниченный буфер)

```csharp
var channel = Channel.CreateUnbounded<LogEntry>(new UnboundedChannelOptions
{
    SingleReader = true, // оптимизация, если один потребитель
    SingleWriter = false
});
```

### Pipeline с Channels

```csharp
// Многоэтапная pipeline
public static class Pipeline
{
    public static ChannelReader<TOut> Transform<TIn, TOut>(
        this ChannelReader<TIn> source,
        Func<TIn, TOut> transform,
        int concurrency = 1)
    {
        var output = Channel.CreateUnbounded<TOut>();

        Task.Run(async () =>
        {
            var tasks = Enumerable.Range(0, concurrency).Select(async _ =>
            {
                await foreach (var item in source.ReadAllAsync())
                {
                    var result = transform(item);
                    await output.Writer.WriteAsync(result);
                }
            });

            await Task.WhenAll(tasks);
            output.Writer.Complete();
        });

        return output.Reader;
    }
}

// Использование
var source = Channel.CreateUnbounded<RawData>();
var pipeline = source.Reader
    .Transform(data => Parse(data), concurrency: 4)
    .Transform(parsed => Validate(parsed), concurrency: 2)
    .Transform(valid => Enrich(valid), concurrency: 4);

await foreach (var result in pipeline.ReadAllAsync())
{
    await SaveAsync(result);
}
```

### BoundedChannelFullMode

| Режим | Поведение |
|---|---|
| `Wait` | `WriteAsync` блокируется, пока не освободится место |
| `DropNewest` | Новый элемент вытесняет последний добавленный |
| `DropOldest` | Новый элемент вытесняет самый старый |
| `DropWrite` | Новый элемент отбрасывается |

---

## 10. IAsyncEnumerable

`IAsyncEnumerable<T>` позволяет асинхронно итерировать по последовательности элементов. Идеально для потоковой обработки данных из базы данных, API, файлов.

```csharp
// Генератор асинхронной последовательности
public async IAsyncEnumerable<User> GetUsersAsync(
    [EnumeratorCancellation] CancellationToken ct = default)
{
    int page = 0;
    bool hasMore = true;

    while (hasMore)
    {
        ct.ThrowIfCancellationRequested();
        var batch = await _repository.GetUsersPageAsync(page, pageSize: 100, ct);

        foreach (var user in batch)
        {
            yield return user;
        }

        hasMore = batch.Count == 100;
        page++;
    }
}

// Потребление
await foreach (var user in GetUsersAsync(cancellationToken))
{
    await ProcessUserAsync(user);
}

// LINQ-операции с System.Linq.Async (NuGet-пакет)
var activeUsers = GetUsersAsync()
    .WhereAwait(async u => await IsActiveAsync(u))
    .SelectAwait(async u => await EnrichUserAsync(u))
    .Take(100);

await foreach (var user in activeUsers)
{
    Console.WriteLine(user.Name);
}
```

### IAsyncEnumerable в ASP.NET Core

```csharp
// Контроллер: потоковая отдача данных
[HttpGet("users")]
public async IAsyncEnumerable<UserDto> GetUsers(
    [EnumeratorCancellation] CancellationToken ct)
{
    await foreach (var user in _userService.GetUsersAsync(ct))
    {
        yield return MapToDto(user);
    }
}
// ASP.NET Core сериализует элементы по мере их поступления (streaming JSON)
```

### Реализация IAsyncEnumerable вручную

```csharp
public class AsyncCounter : IAsyncEnumerable<int>
{
    private readonly int _count;
    private readonly int _delayMs;

    public AsyncCounter(int count, int delayMs)
    {
        _count = count;
        _delayMs = delayMs;
    }

    public IAsyncEnumerator<int> GetAsyncEnumerator(
        CancellationToken ct = default)
    {
        return new Enumerator(_count, _delayMs, ct);
    }

    private class Enumerator : IAsyncEnumerator<int>
    {
        private readonly int _count;
        private readonly int _delayMs;
        private readonly CancellationToken _ct;
        private int _current = -1;

        public Enumerator(int count, int delayMs, CancellationToken ct)
        {
            _count = count;
            _delayMs = delayMs;
            _ct = ct;
        }

        public int Current => _current;

        public async ValueTask<bool> MoveNextAsync()
        {
            await Task.Delay(_delayMs, _ct);
            _current++;
            return _current < _count;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
```

---

## 11. Cancellation (CancellationToken)

`CancellationToken` — стандартный механизм кооперативной отмены операций в .NET.

### Основы

```csharp
// Создание источника и токена
using var cts = new CancellationTokenSource();

// Передача токена в задачу
var task = DoWorkAsync(cts.Token);

// Отмена через 5 секунд
cts.CancelAfter(TimeSpan.FromSeconds(5));

// Или немедленная отмена
cts.Cancel();

try
{
    await task;
}
catch (OperationCanceledException)
{
    Console.WriteLine("Операция отменена");
}
```

### Паттерны использования

```csharp
// Проверка токена в цикле
public async Task ProcessItemsAsync(IEnumerable<Item> items, CancellationToken ct)
{
    foreach (var item in items)
    {
        ct.ThrowIfCancellationRequested(); // бросит OperationCanceledException
        await ProcessAsync(item, ct);
    }
}

// Передача токена во все асинхронные вызовы
public async Task<string> FetchDataAsync(CancellationToken ct)
{
    using var client = new HttpClient();
    var response = await client.GetAsync("https://api.example.com", ct);
    return await response.Content.ReadAsStringAsync(ct);
}

// Регистрация callback при отмене
public async Task MonitorAsync(CancellationToken ct)
{
    using var registration = ct.Register(() =>
    {
        Console.WriteLine("Отмена запрошена — выполняю очистку...");
        Cleanup();
    });

    await LongRunningOperationAsync(ct);
}
```

### Связывание токенов

```csharp
// Комбинирование нескольких токенов
using var cts1 = new CancellationTokenSource();
using var cts2 = new CancellationTokenSource();
using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts1.Token, cts2.Token);

// linkedCts.Token будет отменён, если отменён ЛЮБОЙ из исходных
var task = DoWorkAsync(linkedCts.Token);

cts1.Cancel(); // отменит и linkedCts.Token
```

### Таймаут через CancellationToken

```csharp
// Таймаут с помощью CancellationTokenSource
public async Task<Result> WithTimeoutAsync(CancellationToken externalToken)
{
    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
        externalToken, timeoutCts.Token);

    try
    {
        return await _service.CallAsync(linkedCts.Token);
    }
    catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
    {
        throw new TimeoutException("Операция превысила таймаут 30 секунд");
    }
}
```

### CancellationToken в ASP.NET Core

```csharp
[HttpGet("data")]
public async Task<IActionResult> GetData(CancellationToken ct)
{
    // ASP.NET Core автоматически передаёт токен,
    // привязанный к HttpContext.RequestAborted.
    // Если клиент разорвал соединение — токен отменяется.
    var data = await _repository.GetDataAsync(ct);
    return Ok(data);
}
```

---

## 12. Thread Safety паттерны

### Immutable объекты

Неизменяемые объекты потокобезопасны по определению, поскольку их состояние не может измениться после создания.

```csharp
// Immutable класс
public sealed class Configuration
{
    public string ConnectionString { get; }
    public int MaxRetries { get; }
    public TimeSpan Timeout { get; }

    public Configuration(string connectionString, int maxRetries, TimeSpan timeout)
    {
        ConnectionString = connectionString;
        MaxRetries = maxRetries;
        Timeout = timeout;
    }

    // Создание изменённой копии
    public Configuration WithMaxRetries(int maxRetries)
        => new(ConnectionString, maxRetries, Timeout);
}

// C# record — неизменяемый по умолчанию
public record UserDto(string Name, string Email, int Age);
```

### Thread-Local Storage

```csharp
// ThreadLocal<T> — каждый поток имеет свой экземпляр
private static readonly ThreadLocal<Random> _random =
    new(() => new Random(Thread.CurrentThread.ManagedThreadId));

public int GetRandomNumber()
{
    return _random.Value!.Next();
}

// AsyncLocal<T> — передаётся через async/await контекст
private static readonly AsyncLocal<string?> _correlationId = new();

public static string? CorrelationId
{
    get => _correlationId.Value;
    set => _correlationId.Value = value;
}

public async Task ProcessRequestAsync()
{
    CorrelationId = Guid.NewGuid().ToString();
    await DoWorkAsync(); // CorrelationId доступен внутри
}
```

### Double-Check Locking (Lazy Initialization)

```csharp
// Классический double-check locking
private volatile ExpensiveResource? _resource;
private readonly object _lock = new();

public ExpensiveResource Resource
{
    get
    {
        if (_resource is null)                // первая проверка без lock
        {
            lock (_lock)
            {
                if (_resource is null)        // вторая проверка с lock
                {
                    _resource = new ExpensiveResource();
                }
            }
        }
        return _resource;
    }
}

// Лучше: используйте Lazy<T>
private readonly Lazy<ExpensiveResource> _lazyResource =
    new(() => new ExpensiveResource(), LazyThreadSafetyMode.ExecutionAndPublication);

public ExpensiveResource Resource => _lazyResource.Value;
```

### Producer-Consumer с каналами

```csharp
public class BackgroundTaskQueue
{
    private readonly Channel<Func<CancellationToken, ValueTask>> _queue;

    public BackgroundTaskQueue(int capacity = 100)
    {
        _queue = Channel.CreateBounded<Func<CancellationToken, ValueTask>>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            });
    }

    public async ValueTask EnqueueAsync(Func<CancellationToken, ValueTask> workItem)
    {
        await _queue.Writer.WriteAsync(workItem);
    }

    public async ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(
        CancellationToken ct)
    {
        return await _queue.Reader.ReadAsync(ct);
    }
}

// Фоновый обработчик (BackgroundService)
public class QueuedHostedService : BackgroundService
{
    private readonly BackgroundTaskQueue _taskQueue;
    private readonly ILogger<QueuedHostedService> _logger;

    public QueuedHostedService(BackgroundTaskQueue taskQueue,
        ILogger<QueuedHostedService> logger)
    {
        _taskQueue = taskQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var workItem = await _taskQueue.DequeueAsync(ct);
            try
            {
                await workItem(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при выполнении задачи");
            }
        }
    }
}
```

### Атомарная замена ссылки (Copy-on-Write)

```csharp
// Потокобезопасное обновление коллекции без блокировок
private volatile ImmutableList<Subscriber> _subscribers = ImmutableList<Subscriber>.Empty;

public void Subscribe(Subscriber subscriber)
{
    ImmutableList<Subscriber> initial, computed;
    do
    {
        initial = _subscribers;
        computed = initial.Add(subscriber);
    }
    while (Interlocked.CompareExchange(ref _subscribers, computed, initial) != initial);
}

// Упрощённая версия с ImmutableInterlocked
public void SubscribeSimple(Subscriber subscriber)
{
    ImmutableInterlocked.Update(ref _subscribers, list => list.Add(subscriber));
}
```

### Striped Lock (Сегментная блокировка)

```csharp
// Уменьшение contention через несколько блокировок
public class StripedMap<TKey, TValue> where TKey : notnull
{
    private const int StripeCount = 16;
    private readonly object[] _locks;
    private readonly Dictionary<TKey, TValue>[] _stripes;

    public StripedMap()
    {
        _locks = Enumerable.Range(0, StripeCount).Select(_ => new object()).ToArray();
        _stripes = Enumerable.Range(0, StripeCount)
            .Select(_ => new Dictionary<TKey, TValue>()).ToArray();
    }

    private int GetStripeIndex(TKey key)
        => Math.Abs(key.GetHashCode() % StripeCount);

    public void AddOrUpdate(TKey key, TValue value)
    {
        int index = GetStripeIndex(key);
        lock (_locks[index])
        {
            _stripes[index][key] = value;
        }
    }

    public bool TryGet(TKey key, out TValue? value)
    {
        int index = GetStripeIndex(key);
        lock (_locks[index])
        {
            return _stripes[index].TryGetValue(key, out value);
        }
    }
}
```

---

## 13. Вопросы на собеседовании с ответами

### Вопрос 1: В чём разница между `Task.Run` и `Task.Factory.StartNew`?

**Ответ:**

`Task.Run` — это упрощённая обёртка над `Task.Factory.StartNew` со значениями по умолчанию, оптимизированными для большинства сценариев:

```csharp
// Task.Run эквивалентен:
Task.Factory.StartNew(action,
    CancellationToken.None,
    TaskCreationOptions.DenyChildAttach, // важно!
    TaskScheduler.Default);
```

Ключевые различия:

- `Task.Run` всегда использует `TaskScheduler.Default` (пул потоков), а `StartNew` использует текущий `TaskScheduler`.
- `Task.Run` автоматически разворачивает `Task<Task>` при передаче `async`-делегата. `StartNew` — нет.
- `Task.Run` устанавливает `DenyChildAttach`, предотвращая привязку дочерних задач.

```csharp
// ЛОВУШКА с StartNew и async-делегатом:
Task outer = Task.Factory.StartNew(async () =>
{
    await Task.Delay(1000);
});
await outer; // завершится немедленно! outer — это Task<Task>

// Правильно:
await Task.Factory.StartNew(async () =>
{
    await Task.Delay(1000);
}).Unwrap(); // разворачиваем Task<Task> -> Task

// Или просто:
await Task.Run(async () =>
{
    await Task.Delay(1000);
}); // Task.Run разворачивает автоматически
```

---

### Вопрос 2: Что такое `SynchronizationContext` и зачем нужен `ConfigureAwait(false)`?

**Ответ:**

`SynchronizationContext` — это абстракция, определяющая, в каком контексте (потоке) выполнять продолжение после `await`. В UI-приложениях (WPF, WinForms) контекст маршалит продолжение обратно на UI-поток. В ASP.NET (classic) — на поток с HttpContext.

`ConfigureAwait(false)` указывает, что продолжение не обязательно выполнять в захваченном контексте, а можно в любом потоке пула. Это:

1. Предотвращает deadlock при синхронном ожидании async-кода.
2. Улучшает производительность, избегая лишнего маршалинга.

В ASP.NET Core `SynchronizationContext` равен `null`, поэтому `ConfigureAwait(false)` технически не нужен, но в библиотечном коде его всё равно следует использовать для переносимости.

---

### Вопрос 3: Когда использовать `ValueTask` вместо `Task`?

**Ответ:**

`ValueTask<T>` следует использовать, когда метод часто возвращает результат синхронно (например, из кэша). В этом случае `ValueTask` позволяет избежать аллокации объекта `Task` в куче.

**Используйте ValueTask, когда:**
- Метод часто завершается синхронно.
- Метод вызывается в hot path с высокой частотой.
- Аллокация Task является узким местом (подтверждено профилированием).

**Не используйте ValueTask, когда:**
- Результат всегда асинхронный.
- Нужно await несколько раз или из нескольких потоков.
- Простота кода важнее производительности.

---

### Вопрос 4: Как происходит обработка исключений в `Task.WhenAll`?

**Ответ:**

```csharp
var task1 = Task.Run(() => throw new InvalidOperationException("Ошибка 1"));
var task2 = Task.Run(() => throw new ArgumentException("Ошибка 2"));

try
{
    await Task.WhenAll(task1, task2);
}
catch (Exception ex)
{
    // ex — только ПЕРВОЕ исключение!
    Console.WriteLine(ex.GetType()); // InvalidOperationException

    // Для получения ВСЕХ исключений:
    var allTask = Task.WhenAll(task1, task2);
    try { await allTask; } catch { }

    AggregateException aggregate = allTask.Exception!;
    foreach (var inner in aggregate.InnerExceptions)
    {
        Console.WriteLine(inner.Message); // "Ошибка 1", "Ошибка 2"
    }
}
```

`await` разворачивает `AggregateException` и бросает только первое внутреннее исключение. Для доступа ко всем исключениям нужно обратиться к свойству `Exception` результирующей задачи.

---

### Вопрос 5: В чём разница между `lock`, `Mutex`, `Semaphore` и `SemaphoreSlim`?

**Ответ:**

| Характеристика | `lock` / `Monitor` | `Mutex` | `Semaphore` | `SemaphoreSlim` |
|---|---|---|---|---|
| Уровень | Процесс | ОС (межпроцессный) | ОС (межпроцессный) | Процесс |
| Async-поддержка | Нет | Нет | Нет | Да (`WaitAsync`) |
| Производительность | Быстрый | Медленный | Медленный | Быстрый |
| Параллельный вход | 1 поток | 1 поток | N потоков | N потоков |
| Реентерабельность | Да | Да | Нет | Нет |
| Именованный | Нет | Да | Да | Нет |

---

### Вопрос 6: Что такое Thread Pool Starvation и как его обнаружить?

**Ответ:**

**Thread Pool Starvation** — ситуация, когда все потоки пула заняты (обычно заблокированы) и новые задачи не могут быть обработаны. CLR медленно создаёт новые потоки (1 поток в секунду), что приводит к резкому снижению производительности.

**Причины:**
- Синхронная блокировка в async-коде (`.Result`, `.Wait()`, `Thread.Sleep`).
- Длительные CPU-bound операции без `Task.Run`.
- Слишком много параллельных задач с блокирующими вызовами.

**Диагностика:**

```csharp
// Мониторинг пула потоков
ThreadPool.GetAvailableThreads(out int workerAvail, out int ioAvail);
ThreadPool.GetMaxThreads(out int workerMax, out int ioMax);
int busyWorkers = workerMax - workerAvail;
Console.WriteLine($"Занято потоков: {busyWorkers}/{workerMax}");
```

Также можно использовать `dotnet-counters` и `EventPipe`:
```
dotnet-counters monitor --counters System.Runtime[threadpool-thread-count,threadpool-queue-length]
```

**Решение:** не блокируйте асинхронный код. Используйте `async/await` «до конца» (async all the way down).

---

### Вопрос 7: Объясните разницу между параллелизмом (parallelism) и конкурентностью (concurrency).

**Ответ:**

- **Concurrency (конкурентность)** — это способность обрабатывать несколько задач, которые перекрываются во времени. Задачи могут выполняться на одном ядре, переключаясь между собой (time-slicing). Пример: async/await, event loop.

- **Parallelism (параллелизм)** — это буквально одновременное выполнение нескольких задач на разных ядрах/процессорах. Пример: `Parallel.ForEach`, PLINQ.

Конкурентность — это про структуру программы (composition), параллелизм — про выполнение (execution). Конкурентная программа может быть параллельной, но не обязана.

```csharp
// Concurrency: I/O-bound задачи, один поток
await Task.WhenAll(
    httpClient.GetAsync("url1"),
    httpClient.GetAsync("url2"),
    httpClient.GetAsync("url3")
);

// Parallelism: CPU-bound задачи, несколько ядер
Parallel.For(0, 1000, i => HeavyComputation(i));
```

---

### Вопрос 8: Как реализовать паттерн «огонь и забудь» (fire-and-forget) безопасно?

**Ответ:**

```csharp
// ПЛОХО: исключение проглотится молча
_ = DoWorkAsync(); // предупреждение CS4014

// ПЛОХО: async void
public async void FireAndForget()
{
    await DoWorkAsync(); // исключение убьёт приложение
}

// ХОРОШО: безопасный fire-and-forget с логированием
public static class TaskExtensions
{
    public static void SafeFireAndForget(
        this Task task,
        ILogger? logger = null,
        [CallerMemberName] string? caller = null)
    {
        task.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                logger?.LogError(t.Exception,
                    "Fire-and-forget задача завершилась с ошибкой в {Caller}",
                    caller);
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}

// Использование
SendEmailAsync(user.Email).SafeFireAndForget(_logger);

// ЛУЧШЕ: используйте фоновую очередь (Channels + BackgroundService)
await _backgroundQueue.EnqueueAsync(ct => SendEmailAsync(user.Email, ct));
```

---

### Вопрос 9: Как работает `ConcurrentDictionary` внутри? Почему `GetOrAdd` может вызвать фабрику несколько раз?

**Ответ:**

`ConcurrentDictionary` использует **сегментную блокировку** (lock striping). Внутри — массив «бакетов» (Node[]), разделённых на сегменты, каждый со своим `lock`. Это позволяет нескольким потокам одновременно работать с разными сегментами.

`GetOrAdd(key, factory)` работает в два этапа:
1. Проверяет наличие ключа **без блокировки** (lock-free чтение).
2. Если ключа нет — вызывает фабрику и добавляет результат **с блокировкой сегмента**.

Между шагами 1 и 2 другой поток может добавить значение для того же ключа. Фабрика может быть вызвана несколькими потоками, но в словарь попадёт только одно значение.

```csharp
// Решение: Lazy<T> для гарантии однократного вычисления
var cache = new ConcurrentDictionary<string, Lazy<ExpensiveData>>();

var data = cache.GetOrAdd("key",
    k => new Lazy<ExpensiveData>(() => ComputeExpensiveData(k))
).Value; // Lazy обеспечивает однократное выполнение фабрики
```

---

### Вопрос 10: Что такое `async` state machine и какие накладные расходы она несёт?

**Ответ:**

Компилятор преобразует каждый `async`-метод в структуру (или класс), реализующую `IAsyncStateMachine`. Накладные расходы:

1. **Аллокация:** При первом `await`, который не завершился синхронно, state machine боксится (struct -> heap) и создаётся объект `Task`.
2. **Поля:** Все локальные переменные становятся полями state machine.
3. **Continuation:** Регистрация callback через `SynchronizationContext` или `TaskScheduler`.
4. **ExecutionContext:** Захват и восстановление контекста выполнения.

**Оптимизации CLR:**
- Если все `await` завершаются синхронно — state machine остаётся на стеке, аллокации нет.
- `ValueTask` позволяет избежать аллокации `Task` для синхронного пути.
- В .NET 6+ `async` state machine использует `PoolingAsyncValueTaskMethodBuilder` для переиспользования объектов из пула.

```csharp
// Для критичных к производительности методов в .NET 6+
[AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
public async ValueTask<int> HighFrequencyMethodAsync()
{
    var data = await GetCachedDataAsync();
    return data.Length;
}
```

---

### Вопрос 11: В чём опасность `async void` и когда его можно использовать?

**Ответ:**

`async void` опасен по трём причинам:

1. **Исключения:** Необработанные исключения бросаются напрямую в `SynchronizationContext` (или в `ThreadPool` при его отсутствии), что может привести к аварийному завершению приложения.
2. **Невозможность ожидания:** Вызывающий код не может `await` метод или узнать о его завершении.
3. **Тестирование:** Невозможно написать корректный unit-тест.

**Единственное допустимое использование** — обработчики событий:

```csharp
// Допустимо: обработчик события
private async void Button_Click(object sender, EventArgs e)
{
    try
    {
        await DoWorkAsync();
    }
    catch (Exception ex)
    {
        // Обязательно обрабатывайте исключения!
        MessageBox.Show($"Ошибка: {ex.Message}");
    }
}
```

---

### Вопрос 12: Как корректно остановить длительную фоновую операцию?

**Ответ:**

Через кооперативную отмену с использованием `CancellationToken`:

```csharp
public class DataProcessor : BackgroundService
{
    private readonly ILogger<DataProcessor> _logger;

    public DataProcessor(ILogger<DataProcessor> logger) => _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DataProcessor запущен");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessNextBatchAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Нормальное завершение при остановке хоста
            _logger.LogInformation("DataProcessor остановлен");
        }
    }

    private async Task ProcessNextBatchAsync(CancellationToken ct)
    {
        var items = await _repository.GetPendingItemsAsync(ct);
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            await ProcessItemAsync(item, ct);
        }
    }
}
```

---

### Вопрос 13: Чем `Parallel.ForEachAsync` отличается от `Task.WhenAll` с `Select`?

**Ответ:**

```csharp
// Вариант 1: Task.WhenAll — все задачи запускаются ОДНОВРЕМЕННО
var tasks = urls.Select(url => httpClient.GetStringAsync(url));
await Task.WhenAll(tasks); // 1000 одновременных запросов!

// Вариант 2: SemaphoreSlim — ограниченный параллелизм
var semaphore = new SemaphoreSlim(10);
var tasks = urls.Select(async url =>
{
    await semaphore.WaitAsync();
    try { return await httpClient.GetStringAsync(url); }
    finally { semaphore.Release(); }
});
await Task.WhenAll(tasks);

// Вариант 3: Parallel.ForEachAsync (.NET 6+) — встроенное ограничение
await Parallel.ForEachAsync(urls,
    new ParallelOptions { MaxDegreeOfParallelism = 10 },
    async (url, ct) => await httpClient.GetStringAsync(url, ct));
```

`Parallel.ForEachAsync` предпочтительнее:
- Встроенное ограничение параллелизма.
- Правильная обработка `CancellationToken`.
- Не создаёт все задачи заранее (ленивая итерация).
- Корректно агрегирует исключения.

---

### Вопрос 14: Как избежать захвата контекста в async-лямбдах?

**Ответ:**

```csharp
// ПРОБЛЕМА: лямбда захватывает переменную цикла
var tasks = new List<Task>();
for (int i = 0; i < 10; i++)
{
    tasks.Add(Task.Run(() => Console.WriteLine(i))); // все выведут 10!
}

// РЕШЕНИЕ: локальная копия
for (int i = 0; i < 10; i++)
{
    int local = i; // копия для каждой итерации
    tasks.Add(Task.Run(() => Console.WriteLine(local)));
}

// Или используйте foreach — в C# 5+ переменная не захватывается по ссылке
foreach (var item in items)
{
    tasks.Add(Task.Run(() => Process(item))); // корректно
}
```

---

## Шпаргалка: выбор правильного инструмента

```
Задача                          → Инструмент
────────────────────────────────────────────────────────────
CPU-bound, параллельно          → Parallel.For / PLINQ / Task.Run
I/O-bound, асинхронно           → async/await
Ограничить параллелизм          → SemaphoreSlim / Parallel.ForEachAsync
Producer-Consumer (async)       → Channel<T>
Producer-Consumer (sync)        → BlockingCollection<T>
Потокобезопасный словарь        → ConcurrentDictionary<K,V>
Потокобезопасная очередь        → ConcurrentQueue<T>
Атомарные операции              → Interlocked
Простая взаимная блокировка     → lock
Async-совместимая блокировка    → SemaphoreSlim(1,1)
Много читателей, мало писателей → ReaderWriterLockSlim
Межпроцессная блокировка        → Mutex / Semaphore
Отмена операций                 → CancellationToken
Потоковая async-итерация        → IAsyncEnumerable<T>
Lazy-инициализация              → Lazy<T>
```

---

## Рекомендуемая литература

- **CLR via C#** — Jeffrey Richter (глубокое понимание потоков и TPL).
- **Concurrency in C# Cookbook** — Stephen Cleary (практические рецепты).
- **Stephen Cleary's Blog** — https://blog.stephencleary.com (лучший ресурс по async/await).
- **Threading in C#** — Joseph Albahari — https://www.albahari.com/threading/
- **.NET Documentation** — https://learn.microsoft.com/dotnet/standard/parallel-programming/
