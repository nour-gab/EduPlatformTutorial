namespace EduPlatform.Web.Infrastructure
{
    // Stub — full implementation walked through in Ch.08 §8.3.
    // Adds a per-request correlation ID to HttpContext.Items and Serilog log context.
    public class CorrelationIdMiddleware
    {
        public const string HeaderName = "X-Correlation-Id";
        private const string ItemsKey = "CorrelationId";
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

        public async Task Invoke(HttpContext context)
        {
            var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                                ?? Guid.NewGuid().ToString("N")[..16];

            context.Items[ItemsKey] = correlationId;
            context.Response.Headers[HeaderName] = correlationId;

            using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
            {
                await _next(context);
            }
        }
    }
}
