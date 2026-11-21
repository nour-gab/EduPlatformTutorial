using EduPlatform.Application.Common.Interfaces;
using MediatR;

namespace EduPlatform.Application.Common.Behaviors;

public class TransactionBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork _uow;

    public TransactionBehavior(IUnitOfWork uow) => _uow = uow;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (typeof(TRequest).Name.EndsWith("Query")) return await next();

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var response = await next();
            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
            return response;
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }
}
