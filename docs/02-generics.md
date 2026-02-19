# Generics в C# -- Полное руководство для Senior .NET собеседования

## Содержание

1. [Зачем нужны Generics. Проблема boxing/unboxing](#1-зачем-нужны-generics-проблема-boxingunboxing)
2. [Generic классы, интерфейсы, методы, делегаты](#2-generic-классы-интерфейсы-методы-делегаты)
3. [Constraints (ограничения типов)](#3-constraints-ограничения-типов)
4. [Ковариантность (out) и контравариантность (in)](#4-ковариантность-out-и-контравариантность-in)
5. [Generic коллекции vs non-generic](#5-generic-коллекции-vs-non-generic)
6. [Как generics работают в CLR (reification vs type erasure)](#6-как-generics-работают-в-clr)
7. [default(T) и default литерал](#7-defaultt-и-default-литерал)
8. [Статические поля в generic типах](#8-статические-поля-в-generic-типах)
9. [Паттерны с Generics](#9-паттерны-с-generics)
10. [Ограничения generics в C#](#10-ограничения-generics-в-c)
11. [Reflection и Generics](#11-reflection-и-generics)
12. [Вопросы на собеседовании с ответами](#12-вопросы-на-собеседовании-с-ответами)

---

## 1. Зачем нужны Generics. Проблема boxing/unboxing

### Проблема до появления Generics

До C# 2.0 (2005) единственным способом создать универсальную коллекцию было использование `System.Collections.ArrayList`, который хранил элементы как `object`. Это порождало две серьёзные проблемы:

1. **Boxing/Unboxing** -- при добавлении value-типов (int, struct и т.д.) в коллекцию типа `object` происходит boxing (упаковка значения в кучу), а при извлечении -- unboxing. Это дорого по производительности.
2. **Отсутствие типобезопасности** -- в `ArrayList` можно положить любой объект, и ошибка типа обнаружится только в runtime.

```csharp
// Проблема 1: Boxing/Unboxing
ArrayList list = new ArrayList();
list.Add(42);        // boxing: int -> object (аллокация в heap)
int value = (int)list[0]; // unboxing: object -> int

// Проблема 2: Нет типобезопасности
list.Add("строка");  // компилятор не ругается!
int wrong = (int)list[1]; // InvalidCastException в runtime
```

### Что такое boxing/unboxing подробно

```csharp
// Boxing -- аллокация объекта в managed heap + копирование значения
int x = 42;
object boxed = x; // IL: box [mscorlib]System.Int32

// Unboxing -- проверка типа + копирование значения обратно в стек
int y = (int)boxed; // IL: unbox.any [mscorlib]System.Int32
```

**Стоимость boxing:**
- Аллокация памяти в managed heap (~24 байт для int: 8 sync block + 8 method table + 4 данные + 4 padding).
- Копирование значения из стека в heap.
- Дополнительная нагрузка на GC (каждый boxed объект -- мусор для сборщика).

### Решение -- Generics

```csharp
// Типобезопасность на этапе компиляции + нет boxing
List<int> list = new List<int>();
list.Add(42);        // НЕТ boxing -- хранится как int
int value = list[0]; // НЕТ unboxing -- возвращается int

// list.Add("строка"); // Ошибка компиляции CS1503!
```

---

## 2. Generic классы, интерфейсы, методы, делегаты

### Generic классы

```csharp
public class Stack<T>
{
    private T[] _items;
    private int _count;

    public Stack(int capacity = 16)
    {
        _items = new T[capacity];
        _count = 0;
    }

    public void Push(T item)
    {
        if (_count == _items.Length)
            Array.Resize(ref _items, _items.Length * 2);
        _items[_count++] = item;
    }

    public T Pop()
    {
        if (_count == 0)
            throw new InvalidOperationException("Стек пуст");
        return _items[--_count];
    }

    public T Peek() => _count > 0
        ? _items[_count - 1]
        : throw new InvalidOperationException("Стек пуст");
}

// Несколько параметров типа
public class Pair<TFirst, TSecond>
{
    public TFirst First { get; }
    public TSecond Second { get; }

    public Pair(TFirst first, TSecond second)
    {
        First = first;
        Second = second;
    }
}

// Использование
var stack = new Stack<int>();
stack.Push(10);

var pair = new Pair<string, DateTime>("создан", DateTime.Now);
```

### Generic интерфейсы

```csharp
public interface IRepository<TEntity> where TEntity : class
{
    TEntity? GetById(int id);
    IEnumerable<TEntity> GetAll();
    void Add(TEntity entity);
    void Update(TEntity entity);
    void Delete(TEntity entity);
}

// Реализация
public class UserRepository : IRepository<User>
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context) => _context = context;

    public User? GetById(int id) => _context.Users.Find(id);
    public IEnumerable<User> GetAll() => _context.Users.ToList();
    public void Add(User entity) => _context.Users.Add(entity);
    public void Update(User entity) => _context.Users.Update(entity);
    public void Delete(User entity) => _context.Users.Remove(entity);
}
```

### Generic методы

```csharp
public static class CollectionExtensions
{
    // Generic метод с выводом типа (type inference)
    public static T? FindFirst<T>(IEnumerable<T> source, Func<T, bool> predicate)
    {
        foreach (var item in source)
        {
            if (predicate(item))
                return item;
        }
        return default;
    }

    // Generic extension method
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source) where T : class
    {
        foreach (var item in source)
        {
            if (item is not null)
                yield return item;
        }
    }
}

// Использование -- тип выводится автоматически (type inference)
var numbers = new List<int> { 1, 2, 3, 4, 5 };
int? first = CollectionExtensions.FindFirst(numbers, n => n > 3); // T = int (выведен)
```

### Generic делегаты

```csharp
// Встроенные generic делегаты
// Action<T>         -- void метод с параметрами
// Func<T, TResult>  -- метод с возвращаемым значением
// Predicate<T>      -- метод, возвращающий bool
// Comparison<T>     -- метод для сравнения двух объектов

// Собственный generic делегат
public delegate TResult Converter<in TInput, out TResult>(TInput input);

// Пример использования
Func<int, int, int> add = (a, b) => a + b;
Predicate<string> isLong = s => s.Length > 10;
Converter<string, int> toInt = s => int.Parse(s);

Console.WriteLine(add(2, 3));        // 5
Console.WriteLine(isLong("hello"));  // False
Console.WriteLine(toInt("42"));      // 42
```

---

## 3. Constraints (ограничения типов)

Constraints позволяют ограничить типы, которые могут быть подставлены в generic параметр. Это даёт компилятору больше информации о T и позволяет вызывать методы, специфичные для ограничения.

### Полный список constraints

| Constraint | Описание |
|---|---|
| `where T : struct` | T -- value type (исключая Nullable) |
| `where T : class` | T -- reference type |
| `where T : class?` | T -- nullable reference type |
| `where T : notnull` | T -- non-nullable (value или reference) |
| `where T : unmanaged` | T -- unmanaged type (int, byte, struct без ссылок) |
| `where T : new()` | T имеет публичный конструктор без параметров |
| `where T : BaseClass` | T наследуется от BaseClass |
| `where T : IInterface` | T реализует IInterface |
| `where T : U` | T наследуется от другого параметра U |
| `where T : Enum` | T -- перечисление (C# 7.3+) |
| `where T : Delegate` | T -- делегат (C# 7.3+) |

### Примеры constraints

```csharp
// struct constraint -- запрещает null, гарантирует value type
public struct Option<T> where T : struct
{
    private readonly T? _value;
    public bool HasValue => _value.HasValue;
    public T Value => _value ?? throw new InvalidOperationException("No value");

    public Option(T value) => _value = value;
}

// class constraint -- гарантирует reference type
public class WeakCache<T> where T : class
{
    private readonly Dictionary<string, WeakReference<T>> _cache = new();

    public void Set(string key, T value) =>
        _cache[key] = new WeakReference<T>(value);

    public T? Get(string key) =>
        _cache.TryGetValue(key, out var weakRef) && weakRef.TryGetTarget(out var target)
            ? target
            : null;
}

// new() constraint -- можно создавать экземпляры через new T()
public class Factory<T> where T : new()
{
    public T Create() => new T();

    public List<T> CreateMany(int count)
    {
        var list = new List<T>(count);
        for (int i = 0; i < count; i++)
            list.Add(new T());
        return list;
    }
}

// unmanaged constraint -- для работы с указателями и Span
public static unsafe void WriteToBuffer<T>(T value, byte* buffer) where T : unmanaged
{
    int size = sizeof(T);
    *(T*)buffer = value;
}

// Комбинирование constraints
public class EntityService<TEntity, TKey>
    where TEntity : class, IEntity<TKey>, new()
    where TKey : struct, IEquatable<TKey>
{
    public TEntity CreateWithId(TKey id)
    {
        var entity = new TEntity();
        entity.Id = id;
        return entity;
    }
}

public interface IEntity<TKey>
{
    TKey Id { get; set; }
}

// Enum constraint (C# 7.3+)
public static TEnum Parse<TEnum>(string value) where TEnum : struct, Enum
{
    return Enum.Parse<TEnum>(value);
}

// Delegate constraint (C# 7.3+)
public static T Combine<T>(T a, T b) where T : Delegate
{
    return (T)Delegate.Combine(a, b);
}
```

### Порядок constraints

Constraints должны идти в определённом порядке:
1. `class`, `struct`, `unmanaged` или `notnull` (первичный constraint)
2. Имя базового класса
3. Имена интерфейсов
4. `new()` (всегда последний)

```csharp
// Корректный порядок
public class Service<T> where T : class, IDisposable, ICloneable, new() { }

// Ошибка компиляции -- new() должен быть последним
// public class Bad<T> where T : new(), class { }
```

---

## 4. Ковариантность (out) и контравариантность (in)

Вариантность -- это свойство generic типов, определяющее, можно ли использовать более производный или менее производный тип вместо указанного. Применяется **только к интерфейсам и делегатам**.

### Ковариантность (`out`)

Ковариантный параметр типа может использоваться **только в выходных (output)** позициях (возвращаемые значения). Позволяет присвоить `IEnumerable<Dog>` переменной типа `IEnumerable<Animal>`.

```csharp
// Определение ковариантного интерфейса
public interface IProducer<out T>
{
    T Produce();
    // void Consume(T item); // Ошибка! T нельзя использовать как input
}

// Стандартный пример: IEnumerable<out T>
public interface IEnumerable<out T> : IEnumerable
{
    IEnumerator<T> GetEnumerator();
}

// Использование ковариантности
class Animal { public string Name { get; set; } = ""; }
class Dog : Animal { public string Breed { get; set; } = ""; }

IEnumerable<Dog> dogs = new List<Dog>
{
    new Dog { Name = "Рекс", Breed = "Овчарка" },
    new Dog { Name = "Бобик", Breed = "Дворняга" }
};

// Ковариантность: IEnumerable<Dog> -> IEnumerable<Animal>
IEnumerable<Animal> animals = dogs; // OK!

foreach (Animal animal in animals)
    Console.WriteLine(animal.Name);
```

### Контравариантность (`in`)

Контравариантный параметр типа может использоваться **только во входных (input)** позициях (параметры методов). Позволяет присвоить `Action<Animal>` переменной типа `Action<Dog>`.

```csharp
// Определение контравариантного интерфейса
public interface IConsumer<in T>
{
    void Consume(T item);
    // T Produce(); // Ошибка! T нельзя использовать как output
}

// Стандартный пример: Action<in T>
public delegate void Action<in T>(T obj);

// Стандартный пример: IComparer<in T>
public interface IComparer<in T>
{
    int Compare(T? x, T? y);
}

// Использование контравариантности
Action<Animal> printAnimal = a => Console.WriteLine($"Животное: {a.Name}");

// Контравариантность: Action<Animal> -> Action<Dog>
Action<Dog> printDog = printAnimal; // OK!
printDog(new Dog { Name = "Рекс", Breed = "Овчарка" });

// IComparer пример
IComparer<Animal> animalComparer = Comparer<Animal>.Create(
    (a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

// Контравариантность: IComparer<Animal> -> IComparer<Dog>
IComparer<Dog> dogComparer = animalComparer;
```

### Ковариантность и контравариантность вместе

```csharp
// Func<in T, out TResult> -- контравариантен по входу, ковариантен по выходу
Func<Dog, Animal> func1 = dog => dog;

// Можно расширить вход (Animal вместо Dog) и сузить выход (Dog вместо Animal)
Func<Animal, Dog> func2 = animal => new Dog { Name = animal.Name };

// Пользовательский интерфейс с обоими видами вариантности
public interface IConverter<in TInput, out TOutput>
{
    TOutput Convert(TInput input);
}

public class DogToAnimalConverter : IConverter<Dog, Animal>
{
    public Animal Convert(Dog input) => input; // Dog уже Animal
}
```

### Ограничения вариантности

```csharp
// Вариантность работает ТОЛЬКО для интерфейсов и делегатов
// Классы НЕ поддерживают вариантность:
// public class MyList<out T> { } // Ошибка компиляции!

// Вариантность работает ТОЛЬКО для reference types:
// IEnumerable<int> -> IEnumerable<object> -- НЕВОЗМОЖНО (int -- value type)
```

---

## 5. Generic коллекции vs non-generic

### Сравнительная таблица

| Non-Generic (System.Collections) | Generic (System.Collections.Generic) | Описание |
|---|---|---|
| `ArrayList` | `List<T>` | Динамический массив |
| `Hashtable` | `Dictionary<TKey, TValue>` | Хэш-таблица |
| `Queue` | `Queue<T>` | Очередь FIFO |
| `Stack` | `Stack<T>` | Стек LIFO |
| `SortedList` | `SortedList<TKey, TValue>` | Сортированный список |
| `BitArray` | нет прямого аналога | Массив битов |
| -- | `HashSet<T>` | Множество (нет non-generic аналога) |
| -- | `LinkedList<T>` | Двусвязный список |
| -- | `SortedSet<T>` | Сортированное множество |

### Бенчмарк: Generic vs Non-Generic

```csharp
using System.Collections;
using System.Diagnostics;

const int iterations = 10_000_000;

// Non-generic ArrayList -- с boxing
var stopwatch = Stopwatch.StartNew();
var arrayList = new ArrayList();
for (int i = 0; i < iterations; i++)
    arrayList.Add(i); // boxing каждый раз
long sum1 = 0;
for (int i = 0; i < iterations; i++)
    sum1 += (int)arrayList[i]; // unboxing каждый раз
stopwatch.Stop();
Console.WriteLine($"ArrayList: {stopwatch.ElapsedMilliseconds} ms");

// Generic List<int> -- без boxing
stopwatch.Restart();
var list = new List<int>();
for (int i = 0; i < iterations; i++)
    list.Add(i); // нет boxing
long sum2 = 0;
for (int i = 0; i < iterations; i++)
    sum2 += list[i]; // нет unboxing
stopwatch.Stop();
Console.WriteLine($"List<int>: {stopwatch.ElapsedMilliseconds} ms");

// Типичные результаты:
// ArrayList: ~1200 ms (+ ~300 MB аллокаций для GC)
// List<int>: ~250 ms  (минимальные аллокации)
```

### Concurrent коллекции

```csharp
using System.Collections.Concurrent;

// Потокобезопасные generic коллекции
ConcurrentDictionary<string, int> concurrentDict = new();
ConcurrentQueue<string> concurrentQueue = new();
ConcurrentStack<int> concurrentStack = new();
ConcurrentBag<double> concurrentBag = new();
BlockingCollection<string> blockingCollection = new();
```

---

## 6. Как generics работают в CLR

### Reification в .NET vs Type Erasure в Java

Это один из самых популярных вопросов на Senior-уровне.

**C# / .NET -- Reification (овеществление):**
- Информация о generic типах **сохраняется в runtime**.
- CLR генерирует **специализированный код** для каждого value type (`List<int>`, `List<double>` -- разный машинный код).
- Для reference types CLR **разделяет один JIT-скомпилированный код** (`List<string>`, `List<object>` используют один машинный код, так как все ссылки одного размера).
- Можно делать `typeof(T)`, `typeof(List<>)`, `typeof(List<int>)` в runtime.

**Java -- Type Erasure (стирание типов):**
- Информация о generic типах **удаляется при компиляции**.
- `List<Integer>` и `List<String>` -- один и тот же класс `List` в runtime.
- Нельзя написать `new T()`, `instanceof T`, `T.class` в runtime.

```csharp
// В C# -- полная информация о типах в runtime
public static void PrintTypeInfo<T>()
{
    Type type = typeof(T);
    Console.WriteLine($"Тип: {type.FullName}");
    Console.WriteLine($"IsValueType: {type.IsValueType}");
    Console.WriteLine($"IsClass: {type.IsClass}");
}

PrintTypeInfo<int>();
// Тип: System.Int32
// IsValueType: True
// IsClass: False

PrintTypeInfo<string>();
// Тип: System.String
// IsValueType: False
// IsClass: True

// Сравнение типов
Console.WriteLine(typeof(List<int>) == typeof(List<string>)); // False!
Console.WriteLine(typeof(List<int>) == typeof(List<int>));    // True
```

### Как CLR компилирует Generics

```
Исходный код: List<T>
        |
        v
Компилятор C# -> IL-код с метаданными generic типов
        |
        v
JIT-компиляция (в runtime):
  - List<int>    -> отдельный нативный код (специализация для int)
  - List<double> -> отдельный нативный код (специализация для double)
  - List<string> -> общий нативный код  ─┐ (все reference types
  - List<object> -> общий нативный код  ─┘  разделяют один код)
```

```csharp
// Доказательство: разные generic типы -- разные Type
var listInt = new List<int>();
var listStr = new List<string>();

Console.WriteLine(listInt.GetType() == listStr.GetType()); // False
Console.WriteLine(listInt.GetType().GetGenericTypeDefinition()
    == listStr.GetType().GetGenericTypeDefinition()); // True -- оба List<>
```

---

## 7. default(T) и default литерал

### default(T)

Возвращает значение по умолчанию для типа T:
- **Reference types** -> `null`
- **Value types** -> нулевое значение (`0`, `false`, `\0`, etc.)
- **Nullable value types** -> `null`

```csharp
public class DefaultDemo
{
    public static T? GetDefaultOrValue<T>(Dictionary<string, T> dict, string key)
    {
        if (dict.TryGetValue(key, out T? value))
            return value;
        return default(T); // безопасное значение по умолчанию
    }

    public static void ShowDefaults()
    {
        Console.WriteLine(default(int));         // 0
        Console.WriteLine(default(bool));        // False
        Console.WriteLine(default(string));      // (null -- пустая строка)
        Console.WriteLine(default(DateTime));    // 01/01/0001 00:00:00
        Console.WriteLine(default(int?));        // (null)
        Console.WriteLine(default(Guid));        // 00000000-0000-0000-0000-000000000000

        // Для struct -- все поля нулевые
        Console.WriteLine(default(Point)); // Point { X = 0, Y = 0 }
    }
}

public struct Point
{
    public int X;
    public int Y;
    public override string ToString() => $"Point {{ X = {X}, Y = {Y} }}";
}
```

### default литерал (C# 7.1+)

Начиная с C# 7.1 можно использовать `default` без указания типа, если тип выводится из контекста.

```csharp
// До C# 7.1
int x = default(int);
string s = default(string);
Func<int, bool> predicate = default(Func<int, bool>);

// С C# 7.1 -- default литерал
int x = default;
string s = default;
Func<int, bool> predicate = default;

// В параметрах методов
public void Process(CancellationToken token = default) { }

// В switch выражениях
public string Describe<T>(T value) => value switch
{
    null => "null",
    0 => "ноль",
    "" => "пустая строка",
    _ => value.ToString() ?? "unknown"
};

// В тернарном операторе
public T? Find<T>(bool condition) where T : class
{
    return condition ? GetSomething<T>() : default;
}
```

---

## 8. Статические поля в generic типах

Это **классическая ловушка** на собеседованиях. Каждый закрытый (constructed) generic тип имеет **свой собственный набор статических полей**.

```csharp
public class Counter<T>
{
    // Каждый Counter<T> для разного T имеет СВОЙ Count
    public static int Count;

    public Counter()
    {
        Count++;
    }
}

// Демонстрация
var a = new Counter<int>();
var b = new Counter<int>();
var c = new Counter<string>();

Console.WriteLine(Counter<int>.Count);    // 2 (два экземпляра Counter<int>)
Console.WriteLine(Counter<string>.Count); // 1 (один экземпляр Counter<string>)
Console.WriteLine(Counter<double>.Count); // 0 (не создавали Counter<double>)
```

### Практическое применение: Generic кэш типов

```csharp
// Паттерн: статический generic кэш -- быстрее Dictionary<Type, ...>
public static class TypeCache<T>
{
    // Вычисляется один раз для каждого T при первом обращении
    public static readonly string TypeName = typeof(T).FullName ?? typeof(T).Name;
    public static readonly bool IsValueType = typeof(T).IsValueType;
    public static readonly int Size = System.Runtime.InteropServices.Marshal.SizeOf<T>();
}

// Использование -- моментальный доступ, без словаря
Console.WriteLine(TypeCache<int>.TypeName);     // System.Int32
Console.WriteLine(TypeCache<int>.IsValueType);  // True
Console.WriteLine(TypeCache<int>.Size);         // 4
```

### Статические конструкторы

```csharp
public class Singleton<T> where T : class, new()
{
    // Статический конструктор вызывается отдельно для каждого T
    private static readonly Lazy<T> _instance = new(() => new T());

    public static T Instance => _instance.Value;
}

// Singleton<ServiceA>.Instance -- один экземпляр ServiceA
// Singleton<ServiceB>.Instance -- один экземпляр ServiceB (другой!)
```

---

## 9. Паттерны с Generics

### Generic Repository

```csharp
// Базовый интерфейс
public interface IRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
    where TKey : IEquatable<TKey>
{
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default);
    Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default);
    Task UpdateAsync(TEntity entity, CancellationToken ct = default);
    Task DeleteAsync(TKey id, CancellationToken ct = default);
}

// Базовая реализация с Entity Framework Core
public class EfRepository<TEntity, TKey> : IRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
    where TKey : IEquatable<TKey>
{
    protected readonly DbContext _context;
    protected readonly DbSet<TEntity> _dbSet;

    public EfRepository(DbContext context)
    {
        _context = context;
        _dbSet = context.Set<TEntity>();
    }

    public virtual async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken ct = default)
        => await _dbSet.FindAsync(new object[] { id }, ct);

    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default)
        => await _dbSet.ToListAsync(ct);

    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default)
    {
        await _dbSet.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public virtual async Task UpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync(ct);
    }

    public virtual async Task DeleteAsync(TKey id, CancellationToken ct = default)
    {
        var entity = await GetByIdAsync(id, ct);
        if (entity is not null)
        {
            _dbSet.Remove(entity);
            await _context.SaveChangesAsync(ct);
        }
    }
}

// Специализированный репозиторий наследует базовый
public class UserRepository : EfRepository<User, Guid>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(u => u.Email == email, ct);
}
```

### Generic Factory

```csharp
// Абстрактная generic фабрика
public interface IFactory<out TProduct>
{
    TProduct Create();
}

public interface IFactory<in TParam, out TProduct>
{
    TProduct Create(TParam parameter);
}

// Реализация с регистрацией
public class GenericFactory<TKey, TProduct> where TKey : notnull
{
    private readonly Dictionary<TKey, Func<TProduct>> _creators = new();

    public void Register(TKey key, Func<TProduct> creator)
        => _creators[key] = creator;

    public TProduct Create(TKey key)
    {
        if (!_creators.TryGetValue(key, out var creator))
            throw new KeyNotFoundException($"Нет фабрики для ключа '{key}'");
        return creator();
    }
}

// Использование
var factory = new GenericFactory<string, INotificationSender>();
factory.Register("email", () => new EmailSender());
factory.Register("sms", () => new SmsSender());
factory.Register("push", () => new PushSender());

INotificationSender sender = factory.Create("email");
```

### Generic Builder

```csharp
// Fluent Generic Builder с сохранением типа
public class Builder<T> where T : class, new()
{
    private readonly T _instance = new();
    private readonly List<Action<T>> _modifications = new();

    public Builder<T> With(Action<T> modifier)
    {
        _modifications.Add(modifier);
        return this;
    }

    public Builder<T> WithProperty<TProp>(
        Expression<Func<T, TProp>> propertyExpr,
        TProp value)
    {
        if (propertyExpr.Body is MemberExpression memberExpr
            && memberExpr.Member is PropertyInfo property)
        {
            _modifications.Add(obj => property.SetValue(obj, value));
        }
        return this;
    }

    public T Build()
    {
        foreach (var mod in _modifications)
            mod(_instance);
        return _instance;
    }
}

// Использование
var user = new Builder<User>()
    .With(u => u.Name = "Иван")
    .With(u => u.Email = "ivan@example.com")
    .WithProperty(u => u.Age, 30)
    .Build();
```

### Паттерн: Generic Specification

```csharp
public interface ISpecification<T>
{
    bool IsSatisfiedBy(T entity);
    Expression<Func<T, bool>> ToExpression();
}

public abstract class Specification<T> : ISpecification<T>
{
    public bool IsSatisfiedBy(T entity) => ToExpression().Compile()(entity);
    public abstract Expression<Func<T, bool>> ToExpression();

    // Комбинирование спецификаций
    public Specification<T> And(Specification<T> other) => new AndSpecification<T>(this, other);
    public Specification<T> Or(Specification<T> other) => new OrSpecification<T>(this, other);
    public Specification<T> Not() => new NotSpecification<T>(this);
}

// Пример спецификации
public class ActiveUserSpec : Specification<User>
{
    public override Expression<Func<User, bool>> ToExpression()
        => user => user.IsActive;
}

public class AdultUserSpec : Specification<User>
{
    public override Expression<Func<User, bool>> ToExpression()
        => user => user.Age >= 18;
}

// Использование
var spec = new ActiveUserSpec().And(new AdultUserSpec());
var activeAdults = users.Where(spec.IsSatisfiedBy);
```

---

## 10. Ограничения generics в C#

### Нет арифметических constraints (до .NET 7)

```csharp
// ДО .NET 7 -- это НЕ скомпилируется:
// public static T Sum<T>(T a, T b) => a + b; // Ошибка: оператор '+' не определён для T

// Обходные пути до .NET 7:

// 1. Через dynamic (медленно, нет проверки компилятора)
public static T SumDynamic<T>(T a, T b) => (dynamic)a + (dynamic)b;

// 2. Через делегат
public static T Sum<T>(T a, T b, Func<T, T, T> adder) => adder(a, b);
int result = Sum(3, 5, (a, b) => a + b);

// 3. Через интерфейс
public interface IAddable<T>
{
    T Add(T other);
}
```

### INumber<T> в .NET 7+ (Generic Math)

```csharp
// .NET 7+ -- полноценная поддержка через static abstract members в интерфейсах
using System.Numerics;

public static T Sum<T>(IEnumerable<T> values) where T : INumber<T>
{
    T result = T.Zero;
    foreach (var value in values)
        result += value;
    return result;
}

public static T Average<T>(IEnumerable<T> values)
    where T : INumber<T>
{
    T sum = T.Zero;
    T count = T.Zero;
    foreach (var value in values)
    {
        sum += value;
        count++;
    }
    return sum / count;
}

// Использование -- работает с ЛЮБЫМ числовым типом
var intSum = Sum(new[] { 1, 2, 3, 4, 5 });           // 15
var doubleSum = Sum(new[] { 1.1, 2.2, 3.3 });         // 6.6
var decimalAvg = Average(new[] { 10m, 20m, 30m });     // 20

// Иерархия интерфейсов Generic Math
// INumber<T>
//   IAdditionOperators<T, T, T>
//   ISubtractionOperators<T, T, T>
//   IMultiplyOperators<T, T, T>
//   IDivisionOperators<T, T, T>
//   IComparisonOperators<T, T, bool>
//   IMinMaxValue<T>
//   и др.
```

### Другие ограничения generics

```csharp
// 1. Нельзя использовать операторы == и != для T без constraint
public static bool AreEqual<T>(T a, T b)
{
    // return a == b; // Ошибка!
    return EqualityComparer<T>.Default.Equals(a, b); // Правильный способ
}

// 2. Нельзя наследоваться от T
// public class MyClass<T> : T { } // Ошибка!

// 3. Нельзя создать массив generic типа напрямую в некоторых контекстах
// (хотя new T[10] работает, new T() требует new() constraint)

// 4. Нельзя указать конструктор с параметрами в constraint
// where T : new(string, int) -- НЕ поддерживается
// Обходной путь -- через фабричный делегат:
public class Builder<T>
{
    private readonly Func<string, T> _factory;

    public Builder(Func<string, T> factory) => _factory = factory;
    public T Create(string name) => _factory(name);
}

// 5. Нет constraint "T является числом" до .NET 7

// 6. Нет поддержки generic атрибутов до C# 11
// C# 11+:
// public class ValidateAttribute<T> : Attribute where T : IValidator { }
```

---

## 11. Reflection и Generics

### Работа с открытыми и закрытыми generic типами

```csharp
// Открытый generic тип (open generic type)
Type openType = typeof(List<>);
Console.WriteLine(openType.IsGenericTypeDefinition); // True
Console.WriteLine(openType.Name);                     // List`1

// Закрытый generic тип (closed generic type)
Type closedType = typeof(List<int>);
Console.WriteLine(closedType.IsGenericType);          // True
Console.WriteLine(closedType.IsGenericTypeDefinition); // False

// Получение определения из закрытого типа
Type definition = closedType.GetGenericTypeDefinition();
Console.WriteLine(definition == openType); // True

// Получение аргументов типа
Type[] args = closedType.GetGenericArguments();
Console.WriteLine(args[0].Name); // Int32
```

### MakeGenericType -- создание типов в runtime

```csharp
// Динамическое создание generic типа
Type openDict = typeof(Dictionary<,>);
Type closedDict = openDict.MakeGenericType(typeof(string), typeof(int));

// Создание экземпляра
object instance = Activator.CreateInstance(closedDict)!;
Console.WriteLine(instance.GetType().Name); // Dictionary`2

// Вызов метода
var addMethod = closedDict.GetMethod("Add")!;
addMethod.Invoke(instance, new object[] { "ключ", 42 });

var getItem = closedDict.GetProperty("Item")!;
int value = (int)getItem.GetValue(instance, new object[] { "ключ" })!;
Console.WriteLine(value); // 42
```

### Вызов generic методов через Reflection

```csharp
public class Serializer
{
    public T Deserialize<T>(string json)
    {
        return System.Text.Json.JsonSerializer.Deserialize<T>(json)!;
    }
}

// Вызов generic метода через Reflection
var serializer = new Serializer();
var method = typeof(Serializer).GetMethod("Deserialize")!;
var genericMethod = method.MakeGenericMethod(typeof(User));

string json = """{"Name":"Иван","Age":30}""";
var user = (User)genericMethod.Invoke(serializer, new object[] { json })!;
Console.WriteLine(user.Name); // Иван
```

### Проверка generic constraints в runtime

```csharp
public static void InspectGenericConstraints(Type type)
{
    if (!type.IsGenericTypeDefinition) return;

    foreach (var param in type.GetGenericArguments())
    {
        Console.WriteLine($"Параметр: {param.Name}");

        var constraints = param.GetGenericParameterConstraints();
        var attrs = param.GenericParameterAttributes;

        if (attrs.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint))
            Console.WriteLine("  - class constraint");
        if (attrs.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
            Console.WriteLine("  - struct constraint");
        if (attrs.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint))
            Console.WriteLine("  - new() constraint");

        foreach (var constraint in constraints)
            Console.WriteLine($"  - наследует: {constraint.Name}");
    }
}

// Пример
InspectGenericConstraints(typeof(Dictionary<,>));
// Параметр: TKey
// Параметр: TValue
```

### Generic и DI контейнер

```csharp
// Регистрация открытых generic типов в Microsoft DI
services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));

// При запросе IRepository<User> контейнер автоматически создаёт EfRepository<User>
// При запросе IRepository<Order> -- EfRepository<Order>

// Собственная реализация open generic resolution
public class SimpleContainer
{
    private readonly Dictionary<Type, Type> _registrations = new();

    public void Register(Type serviceType, Type implementationType)
        => _registrations[serviceType] = implementationType;

    public object Resolve(Type serviceType)
    {
        if (_registrations.TryGetValue(serviceType, out var implType))
            return Activator.CreateInstance(implType)!;

        // Open generic resolution
        if (serviceType.IsGenericType)
        {
            var openServiceType = serviceType.GetGenericTypeDefinition();
            if (_registrations.TryGetValue(openServiceType, out var openImplType))
            {
                var closedImplType = openImplType.MakeGenericType(
                    serviceType.GetGenericArguments());
                return Activator.CreateInstance(closedImplType)!;
            }
        }

        throw new InvalidOperationException($"Тип {serviceType} не зарегистрирован");
    }
}
```

---

## 12. Вопросы на собеседовании с ответами

### Вопрос 1: В чём разница между generics в C# и generics в Java?

**Ответ:** Ключевое различие -- **reification vs type erasure**. В C# информация о generic типах сохраняется в runtime (reification): CLR знает разницу между `List<int>` и `List<string>`, генерирует специализированный машинный код для value types и позволяет использовать `typeof(T)` и reflection. В Java generic типы стираются при компиляции (type erasure): `List<Integer>` и `List<String>` становятся одним `List` в bytecode, нельзя написать `new T()` или `T.class`.

---

### Вопрос 2: Сколько статических полей будет создано?

```csharp
public class Cache<T>
{
    public static int Count = 0;
}

Cache<int>.Count = 1;
Cache<string>.Count = 2;
Cache<double>.Count = 3;
```

**Ответ:** **Три отдельных** статических поля. Каждый закрытый generic тип (`Cache<int>`, `Cache<string>`, `Cache<double>`) -- это отдельный тип с собственным набором статических членов. `Cache<int>.Count == 1`, `Cache<string>.Count == 2`, `Cache<double>.Count == 3`.

---

### Вопрос 3: Почему следующий код не компилируется?

```csharp
public static T Add<T>(T a, T b) => a + b;
```

**Ответ:** Компилятор не знает, что `T` поддерживает оператор `+`. В C# нет constraint для арифметических операторов (до .NET 7). Решения: (1) использовать `dynamic`, (2) передавать делегат `Func<T, T, T>`, (3) в .NET 7+ использовать `where T : INumber<T>` или `where T : IAdditionOperators<T, T, T>`.

---

### Вопрос 4: Что такое ковариантность и контравариантность? Приведите примеры из BCL.

**Ответ:** **Ковариантность** (`out T`) позволяет использовать более производный тип: `IEnumerable<Dog>` можно присвоить `IEnumerable<Animal>`. Примеры из BCL: `IEnumerable<out T>`, `IReadOnlyList<out T>`, `Func<out TResult>`.

**Контравариантность** (`in T`) позволяет использовать менее производный тип: `Action<Animal>` можно присвоить `Action<Dog>`. Примеры из BCL: `Action<in T>`, `IComparer<in T>`, `IEqualityComparer<in T>`.

Вариантность работает только для интерфейсов и делегатов, и только для reference types.

---

### Вопрос 5: Что вернёт default(T) для reference type и value type?

**Ответ:** Для reference types -- `null`. Для value types -- нулевое значение (0 для числовых, false для bool, DateTime.MinValue для DateTime, все поля в нуль для struct). Для `Nullable<T>` -- `null` (HasValue = false). Начиная с C# 7.1 можно писать просто `default` без указания типа.

---

### Вопрос 6: Можно ли создать generic атрибут? Если да, то с какой версии?

**Ответ:** До C# 11 generic атрибуты были запрещены -- попытка объявить `class MyAttribute<T> : Attribute` вызывала ошибку компиляции. Начиная с **C# 11 (.NET 7)** generic атрибуты полностью поддерживаются:

```csharp
// C# 11+
public class ValidatorAttribute<T> : Attribute where T : IValidator, new() { }

[Validator<EmailValidator>]
public string Email { get; set; }
```

---

### Вопрос 7: Как зарегистрировать открытый generic тип в DI-контейнере?

**Ответ:** В Microsoft.Extensions.DependencyInjection используется `typeof()` с открытым generic типом:

```csharp
services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
```

При запросе `IRepository<User>` контейнер автоматически подставит `EfRepository<User>`. Это работает благодаря reification -- CLR может создать закрытый тип в runtime через `MakeGenericType`.

---

### Вопрос 8: Чем отличается `where T : struct` от `where T : unmanaged`?

**Ответ:** `struct` допускает любой value type, включая struct с managed-полями (string, массивы, reference-типы). `unmanaged` -- более строгий: допускает только value types, которые не содержат ссылочных полей на любом уровне вложенности. К unmanaged относятся: примитивные типы (`int`, `double`), `enum`, `pointer types`, а также struct, все поля которого тоже unmanaged. Constraint `unmanaged` нужен для работы с `Span<T>`, указателями и interop.

```csharp
struct Managed { public string Name; }   // НЕ unmanaged (string -- ссылка)
struct Unmanaged { public int X, Y; }     // unmanaged

// void Test<T>(T val) where T : unmanaged { }
// Test(new Managed());   // Ошибка компиляции
// Test(new Unmanaged()); // OK
```

---

### Вопрос 9: Как работает JIT-компиляция для generics с value и reference типами?

**Ответ:** CLR использует **code sharing** для reference types и **specialization** для value types.

- `List<string>`, `List<object>`, `List<User>` -- все используют **один JIT-скомпилированный машинный код**, потому что все ссылки одного размера (IntPtr).
- `List<int>`, `List<double>`, `List<Guid>` -- каждый получает **свой отдельный машинный код**, потому что value types имеют разный размер и layout.

Это оптимальный компромисс: уникальный код для value types исключает boxing, а общий код для reference types экономит память.

---

### Вопрос 10: Как через Reflection вызвать generic метод?

**Ответ:**

```csharp
// 1. Получить MethodInfo открытого generic метода
MethodInfo openMethod = typeof(MyClass).GetMethod("MyMethod")!;

// 2. Создать закрытый generic метод с конкретным типом
MethodInfo closedMethod = openMethod.MakeGenericMethod(typeof(int));

// 3. Вызвать
object? result = closedMethod.Invoke(instance, new object[] { arg1, arg2 });
```

Для generic типов аналогично: `typeof(MyClass<>).MakeGenericType(typeof(int))` и затем `Activator.CreateInstance(closedType)`.

---

### Вопрос 11: Можно ли создать экземпляр T внутри generic метода?

**Ответ:** Только если указан constraint `where T : new()`. Без этого constraint компилятор не гарантирует, что у T есть публичный конструктор без параметров. Для конструкторов с параметрами constraint не существует -- используйте фабричный делегат или `Activator.CreateInstance`.

```csharp
// С constraint
public T Create<T>() where T : new() => new T();

// Без constraint -- через Activator (без проверки компилятора)
public T CreateUnsafe<T>() => (T)Activator.CreateInstance(typeof(T))!;

// С параметрами -- через фабрику
public T Create<T>(Func<string, T> factory, string name) => factory(name);
```

---

### Вопрос 12: Почему `List<T>` не ковариантен?

**Ответ:** Потому что `List<T>` -- это **класс**, а не интерфейс. Вариантность поддерживается только для интерфейсов и делегатов. Кроме того, `List<T>` использует T как в output-позициях (индексатор get, методы Find, First), так и в input-позициях (Add, Insert). Ковариантность требует, чтобы T использовался **только** в output-позициях. Именно поэтому `IReadOnlyList<out T>` ковариантен (нет методов записи), а `IList<T>` -- нет.

```csharp
// Это работает -- IReadOnlyList<out T> ковариантен
IReadOnlyList<Dog> dogs = new List<Dog>();
IReadOnlyList<Animal> animals = dogs; // OK!

// Это НЕ работает -- List<T> не ковариантен
// List<Animal> animals2 = dogs; // Ошибка компиляции
```

---

### Вопрос 13 (бонус): Что такое Curiously Recurring Template Pattern (CRTP) в C#?

**Ответ:** Паттерн, где класс наследуется от generic базового класса, передавая **сам себя** как параметр типа. Используется для реализации строго типизированных методов в базовом классе.

```csharp
public abstract class Entity<TSelf> where TSelf : Entity<TSelf>
{
    public int Id { get; set; }

    public bool Equals(TSelf? other) => other is not null && Id == other.Id;

    // Возвращаем TSelf, а не Entity -- fluent API сохраняет конкретный тип
    public TSelf WithId(int id)
    {
        Id = id;
        return (TSelf)this;
    }
}

public class User : Entity<User>
{
    public string Name { get; set; } = "";
}

// Возвращается User, а не Entity
User user = new User().WithId(1); // тип -- User, не Entity<User>
```

---

## Краткая шпаргалка

| Тема | Ключевые моменты |
|---|---|
| Boxing | Value type -> heap, ~24 байт для int, нагрузка на GC |
| Reification | .NET сохраняет типы в runtime, Java -- нет |
| Ковариантность | `out T` -- только output, `IEnumerable<out T>` |
| Контравариантность | `in T` -- только input, `Action<in T>` |
| Static поля | Каждый `T` -- свой набор static полей |
| default(T) | null для ссылок, нуль для значений |
| new() constraint | Позволяет `new T()`, всегда последний |
| unmanaged | struct без ссылочных полей, для указателей |
| INumber<T> | .NET 7+, Generic Math |
| MakeGenericType | Создание закрытых типов через Reflection |
