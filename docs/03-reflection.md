# Reflection в C# — Подготовка к Senior .NET собеседованию

## Содержание

1. [Что такое Reflection и System.Reflection](#1-что-такое-reflection-и-systemreflection)
2. [Основные классы: Type, MethodInfo, PropertyInfo, FieldInfo, ConstructorInfo](#2-основные-классы-type-methodinfo-propertyinfo-fieldinfo-constructorinfo)
3. [Получение типов: typeof(), GetType(), Type.GetType()](#3-получение-типов-typeof-gettype-typegettype)
4. [Создание экземпляров: Activator.CreateInstance](#4-создание-экземпляров-activatorcreateinstance)
5. [Вызов методов и свойств через Reflection](#5-вызов-методов-и-свойств-через-reflection)
6. [Атрибуты: создание и получение через Reflection](#6-атрибуты-создание-и-получение-через-reflection)
7. [Assembly Loading](#7-assembly-loading)
8. [Dynamic Method Invocation](#8-dynamic-method-invocation)
9. [Expression Trees vs Reflection](#9-expression-trees-vs-reflection)
10. [Source Generators (.NET 5+) как альтернатива Reflection](#10-source-generators-net-5-как-альтернатива-reflection)
11. [System.Text.Json Source Generation](#11-systemtextjson-source-generation)
12. [Performance: Reflection vs скомпилированные делегаты vs Source Generators](#12-performance-reflection-vs-скомпилированные-делегаты-vs-source-generators)
13. [Кеширование Reflection](#13-кеширование-reflection)
14. [Практические сценарии](#14-практические-сценарии)
15. [Вопросы на собеседовании с ответами](#15-вопросы-на-собеседовании-с-ответами)

---

## 1. Что такое Reflection и System.Reflection

**Reflection** (рефлексия) — это механизм в .NET, позволяющий программе исследовать и манипулировать собственной структурой во время выполнения. С помощью Reflection можно:

- Получать информацию о типах, методах, свойствах и полях во время выполнения
- Создавать экземпляры типов динамически
- Вызывать методы и обращаться к свойствам/полям, не зная их на этапе компиляции
- Читать и анализировать атрибуты
- Загружать сборки динамически

Пространство имён `System.Reflection` содержит все необходимые классы для работы с рефлексией.

```csharp
using System.Reflection;

// Получаем информацию о текущей сборке
Assembly currentAssembly = Assembly.GetExecutingAssembly();
Console.WriteLine($"Сборка: {currentAssembly.FullName}");

// Получаем все типы из сборки
Type[] types = currentAssembly.GetTypes();
foreach (Type type in types)
{
    Console.WriteLine($"Тип: {type.FullName}");
    Console.WriteLine($"  Является классом: {type.IsClass}");
    Console.WriteLine($"  Является интерфейсом: {type.IsInterface}");
    Console.WriteLine($"  Является абстрактным: {type.IsAbstract}");
}
```

### Ключевые классы пространства имён System.Reflection

| Класс | Описание |
|-------|----------|
| `Assembly` | Представляет сборку (.dll / .exe) |
| `Module` | Представляет модуль внутри сборки |
| `Type` | Центральный класс — представляет тип |
| `MethodInfo` | Информация о методе |
| `PropertyInfo` | Информация о свойстве |
| `FieldInfo` | Информация о поле |
| `ConstructorInfo` | Информация о конструкторе |
| `EventInfo` | Информация о событии |
| `ParameterInfo` | Информация о параметре метода |
| `MemberInfo` | Базовый класс для всех Info-классов |

---

## 2. Основные классы: Type, MethodInfo, PropertyInfo, FieldInfo, ConstructorInfo

### Type — центральный класс Reflection

`Type` — это точка входа в мир рефлексии. Все остальные Info-объекты получаются через `Type`.

```csharp
public class Employee
{
    private int _id;
    public string Name { get; set; }
    public decimal Salary { get; private set; }

    public Employee() { }
    public Employee(string name, decimal salary)
    {
        Name = name;
        Salary = salary;
    }

    public void Promote(decimal raise) => Salary += raise;
    private void LogAction(string action) => Console.WriteLine(action);
}

Type employeeType = typeof(Employee);

// Основные свойства Type
Console.WriteLine($"Полное имя: {employeeType.FullName}");
Console.WriteLine($"Namespace: {employeeType.Namespace}");
Console.WriteLine($"Базовый тип: {employeeType.BaseType}");
Console.WriteLine($"Является sealed: {employeeType.IsSealed}");
Console.WriteLine($"Является generic: {employeeType.IsGenericType}");
```

### MethodInfo

```csharp
Type type = typeof(Employee);

// Получить все публичные методы
MethodInfo[] publicMethods = type.GetMethods();

// Получить конкретный метод
MethodInfo promoteMethod = type.GetMethod("Promote");
Console.WriteLine($"Метод: {promoteMethod.Name}");
Console.WriteLine($"Возвращаемый тип: {promoteMethod.ReturnType}");
Console.WriteLine($"Является статическим: {promoteMethod.IsStatic}");

// Параметры метода
ParameterInfo[] parameters = promoteMethod.GetParameters();
foreach (var param in parameters)
{
    Console.WriteLine($"  Параметр: {param.Name}, Тип: {param.ParameterType}");
}

// Получить приватный метод
MethodInfo privateMethod = type.GetMethod("LogAction",
    BindingFlags.NonPublic | BindingFlags.Instance);
Console.WriteLine($"Приватный метод: {privateMethod?.Name}");
```

### PropertyInfo

```csharp
Type type = typeof(Employee);

PropertyInfo[] properties = type.GetProperties();
foreach (PropertyInfo prop in properties)
{
    Console.WriteLine($"Свойство: {prop.Name}");
    Console.WriteLine($"  Тип: {prop.PropertyType}");
    Console.WriteLine($"  Можно читать: {prop.CanRead}");
    Console.WriteLine($"  Можно писать: {prop.CanWrite}");

    // Проверяем доступность getter/setter
    MethodInfo getter = prop.GetGetMethod(nonPublic: true);
    MethodInfo setter = prop.GetSetMethod(nonPublic: true);
    Console.WriteLine($"  Getter публичный: {getter?.IsPublic}");
    Console.WriteLine($"  Setter публичный: {setter?.IsPublic}");
}
```

### FieldInfo

```csharp
Type type = typeof(Employee);

// Получить приватные поля
FieldInfo[] fields = type.GetFields(
    BindingFlags.NonPublic | BindingFlags.Instance);

foreach (FieldInfo field in fields)
{
    Console.WriteLine($"Поле: {field.Name}");
    Console.WriteLine($"  Тип: {field.FieldType}");
    Console.WriteLine($"  Приватное: {field.IsPrivate}");
}

// Чтение и установка приватного поля
var employee = new Employee("Иван", 100000m);
FieldInfo idField = type.GetField("_id",
    BindingFlags.NonPublic | BindingFlags.Instance);
idField.SetValue(employee, 42);
int id = (int)idField.GetValue(employee);
Console.WriteLine($"ID (через reflection): {id}"); // 42
```

### ConstructorInfo

```csharp
Type type = typeof(Employee);

ConstructorInfo[] constructors = type.GetConstructors();
foreach (ConstructorInfo ctor in constructors)
{
    ParameterInfo[] ctorParams = ctor.GetParameters();
    string paramList = string.Join(", ",
        ctorParams.Select(p => $"{p.ParameterType.Name} {p.Name}"));
    Console.WriteLine($"Конструктор({paramList})");
}

// Вызов конструктора с параметрами
ConstructorInfo paramCtor = type.GetConstructor(
    new[] { typeof(string), typeof(decimal) });
object instance = paramCtor.Invoke(new object[] { "Мария", 150000m });
```

### BindingFlags — управление поиском

`BindingFlags` определяет, какие члены класса будут найдены:

```csharp
// Все инстанс-члены (публичные и приватные)
var allInstanceMembers = type.GetMembers(
    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

// Только статические публичные
var staticPublic = type.GetMembers(
    BindingFlags.Public | BindingFlags.Static);

// Только объявленные в этом типе (без наследованных)
var declaredOnly = type.GetMembers(
    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
```

---

## 3. Получение типов: typeof(), GetType(), Type.GetType()

Три основных способа получить объект `Type`:

### typeof() — оператор времени компиляции

```csharp
// Тип известен на этапе компиляции
Type stringType = typeof(string);
Type listType = typeof(List<>);         // open generic
Type listIntType = typeof(List<int>);   // closed generic

// Часто используется для сравнения типов
if (someType == typeof(int))
{
    Console.WriteLine("Это int");
}
```

### GetType() — метод экземпляра (полиморфный)

```csharp
object obj = "Hello, World!";
Type type = obj.GetType(); // System.String

// GetType() возвращает реальный тип объекта (runtime type)
Animal animal = new Dog();
Console.WriteLine(animal.GetType()); // Dog, не Animal!

// Внимание: GetType() не работает с null
object nullObj = null;
// nullObj.GetType(); // NullReferenceException!
```

### Type.GetType() — получение по строковому имени

```csharp
// Из текущей сборки или mscorlib/System.Private.CoreLib
Type intType = Type.GetType("System.Int32");

// Из другой сборки — нужно полное имя (assembly-qualified name)
Type jsonType = Type.GetType(
    "System.Text.Json.JsonSerializer, System.Text.Json");

// Вернёт null, если тип не найден
Type unknown = Type.GetType("NonExistent.Type");
Console.WriteLine(unknown == null); // True

// Версия, бросающая исключение при ненахождении
Type strict = Type.GetType("System.Int32", throwOnError: true);

// Case-insensitive поиск
Type caseInsensitive = Type.GetType("system.int32",
    throwOnError: false, ignoreCase: true);
```

### Сравнение трёх подходов

| Способ | Когда использовать | Время выполнения |
|--------|-------------------|-----------------|
| `typeof()` | Тип известен при компиляции | Компиляция (самый быстрый) |
| `GetType()` | Нужен runtime-тип объекта | Runtime (быстрый) |
| `Type.GetType()` | Тип задан строкой (конфигурация, плагины) | Runtime (медленнее) |

---

## 4. Создание экземпляров: Activator.CreateInstance

### Базовое использование

```csharp
// Создание через Type
Type type = typeof(Employee);
object instance = Activator.CreateInstance(type);

// С параметрами конструктора
object employee = Activator.CreateInstance(type, "Алексей", 120000m);

// Generic-версия (возвращает типизированный результат)
Employee emp = Activator.CreateInstance<Employee>();
```

### Создание generic-типов

```csharp
// Создание List<string> динамически
Type openGeneric = typeof(List<>);
Type closedGeneric = openGeneric.MakeGenericType(typeof(string));
object list = Activator.CreateInstance(closedGeneric);

// Можно привести к интерфейсу
IList<string> typedList = (IList<string>)list;
typedList.Add("Элемент");
```

### Создание типов из другой сборки

```csharp
// По имени типа и сборки
ObjectHandle handle = Activator.CreateInstance(
    assemblyName: "MyLibrary",
    typeName: "MyLibrary.Services.UserService");
object service = handle.Unwrap();

// Создание с непубличным конструктором
object singleton = Activator.CreateInstance(
    type,
    bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance,
    binder: null,
    args: null,
    culture: null);
```

### Альтернатива: ConstructorInfo.Invoke

```csharp
Type type = typeof(Employee);
ConstructorInfo ctor = type.GetConstructor(
    new[] { typeof(string), typeof(decimal) });

// Быстрее, чем Activator.CreateInstance, если конструктор уже найден
Employee emp = (Employee)ctor.Invoke(new object[] { "Дмитрий", 200000m });
```

### Создание через скомпилированное выражение (быстрая фабрика)

```csharp
// Компилируем делегат-фабрику — один раз, используем многократно
public static Func<T> CreateFactory<T>() where T : new()
{
    var newExpr = Expression.New(typeof(T));
    var lambda = Expression.Lambda<Func<T>>(newExpr);
    return lambda.Compile();
}

// Использование
var factory = CreateFactory<Employee>();
Employee emp1 = factory(); // намного быстрее Activator.CreateInstance
Employee emp2 = factory();
```

---

## 5. Вызов методов и свойств через Reflection

### Вызов метода

```csharp
var employee = new Employee("Ольга", 100000m);
Type type = employee.GetType();

// Вызов публичного метода
MethodInfo promoteMethod = type.GetMethod("Promote");
promoteMethod.Invoke(employee, new object[] { 20000m });

// Вызов приватного метода
MethodInfo logMethod = type.GetMethod("LogAction",
    BindingFlags.NonPublic | BindingFlags.Instance);
logMethod.Invoke(employee, new object[] { "Повышение" });

// Вызов статического метода
MethodInfo staticMethod = type.GetMethod("StaticMethod",
    BindingFlags.Public | BindingFlags.Static);
staticMethod?.Invoke(null, null); // obj = null для статических
```

### Вызов generic-метода

```csharp
public class DataProcessor
{
    public T Parse<T>(string input) where T : IParsable<T>
    {
        return T.Parse(input, null);
    }
}

Type type = typeof(DataProcessor);
MethodInfo openMethod = type.GetMethod("Parse");
MethodInfo closedMethod = openMethod.MakeGenericMethod(typeof(int));

var processor = new DataProcessor();
object result = closedMethod.Invoke(processor, new object[] { "42" });
Console.WriteLine(result); // 42
```

### Чтение и установка свойств

```csharp
var employee = new Employee("Сергей", 90000m);
Type type = employee.GetType();

// Чтение свойства
PropertyInfo nameProp = type.GetProperty("Name");
string name = (string)nameProp.GetValue(employee);

// Установка свойства
nameProp.SetValue(employee, "Сергей Петров");

// Установка свойства с private setter
PropertyInfo salaryProp = type.GetProperty("Salary");
// salaryProp.SetValue(employee, 150000m); // Ошибка — setter приватный
// Обход через приватный setter:
MethodInfo privateSetter = salaryProp.GetSetMethod(nonPublic: true);
privateSetter.Invoke(employee, new object[] { 150000m });
```

### Работа с индексаторами

```csharp
public class Matrix
{
    private readonly int[,] _data = new int[3, 3];

    public int this[int row, int col]
    {
        get => _data[row, col];
        set => _data[row, col] = value;
    }
}

var matrix = new Matrix();
Type type = typeof(Matrix);

// Индексатор — это свойство с именем "Item"
PropertyInfo indexer = type.GetProperty("Item");
indexer.SetValue(matrix, 42, new object[] { 1, 2 });
int value = (int)indexer.GetValue(matrix, new object[] { 1, 2 });
Console.WriteLine(value); // 42
```

---

## 6. Атрибуты: создание и получение через Reflection

### Создание custom attribute

```csharp
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = false,
    Inherited = true)]
public class AuditAttribute : Attribute
{
    public string Action { get; }
    public string Description { get; set; }

    public AuditAttribute(string action)
    {
        Action = action;
    }
}

[AttributeUsage(AttributeTargets.Property)]
public class ValidateRangeAttribute : Attribute
{
    public int Min { get; }
    public int Max { get; }

    public ValidateRangeAttribute(int min, int max)
    {
        Min = min;
        Max = max;
    }
}
```

### Применение атрибутов

```csharp
[Audit("UserManagement", Description = "Управление пользователями")]
public class UserService
{
    [ValidateRange(1, 150)]
    public int Age { get; set; }

    [Audit("CreateUser")]
    public void CreateUser(string name)
    {
        // ...
    }
}
```

### Получение атрибутов через Reflection

```csharp
Type type = typeof(UserService);

// Проверка наличия атрибута
bool hasAudit = type.IsDefined(typeof(AuditAttribute), inherit: true);

// Получение атрибута класса
AuditAttribute classAudit = type.GetCustomAttribute<AuditAttribute>();
Console.WriteLine($"Action: {classAudit.Action}");
Console.WriteLine($"Description: {classAudit.Description}");

// Получение атрибутов всех методов
foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
{
    var audit = method.GetCustomAttribute<AuditAttribute>();
    if (audit != null)
    {
        Console.WriteLine($"Метод {method.Name}: Action={audit.Action}");
    }
}

// Получение атрибутов свойств
foreach (PropertyInfo prop in type.GetProperties())
{
    var range = prop.GetCustomAttribute<ValidateRangeAttribute>();
    if (range != null)
    {
        Console.WriteLine($"Свойство {prop.Name}: [{range.Min}..{range.Max}]");
    }
}
```

### Практический пример: простой валидатор на атрибутах

```csharp
public static class ReflectionValidator
{
    public static List<string> Validate(object obj)
    {
        var errors = new List<string>();
        Type type = obj.GetType();

        foreach (PropertyInfo prop in type.GetProperties())
        {
            var rangeAttr = prop.GetCustomAttribute<ValidateRangeAttribute>();
            if (rangeAttr != null && prop.PropertyType == typeof(int))
            {
                int value = (int)prop.GetValue(obj);
                if (value < rangeAttr.Min || value > rangeAttr.Max)
                {
                    errors.Add(
                        $"{prop.Name}: значение {value} вне диапазона " +
                        $"[{rangeAttr.Min}, {rangeAttr.Max}]");
                }
            }
        }
        return errors;
    }
}

// Использование
var service = new UserService { Age = 200 };
var errors = ReflectionValidator.Validate(service);
// "Age: значение 200 вне диапазона [1, 150]"
```

---

## 7. Assembly Loading

### Assembly.Load — загрузка по имени

```csharp
// По простому имени (ищет в probing paths)
Assembly asm = Assembly.Load("System.Text.Json");

// По полному имени (assembly-qualified name)
Assembly asm2 = Assembly.Load(
    "System.Text.Json, Version=8.0.0.0, Culture=neutral, " +
    "PublicKeyToken=cc7b13ffcd2ddd51");

// Загрузка из byte[]
byte[] rawAssembly = File.ReadAllBytes("MyPlugin.dll");
Assembly asm3 = Assembly.Load(rawAssembly);
```

### Assembly.LoadFrom — загрузка по пути

```csharp
// Загрузка из конкретного файла
Assembly plugin = Assembly.LoadFrom(@"/plugins/MyPlugin.dll");

// Получаем типы из загруженной сборки
Type[] types = plugin.GetTypes();
foreach (Type type in types)
{
    if (typeof(IPlugin).IsAssignableFrom(type) && !type.IsAbstract)
    {
        IPlugin pluginInstance = (IPlugin)Activator.CreateInstance(type);
        pluginInstance.Execute();
    }
}
```

### Assembly.LoadFrom vs Assembly.Load

| Аспект | `Assembly.Load` | `Assembly.LoadFrom` |
|--------|----------------|-------------------|
| Контекст загрузки | Default load context | LoadFrom context |
| Поиск зависимостей | Стандартный probing | Из директории файла |
| Идентичность | По имени сборки | По пути файла |
| Рекомендация | Предпочтительнее | Для плагинов |

### AssemblyLoadContext (.NET Core / .NET 5+)

```csharp
// Изолированная загрузка сборок (для плагинов)
public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly Load(AssemblyName assemblyName)
    {
        string assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }
        return null; // fallback к Default context
    }
}

// Использование
var context = new PluginLoadContext("/plugins/MyPlugin.dll");
Assembly pluginAssembly = context.LoadFromAssemblyPath("/plugins/MyPlugin.dll");

// Выгрузка (возможна благодаря isCollectible: true)
context.Unload();
```

### Рефлексия по метаданным без загрузки (MetadataLoadContext)

```csharp
// Только чтение метаданных — сборка не загружается в runtime
using System.Reflection;

var paths = new[] { typeof(object).Assembly.Location };
var resolver = new PathAssemblyResolver(paths);

using var mlc = new MetadataLoadContext(resolver);
Assembly asm = mlc.LoadFromAssemblyPath("/path/to/MyLib.dll");

// Можно исследовать типы, но нельзя создавать экземпляры
foreach (Type type in asm.GetTypes())
{
    Console.WriteLine(type.FullName);
}
```

---

## 8. Dynamic Method Invocation

### Delegate.CreateDelegate — быстрый вызов

```csharp
public class Calculator
{
    public int Add(int a, int b) => a + b;
    public static int Multiply(int a, int b) => a * b;
}

Type type = typeof(Calculator);
var calc = new Calculator();

// Для статического метода
MethodInfo multiplyMethod = type.GetMethod("Multiply");
var multiplyDelegate = (Func<int, int, int>)Delegate.CreateDelegate(
    typeof(Func<int, int, int>), multiplyMethod);
int result = multiplyDelegate(3, 4); // 12 — быстро как прямой вызов

// Для инстанс-метода (привязка к конкретному экземпляру)
MethodInfo addMethod = type.GetMethod("Add");
var addDelegate = (Func<int, int, int>)Delegate.CreateDelegate(
    typeof(Func<int, int, int>), calc, addMethod);
int sum = addDelegate(5, 7); // 12
```

### DynamicMethod и IL Emit

```csharp
using System.Reflection.Emit;

// Создаём динамический метод, который складывает два числа
var dynamicMethod = new DynamicMethod(
    name: "DynamicAdd",
    returnType: typeof(int),
    parameterTypes: new[] { typeof(int), typeof(int) },
    m: typeof(Program).Module);

ILGenerator il = dynamicMethod.GetILGenerator();
il.Emit(OpCodes.Ldarg_0);  // Загрузить первый аргумент
il.Emit(OpCodes.Ldarg_1);  // Загрузить второй аргумент
il.Emit(OpCodes.Add);       // Сложить
il.Emit(OpCodes.Ret);       // Вернуть результат

var addFunc = (Func<int, int, int>)dynamicMethod.CreateDelegate(
    typeof(Func<int, int, int>));

Console.WriteLine(addFunc(10, 20)); // 30
```

### Быстрый доступ к свойствам через скомпилированные делегаты

```csharp
public static class FastPropertyAccess
{
    public static Func<object, object> CreateGetter(PropertyInfo property)
    {
        var instance = Expression.Parameter(typeof(object), "instance");
        var cast = Expression.Convert(instance, property.DeclaringType);
        var propertyAccess = Expression.Property(cast, property);
        var convert = Expression.Convert(propertyAccess, typeof(object));
        var lambda = Expression.Lambda<Func<object, object>>(convert, instance);
        return lambda.Compile();
    }

    public static Action<object, object> CreateSetter(PropertyInfo property)
    {
        var instance = Expression.Parameter(typeof(object), "instance");
        var value = Expression.Parameter(typeof(object), "value");
        var cast = Expression.Convert(instance, property.DeclaringType);
        var valueCast = Expression.Convert(value, property.PropertyType);
        var assign = Expression.Assign(
            Expression.Property(cast, property), valueCast);
        var lambda = Expression.Lambda<Action<object, object>>(
            assign, instance, value);
        return lambda.Compile();
    }
}

// Использование
PropertyInfo nameProp = typeof(Employee).GetProperty("Name");
var getter = FastPropertyAccess.CreateGetter(nameProp);
var setter = FastPropertyAccess.CreateSetter(nameProp);

var emp = new Employee("Тест", 100m);
setter(emp, "Новое имя");
string name = (string)getter(emp); // "Новое имя"
```

---

## 9. Expression Trees vs Reflection

### Что такое Expression Trees

Expression Trees — это представление кода в виде дерева данных. Они позволяют анализировать и компилировать код во время выполнения.

```csharp
// Простое выражение
Expression<Func<int, int, int>> addExpr = (a, b) => a + b;

// Это дерево можно разобрать
BinaryExpression body = (BinaryExpression)addExpr.Body;
Console.WriteLine($"Оператор: {body.NodeType}");   // Add
Console.WriteLine($"Левый: {body.Left}");            // a
Console.WriteLine($"Правый: {body.Right}");          // b

// И скомпилировать в делегат
Func<int, int, int> compiled = addExpr.Compile();
Console.WriteLine(compiled(3, 5)); // 8
```

### Построение Expression Trees программно

```csharp
// Динамическое создание: (Employee e) => e.Name == "Иван"
var param = Expression.Parameter(typeof(Employee), "e");
var property = Expression.Property(param, "Name");
var constant = Expression.Constant("Иван");
var equality = Expression.Equal(property, constant);
var lambda = Expression.Lambda<Func<Employee, bool>>(equality, param);

// Компиляция
Func<Employee, bool> filter = lambda.Compile();

var employees = new List<Employee>
{
    new("Иван", 100000m),
    new("Мария", 120000m)
};

var result = employees.Where(filter).ToList();
```

### Сравнение Expression Trees и Reflection

| Аспект | Reflection | Expression Trees |
|--------|-----------|-----------------|
| Скорость (первый вызов) | Быстрее | Медленнее (компиляция) |
| Скорость (повторные) | Медленно | Быстро (как прямой вызов) |
| Анализ кода | Нет | Да (можно обходить дерево) |
| LINQ Providers | Нет | Да (EF Core, etc.) |
| Сложность | Низкая | Средняя–высокая |
| Типобезопасность | Нет (object) | Частичная |

### Expression Trees для маппинга (аналог AutoMapper)

```csharp
public static class SimpleMapper
{
    private static readonly ConcurrentDictionary<(Type, Type), Delegate> _cache = new();

    public static TDest Map<TSource, TDest>(TSource source)
        where TDest : new()
    {
        var key = (typeof(TSource), typeof(TDest));
        var mapper = (Func<TSource, TDest>)_cache.GetOrAdd(key, _ =>
        {
            var srcParam = Expression.Parameter(typeof(TSource), "src");
            var bindings = new List<MemberBinding>();

            foreach (var destProp in typeof(TDest).GetProperties()
                .Where(p => p.CanWrite))
            {
                var srcProp = typeof(TSource).GetProperty(destProp.Name);
                if (srcProp != null && srcProp.PropertyType == destProp.PropertyType)
                {
                    var srcAccess = Expression.Property(srcParam, srcProp);
                    bindings.Add(Expression.Bind(destProp, srcAccess));
                }
            }

            var body = Expression.MemberInit(
                Expression.New(typeof(TDest)), bindings);
            var lambda = Expression.Lambda<Func<TSource, TDest>>(body, srcParam);
            return lambda.Compile();
        });

        return mapper(source);
    }
}
```

---

## 10. Source Generators (.NET 5+) как альтернатива Reflection

### Что такое Source Generators

Source Generators — это компоненты компилятора Roslyn, которые генерируют C# код на этапе компиляции. Они позволяют заменить многие сценарии, где раньше использовалась Reflection.

**Преимущества над Reflection:**
- Нулевые затраты во время выполнения
- Ошибки обнаруживаются на этапе компиляции
- Совместимость с AOT (NativeAOT, Blazor WASM)
- Код можно увидеть и отладить

### Пример Source Generator

```csharp
// Атрибут-маркер
[AttributeUsage(AttributeTargets.Class)]
public class AutoToStringAttribute : Attribute { }

// Source Generator
[Generator]
public class AutoToStringGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "AutoToStringAttribute",
                predicate: (node, _) => node is ClassDeclarationSyntax,
                transform: (ctx, _) => GetClassInfo(ctx))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(classDeclarations,
            static (spc, source) => Execute(spc, source));
    }

    private static void Execute(
        SourceProductionContext context, ClassInfo classInfo)
    {
        var properties = classInfo.Properties;
        var propStrings = properties
            .Select(p => $"\"{p.Name}={{{{this.{p.Name}}}}}\"");

        string source = $$"""
            namespace {{classInfo.Namespace}};

            partial class {{classInfo.Name}}
            {
                public override string ToString()
                {
                    return $"{{classInfo.Name}} {{ {{string.Join(", ", propStrings)}} }}";
                }
            }
            """;

        context.AddSource($"{classInfo.Name}.g.cs", source);
    }
}
```

### Использование

```csharp
[AutoToString]
public partial class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}

// Автоматически сгенерируется:
// public override string ToString()
// {
//     return $"Product {{ Id={this.Id}, Name={this.Name}, Price={this.Price} }}";
// }

var product = new Product { Id = 1, Name = "Книга", Price = 499.99m };
Console.WriteLine(product.ToString());
// Product { Id=1, Name=Книга, Price=499.99 }
```

### Incremental vs Non-Incremental Generators

В .NET 6+ рекомендуется использовать `IIncrementalGenerator` вместо `ISourceGenerator`:

```csharp
// Старый подход (ISourceGenerator) — пересоздаёт всё при каждом изменении
[Generator]
public class OldGenerator : ISourceGenerator { ... }

// Новый подход (IIncrementalGenerator) — инкрементальный, кеширует результаты
[Generator]
public class NewGenerator : IIncrementalGenerator { ... }
```

---

## 11. System.Text.Json Source Generation

### Проблема Reflection в сериализации

Стандартный `System.Text.Json` использует Reflection для:
- Обнаружения свойств
- Создания экземпляров
- Чтения/записи значений

Это медленно при старте и несовместимо с AOT-компиляцией.

### Source Generation для JSON

```csharp
// Определяем контекст сериализации
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(WeatherForecast))]
[JsonSerializable(typeof(List<WeatherForecast>))]
public partial class AppJsonContext : JsonSerializerContext
{
}

public class WeatherForecast
{
    public DateTime Date { get; set; }
    public int TemperatureC { get; set; }
    public string Summary { get; set; }
}
```

### Использование

```csharp
var forecast = new WeatherForecast
{
    Date = DateTime.Now,
    TemperatureC = 25,
    Summary = "Тёплый"
};

// Сериализация с source generation (без Reflection)
string json = JsonSerializer.Serialize(forecast,
    AppJsonContext.Default.WeatherForecast);

// Десериализация
WeatherForecast result = JsonSerializer.Deserialize(json,
    AppJsonContext.Default.WeatherForecast);

// Для коллекций
var list = new List<WeatherForecast> { forecast };
string jsonArray = JsonSerializer.Serialize(list,
    AppJsonContext.Default.ListWeatherForecast);
```

### Режимы source generation

```csharp
// Metadata-based — генерирует только метаданные типов
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(MyType))]
public partial class MetadataOnlyContext : JsonSerializerContext { }

// Serialization-based — генерирует оптимизированную логику сериализации
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Serialization)]
[JsonSerializable(typeof(MyType))]
public partial class SerializationContext : JsonSerializerContext { }

// Default — оба режима (максимальная производительность)
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(MyType))]
public partial class FullContext : JsonSerializerContext { }
```

### Интеграция с ASP.NET Core Minimal APIs

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0,
        AppJsonContext.Default);
});

var app = builder.Build();
app.MapGet("/weather", () => new WeatherForecast
{
    Date = DateTime.Now,
    TemperatureC = 20,
    Summary = "Прохладно"
});
app.Run();
```

---

## 12. Performance: Reflection vs скомпилированные делегаты vs Source Generators

### Бенчмарки вызова метода

```csharp
// BenchmarkDotNet тест
[MemoryDiagnoser]
public class ReflectionBenchmarks
{
    private readonly Employee _employee = new("Тест", 100000m);
    private readonly MethodInfo _method;
    private readonly Func<Employee, decimal, decimal> _compiled;
    private readonly Action<object, object[]> _delegateInvoke;

    public ReflectionBenchmarks()
    {
        _method = typeof(Employee).GetMethod("Promote");

        // Компилируем делегат один раз
        var instance = Expression.Parameter(typeof(Employee));
        var arg = Expression.Parameter(typeof(decimal));
        var call = Expression.Call(instance, _method, arg);
        _compiled = Expression.Lambda<Func<Employee, decimal, decimal>>(
            call, instance, arg).Compile();
    }

    [Benchmark(Baseline = true)]
    public void DirectCall()
    {
        _employee.Promote(1000m);
    }

    [Benchmark]
    public void ReflectionInvoke()
    {
        _method.Invoke(_employee, new object[] { 1000m });
    }

    [Benchmark]
    public void CompiledDelegate()
    {
        _compiled(_employee, 1000m);
    }
}
```

### Типичные результаты бенчмарков

| Метод | Среднее время | Аллокации | Отношение |
|-------|--------------|-----------|-----------|
| Прямой вызов | ~1 ns | 0 B | 1x |
| Скомпилированный делегат | ~2-3 ns | 0 B | ~2-3x |
| `Delegate.CreateDelegate` | ~2-3 ns | 0 B | ~2-3x |
| `MethodInfo.Invoke` | ~100-200 ns | 40-80 B | ~100-200x |
| `Activator.CreateInstance` | ~50-100 ns | 24-48 B | ~50-100x |
| Source Generator (compile-time) | ~1 ns | 0 B | 1x |

### Бенчмарки чтения свойств

```csharp
[MemoryDiagnoser]
public class PropertyAccessBenchmarks
{
    private readonly Employee _emp = new("Тест", 100000m);
    private readonly PropertyInfo _prop;
    private readonly Func<object, object> _compiledGetter;

    public PropertyAccessBenchmarks()
    {
        _prop = typeof(Employee).GetProperty("Name");
        _compiledGetter = FastPropertyAccess.CreateGetter(_prop);
    }

    [Benchmark(Baseline = true)]
    public string DirectAccess() => _emp.Name;

    [Benchmark]
    public object ReflectionGet() => _prop.GetValue(_emp);

    [Benchmark]
    public object CompiledGet() => _compiledGetter(_emp);
}
```

### Типичные результаты

| Метод | Среднее время | Аллокации |
|-------|--------------|-----------|
| Прямой доступ | ~0.5 ns | 0 B |
| Скомпилированный getter | ~2 ns | 0 B |
| `PropertyInfo.GetValue` | ~80-120 ns | 24 B (boxing) |

---

## 13. Кеширование Reflection

### Проблема: повторное получение метаданных

Каждый вызов `GetMethod()`, `GetProperty()` и т.д. — относительно дорогая операция. При частом использовании необходимо кеширование.

### Паттерн: ConcurrentDictionary для кеширования

```csharp
public static class ReflectionCache
{
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo>
        _propertyCache = new();

    private static readonly ConcurrentDictionary<(Type, string), MethodInfo>
        _methodCache = new();

    private static readonly ConcurrentDictionary<PropertyInfo, Func<object, object>>
        _getterCache = new();

    private static readonly ConcurrentDictionary<PropertyInfo, Action<object, object>>
        _setterCache = new();

    public static PropertyInfo GetCachedProperty(Type type, string name)
    {
        return _propertyCache.GetOrAdd((type, name),
            key => key.Item1.GetProperty(key.Item2));
    }

    public static Func<object, object> GetCachedGetter(PropertyInfo property)
    {
        return _getterCache.GetOrAdd(property,
            prop => FastPropertyAccess.CreateGetter(prop));
    }

    public static Action<object, object> GetCachedSetter(PropertyInfo property)
    {
        return _setterCache.GetOrAdd(property,
            prop => FastPropertyAccess.CreateSetter(prop));
    }
}

// Использование
PropertyInfo prop = ReflectionCache.GetCachedProperty(typeof(Employee), "Name");
var getter = ReflectionCache.GetCachedGetter(prop);
string name = (string)getter(employee);
```

### Бенчмарк: с кешированием и без

```csharp
[MemoryDiagnoser]
public class CachingBenchmarks
{
    private readonly Employee _emp = new("Тест", 100000m);

    // Кешированные значения
    private static readonly PropertyInfo CachedProp =
        typeof(Employee).GetProperty("Name");
    private static readonly Func<object, object> CachedGetter =
        FastPropertyAccess.CreateGetter(CachedProp);

    [Benchmark]
    public object NoCaching()
    {
        // Каждый раз ищем свойство заново
        var prop = typeof(Employee).GetProperty("Name");
        return prop.GetValue(_emp);
    }

    [Benchmark]
    public object CachedPropertyInfo()
    {
        // PropertyInfo кеширован, но GetValue — через reflection
        return CachedProp.GetValue(_emp);
    }

    [Benchmark]
    public object CachedCompiledGetter()
    {
        // Всё кешировано и скомпилировано
        return CachedGetter(_emp);
    }
}
```

### Типичные результаты

| Подход | Среднее время | Аллокации |
|--------|--------------|-----------|
| Без кеширования | ~250 ns | 48 B |
| Кешированный PropertyInfo | ~90 ns | 24 B |
| Кешированный скомпилированный getter | ~3 ns | 0 B |

### TypeDescriptor как альтернативный кеш

```csharp
// TypeDescriptor кеширует метаданные автоматически
PropertyDescriptorCollection properties =
    TypeDescriptor.GetProperties(typeof(Employee));

PropertyDescriptor nameProp = properties["Name"];
object value = nameProp.GetValue(employee);
nameProp.SetValue(employee, "Новое имя");
```

---

## 14. Практические сценарии

### 14.1 DI-контейнер (упрощённый)

```csharp
public class SimpleDIContainer
{
    private readonly Dictionary<Type, Type> _registrations = new();
    private readonly Dictionary<Type, object> _singletons = new();

    public void Register<TInterface, TImplementation>()
        where TImplementation : TInterface
    {
        _registrations[typeof(TInterface)] = typeof(TImplementation);
    }

    public void RegisterSingleton<TInterface, TImplementation>()
        where TImplementation : TInterface
    {
        _registrations[typeof(TInterface)] = typeof(TImplementation);
        _singletons[typeof(TInterface)] = null; // маркер
    }

    public T Resolve<T>() => (T)Resolve(typeof(T));

    public object Resolve(Type type)
    {
        // Проверяем синглтоны
        if (_singletons.ContainsKey(type) && _singletons[type] != null)
            return _singletons[type];

        Type implementationType = _registrations.ContainsKey(type)
            ? _registrations[type]
            : type;

        // Находим конструктор с наибольшим количеством параметров
        ConstructorInfo ctor = implementationType.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();

        // Рекурсивно резолвим зависимости
        ParameterInfo[] parameters = ctor.GetParameters();
        object[] args = parameters
            .Select(p => Resolve(p.ParameterType))
            .ToArray();

        object instance = ctor.Invoke(args);

        // Сохраняем синглтон
        if (_singletons.ContainsKey(type))
            _singletons[type] = instance;

        return instance;
    }
}

// Использование
var container = new SimpleDIContainer();
container.Register<ILogger, ConsoleLogger>();
container.Register<IUserRepository, SqlUserRepository>();
container.Register<IUserService, UserService>();

var service = container.Resolve<IUserService>();
```

### 14.2 Простой ORM (маппинг DataReader)

```csharp
public static class SimpleORM
{
    private static readonly ConcurrentDictionary<Type, Delegate> _mappers = new();

    public static List<T> MapToList<T>(DbDataReader reader) where T : new()
    {
        var mapper = (Func<DbDataReader, T>)_mappers.GetOrAdd(
            typeof(T), _ => CreateMapper<T>(reader));

        var results = new List<T>();
        while (reader.Read())
        {
            results.Add(mapper(reader));
        }
        return results;
    }

    private static Func<DbDataReader, T> CreateMapper<T>(DbDataReader reader)
        where T : new()
    {
        var readerParam = Expression.Parameter(typeof(DbDataReader), "reader");
        var bindings = new List<MemberBinding>();
        var properties = typeof(T).GetProperties()
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < reader.FieldCount; i++)
        {
            string columnName = reader.GetName(i);
            if (properties.TryGetValue(columnName, out var prop))
            {
                // reader.GetValue(i)
                var getValueCall = Expression.Call(
                    readerParam,
                    typeof(DbDataReader).GetMethod("GetValue"),
                    Expression.Constant(i));

                var converted = Expression.Convert(getValueCall, prop.PropertyType);
                bindings.Add(Expression.Bind(prop, converted));
            }
        }

        var body = Expression.MemberInit(Expression.New(typeof(T)), bindings);
        var lambda = Expression.Lambda<Func<DbDataReader, T>>(body, readerParam);
        return lambda.Compile();
    }
}
```

### 14.3 Система плагинов

```csharp
public interface IPlugin
{
    string Name { get; }
    string Version { get; }
    void Initialize();
    void Execute(PluginContext context);
}

public class PluginManager
{
    private readonly List<IPlugin> _plugins = new();
    private readonly string _pluginDirectory;

    public PluginManager(string pluginDirectory)
    {
        _pluginDirectory = pluginDirectory;
    }

    public void LoadPlugins()
    {
        foreach (string dll in Directory.GetFiles(_pluginDirectory, "*.dll"))
        {
            var context = new PluginLoadContext(dll);
            Assembly assembly = context.LoadFromAssemblyPath(
                Path.GetFullPath(dll));

            foreach (Type type in assembly.GetTypes())
            {
                if (typeof(IPlugin).IsAssignableFrom(type)
                    && !type.IsAbstract
                    && !type.IsInterface)
                {
                    IPlugin plugin = (IPlugin)Activator.CreateInstance(type);
                    plugin.Initialize();
                    _plugins.Add(plugin);
                    Console.WriteLine(
                        $"Загружен плагин: {plugin.Name} v{plugin.Version}");
                }
            }
        }
    }

    public void ExecuteAll(PluginContext context)
    {
        foreach (var plugin in _plugins)
        {
            try
            {
                plugin.Execute(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Ошибка в плагине {plugin.Name}: {ex.Message}");
            }
        }
    }
}
```

### 14.4 Универсальный сериализатор (упрощённый)

```csharp
public static class SimpleSerializer
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]>
        _propertyCache = new();

    public static Dictionary<string, object> Serialize(object obj)
    {
        if (obj == null) return null;

        Type type = obj.GetType();
        var properties = _propertyCache.GetOrAdd(type,
            t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                  .Where(p => p.CanRead)
                  .ToArray());

        var result = new Dictionary<string, object>();
        foreach (var prop in properties)
        {
            object value = prop.GetValue(obj);

            if (value != null && !prop.PropertyType.IsPrimitive
                && prop.PropertyType != typeof(string)
                && prop.PropertyType != typeof(decimal))
            {
                value = Serialize(value); // рекурсивно
            }

            result[prop.Name] = value;
        }

        return result;
    }

    public static T Deserialize<T>(Dictionary<string, object> data)
        where T : new()
    {
        var obj = new T();
        Type type = typeof(T);

        foreach (var kvp in data)
        {
            PropertyInfo prop = type.GetProperty(kvp.Key);
            if (prop != null && prop.CanWrite)
            {
                object value = Convert.ChangeType(kvp.Value, prop.PropertyType);
                prop.SetValue(obj, value);
            }
        }

        return obj;
    }
}
```

### 14.5 Автоматическая регистрация сервисов

```csharp
// Атрибут для автоматической регистрации
[AttributeUsage(AttributeTargets.Class)]
public class ServiceAttribute : Attribute
{
    public ServiceLifetime Lifetime { get; }
    public Type ServiceType { get; }

    public ServiceAttribute(
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        Type serviceType = null)
    {
        Lifetime = lifetime;
        ServiceType = serviceType;
    }
}

// Расширение для автоматической регистрации
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServicesFromAssembly(
        this IServiceCollection services,
        Assembly assembly)
    {
        var serviceTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.GetCustomAttribute<ServiceAttribute>() != null);

        foreach (Type type in serviceTypes)
        {
            var attr = type.GetCustomAttribute<ServiceAttribute>();
            Type serviceType = attr.ServiceType
                ?? type.GetInterfaces().FirstOrDefault()
                ?? type;

            var descriptor = new ServiceDescriptor(
                serviceType, type, attr.Lifetime);
            services.Add(descriptor);
        }

        return services;
    }
}

// Использование
[Service(ServiceLifetime.Singleton)]
public class CacheService : ICacheService { ... }

[Service(ServiceLifetime.Scoped, serviceType: typeof(IOrderService))]
public class OrderService : IOrderService { ... }

// В Program.cs
builder.Services.AddServicesFromAssembly(Assembly.GetExecutingAssembly());
```

---

## 15. Вопросы на собеседовании с ответами

### Вопрос 1: В чём разница между typeof(), GetType() и Type.GetType()?

**Ответ:**

- `typeof(T)` — оператор времени компиляции. Тип должен быть известен при компиляции. Не требует экземпляра объекта. Самый быстрый вариант.
- `obj.GetType()` — метод экземпляра, определённый в `System.Object`. Возвращает реальный runtime-тип объекта (учитывает полиморфизм). Требует ненулевой экземпляр.
- `Type.GetType(string)` — статический метод, ищущий тип по строковому имени. Для типов вне текущей сборки и `mscorlib` необходимо указать assembly-qualified name. Возвращает `null`, если тип не найден.

```csharp
Animal animal = new Dog();
typeof(Animal);        // Animal
animal.GetType();      // Dog (реальный тип!)
Type.GetType("Dog");   // null (если Dog не в mscorlib)
```

---

### Вопрос 2: Почему Reflection медленный и как это оптимизировать?

**Ответ:**

Reflection медленный по нескольким причинам:
1. **Поиск метаданных** — каждый вызов `GetMethod`/`GetProperty` ищет по таблицам метаданных
2. **Проверки безопасности** — CAS (Code Access Security) и проверки видимости
3. **Boxing/Unboxing** — параметры и возвращаемые значения передаются как `object`
4. **Отсутствие инлайнинга** — JIT не может оптимизировать рефлективные вызовы

**Оптимизации:**
- Кешировать `PropertyInfo`, `MethodInfo` и т.д.
- Использовать `Delegate.CreateDelegate()` для создания типизированных делегатов
- Компилировать Expression Trees для повторных операций
- Применять Source Generators, если код известен при компиляции
- Использовать `DynamicMethod` с IL Emit для максимальной производительности

---

### Вопрос 3: Что такое BindingFlags и зачем они нужны?

**Ответ:**

`BindingFlags` — это перечисление (enum с атрибутом `[Flags]`), которое управляет тем, какие члены типа будут найдены при поиске через Reflection.

Ключевые флаги:
- `Public` / `NonPublic` — фильтр по видимости
- `Instance` / `Static` — инстанс-члены или статические
- `DeclaredOnly` — только объявленные в этом типе (без наследованных)
- `FlattenHierarchy` — включить статические члены из базовых классов

Важно: нужно указать хотя бы один из `Public`/`NonPublic` и один из `Instance`/`Static`, иначе поиск вернёт пустой результат.

```csharp
// Все приватные инстанс-поля (включая backing fields)
type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
```

---

### Вопрос 4: Как Reflection используется в DI-контейнерах?

**Ответ:**

DI-контейнеры (Microsoft.Extensions.DependencyInjection, Autofac, etc.) используют Reflection для:

1. **Анализа конструкторов** — `GetConstructors()` для определения зависимостей
2. **Резолва зависимостей** — `GetParameters()` для получения типов параметров
3. **Создания экземпляров** — `ConstructorInfo.Invoke()` или `Activator.CreateInstance()`
4. **Property injection** — `GetProperties()` + `SetValue()`
5. **Сканирования сборок** — автоматическая регистрация сервисов по конвенциям

Современные контейнеры оптимизируют это, компилируя Expression Trees или генерируя IL при первом резолве, а затем кешируя скомпилированные делегаты.

---

### Вопрос 5: Чем Assembly.Load отличается от Assembly.LoadFrom?

**Ответ:**

- `Assembly.Load(name)` — загружает сборку по имени в Default Load Context. CLR использует стандартные правила probing (GAC, AppBase, PrivateBin). Одна и та же сборка загружается только один раз.
- `Assembly.LoadFrom(path)` — загружает по пути в LoadFrom Context. Если сборка с тем же именем уже в Default Context, может возникнуть конфликт (два разных экземпляра `Type` для одного типа). Зависимости ищутся рядом с файлом.

В .NET Core/5+ рекомендуется использовать `AssemblyLoadContext` для изоляции загрузки плагинов с возможностью выгрузки (`isCollectible: true`).

---

### Вопрос 6: Что такое Source Generators и чем они лучше Reflection?

**Ответ:**

Source Generators — это компоненты компилятора Roslyn, которые анализируют исходный код и генерируют дополнительный C# код на этапе компиляции.

**Преимущества:**
- Нулевые затраты во время выполнения (всё сделано при компиляции)
- Полная совместимость с AOT (NativeAOT, iOS, Blazor WASM)
- Ошибки обнаруживаются при компиляции
- Сгенерированный код можно отладить

**Ограничения:**
- Работают только с информацией, доступной при компиляции
- Не могут модифицировать существующий код (только добавлять новый)
- Сложнее в разработке и отладке

Примеры использования: `System.Text.Json` source generation, `LoggerMessage`, `RegexGenerator`, маппинг DTO.

---

### Вопрос 7: Как Expression Trees связаны с Reflection?

**Ответ:**

Expression Trees и Reflection дополняют друг друга:

1. **Expression Trees используют Reflection API** — `Expression.Property()`, `Expression.Call()` принимают `PropertyInfo`, `MethodInfo`
2. **Expression Trees решают проблему производительности Reflection** — компилированные деревья выражений работают со скоростью, близкой к прямому вызову
3. **LINQ Providers** (EF Core) используют Expression Trees для трансляции C# выражений в SQL

Типичный паттерн: один раз через Reflection получить метаданные, построить Expression Tree, скомпилировать в делегат и кешировать.

```csharp
// Reflection: медленно при каждом вызове
prop.GetValue(obj);

// Expression Tree: медленно при создании, быстро при вызове
var getter = CompileGetter(prop); // один раз
getter(obj); // быстро, многократно
```

---

### Вопрос 8: Безопасно ли использовать Reflection для доступа к приватным членам?

**Ответ:**

Технически возможно, но есть нюансы:

1. **Нарушение инкапсуляции** — приватные члены могут измениться без предупреждения (нет гарантии обратной совместимости)
2. **Безопасность** — в .NET Core/5+ некоторые ограничения ослаблены, но `[DynamicallyAccessedMembers]` позволяет контролировать trimming
3. **AOT-совместимость** — trimmer может удалить приватные члены, к которым обращаются через Reflection
4. **Производительность** — проверки безопасности при доступе к приватным членам дополнительно замедляют операции

**Когда допустимо:** тестирование, ORM-фреймворки, сериализация, отладка. **Когда нет:** бизнес-логика, публичные API.

---

### Вопрос 9: Как System.Text.Json source generation работает без Reflection?

**Ответ:**

При компиляции Source Generator:
1. Анализирует типы, помеченные `[JsonSerializable]`
2. Генерирует класс-контекст, наследующий `JsonSerializerContext`
3. Для каждого типа генерирует `JsonTypeInfo<T>` с метаданными (свойства, конвертеры)
4. Генерирует оптимизированные методы сериализации/десериализации

Весь код доступа к свойствам генерируется статически — вместо `PropertyInfo.GetValue()` генерируется прямой доступ `obj.PropertyName`. Это даёт:
- Нулевые аллокации от boxing
- Полную AOT-совместимость
- Более быстрый cold start (нет инициализации Reflection-кеша)

---

### Вопрос 10: Как Reflection влияет на AOT-компиляцию (NativeAOT)?

**Ответ:**

NativeAOT компилирует код целиком в нативный бинарник. Это создаёт проблемы:

1. **Trimming** — неиспользуемые типы удаляются. Если тип создаётся только через `Activator.CreateInstance`, trimmer может его удалить.
2. **Отсутствие JIT** — `Reflection.Emit` и динамическая генерация кода не работают.
3. **Ограниченная Reflection** — не все операции доступны.

**Решения:**
- Аннотации `[DynamicallyAccessedMembers]` — указать trimmer-у сохранить нужные члены
- `[RequiresUnreferencedCode]` — пометить код, несовместимый с trimming
- `rd.xml` / `TrimmerRootAssembly` — конфигурация trimmer-а
- **Source Generators** — лучшая альтернатива (код генерируется при компиляции)

```csharp
// Подсказка trimmer-у сохранить конструктор
public void CreateInstance(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    Type type)
{
    Activator.CreateInstance(type);
}
```

---

### Вопрос 11: Расскажите о паттерне «скомпилированный делегат» для оптимизации Reflection.

**Ответ:**

Паттерн заключается в замене повторяющихся вызовов `MethodInfo.Invoke()` или `PropertyInfo.GetValue()`/`SetValue()` скомпилированными делегатами. Три основных подхода:

1. **Delegate.CreateDelegate** — самый простой, но требует знания сигнатуры
2. **Expression Trees** — гибкий, работает с произвольными сигнатурами
3. **IL Emit (DynamicMethod)** — максимальная производительность, сложный код

```csharp
// 1. Delegate.CreateDelegate
var del = (Func<Employee, string>)Delegate.CreateDelegate(
    typeof(Func<Employee, string>),
    typeof(Employee).GetProperty("Name").GetGetMethod());

// 2. Expression Trees
var param = Expression.Parameter(typeof(Employee));
var body = Expression.Property(param, "Name");
var getter = Expression.Lambda<Func<Employee, string>>(body, param).Compile();

// Оба варианта работают в ~2-3 ns (vs ~100 ns для PropertyInfo.GetValue)
```

Кеширование делегата в `ConcurrentDictionary<Type, Delegate>` обеспечивает потокобезопасность и amortized O(1) доступ.

---

### Вопрос 12: Как работает MetadataLoadContext и когда его использовать?

**Ответ:**

`MetadataLoadContext` (из пакета `System.Reflection.MetadataLoadContext`) позволяет загрузить сборку только для чтения метаданных, без загрузки в рантайм:

- Типы не могут быть инстанцированы
- Методы не могут быть вызваны
- Нет конфликтов с уже загруженными сборками
- Можно анализировать сборки для другого runtime (например, .NET Framework сборку из .NET 8 приложения)

**Сценарии использования:**
- Инструменты анализа кода
- Генераторы документации
- Системы плагинов (проверка совместимости перед загрузкой)
- CI/CD пайплайны

```csharp
var resolver = new PathAssemblyResolver(
    Directory.GetFiles(runtimeDir, "*.dll")
        .Append(targetAssemblyPath));

using var mlc = new MetadataLoadContext(resolver);
Assembly asm = mlc.LoadFromAssemblyPath(targetAssemblyPath);
// Безопасно анализируем типы без побочных эффектов
```

---

### Вопрос 13: Какие есть альтернативы Reflection в современном .NET?

**Ответ:**

| Альтернатива | Когда использовать |
|-------------|-------------------|
| **Source Generators** | Генерация кода при компиляции (сериализация, маппинг, логирование) |
| **Expression Trees** | Динамическое построение запросов, высокопроизводительный доступ к членам |
| **`Delegate.CreateDelegate`** | Быстрый вызов методов с известной сигнатурой |
| **Generic constraints** | Замена `typeof()` проверок на compile-time constraints |
| **Pattern matching** | Замена `is`/`GetType()` проверок |
| **Interface-based design** | Вместо рефлективного вызова — через интерфейс |
| **`Unsafe.As<T>`** | Низкоуровневые преобразования типов (осторожно!) |

Тренд в .NET: постепенный уход от Reflection в сторону compile-time кодогенерации (Source Generators, Interceptors в .NET 8+).

---

## Заключение

Reflection — мощный, но затратный инструмент. При подготовке к Senior .NET собеседованию важно знать не только как использовать Reflection, но и:

1. **Когда НЕ использовать** — если задачу можно решить статически
2. **Как оптимизировать** — кеширование, скомпилированные делегаты, Expression Trees
3. **Современные альтернативы** — Source Generators, `System.Text.Json` source gen
4. **Ограничения AOT** — и как их обходить с помощью аннотаций и генерации кода
5. **Практическое применение** — DI, ORM, сериализация, плагины

Понимание внутренних механизмов и trade-offs Reflection отличает Senior-разработчика от Middle.
