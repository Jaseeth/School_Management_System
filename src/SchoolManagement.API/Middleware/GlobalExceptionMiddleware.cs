using System.Net;
using System.Text.Json;

namespace SchoolManagement.API.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(
        HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception occurred. TraceId: {TraceId}",
                context.TraceIdentifier);

            await HandleExceptionAsync(
                context,
                ex);
        }
    }

    private async Task HandleExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        context.Response.ContentType =
            "application/json";

        context.Response.StatusCode =
            (int)HttpStatusCode.InternalServerError;

        var response =
            _environment.IsDevelopment()
                ? new
                {
                    statusCode =
                        context.Response.StatusCode,

                    message =
                        "An unexpected error occurred.",

                    detail =
                        exception.Message,

                    traceId =
                        context.TraceIdentifier
                }
                : new
                {
                    statusCode =
                        context.Response.StatusCode,

                    message =
                        "An unexpected error occurred. Please contact the system administrator if the problem continues.",

                    detail =
                        (string?)null,

                    traceId =
                        context.TraceIdentifier
                };

        var json =
            JsonSerializer.Serialize(
                response);

        await context.Response
            .WriteAsync(json);
    }
}