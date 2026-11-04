using FluentAssertions;
using MiniPspPix.Application.Idempotency;
using MiniPspPix.Domain.Idempotency;

namespace MiniPspPix.UnitTests;

public class IdempotencyTests
{
    [Fact]
    public void ComputeFingerprint_WithDifferentKeyOrder_ShouldReturnSameHash()
    {
        var body1 = "{\"amount\": 100.50, \"pixKey\": \"test@test.com\"}";
        var body2 = "{\"pixKey\": \"test@test.com\", \"amount\": 100.50}";

        var hash1 = IdempotencyHasher.ComputeFingerprint("POST", "/v1/pix/cob", body1);
        var hash2 = IdempotencyHasher.ComputeFingerprint("POST", "/v1/pix/cob", body2);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeFingerprint_WithDifferentPayloads_ShouldReturnDifferentHash()
    {
        var body1 = "{\"amount\": 100.50}";
        var body2 = "{\"amount\": 200.00}";

        var hash1 = IdempotencyHasher.ComputeFingerprint("POST", "/v1/pix/cob", body1);
        var hash2 = IdempotencyHasher.ComputeFingerprint("POST", "/v1/pix/cob", body2);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void IdempotencyRecord_LockLifecycle_ShouldTransitionCorrectly()
    {
        var record = new IdempotencyRecord("key-123", "hash-abc", "/v1/pix/cob", TimeSpan.FromMinutes(1));

        record.Status.Should().Be(IdempotencyStatus.InProgress);
        record.HasLockExpired().Should().BeFalse();

        record.Complete(201, "{\"status\":\"created\"}");

        record.Status.Should().Be(IdempotencyStatus.Completed);
        record.StatusCode.Should().Be(201);
        record.ResponseBody.Should().Be("{\"status\":\"created\"}");
        record.LockedUntilUtc.Should().BeNull();
    }
}

