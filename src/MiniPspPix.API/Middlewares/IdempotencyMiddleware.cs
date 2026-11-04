using System.Text;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using Infrastructure.Idempotency;
using MiniPspPix.API.Observability;

namespace MiniPspPix.API.Middlewares;

public class IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<IdempotencyMiddleware> _logger = logger;
    public const string HeaderName = "Idempotency-Key";

    public async Task InvokeAsync(HttpContext context, IIdempotencyStore idempotencyStore)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var headerValue) || string.IsNullOrWhiteSpace(headerValue))
        {
            await _next(context);
            return;
        }

        var key = headerValue.ToString().Trim();
        context.Request.EnableBuffering();

        string bodyText;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            bodyText = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        var hash = IdempotencyHasher.ComputeRequestHash(context.Request.Method, context.Request.Path, bodyText);
        var existingRecord = await idempotencyStore.GetAsync(key, context.RequestAborted);

        if (existingRecord != null)
        {
            if (existingRecord.Status == IdempotencyStatus.Completed)
            {
                if (existingRecord.RequestHash == hash)
                {
                    _logger.LogInformation("Idempotency replay for key {Key}", key);
                    PixMetrics.IdempotentReplays.Add(1);

                    context.Response.Headers["Idempotent-Replay"] = "true";
                    context.Response.ContentType = "application/json";
                    context.Response.StatusCode = existingRecord.StatusCode ?? StatusCodes.Status200OK;
                    await context.Response.WriteAsync(existingRecord.ResponseBody ?? string.Empty);
                    return;
                }

                _logger.LogWarning("Idempotency key conflict (different payload) for key {Key}", key);
                PixMetrics.IdempotentConflicts.Add(1);

                context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://datatracker.ietf.org/doc/html/rfc9444#section-2.6",
                    title = "Unprocessable Content",
                    status = 422,
                    detail = "Idempotency-Key conflict: A request with this key already completed with different parameters."
                });
                return;
            }

            _logger.LogWarning("Concurrent in-flight request detected for key {Key}", key);
            PixMetrics.IdempotentConflicts.Add(1);

            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://datatracker.ietf.org/doc/html/rfc9444#section-2.5",
                title = "Conflict",
                status = 409,
                detail = "A request with this Idempotency-Key is currently in-flight."
            });
            return;
        }

        var acquired = await idempotencyStore.TryAcquireLockAsync(key, hash, context.Request.Path, TimeSpan.FromMinutes(2), context.RequestAborted);
        if (!acquired)
        {
            PixMetrics.IdempotentConflicts.Add(1);

            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://datatracker.ietf.org/doc/html/rfc9444#section-2.5",
                title = "Conflict",
                status = 409,
                detail = "A concurrent request with this Idempotency-Key is being processed."
            });
            return;
        }

        var originalResponseBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);

            responseBody.Position = 0;
            var responseText = await new StreamReader(responseBody).ReadToEndAsync();
            responseBody.Position = 0;

            if (context.Response.StatusCode is >= 200 and < 400)
            {
                await idempotencyStore.SaveResultAsync(key, context.Response.StatusCode, responseText, context.RequestAborted);
            }

            await responseBody.CopyToAsync(originalResponseBodyStream);
        }
        finally
        {
            context.Response.Body = originalResponseBodyStream;
        }
    }
}
