using EduPlatform.Application.Common.Exceptions;
using EduPlatform.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EduPlatform.Web.Infrastructure;

public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
        => _logger = logger;

    public void OnException(ExceptionContext context)
    {
        var (status, title, detail) = context.Exception switch
        {
            DomainException dex      => (400, "Domain rule violated", dex.Message),
            ValidationException vex  => (400, "Validation failed",
                string.Join(" | ", vex.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"))),
            NotFoundException nf     => (404, "Resource not found", nf.Message),
            UnauthorizedAccessException => (401, "Unauthorized", "Authentication required."),
            _                        => (500, "Unhandled error", "An unexpected error occurred. Please try again later."),
        };

        if (status >= 500)
        {
            _logger.LogError(context.Exception,
                "Unhandled exception: {ExceptionType} — {Message}",
                context.Exception.GetType().Name, context.Exception.Message);
        }
        else
        {
            _logger.LogWarning("Handled exception: {ExceptionType} — {Message}",
                context.Exception.GetType().Name, context.Exception.Message);
        }

        var correlationId = context.HttpContext.Items[CorrelationIdMiddleware.HeaderName]?.ToString() ?? "?";

        var problem = new ProblemDetails
        {
            Status   = status,
            Title    = title,
            Detail   = detail,
            Instance = context.HttpContext.Request.Path,
            Type     = $"https://httpstatuses.io/{status}",
        };
        problem.Extensions["correlationId"] = correlationId;

        context.Result = new ObjectResult(problem) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
