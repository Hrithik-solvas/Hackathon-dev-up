using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace CodeCompassProject.CodeCompass.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
            _logger.LogError(ex, "An unhandled exception occurred while processing {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        var (statusCode, title, detail) = exception switch
        {
            ArgumentException argEx =>
                (HttpStatusCode.BadRequest, "Bad Request", argEx.Message),

            DirectoryNotFoundException dirEx =>
                (HttpStatusCode.NotFound, "Not Found",
                    $"The specified directory was not found: {dirEx.Message}"),

            HttpRequestException =>
                (HttpStatusCode.ServiceUnavailable, "Service Unavailable",
                    "An external service is temporarily unavailable. Please try again later."),

            TaskCanceledException =>
                (HttpStatusCode.ServiceUnavailable, "Service Unavailable",
                    "The request timed out while waiting for an external service. Please try again later."),

            InvalidOperationException invalidOpEx =>
                (HttpStatusCode.BadRequest, "Invalid operation", invalidOpEx.Message),

            UnauthorizedAccessException =>
                (HttpStatusCode.Unauthorized, "Unauthorized", "Access denied."),

            FileNotFoundException fileEx =>
                (HttpStatusCode.NotFound, "Resource not found", fileEx.Message),

            _ =>
                (HttpStatusCode.InternalServerError, "Internal Server Error",
                    "An unexpected error occurred.")
        };

        context.Response.StatusCode = (int)statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(problemDetails, options);
        await context.Response.WriteAsync(json);
    }
}
