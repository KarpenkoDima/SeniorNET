# Безопасность в .NET: JWT, OAuth 2.0 и смежные темы

> Полное руководство для подготовки к собеседованию на позицию Senior .NET Developer.

---

## Содержание

1. [Аутентификация vs Авторизация](#1-аутентификация-vs-авторизация)
2. [OAuth 2.0](#2-oauth-20)
3. [OpenID Connect (OIDC)](#3-openid-connect-oidc)
4. [JWT: структура и алгоритмы](#4-jwt-структура-и-алгоритмы)
5. [Access Token vs Refresh Token](#5-access-token-vs-refresh-token)
6. [Claims-based авторизация в ASP.NET Core](#6-claims-based-авторизация-в-aspnet-core)
7. [Middleware: UseAuthentication и UseAuthorization](#7-middleware-useauthentication-и-useauthorization)
8. [Policy-based авторизация](#8-policy-based-авторизация)
9. [Role-based vs Claims-based vs Policy-based](#9-role-based-vs-claims-based-vs-policy-based)
10. [Identity Server / Duende / Keycloak](#10-identity-server--duende--keycloak)
11. [ASP.NET Core Identity](#11-aspnet-core-identity)
12. [Безопасное хранение токенов](#12-безопасное-хранение-токенов)
13. [CORS](#13-cors)
14. [CSRF / XSS / SQL Injection](#14-csrf--xss--sql-injection)
15. [HTTPS и HSTS](#15-https-и-hsts)
16. [API Key Authentication](#16-api-key-authentication)
17. [Rate Limiting в .NET 7+](#17-rate-limiting-в-net-7)
18. [Секреты: User Secrets, Azure Key Vault, Environment Variables](#18-секреты-user-secrets-azure-key-vault-environment-variables)
19. [Вопросы на собеседовании с ответами](#19-вопросы-на-собеседовании-с-ответами)

---

## 1. Аутентификация vs Авторизация

| Аспект | Аутентификация (Authentication) | Авторизация (Authorization) |
|--------|--------------------------------|----------------------------|
| **Что проверяет** | *Кто* вы (идентичность) | *Что* вам разрешено делать |
| **Когда** | Всегда первой | После успешной аутентификации |
| **Пример** | Логин + пароль, OAuth-токен | Роль Admin, Policy "CanEditPosts" |
| **HTTP-статус при отказе** | `401 Unauthorized` | `403 Forbidden` |
| **В ASP.NET Core** | `UseAuthentication()` | `UseAuthorization()` |

### Ключевое различие

```
Аутентификация → "Докажи, что ты — это ты"
Авторизация    → "Имеешь ли ты право на это действие?"
```

В ASP.NET Core аутентификация устанавливает `HttpContext.User` (объект `ClaimsPrincipal`),
а авторизация проверяет Claims/Roles/Policies этого пользователя перед доступом к ресурсу.

---

## 2. OAuth 2.0

OAuth 2.0 — это **протокол авторизации** (не аутентификации!). Он позволяет приложению
получить ограниченный доступ к ресурсам пользователя на другом сервисе без передачи пароля.

### Основные роли

| Роль | Описание |
|------|----------|
| **Resource Owner** | Пользователь, владелец данных |
| **Client** | Приложение, запрашивающее доступ |
| **Authorization Server** | Сервер, выдающий токены (Duende, Keycloak) |
| **Resource Server** | API, защищающий ресурсы |

### Grant Types

#### 2.1 Authorization Code (наиболее безопасный для веб-приложений)

```
1. Client → Authorization Server: redirect пользователя на /authorize
2. Пользователь вводит логин/пароль
3. Authorization Server → Client: redirect с ?code=AUTH_CODE
4. Client → Authorization Server: POST /token { code, client_secret }
5. Authorization Server → Client: { access_token, refresh_token }
```

Используется для серверных (confidential) приложений, где `client_secret` можно безопасно хранить.

#### 2.2 Authorization Code + PKCE (Proof Key for Code Exchange)

**PKCE** — расширение для публичных клиентов (SPA, мобильные приложения),
где невозможно безопасно хранить `client_secret`.

```
1. Client генерирует code_verifier (случайная строка) и code_challenge = SHA256(code_verifier)
2. Client → Auth Server: /authorize?code_challenge=...&code_challenge_method=S256
3. Auth Server → Client: redirect с ?code=AUTH_CODE
4. Client → Auth Server: POST /token { code, code_verifier }
5. Auth Server проверяет SHA256(code_verifier) == code_challenge
6. Auth Server → Client: { access_token, refresh_token }
```

**Пример настройки PKCE-клиента в C#:**

```csharp
services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie()
.AddOpenIdConnect(options =>
{
    options.Authority = "https://auth.example.com";
    options.ClientId = "spa-client";
    options.ResponseType = "code";
    options.UsePkce = true; // Включаем PKCE
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("api");
    options.SaveTokens = true;
});
```

#### 2.3 Client Credentials (machine-to-machine)

Используется для межсервисного взаимодействия, когда нет пользователя.

```
Client → Auth Server: POST /token { client_id, client_secret, grant_type=client_credentials }
Auth Server → Client: { access_token }
```

```csharp
// Получение токена для межсервисного вызова
public class TokenService
{
    private readonly HttpClient _httpClient;

    public TokenService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        var request = new HttpRequestMessage(HttpMethod.Post,
            "https://auth.example.com/connect/token");

        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "service-a",
            ["client_secret"] = "secret",
            ["scope"] = "api.read"
        });

        var response = await _httpClient.SendAsync(request);
        var json = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return json!.AccessToken;
    }
}

public record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("token_type")] string TokenType
);
```

#### 2.4 Устаревшие Grant Types

- **Implicit** — устарел (токен в URL-фрагменте, уязвим). Заменён на Authorization Code + PKCE.
- **Resource Owner Password Credentials (ROPC)** — устарел (передача логина/пароля клиенту).

---

## 3. OpenID Connect (OIDC)

OIDC — это **слой аутентификации поверх OAuth 2.0**. OAuth 2.0 говорит "у тебя есть доступ",
а OIDC говорит "вот кто ты".

### Ключевые отличия от OAuth 2.0

| Аспект | OAuth 2.0 | OIDC |
|--------|-----------|------|
| **Назначение** | Авторизация | Аутентификация + авторизация |
| **Токен** | Access Token | Access Token + **ID Token** |
| **Endpoint** | /authorize, /token | + /userinfo, /.well-known/openid-configuration |
| **Scope** | Произвольные | Обязательный `openid`, опциональные `profile`, `email` |

### ID Token

ID Token — это JWT, содержащий информацию о пользователе:

```json
{
  "iss": "https://auth.example.com",
  "sub": "user-123",
  "aud": "my-client-id",
  "exp": 1700000000,
  "iat": 1699999000,
  "nonce": "abc123",
  "name": "Иван Петров",
  "email": "ivan@example.com"
}
```

### Discovery Document

OIDC-провайдер публикует метаданные по адресу `/.well-known/openid-configuration`:

```json
{
  "issuer": "https://auth.example.com",
  "authorization_endpoint": "https://auth.example.com/connect/authorize",
  "token_endpoint": "https://auth.example.com/connect/token",
  "userinfo_endpoint": "https://auth.example.com/connect/userinfo",
  "jwks_uri": "https://auth.example.com/.well-known/jwks.json",
  "scopes_supported": ["openid", "profile", "email"],
  "response_types_supported": ["code", "id_token", "token"]
}
```

---

## 4. JWT: структура и алгоритмы

JWT (JSON Web Token) — компактный, URL-safe формат передачи claims между сторонами.

### Структура: Header.Payload.Signature

```
eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.
eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6Ikl2YW4iLCJyb2xlIjoiQWRtaW4ifQ.
SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c
```

#### Header (Заголовок)

```json
{
  "alg": "RS256",
  "typ": "JWT",
  "kid": "key-id-123"
}
```

#### Payload (Полезная нагрузка)

```json
{
  "sub": "1234567890",
  "name": "Ivan",
  "role": "Admin",
  "iss": "https://auth.example.com",
  "aud": "my-api",
  "exp": 1700000000,
  "iat": 1699999000,
  "nbf": 1699999000,
  "jti": "unique-token-id"
}
```

**Стандартные claims (Registered Claims):**

| Claim | Описание |
|-------|----------|
| `sub` | Subject — идентификатор пользователя |
| `iss` | Issuer — кто выпустил токен |
| `aud` | Audience — для кого предназначен |
| `exp` | Expiration Time — время истечения |
| `iat` | Issued At — время выпуска |
| `nbf` | Not Before — токен недействителен до этого времени |
| `jti` | JWT ID — уникальный идентификатор токена |

#### Signature (Подпись)

```
RSASHA256(
  base64UrlEncode(header) + "." + base64UrlEncode(payload),
  privateKey
)
```

### Алгоритмы подписи

| Алгоритм | Тип | Описание | Когда использовать |
|----------|-----|----------|--------------------|
| **HS256** | Симметричный (HMAC + SHA-256) | Один и тот же секретный ключ для подписи и верификации | Внутренние сервисы, один издатель = один потребитель |
| **RS256** | Асимметричный (RSA + SHA-256) | Приватный ключ для подписи, публичный для верификации | Микросервисы, внешние API. Auth Server подписывает, API проверяет публичным ключом |
| **ES256** | Асимметричный (ECDSA + SHA-256) | Аналог RS256, но меньше размер ключей и подписи | Высокопроизводительные сценарии, IoT |

### Генерация и валидация JWT в C#

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

public class JwtService
{
    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;

    public JwtService(IConfiguration config)
    {
        _secret = config["Jwt:Secret"]!;
        _issuer = config["Jwt:Issuer"]!;
        _audience = config["Jwt:Audience"]!;
    }

    // Генерация токена (HS256)
    public string GenerateToken(string userId, string email, IList<string> roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        // Добавляем роли
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Валидация токена
    public ClaimsPrincipal? ValidateToken(string token)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.FromSeconds(30) // допуск по времени
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            return handler.ValidateToken(token, validationParameters, out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }
}
```

### Настройка JWT-аутентификации в ASP.NET Core

```csharp
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!)),
        ClockSkew = TimeSpan.Zero // Без допуска для точного контроля TTL
    };

    // Обработка событий
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            if (context.Exception is SecurityTokenExpiredException)
            {
                context.Response.Headers.Append("X-Token-Expired", "true");
            }
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            // Дополнительная валидация (например, проверка в БД)
            var userId = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            // ... проверка что пользователь не заблокирован
            return Task.CompletedTask;
        }
    };
});
```

---

## 5. Access Token vs Refresh Token

| Аспект | Access Token | Refresh Token |
|--------|-------------|---------------|
| **Назначение** | Доступ к защищённым ресурсам | Получение нового Access Token |
| **Время жизни** | Короткое (5-30 минут) | Длительное (дни, недели) |
| **Где хранится** | Память / HttpOnly cookie | HttpOnly cookie / защищённое хранилище |
| **Отправляется** | С каждым API-запросом | Только на endpoint обновления токена |
| **Формат** | Обычно JWT (self-contained) | Обычно opaque string (ссылка на запись в БД) |

### Жизненный цикл токенов

```
1. Пользователь логинится → получает Access Token (15 мин) + Refresh Token (7 дней)
2. Access Token отправляется в заголовке Authorization: Bearer <token>
3. API валидирует Access Token (без обращения к БД — self-contained)
4. Access Token истекает → клиент получает 401
5. Клиент отправляет Refresh Token на /token endpoint
6. Auth Server проверяет Refresh Token в БД, выдаёт новую пару
7. Старый Refresh Token инвалидируется (Refresh Token Rotation)
```

### Refresh Token Rotation

```csharp
public class TokenRefreshService
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwtService;

    public async Task<TokenPair?> RefreshAsync(string refreshToken)
    {
        var storedToken = await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

        if (storedToken is null)
            return null;

        // Проверка: не истёк ли, не отозван ли
        if (storedToken.ExpiresAt < DateTime.UtcNow || storedToken.IsRevoked)
        {
            // Если токен уже использован — возможна атака: отзываем все токены семьи
            if (storedToken.IsUsed)
            {
                await RevokeTokenFamilyAsync(storedToken.FamilyId);
            }
            return null;
        }

        // Помечаем текущий как использованный
        storedToken.IsUsed = true;

        // Генерируем новую пару
        var newAccessToken = _jwtService.GenerateToken(
            storedToken.User.Id,
            storedToken.User.Email,
            await GetRolesAsync(storedToken.User));

        var newRefreshToken = new RefreshToken
        {
            Token = GenerateSecureToken(),
            UserId = storedToken.UserId,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            FamilyId = storedToken.FamilyId // Сохраняем семью токенов
        };

        _db.RefreshTokens.Add(newRefreshToken);
        await _db.SaveChangesAsync();

        return new TokenPair(newAccessToken, newRefreshToken.Token);
    }

    private static string GenerateSecureToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private async Task RevokeTokenFamilyAsync(Guid familyId)
    {
        await _db.RefreshTokens
            .Where(rt => rt.FamilyId == familyId)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.IsRevoked, true));
    }
}

public record TokenPair(string AccessToken, string RefreshToken);
```

---

## 6. Claims-based авторизация в ASP.NET Core

**Claim** — это утверждение о пользователе в формате "тип — значение", выданное доверенным издателем.

```csharp
// Пример claims в ClaimsPrincipal
var claims = new List<Claim>
{
    new(ClaimTypes.Name, "ivan@example.com"),
    new(ClaimTypes.Role, "Admin"),
    new("department", "Engineering"),
    new("subscription", "Premium"),
    new("employee_id", "12345")
};

var identity = new ClaimsIdentity(claims, "jwt");
var principal = new ClaimsPrincipal(identity);
```

### Чтение claims в контроллере

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    [HttpGet]
    public IActionResult GetProfile()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value);
        var department = User.FindFirst("department")?.Value;

        return Ok(new { userId, email, roles, department });
    }
}
```

### Claims Transformation

Позволяет обогащать claims после аутентификации (например, загрузить разрешения из БД):

```csharp
public class CustomClaimsTransformation : IClaimsTransformation
{
    private readonly IPermissionService _permissions;

    public CustomClaimsTransformation(IPermissionService permissions)
    {
        _permissions = permissions;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = (ClaimsIdentity)principal.Identity!;
        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId is null) return principal;

        // Загружаем разрешения из БД
        var permissions = await _permissions.GetPermissionsAsync(userId);

        foreach (var permission in permissions)
        {
            if (!identity.HasClaim("permission", permission))
            {
                identity.AddClaim(new Claim("permission", permission));
            }
        }

        return principal;
    }
}

// Регистрация
builder.Services.AddTransient<IClaimsTransformation, CustomClaimsTransformation>();
```

---

## 7. Middleware: UseAuthentication и UseAuthorization

### Порядок middleware критически важен

```csharp
var app = builder.Build();

app.UseHttpsRedirection();

app.UseCors("AllowSpecificOrigins"); // CORS перед аутентификацией

app.UseAuthentication(); // 1. Устанавливает HttpContext.User
app.UseAuthorization();  // 2. Проверяет права доступа

app.MapControllers();

app.Run();
```

### Что делает каждый middleware

**`UseAuthentication()`:**
- Вызывает зарегистрированный `AuthenticationHandler` (например, `JwtBearerHandler`)
- Извлекает токен из заголовка `Authorization: Bearer <token>`
- Валидирует токен и создаёт `ClaimsPrincipal`
- Устанавливает `HttpContext.User`
- **Не блокирует запрос** — если токена нет, `User` будет анонимным

**`UseAuthorization()`:**
- Проверяет `[Authorize]` атрибуты на endpoints
- Вызывает `IAuthorizationService` для проверки policies
- Возвращает `401` если пользователь не аутентифицирован
- Возвращает `403` если пользователь не авторизован

### Частая ошибка

```csharp
// НЕПРАВИЛЬНО — авторизация не будет работать корректно
app.UseAuthorization();
app.UseAuthentication(); // Слишком поздно!

// ПРАВИЛЬНО
app.UseAuthentication();
app.UseAuthorization();
```

---

## 8. Policy-based авторизация

### Определение политик

```csharp
builder.Services.AddAuthorization(options =>
{
    // Простая политика на основе claim
    options.AddPolicy("PremiumUser", policy =>
        policy.RequireClaim("subscription", "Premium", "Enterprise"));

    // Политика на основе роли
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));

    // Комбинированная политика
    options.AddPolicy("SeniorEngineer", policy =>
    {
        policy.RequireRole("Engineer");
        policy.RequireClaim("level", "Senior", "Lead", "Principal");
        policy.RequireAuthenticatedUser();
    });

    // Политика с кастомным requirement
    options.AddPolicy("MinimumAge", policy =>
        policy.Requirements.Add(new MinimumAgeRequirement(18)));

    // Политика с assertion (для простых случаев)
    options.AddPolicy("BusinessHours", policy =>
        policy.RequireAssertion(context =>
        {
            var hour = DateTime.UtcNow.Hour;
            return hour >= 9 && hour <= 17;
        }));
});
```

### Custom Requirements и Handlers

```csharp
// 1. Requirement — описывает требование
public class MinimumAgeRequirement : IAuthorizationRequirement
{
    public int MinimumAge { get; }

    public MinimumAgeRequirement(int minimumAge)
    {
        MinimumAge = minimumAge;
    }
}

// 2. Handler — реализует проверку
public class MinimumAgeHandler : AuthorizationHandler<MinimumAgeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MinimumAgeRequirement requirement)
    {
        var dateOfBirthClaim = context.User.FindFirst("date_of_birth");

        if (dateOfBirthClaim is null)
            return Task.CompletedTask; // Не вызываем Fail — другой handler может обработать

        var dateOfBirth = DateOnly.Parse(dateOfBirthClaim.Value);
        var age = DateOnly.FromDateTime(DateTime.Today).Year - dateOfBirth.Year;

        if (age >= requirement.MinimumAge)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

// 3. Регистрация
builder.Services.AddSingleton<IAuthorizationHandler, MinimumAgeHandler>();
```

### Resource-based авторизация

```csharp
// Requirement
public class SameAuthorRequirement : IAuthorizationRequirement { }

// Handler с доступом к ресурсу
public class ArticleAuthorizationHandler
    : AuthorizationHandler<SameAuthorRequirement, Article>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SameAuthorRequirement requirement,
        Article resource)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId == resource.AuthorId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

// Использование в контроллере
[ApiController]
[Route("api/articles")]
public class ArticlesController : ControllerBase
{
    private readonly IAuthorizationService _authService;
    private readonly IArticleRepository _articles;

    public ArticlesController(
        IAuthorizationService authService,
        IArticleRepository articles)
    {
        _authService = authService;
        _articles = articles;
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, ArticleDto dto)
    {
        var article = await _articles.GetByIdAsync(id);
        if (article is null) return NotFound();

        var result = await _authService.AuthorizeAsync(
            User, article, new SameAuthorRequirement());

        if (!result.Succeeded)
            return Forbid();

        article.Title = dto.Title;
        article.Content = dto.Content;
        await _articles.UpdateAsync(article);

        return NoContent();
    }
}
```

### Применение политик

```csharp
[Authorize(Policy = "PremiumUser")]
[HttpGet("premium-content")]
public IActionResult GetPremiumContent() => Ok("Secret content");

[Authorize(Policy = "AdminOnly")]
[Authorize(Policy = "BusinessHours")] // AND — обе политики должны пройти
[HttpDelete("{id}")]
public IActionResult Delete(int id) => Ok();
```

---

## 9. Role-based vs Claims-based vs Policy-based

| Критерий | Role-based | Claims-based | Policy-based |
|----------|-----------|-------------|-------------|
| **Гранулярность** | Грубая | Средняя | Тонкая |
| **Гибкость** | Низкая | Средняя | Высокая |
| **Атрибут** | `[Authorize(Roles = "Admin")]` | Через Policy | `[Authorize(Policy = "...")]` |
| **Логика** | Есть роль / нет роли | Есть claim с нужным значением | Произвольная логика в Handler |
| **Тестируемость** | Слабая | Средняя | Высокая (моки Handler'ов) |
| **Рекомендация** | Простые сценарии | Средние проекты | Рекомендуется для Senior |

### Рекомендация для Senior

Policy-based авторизация — **предпочтительный подход** в ASP.NET Core:
- Вся логика централизована в Handlers
- Легко тестируется unit-тестами
- Поддерживает resource-based авторизацию
- Handlers могут использовать DI (доступ к БД, внешним сервисам)
- Можно комбинировать несколько requirements в одной policy

```csharp
// Вместо этого:
[Authorize(Roles = "Admin,Manager")]
public IActionResult Delete(int id) { ... }

// Используйте это:
[Authorize(Policy = "CanDeleteArticles")]
public IActionResult Delete(int id) { ... }

// С гибким handler, который может проверять роли, claims, ресурсы, время и т.д.
```

---

## 10. Identity Server / Duende / Keycloak

### IdentityServer4 (устарел, бесплатный)

IdentityServer4 — open-source OAuth 2.0 / OIDC провайдер для ASP.NET Core.
**С ноября 2022 года не поддерживается.** Перешёл в коммерческий Duende IdentityServer.

### Duende IdentityServer (коммерческий)

- Продолжение IdentityServer4
- Полная поддержка OAuth 2.0, OIDC, FAPI
- Лицензия: бесплатно для разработки и малого бизнеса (<1M revenue), платно для enterprise
- Интеграция с ASP.NET Core Identity

```csharp
// Program.cs — настройка Duende IdentityServer
builder.Services.AddIdentityServer(options =>
{
    options.EmitStaticAudienceClaim = true;
})
    .AddInMemoryIdentityResources(Config.IdentityResources)
    .AddInMemoryApiScopes(Config.ApiScopes)
    .AddInMemoryClients(Config.Clients)
    .AddAspNetIdentity<ApplicationUser>();

// Config.cs
public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResources.Email()
    ];

    public static IEnumerable<ApiScope> ApiScopes =>
    [
        new ApiScope("api.read", "Read API"),
        new ApiScope("api.write", "Write API")
    ];

    public static IEnumerable<Client> Clients =>
    [
        // Machine-to-machine
        new Client
        {
            ClientId = "service-client",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedScopes = { "api.read" }
        },
        // SPA с PKCE
        new Client
        {
            ClientId = "spa-client",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,
            RedirectUris = { "https://localhost:3000/callback" },
            PostLogoutRedirectUris = { "https://localhost:3000" },
            AllowedCorsOrigins = { "https://localhost:3000" },
            AllowedScopes = { "openid", "profile", "api.read", "api.write" }
        }
    ];
}
```

### Keycloak

- Open-source IAM решение от Red Hat
- Поддержка OAuth 2.0, OIDC, SAML 2.0
- Админ-панель с GUI
- Поддержка федерации (LDAP, Active Directory)
- Обычно развёртывается в Docker/Kubernetes

```yaml
# docker-compose.yml
services:
  keycloak:
    image: quay.io/keycloak/keycloak:latest
    environment:
      KEYCLOAK_ADMIN: admin
      KEYCLOAK_ADMIN_PASSWORD: admin
    ports:
      - "8080:8080"
    command: start-dev
```

```csharp
// Подключение ASP.NET Core к Keycloak
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "http://localhost:8080/realms/my-realm";
        options.Audience = "my-api";
        options.RequireHttpsMetadata = false; // Только для dev!

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            RoleClaimType = "realm_access.roles"
        };
    });
```

---

## 11. ASP.NET Core Identity

ASP.NET Core Identity — встроенная система управления пользователями, ролями и аутентификацией.

### Основные компоненты

- `UserManager<TUser>` — управление пользователями (CRUD, пароли, claims)
- `SignInManager<TUser>` — вход/выход (cookie, 2FA)
- `RoleManager<TRole>` — управление ролями
- `IdentityDbContext` — контекст EF Core с таблицами Identity

### Настройка

```csharp
// Регистрация Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Политика паролей
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    // Блокировка аккаунта
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // Пользователь
    options.User.RequireUniqueEmail = true;

    // Подтверждение email
    options.SignIn.RequireConfirmedEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Расширенная модель пользователя
public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### Пример: регистрация и выдача JWT

```csharp
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtService _jwtService;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        JwtService jwtService)
    {
        _userManager = userManager;
        _jwtService = jwtService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName
        };

        var result = await _userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        await _userManager.AddToRoleAsync(user, "User");

        return Ok(new { Message = "Registration successful" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);

        if (user is null || !await _userManager.CheckPasswordAsync(user, dto.Password))
            return Unauthorized(new { Message = "Invalid credentials" });

        if (await _userManager.IsLockedOutAsync(user))
            return StatusCode(423, new { Message = "Account is locked" });

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtService.GenerateToken(user.Id, user.Email!, roles);

        return Ok(new { AccessToken = token });
    }
}
```

---

## 12. Безопасное хранение токенов

### Сравнение подходов

| Хранилище | XSS-защита | CSRF-защита | Рекомендация |
|-----------|-----------|-----------|-------------|
| **localStorage** | Уязвим (JS доступ) | Защищён (не отправляется автоматически) | Не рекомендуется для Access Token |
| **sessionStorage** | Уязвим (JS доступ) | Защищён | Не рекомендуется |
| **HttpOnly Cookie** | Защищён (нет JS доступа) | Уязвим (отправляется автоматически) | Рекомендуется + CSRF-токен |
| **In-memory (переменная)** | Защищён (нет персистентности) | Защищён | Хорошо для SPA (Access Token) |

### Рекомендуемый подход: BFF (Backend for Frontend)

```
SPA → BFF (ASP.NET Core) → API

- SPA общается с BFF через HttpOnly cookies
- BFF хранит токены в сессии / в памяти
- BFF добавляет Bearer-токен к запросам к API
- SPA никогда не видит токены
```

### Настройка HttpOnly Cookie для токена

```csharp
[HttpPost("login")]
public async Task<IActionResult> Login(LoginDto dto)
{
    // ... валидация пользователя ...

    var accessToken = _jwtService.GenerateToken(user.Id, user.Email!, roles);
    var refreshToken = await _tokenService.CreateRefreshTokenAsync(user.Id);

    // Установка Access Token в HttpOnly cookie
    Response.Cookies.Append("access_token", accessToken, new CookieOptions
    {
        HttpOnly = true,       // Недоступен из JavaScript
        Secure = true,         // Только HTTPS
        SameSite = SameSiteMode.Strict, // Защита от CSRF
        Expires = DateTimeOffset.UtcNow.AddMinutes(15),
        Path = "/api"          // Только для API-запросов
    });

    // Refresh Token в отдельной cookie
    Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Expires = DateTimeOffset.UtcNow.AddDays(7),
        Path = "/api/auth/refresh" // Только для endpoint обновления
    });

    return Ok(new { Message = "Logged in" });
}
```

### Чтение токена из cookie в middleware

```csharp
// Кастомный middleware для извлечения JWT из cookie
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Если токен не в заголовке, пробуем cookie
                if (context.Request.Cookies.TryGetValue("access_token", out var token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });
```

---

## 13. CORS

CORS (Cross-Origin Resource Sharing) — механизм, позволяющий ограничивать,
какие домены могут обращаться к вашему API из браузера.

### Настройка CORS в ASP.NET Core

```csharp
builder.Services.AddCors(options =>
{
    // Именованная политика (рекомендуется)
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins(
                "https://app.example.com",
                "https://admin.example.com")
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .WithHeaders("Authorization", "Content-Type")
            .AllowCredentials()  // Для cookies
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });

    // Политика для разработки
    options.AddPolicy("Development", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Применение
var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseCors("Development");
else
    app.UseCors("Production");
```

### CORS на уровне endpoint

```csharp
[EnableCors("Production")]
[ApiController]
[Route("api/[controller]")]
public class PublicController : ControllerBase
{
    [DisableCors] // Отключение CORS для конкретного метода
    [HttpGet("internal")]
    public IActionResult InternalEndpoint() => Ok();
}
```

### Важные нюансы CORS

- `AllowAnyOrigin()` **нельзя** использовать вместе с `AllowCredentials()`
- Preflight-запросы (`OPTIONS`) отправляются браузером автоматически для "непростых" запросов
- CORS — **не замена** серверной авторизации (CORS — ограничение браузера, не сервера)
- Инструменты вроде `curl` и Postman **игнорируют** CORS

---

## 14. CSRF / XSS / SQL Injection

### 14.1 CSRF (Cross-Site Request Forgery)

Атакующий заставляет браузер жертвы отправить запрос к сайту, где жертва авторизована.

**Защита в ASP.NET Core:**

```csharp
// Автоматическая защита для MVC (формы)
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// Для API: SameSite cookies + кастомный заголовок
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Для SPA: отправка CSRF-токена в non-HttpOnly cookie
[HttpGet("antiforgery")]
public IActionResult GetAntiforgeryToken()
{
    var tokens = _antiforgery.GetAndStoreTokens(HttpContext);

    Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
    {
        HttpOnly = false, // Доступен из JavaScript для чтения
        Secure = true,
        SameSite = SameSiteMode.Strict
    });

    return Ok();
}
```

### 14.2 XSS (Cross-Site Scripting)

Внедрение вредоносного JavaScript в страницы.

**Защита в ASP.NET Core:**

```csharp
// 1. Razor автоматически кодирует вывод
<p>@Model.UserName</p>  <!-- Безопасно: HTML-кодирование по умолчанию -->
<p>@Html.Raw(Model.Bio)</p>  <!-- ОПАСНО: нет кодирования! -->

// 2. Content Security Policy (CSP)
app.Use(async (context, next) =>
{
    context.Response.Headers.Append(
        "Content-Security-Policy",
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'");
    await next();
});

// 3. Ручная санитизация
using System.Text.Encodings.Web;

public class CommentService
{
    private readonly HtmlEncoder _htmlEncoder;

    public CommentService(HtmlEncoder htmlEncoder)
    {
        _htmlEncoder = htmlEncoder;
    }

    public string SanitizeInput(string input)
    {
        return _htmlEncoder.Encode(input);
    }
}

// 4. HttpOnly cookies — JS не может прочитать
// 5. X-XSS-Protection (устаревший, но для совместимости)
context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
```

### 14.3 SQL Injection

Внедрение SQL-кода через пользовательский ввод.

**Защита:**

```csharp
// УЯЗВИМО — никогда так не делайте!
var query = $"SELECT * FROM Users WHERE Name = '{userInput}'";
var users = await context.Database.ExecuteSqlRawAsync(query);

// БЕЗОПАСНО — параметризованный запрос
var users = await context.Users
    .Where(u => u.Name == userInput) // EF Core автоматически параметризует
    .ToListAsync();

// БЕЗОПАСНО — явная параметризация для raw SQL
var users = await context.Users
    .FromSqlInterpolated($"SELECT * FROM Users WHERE Name = {userInput}")
    .ToListAsync();

// БЕЗОПАСНО — Dapper с параметрами
var users = await connection.QueryAsync<User>(
    "SELECT * FROM Users WHERE Name = @Name",
    new { Name = userInput });

// Дополнительные меры:
// - Принцип наименьших привилегий для DB-пользователя
// - Input validation на уровне модели
// - Stored procedures
```

---

## 15. HTTPS и HSTS

### HTTPS в ASP.NET Core

```csharp
var builder = WebApplication.CreateBuilder(args);

// Настройка Kestrel с HTTPS
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000); // HTTP
    options.ListenAnyIP(5001, listenOptions =>
    {
        listenOptions.UseHttps("certificate.pfx", "password");
    });
});

var app = builder.Build();

// Перенаправление HTTP → HTTPS
app.UseHttpsRedirection();
```

### HSTS (HTTP Strict Transport Security)

Заголовок, указывающий браузеру **всегда** использовать HTTPS для данного домена.

```csharp
// Настройка HSTS
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
    options.ExcludedHosts.Add("localhost");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts(); // Только для production
}

app.UseHttpsRedirection();
```

**Заголовок ответа:**
```
Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
```

### Security Headers (рекомендуемый набор)

```csharp
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.Append("X-Content-Type-Options", "nosniff");
    headers.Append("X-Frame-Options", "DENY");
    headers.Append("X-XSS-Protection", "1; mode=block");
    headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    headers.Append("Content-Security-Policy",
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'");

    await next();
});
```

---

## 16. API Key Authentication

API Key — простой механизм аутентификации для межсервисного взаимодействия или публичных API.

### Реализация через кастомный AuthenticationHandler

```csharp
public class ApiKeyAuthenticationHandler
    : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeader))
            return AuthenticateResult.NoResult();

        var providedKey = apiKeyHeader.ToString();

        // Проверка ключа (в реальности — из БД или конфигурации)
        var apiKey = Options.ApiKeys
            .FirstOrDefault(k => k.Key == providedKey);

        if (apiKey is null)
            return AuthenticateResult.Fail("Invalid API Key");

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, apiKey.ClientName),
            new("client_id", apiKey.ClientId)
        };

        foreach (var scope in apiKey.Scopes)
        {
            claims.Add(new Claim("scope", scope));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public List<ApiKeyConfig> ApiKeys { get; set; } = [];
}

public record ApiKeyConfig(string Key, string ClientName, string ClientId, string[] Scopes);

// Регистрация
builder.Services.AddAuthentication("ApiKey")
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>("ApiKey", options =>
    {
        options.ApiKeys =
        [
            new("sk-abc123", "Mobile App", "mobile", ["api.read"]),
            new("sk-xyz789", "Partner Service", "partner", ["api.read", "api.write"])
        ];
    });
```

### Комбинирование схем аутентификации

```csharp
// JWT + API Key
builder.Services.AddAuthentication()
    .AddJwtBearer("Bearer", options => { /* ... */ })
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>("ApiKey", options => { /* ... */ });

builder.Services.AddAuthorization(options =>
{
    // Политика, принимающая любую из схем
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes("Bearer", "ApiKey")
        .RequireAuthenticatedUser()
        .Build();
});
```

---

## 17. Rate Limiting в .NET 7+

.NET 7 представил встроенный Rate Limiting middleware (`System.Threading.RateLimiting`).

### Алгоритмы

| Алгоритм | Описание | Когда использовать |
|----------|----------|--------------------|
| **Fixed Window** | N запросов за фиксированный период | Простые сценарии |
| **Sliding Window** | N запросов в скользящем окне | Сглаживание пиков |
| **Token Bucket** | Токены пополняются с фиксированной скоростью | Burst-трафик |
| **Concurrency** | Ограничение одновременных запросов | Защита ресурсоёмких операций |

### Настройка

```csharp
using System.Threading.RateLimiting;

builder.Services.AddRateLimiter(options =>
{
    // Глобальный лимитер: Fixed Window
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.User.Identity?.Name
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 10
                }));

    // Именованная политика: Token Bucket
    options.AddTokenBucketLimiter("api", limiterOptions =>
    {
        limiterOptions.TokenLimit = 50;
        limiterOptions.ReplenishmentPeriod = TimeSpan.FromSeconds(10);
        limiterOptions.TokensPerPeriod = 10;
        limiterOptions.AutoReplenishment = true;
        limiterOptions.QueueLimit = 5;
    });

    // Именованная политика: Sliding Window
    options.AddSlidingWindowLimiter("sliding", limiterOptions =>
    {
        limiterOptions.PermitLimit = 30;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.SegmentsPerWindow = 6; // 10-секундные сегменты
    });

    // Именованная политика: Concurrency
    options.AddConcurrencyLimiter("concurrent", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.QueueLimit = 5;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Обработка отклонённых запросов
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
        }

        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            Error = "Too many requests",
            RetryAfterSeconds = context.Lease.TryGetMetadata(
                MetadataName.RetryAfter, out var retry)
                    ? (int)retry.TotalSeconds
                    : 60
        }, cancellationToken);
    };
});

var app = builder.Build();
app.UseRateLimiter();
```

### Применение к endpoints

```csharp
// На контроллере
[EnableRateLimiting("api")]
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll() => Ok();

    [DisableRateLimiting] // Отключить для конкретного метода
    [HttpGet("health")]
    public IActionResult Health() => Ok("Healthy");

    [EnableRateLimiting("concurrent")] // Переопределение для тяжёлого endpoint
    [HttpGet("report")]
    public async Task<IActionResult> GenerateReport() => Ok();
}

// Minimal API
app.MapGet("/api/data", () => Results.Ok())
    .RequireRateLimiting("api");
```

---

## 18. Секреты: User Secrets, Azure Key Vault, Environment Variables

### 18.1 User Secrets (только для разработки)

```bash
# Инициализация
dotnet user-secrets init

# Добавление секрета
dotnet user-secrets set "Jwt:Secret" "my-super-secret-key-for-development"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;..."

# Список секретов
dotnet user-secrets list

# Удаление
dotnet user-secrets remove "Jwt:Secret"
```

Секреты хранятся в `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`
и автоматически подключаются в `Development` окружении.

### 18.2 Environment Variables

```csharp
// Автоматически подключены в WebApplication.CreateBuilder
// Приоритет: Environment Variables > appsettings.json

// Вложенные ключи через двойное подчёркивание (Linux) или двоеточие (Windows)
// Jwt:Secret → Jwt__Secret

// Dockerfile
// ENV Jwt__Secret=production-secret
```

```bash
# Linux/macOS
export Jwt__Secret="production-secret"
export ConnectionStrings__DefaultConnection="Server=prod-server;..."

# Docker
docker run -e Jwt__Secret="production-secret" myapp
```

### 18.3 Azure Key Vault

```csharp
// Установка пакета
// dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
// dotnet add package Azure.Identity

var builder = WebApplication.CreateBuilder(args);

// Подключение Azure Key Vault
if (!builder.Environment.IsDevelopment())
{
    var keyVaultUri = new Uri(builder.Configuration["KeyVault:Uri"]!);

    builder.Configuration.AddAzureKeyVault(
        keyVaultUri,
        new DefaultAzureCredential());
}

// Секреты из Key Vault доступны как обычная конфигурация
var jwtSecret = builder.Configuration["Jwt:Secret"]; // Из Key Vault: Jwt--Secret
```

### Иерархия приоритетов конфигурации (от низшего к высшему)

```
1. appsettings.json
2. appsettings.{Environment}.json
3. User Secrets (только Development)
4. Environment Variables
5. Command-line arguments
6. Azure Key Vault (если подключен)
```

### Безопасные практики

```csharp
// НЕ ДЕЛАЙТЕ ТАК — секрет в коде!
var key = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes("hardcoded-secret-key-123"));

// ПРАВИЛЬНО — из конфигурации
public class JwtOptions
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenExpirationMinutes { get; set; } = 15;
}

builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection("Jwt"));

// Использование через IOptions<T>
public class JwtService
{
    private readonly JwtOptions _options;

    public JwtService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }
}
```

### .gitignore — обязательно!

```gitignore
# Секреты
appsettings.Development.json
*.pfx
*.pem
.env
```

---

## 19. Вопросы на собеседовании с ответами

### Вопрос 1. В чём разница между аутентификацией и авторизацией?

**Ответ:** Аутентификация проверяет **идентичность** пользователя ("Кто ты?"),
а авторизация — его **права доступа** ("Что тебе разрешено?"). В ASP.NET Core
аутентификация устанавливает `HttpContext.User` через `UseAuthentication()`,
а авторизация проверяет claims/roles/policies через `UseAuthorization()`.
При отказе в аутентификации возвращается `401 Unauthorized`,
при отказе в авторизации — `403 Forbidden`.

---

### Вопрос 2. Объясните структуру JWT. Почему JWT не зашифрован?

**Ответ:** JWT состоит из трёх частей, разделённых точкой: **Header** (алгоритм и тип),
**Payload** (claims — данные о пользователе) и **Signature** (подпись).
Каждая часть кодируется в Base64Url.

JWT по умолчанию **подписан, но не зашифрован** — Payload можно прочитать, декодировав Base64.
Подпись гарантирует **целостность** (токен не был изменён), но не **конфиденциальность**.
Если нужна конфиденциальность — используется JWE (JSON Web Encryption).

Именно поэтому **нельзя** хранить в JWT чувствительные данные (пароли, номера карт).

---

### Вопрос 3. HS256 vs RS256 — когда что использовать?

**Ответ:** **HS256** (HMAC + SHA-256) — **симметричный** алгоритм:
один и тот же секретный ключ для подписи и верификации.
Подходит, когда издатель и потребитель — один сервис или полностью доверенные стороны.

**RS256** (RSA + SHA-256) — **асимметричный** алгоритм:
приватный ключ для подписи (только Auth Server), публичный — для верификации (все API).
Рекомендуется для микросервисной архитектуры, где множество сервисов проверяют токены,
но только один их выпускает. Публичные ключи раздаются через JWKS endpoint.

---

### Вопрос 4. Что произойдёт, если Access Token украдут? Как минимизировать ущерб?

**Ответ:** Поскольку JWT — self-contained токен, его **нельзя отозвать** на стороне сервера
без дополнительных механизмов. Меры защиты:

1. **Короткий TTL** Access Token (5-15 минут) — ограничивает время ущерба.
2. **Refresh Token Rotation** — при каждом обновлении выдаётся новый Refresh Token,
   старый инвалидируется. Повторное использование → отзыв всей "семьи" токенов.
3. **Blacklist токенов** — хранение `jti` отозванных токенов в Redis (проверка при каждом запросе).
4. **HttpOnly Secure cookies** — защита от XSS.
5. **Token Binding** — привязка токена к fingerprint клиента (IP, User-Agent).

---

### Вопрос 5. Объясните OAuth 2.0 Authorization Code Flow с PKCE.

**Ответ:** PKCE (Proof Key for Code Exchange) — расширение для публичных клиентов (SPA, мобильные),
которые не могут безопасно хранить `client_secret`.

1. Клиент генерирует случайный `code_verifier` и вычисляет `code_challenge = SHA256(code_verifier)`.
2. Клиент отправляет пользователя на Auth Server с `code_challenge`.
3. Пользователь авторизуется, Auth Server возвращает `authorization_code`.
4. Клиент обменивает `authorization_code` + `code_verifier` на токены.
5. Auth Server проверяет `SHA256(code_verifier) == code_challenge`.

Это предотвращает атаку перехвата `authorization_code`, так как без `code_verifier`
код бесполезен.

---

### Вопрос 6. Чем OpenID Connect отличается от OAuth 2.0?

**Ответ:** OAuth 2.0 — протокол **авторизации** ("у тебя есть доступ к ресурсу").
OIDC — слой **аутентификации** поверх OAuth 2.0 ("вот кто ты").

Ключевые отличия OIDC:
- Добавляет **ID Token** (JWT с информацией о пользователе) помимо Access Token.
- Обязательный scope `openid`.
- Стандартизированные endpoints: `/userinfo`, `/.well-known/openid-configuration`.
- Стандартные claims: `sub`, `name`, `email`, `picture`.

OAuth 2.0 не говорит ничего об идентичности пользователя — Access Token может быть opaque.
OIDC гарантирует, что ID Token содержит проверяемую информацию о пользователе.

---

### Вопрос 7. Как работает Policy-based авторизация в ASP.NET Core? Зачем она, если есть роли?

**Ответ:** Policy-based авторизация предоставляет **гибкий** и **расширяемый** механизм
проверки доступа. Политика состоит из одного или нескольких **Requirements**, каждый из которых
обрабатывается одним или несколькими **Handlers**.

Преимущества перед ролями:
- **Произвольная логика** — Handler может обращаться к БД, внешним сервисам, проверять время.
- **Resource-based авторизация** — Handler получает доступ к конкретному ресурсу
  (например, "автор может редактировать только свои статьи").
- **Тестируемость** — Handlers легко покрываются unit-тестами через моки.
- **DI-интеграция** — Handlers поддерживают внедрение зависимостей.
- **Композиция** — политика может комбинировать claims, роли и кастомную логику.

Роли (`[Authorize(Roles = "Admin")]`) — частный случай, который можно выразить через Policy.

---

### Вопрос 8. Где безопаснее хранить токены в SPA: localStorage или HttpOnly cookie?

**Ответ:** **HttpOnly cookie** безопаснее, поскольку JavaScript не может прочитать его содержимое,
что защищает от **XSS-атак**. localStorage доступен любому скрипту на странице.

Однако cookies автоматически отправляются с запросами, что создаёт риск **CSRF-атак**.
Для защиты используют: `SameSite=Strict`, CSRF-токены.

**Лучший подход** для SPA — паттерн **BFF (Backend for Frontend)**:
SPA общается только со своим backend через HttpOnly cookies,
а backend хранит токены и проксирует запросы к API, добавляя Bearer-токен.
Так токен **никогда** не попадает в браузер.

---

### Вопрос 9. Как защитить API от основных уязвимостей (XSS, CSRF, SQL Injection)?

**Ответ:**

**XSS:** Автоматическое HTML-кодирование в Razor, Content-Security-Policy заголовок,
HttpOnly cookies, валидация и санитизация ввода через `HtmlEncoder`.

**CSRF:** `SameSite` cookies, Anti-Forgery Tokens (`[ValidateAntiForgeryToken]`),
проверка заголовка `Origin`/`Referer`. Для API на JWT (Bearer) — CSRF не актуален,
так как токен не отправляется автоматически.

**SQL Injection:** Использование ORM (EF Core параметризует запросы автоматически),
параметризованные SQL-запросы (`FromSqlInterpolated`), параметры в Dapper,
принцип наименьших привилегий для DB-пользователя, input validation.

---

### Вопрос 10. Что такое Refresh Token Rotation и зачем это нужно?

**Ответ:** Refresh Token Rotation — паттерн, при котором **каждый раз** при обновлении
Access Token выдаётся **новый** Refresh Token, а старый **инвалидируется**.

Зачем: если злоумышленник перехватит Refresh Token и попытается его использовать
после легитимного пользователя, Auth Server обнаружит повторное использование
уже использованного токена и **отзовёт все токены "семьи"** (все Refresh Token,
выданные в рамках одной сессии). Это обеспечивает обнаружение компрометации.

---

### Вопрос 11. Объясните, как настроить Rate Limiting в .NET 7+. Какие алгоритмы доступны?

**Ответ:** .NET 7+ предоставляет встроенный middleware `UseRateLimiter()` с четырьмя алгоритмами:

1. **Fixed Window** — N запросов за фиксированный временной отрезок (например, 100 запросов в минуту).
   Проблема: burst на границе окон.
2. **Sliding Window** — N запросов в скользящем окне, разделённом на сегменты. Сглаживает пики.
3. **Token Bucket** — "ведро" токенов, пополняющееся с постоянной скоростью.
   Позволяет burst до размера ведра. Хорош для API с неравномерным трафиком.
4. **Concurrency Limiter** — ограничение одновременных запросов, без привязки ко времени.

Партиционирование по `IP`, `User`, `API Key` позволяет применять лимиты индивидуально.
`OnRejected` callback возвращает `429 Too Many Requests` с заголовком `Retry-After`.

---

### Вопрос 12. Как управлять секретами в .NET-приложении для разных окружений?

**Ответ:**

- **Development:** `User Secrets` (`dotnet user-secrets`) — файл за пределами репозитория,
  автоматически подключается в Development-окружении.
- **CI/CD:** переменные окружения (Environment Variables), секреты в GitHub Actions / Azure DevOps.
- **Production:** **Azure Key Vault** или аналоги (AWS Secrets Manager, HashiCorp Vault).
  Секреты загружаются через `AddAzureKeyVault()` и доступны как обычная `IConfiguration`.
- **Контейнеры:** Docker Secrets, Kubernetes Secrets (монтируются как файлы или env vars).

**Никогда** не хранить секреты в `appsettings.json`, исходном коде или системе контроля версий.
Файл `.gitignore` должен исключать `appsettings.Development.json`, `.env`, сертификаты.

---

### Вопрос 13. В чём разница между Authentication Scheme и Authentication Handler?

**Ответ:** **Scheme** — это именованная конфигурация аутентификации (например, `"Bearer"`,
`"Cookies"`, `"ApiKey"`). Каждая схема связана с конкретным **Handler**.

**Handler** (`AuthenticationHandler<TOptions>`) — класс, реализующий логику:
- `HandleAuthenticateAsync()` — извлечение и валидация credentials.
- `HandleChallengeAsync()` — ответ при 401 (redirect на логин или WWW-Authenticate заголовок).
- `HandleForbidAsync()` — ответ при 403.

В приложении может быть **несколько схем** одновременно (JWT + API Key + Cookies),
с указанием `DefaultScheme`, `DefaultChallengeScheme` и т.д. Атрибут `[Authorize]`
может указывать конкретную схему: `[Authorize(AuthenticationSchemes = "Bearer")]`.

---

### Вопрос 14. Что такое CORS и почему это не замена серверной авторизации?

**Ответ:** CORS (Cross-Origin Resource Sharing) — механизм, через который **браузер**
ограничивает JavaScript-запросы к другим доменам. Сервер указывает в заголовках
`Access-Control-Allow-Origin`, какие домены могут обращаться к API.

CORS — это **не защита API**, а ограничение **браузера**. Инструменты вроде `curl`,
Postman или серверный код полностью игнорируют CORS. Поэтому CORS **дополняет**,
но **не заменяет** аутентификацию и авторизацию на сервере.

Важные нюансы: `AllowAnyOrigin()` несовместим с `AllowCredentials()`;
preflight `OPTIONS`-запросы добавляют задержку; настройка должна быть строгой в production.

---

## Краткая шпаргалка

```
Аутентификация → Кто ты?
Авторизация    → Что тебе можно?

OAuth 2.0      → Протокол авторизации (делегирование доступа)
OIDC           → Аутентификация поверх OAuth 2.0 (ID Token)

JWT            → Header.Payload.Signature (подписан, но НЕ зашифрован)
HS256          → Симметричный (один ключ)
RS256          → Асимметричный (пара ключей)

Access Token   → Короткий TTL, self-contained, для API
Refresh Token  → Длинный TTL, opaque, для обновления Access Token

Policy-based   → Рекомендуемый подход (Requirements + Handlers)
Claims         → Утверждения о пользователе (тип — значение)

HttpOnly Cookie → Безопаснее localStorage (защита от XSS)
BFF Pattern     → Лучший подход для SPA

Rate Limiting   → Fixed/Sliding Window, Token Bucket, Concurrency
Секреты         → User Secrets (dev), Key Vault (prod), НИКОГДА в коде
```

---

> **Совет для собеседования:** Демонстрируйте понимание **компромиссов** (trade-offs)
> между подходами, а не просто знание API. Объясняйте **почему** выбран тот или иной подход,
> а не только **как** его реализовать.
