using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MiniPspPix.Api.Middlewares;
using MiniPspPix.Domain.Charges;
using MiniPspPix.Domain.Exceptions;
using MiniPspPix.Infrastructure.Messaging;

namespace MiniPspPix.UnitTests;

public class MiddlewaresAndExceptionsTests
{
    [Fact]
    public async Task ExceptionMiddleware_WhenInsufficientFunds_ShouldReturn422()
    {
        var middleware = new ExceptionMiddleware(
            _ => throw new InsufficientFundsException("Saldo insuficiente"),
            NullLogger<ExceptionMiddleware>.Instance
        );

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ExceptionMiddleware_WhenConcurrency_ShouldReturn409()
    {
        var middleware = new ExceptionMiddleware(
            _ => throw new ConcurrencyException("Conflito de concorrência"),
            NullLogger<ExceptionMiddleware>.Instance
        );

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task ExceptionMiddleware_WhenPixDomainException_ShouldReturn400()
    {
        var middleware = new ExceptionMiddleware(
            _ => throw new PixDomainException("Erro de domínio"),
            NullLogger<ExceptionMiddleware>.Instance
        );

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ExceptionMiddleware_WhenGenericException_ShouldReturn500()
    {
        var middleware = new ExceptionMiddleware(
            _ => throw new InvalidOperationException("Erro fatal"),
            NullLogger<ExceptionMiddleware>.Instance
        );

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Theory]
    [InlineData("12345678909", PixKeyType.Cpf)]
    [InlineData("12345678000195", PixKeyType.Cnpj)]
    [InlineData("marcos@empresa.com", PixKeyType.Email)]
    [InlineData("+5511999998888", PixKeyType.Phone)]
    [InlineData("d18d45ca-9a3b-489e-b8c8-f7b605963b65", PixKeyType.Random)]
    public void PixKey_TypeDetection_ShouldDetectCorrectly(string value, PixKeyType expectedType)
    {
        var key = new PixKey(value);
        key.Type.Should().Be(expectedType);
        key.ToString().Should().Be(value);
    }

    [Fact]
    public void PixKey_EmptyValue_ShouldThrowDomainException()
    {
        var act = () => new PixKey("");
        act.Should().Throw<PixDomainException>();
    }

    [Fact]
    public void DomainExceptions_InnerExceptionConstructors_ShouldWork()
    {
        var inner = new Exception("Inner");
        new PixDomainException("Msg", inner).InnerException.Should().Be(inner);
        new InsufficientFundsException("Msg", inner).InnerException.Should().Be(inner);
        new ConcurrencyException("Msg", inner).InnerException.Should().Be(inner);
        new IdempotencyMismatchException("Msg", inner).InnerException.Should().Be(inner);
        new IdempotencyConflictException("Msg", inner).InnerException.Should().Be(inner);
    }

    [Fact]
    public async Task RabbitMqEventPublisher_WhenBrokerUnavailable_ShouldLogAndNotThrow()
    {
        var publisher = new RabbitMqEventPublisher("amqp://guest:guest@localhost:5679/", NullLogger<RabbitMqEventPublisher>.Instance);

        var act = () => publisher.PublishAsync("pix.test", "{}");

        await act.Should().NotThrowAsync();
        publisher.Dispose();
    }
}

