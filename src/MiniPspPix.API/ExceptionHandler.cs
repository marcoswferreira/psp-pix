using Domain.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MiniPspPix.API.Observability;

namespace MiniPspPix.API;

public class ExceptionHandler(ILogger<ExceptionHandler> logger) : IExceptionHandler
{
    private readonly ILogger<ExceptionHandler> _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);

        int statusCode;
        string title;
        string detail = exception.Message;

        switch (exception)
        {
            case InsufficientFundsException:
                statusCode = StatusCodes.Status422UnprocessableEntity;
                title = "Saldo Insuficiente";
                PixMetrics.DoubleSpendingAttempts.Add(1);
                break;

            case ConcurrencyException:
                statusCode = StatusCodes.Status409Conflict;
                title = "Conflito de Concorrência";
                PixMetrics.DoubleSpendingAttempts.Add(1);
                break;

            case DomainException:
                statusCode = StatusCodes.Status422UnprocessableEntity;
                title = "Violação de Regra de Negócio";
                break;

            case KeyNotFoundException:
                statusCode = StatusCodes.Status404NotFound;
                title = "Recurso Não Encontrado";
                break;

            default:
                statusCode = StatusCodes.Status500InternalServerError;
                title = "Erro Interno do Servidor";
                detail = "Ocorreu um erro interno inesperado.";
                break;
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}

