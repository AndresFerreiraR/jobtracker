using FluentValidation;
using JobTracker.BuildingBlocks.Application.Messaging;
using JobTracker.SharedKernel.Results;
using MediatR;

namespace JobTracker.BuildingBlocks.Application.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseRequest
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = (await Task.WhenAll(
                validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next();

        var firstFailure = failures[0];
        var error = Error.Validation(
            code: string.IsNullOrEmpty(firstFailure.ErrorCode) ? "Validation.Failed" : firstFailure.ErrorCode,
            message: firstFailure.ErrorMessage);

        return (TResponse)CreateFailure(typeof(TResponse), error);
    }

    private static object CreateFailure(Type responseType, Error error)
    {
        if (responseType == typeof(Result))
            return Result.Failure(error);

        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = responseType.GetGenericArguments()[0];
            var factory = typeof(Result<>).MakeGenericType(valueType)
                .GetMethod(nameof(Result<int>.Failure))!;
            return factory.Invoke(null, new object[] { error })!;
        }

        throw new InvalidOperationException(
            $"ValidationBehavior only supports Result / Result<T> responses. Got: {responseType}.");
    }
}
