using System.Text;
using System.Text.Json;
using MiniPspPix.Api.Observability;
using MiniPspPix.Application.Idempotency;
using MiniPspPix.Domain.Exceptions;
using MiniPspPix.Domain.Idempotency;

namespace MiniPspPix.Api.Middlewares;

public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private const string HeaderKey = "Idempotency-Key";

    public IdempotencyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IIdempotencyStore store)
    {
        if (!context.Request.Headers.TryGetValue(HeaderKey, out var headerValues) ||
            string.IsNullOrWhiteSpace(headerValues.FirstOrDefault()))
        {
            await _next(context);
            return;
        }

        var idempotencyKey = headerValues.First()!.Trim();

        // Permite leitura do body múltiplas vezes
        context.Request.EnableBuffering();
        string requestBody;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            requestBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        var requestHash = IdempotencyHasher.ComputeFingerprint(
            context.Request.Method,
            context.Request.Path,
            requestBody
        );

        var existingRecord = await store.GetAsync(idempotencyKey);

        if (existingRecord != null)
        {
            if (existingRecord.Status == IdempotencyStatus.Completed)
            {
                if (existingRecord.RequestHash != requestHash)
                {
                    context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        title = "Idempotency Conflict",
                        status = 422,
                        detail = "A Idempotency-Key já foi utilizada anteriormente com um payload diferente."
                    });
                    return;
                }

                // Replay de resposta idempotente
                PixMetrics.IdempotencyHitsCounter.Add(1);
                context.Response.StatusCode = existingRecord.StatusCode ?? StatusCodes.Status200OK;
                context.Response.Headers["Idempotent-Replay"] = "true";
                context.Response.ContentType = "application/json";
                if (!string.IsNullOrEmpty(existingRecord.ResponseBody))
                {
                    await context.Response.WriteAsync(existingRecord.ResponseBody);
                }
                return;
            }

            if (existingRecord.Status == IdempotencyStatus.InProgress && !existingRecord.HasLockExpired())
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    title = "Concurrent Request In Progress",
                    status = 409,
                    detail = "Uma requisição com a mesma Idempotency-Key está sendo processada no momento. Tente novamente em breve."
                });
                return;
            }
        }

        var acquired = await store.TryAcquireLockAsync(idempotencyKey, requestHash, context.Request.Path, TimeSpan.FromSeconds(30));
        if (!acquired)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Concurrent Lock Failure",
                status = 409,
                detail = "Não foi possível adquirir lock para a Idempotency-Key fornecida."
            });
            return;
        }

        // Intercepta a resposta para gravar o resultado no store
        var originalBodyStream = context.Response.Body;
        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        try
        {
            await _next(context);

            memoryStream.Position = 0;
            var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();
            memoryStream.Position = 0;

            if (context.Response.StatusCode is >= 200 and < 300)
            {
                await store.SaveResultAsync(idempotencyKey, context.Response.StatusCode, responseBody);
            }

            await memoryStream.CopyToAsync(originalBodyStream);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }
}

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (InsufficientFundsException ex)
        {
            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Saldo Insuficiente",
                status = 422,
                detail = ex.Message
            });
        }
        catch (ConcurrencyException ex)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Conflito de Concorrência",
                status = 409,
                detail = ex.Message
            });
        }
        catch (PixDomainException ex)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Erro de Domínio Pix",
                status = 400,
                detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro interno não tratado.");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Erro Interno no PSP",
                status = 500,
                detail = ex.Message
            });
        }
    }
}

