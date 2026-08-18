using System.Text;
using JobTracker.BuildingBlocks.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobTracker.Api.Infrastructure.Idempotency;

public sealed class IdempotencyMiddleware(
    RequestDelegate next,
    IOptions<IdempotencyOptions> options,
    ILogger<IdempotencyMiddleware> logger)
{
    private readonly IdempotencyOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context, IIdempotencyStore store, ITenantContext tenant)
    {
        if (!_options.Enabled || !HttpMethods.IsPost(context.Request.Method))
        {
            await next(context);
            return;
        }

        var headerValues = context.Request.Headers[_options.HeaderName];
        if (headerValues.Count == 0) { await next(context); return; }

        var key = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(key) || key.Length > _options.MaxKeyLength)
        {
            await WriteProblem(context, StatusCodes.Status400BadRequest,
                "Invalid Idempotency-Key",
                $"Header '{_options.HeaderName}' must be non-empty and at most {_options.MaxKeyLength} characters.");
            return;
        }

        if (!tenant.IsPresent)
        {
            await next(context);
            return;
        }

        var orgId = tenant.OrganizationId;
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";

        try
        {
            var replay = await store.TryGetAsync(orgId, key, method, path, context.RequestAborted);
            if (replay is not null)
            {
                context.Response.StatusCode = replay.StatusCode;
                if (!string.IsNullOrEmpty(replay.Location))
                    context.Response.Headers.Location = replay.Location;
                if (!string.IsNullOrEmpty(replay.ResponseBody))
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(replay.ResponseBody, context.RequestAborted);
                }
                logger.LogInformation("Idempotency replay for key {Key}, org {OrgId}: {Status}",
                    key, orgId, replay.StatusCode);
                return;
            }
        }
        catch (InvalidOperationException conflict)
        {
            await WriteProblem(context, StatusCodes.Status409Conflict,
                "Idempotency conflict", conflict.Message);
            return;
        }

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);
        }
        finally
        {
            buffer.Position = 0;
            var body = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync();
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody, context.RequestAborted);
            context.Response.Body = originalBody;

            if (context.Response.StatusCode is >= 200 and < 300)
            {
                var location = context.Response.Headers.Location.ToString();
                await store.SaveAsync(
                    orgId, key, method, path,
                    context.Response.StatusCode,
                    string.IsNullOrEmpty(body) ? null : body,
                    string.IsNullOrEmpty(location) ? null : location,
                    TimeSpan.FromHours(_options.TtlHours),
                    CancellationToken.None);
            }
        }
    }

    private static async Task WriteProblem(HttpContext context, int status, string title, string detail)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
