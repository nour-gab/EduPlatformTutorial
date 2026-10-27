using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace EduPlatform.Web.Infrastructure
{
    // Stub — full implementation walked through in Ch.08.
    // Converts thrown exceptions to RFC 7807 ProblemDetails JSON responses.
    public class GlobalExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<GlobalExceptionFilter> _logger;
        public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger) => _logger = logger;

        public void OnException(ExceptionContext context)
        {
            _logger.LogError(context.Exception,
                "Unhandled exception: {ExceptionType} — {Message}",
                context.Exception.GetType().Name, context.Exception.Message);

            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = 500,
                Title = "Unhandled error",
                Detail = "An unexpected error occurred. Please try again later.",
                Instance = context.HttpContext.Request.Path,
            }) { StatusCode = 500 };
            context.ExceptionHandled = true;
        }
    }
}
