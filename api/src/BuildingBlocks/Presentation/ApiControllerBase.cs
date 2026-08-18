using JobTracker.SharedKernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JobTracker.BuildingBlocks.Presentation;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult ToActionResult(Result result, int successStatus = StatusCodes.Status204NoContent) =>
        result.IsSuccess
            ? StatusCode(successStatus)
            : ProblemFromError(result.Error);

    protected IActionResult ToActionResult<T>(Result<T> result, int successStatus = StatusCodes.Status200OK) =>
        result.IsSuccess
            ? StatusCode(successStatus, result.Value)
            : ProblemFromError(result.Error);

    protected IActionResult Created<T>(string location, Result<T> result) =>
        result.IsSuccess
            ? Created(location, result.Value)
            : ProblemFromError(result.Error);

    private IActionResult ProblemFromError(Error error)
    {
        var (status, title) = MapStatus(error.Type);
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = $"https://jobtracker.dev/errors/{error.Code}",
            Detail = error.Message,
        };
        problem.Extensions["errorCode"] = error.Code;
        return StatusCode(status, problem);
    }

    private static (int Status, string Title) MapStatus(ErrorType type) => type switch
    {
        ErrorType.Validation   => (StatusCodes.Status400BadRequest,   "Validation failed"),
        ErrorType.NotFound     => (StatusCodes.Status404NotFound,     "Resource not found"),
        ErrorType.Conflict     => (StatusCodes.Status409Conflict,     "Conflict"),
        ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
        _                      => (StatusCodes.Status500InternalServerError, "Unexpected error"),
    };
}
