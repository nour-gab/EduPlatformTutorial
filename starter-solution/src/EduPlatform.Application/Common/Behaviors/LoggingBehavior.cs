using MediatR;
using Microsoft.Extensions.Logging;

namespace EduPlatform.Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["RequestType"] = requestName,
                   ["CorrelationId"] = Guid.NewGuid().ToString("N")[..8],
               }))
        {
            _logger.LogInformation("Handling {RequestName}", requestName);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var response = await next();
                stopwatch.Stop();
                _logger.LogInformation("Handled {RequestName} in {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed {RequestName} after {ElapsedMs}ms: {ErrorMessage}", requestName, stopwatch.ElapsedMilliseconds, ex.Message);
                throw;
            }
        }
    }
}
