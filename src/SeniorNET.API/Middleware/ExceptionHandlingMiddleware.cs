using System.Text.Json;
using FluentValidation;
using SeniorNET.Domain.Exceptions;

namespace SeniorNET.API.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, response) = exception switch
        {
            ValidationException ex => (StatusCodes.Status400BadRequest, new ErrorResponse(
                "Validation Failed",
                ex.Errors.Select(e => e.ErrorMessage).ToArray())),

            DomainException ex => (StatusCodes.Status422UnprocessableEntity, new ErrorResponse(
                "Domain Error",
                [ex.Message])),

            KeyNotFoundException ex => (StatusCodes.Status404NotFound, new ErrorResponse(
                "Not Found",
                [ex.Message])),

            _ => (StatusCodes.Status500InternalServerError, new ErrorResponse(
                "Internal Server Error",
                ["An unexpected error occurred."]))
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception");

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}

public sealed record ErrorResponse(string Title, string[] Errors);
