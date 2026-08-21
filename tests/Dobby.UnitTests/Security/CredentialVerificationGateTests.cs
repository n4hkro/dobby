using Dobby.Security.Authentication;
using Dobby.Configuration;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using NSubstitute;
using Shouldly;

namespace Dobby.UnitTests.Security;

public sealed class CredentialVerificationGateTests
{
    [Fact]
    public void Gate_rejects_excess_work_without_queueing_and_releases_capacity_idempotently()
    {
        var gate = new CredentialVerificationGate(maximumConcurrency: 1);
        var first = gate.TryAcquire();

        first.ShouldNotBeNull();
        gate.TryAcquire().ShouldBeNull();

        first.Dispose();
        first.Dispose();
        gate.TryAcquire().ShouldNotBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Gate_rejects_unbounded_configuration(int maximumConcurrency)
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new CredentialVerificationGate(maximumConcurrency));
    }

    [Fact]
    public void Credential_store_returns_busy_before_persistence_or_pbkdf_when_capacity_is_exhausted()
    {
        var executor = Substitute.For<IHostPersistenceCommandExecutor>();
        var protector = Substitute.For<IProtectedDataProtector>();
        var gate = new CredentialVerificationGate(maximumConcurrency: 1);
        using var held = gate.TryAcquire();
        var store = new CredentialStore(
            executor,
            protector,
            new KdfPolicyResolver(new AuthOptions()),
            verificationGate: gate);

        var result = store.VerifyPassword("user-1", "password");

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.ResourceBusy);
        executor.DidNotReceiveWithAnyArgs().Query<object>(default!);
        protector.DidNotReceiveWithAnyArgs().Unprotect(default!, default!);
    }
}
