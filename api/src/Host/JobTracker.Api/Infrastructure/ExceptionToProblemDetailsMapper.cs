using JobTracker.Api.Infrastructure.Tenant;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JobTracker.Api.Infrastructure;

internal sealed class ExceptionToProblemDetailsMapper(ILogger<ExceptionToProblemDetailsMapper> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails problem = exception switch
        {
            MissingTenantException mte => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Missing tenant identity",
                Type = "https://jobtracker.dev/errors/missing-tenant",
                Detail = mte.Message,
            },
            _ => null!
        };

        if (problem is null)
        {
            logger.LogError(exception, "Unhandled exception at {Path}", httpContext.Request.Path);
            problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Unexpected error",
                Type = "https://jobtracker.dev/errors/unexpected",
                Detail = "An unexpected error occurred while processing the request.",
            };
        }
        else
        {
            logger.LogWarning("Client error at {Path}: {Title}", httpContext.Request.Path, problem.Title);
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
