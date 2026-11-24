namespace EduPlatform.Application.Common.Interfaces;

/// <summary>
/// Payment boundary — isolates the domain from Stripe or any other payment provider.
/// Implementation lives in EduPlatform.Infrastructure (Ch.17 §17.6).
/// </summary>
public interface IPaymentService
{
    Task<string> CreateCheckoutSessionAsync(Guid courseId, Guid studentId, CancellationToken ct);
    Task<bool>   VerifyPaymentSucceededAsync(string sessionId, CancellationToken ct);
}
