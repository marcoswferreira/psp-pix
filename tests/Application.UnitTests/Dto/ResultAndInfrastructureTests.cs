using Application.Dto;
using FluentAssertions;
using Infrastructure.Idempotency;
using Infrastructure.Messaging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests.Dto;

public class ResultAndInfrastructureTests
{
    [Fact]
    public void Result_SuccessAndFailure_ShouldSetProperties()
    {
        var ok = Result.Success();
        ok.IsSuccess.Should().BeTrue();
        ok.Errors.Should().BeEmpty();

        var failSingle = Result.Failure("Erro individual");
        failSingle.IsSuccess.Should().BeFalse();
        failSingle.Errors.Should().ContainSingle().Which.Should().Be("Erro individual");

        var failList = Result.Failure(["Erro 1", "Erro 2"]);
        failList.IsSuccess.Should().BeFalse();
        failList.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void ResultGeneric_SuccessAndFailure_ShouldSetProperties()
    {
        var ok = ResultGeneric<string>.Success("Valor OK");
        ok.IsSuccess.Should().BeTrue();
        ok.Value.Should().Be("Valor OK");
        ok.Errors.Should().BeEmpty();

        var failSingle = ResultGeneric<string>.Failure("Erro Genérico");
        failSingle.IsSuccess.Should().BeFalse();
        failSingle.Value.Should().BeNull();
        failSingle.Errors.Should().ContainSingle().Which.Should().Be("Erro Genérico");

        var failList = ResultGeneric<string>.Failure(["Erro A", "Erro B"]);
        failList.IsSuccess.Should().BeFalse();
        failList.Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task InMemoryEventPublisher_ShouldStoreEvents()
    {
        var publisher = new InMemoryEventPublisher(NullLogger<InMemoryEventPublisher>.Instance);
        await publisher.PublishAsync("test.event", "{\"id\":1}");

        publisher.PublishedEvents.Should().HaveCount(1);
        var first = publisher.PublishedEvents.First();
        first.EventType.Should().Be("test.event");
        first.Payload.Should().Be("{\"id\":1}");
    }

    [Fact]
    public void IdempotencyHasher_EdgeCases_ShouldHandleGracefully()
    {
        var hashNull = IdempotencyHasher.ComputeRequestHash("GET", "/test", null);
        hashNull.Should().NotBeNullOrWhiteSpace();

        var hashEmpty = IdempotencyHasher.ComputeRequestHash("GET", "/test", "");
        hashEmpty.Should().Be(hashNull);

        var hashNonJson = IdempotencyHasher.ComputeRequestHash("POST", "/test", "plain text body");
        hashNonJson.Should().NotBeNullOrWhiteSpace();

        var hashArray = IdempotencyHasher.ComputeRequestHash("POST", "/test", "[{\"b\": 2, \"a\": 1}]");
        var hashArrayReordered = IdempotencyHasher.ComputeRequestHash("POST", "/test", "[{\"a\": 1, \"b\": 2}]");
        hashArray.Should().Be(hashArrayReordered);
    }
}

