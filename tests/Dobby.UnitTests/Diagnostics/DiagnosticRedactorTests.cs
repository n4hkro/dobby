using Dobby.Diagnostics;
using Shouldly;
using System.Collections.Immutable;

namespace Dobby.UnitTests.Diagnostics;

public sealed class DiagnosticRedactorTests
{
    [Theory]
    [InlineData("password=secret")]
    [InlineData("passwordHash=hash")]
    [InlineData("token=abc")]
    [InlineData("privateKey=key")]
    [InlineData("sessionMaterial=session")]
    [InlineData("rememberMeSecret=remember")]
    [InlineData("entropy=random")]
    [InlineData("recovery=recovery")]
    [InlineData("recoverySecret=recovery")]
    [InlineData("recoveryNonce=nonce")]
    [InlineData("recoveryResponse=response")]
    [InlineData("licensePayload=payload")]
    [InlineData("licenseArtifact=artifact")]
    public void Redacts_full_write_time_sensitive_vocabulary(string assignment)
    {
        var redacted = DiagnosticRedactor.Redact($"{assignment}; safe=şəffaf");
        var secretValue = assignment.Split('=')[1];

        redacted.ShouldContain("<redacted>");
        redacted.ShouldNotContain($"={secretValue}");
        redacted.ShouldContain("safe=şəffaf");
    }

    [Fact]
    public void Redact_fields_returns_new_immutable_dictionary_without_mutating_input()
    {
        var fields = new Dictionary<string, string>
        {
            ["password"] = "secret",
            ["message"] = "token=abc",
            ["safe"] = "şəffaf"
        };

        var redacted = DiagnosticRedactor.RedactFields(fields);

        redacted.ShouldBeOfType<ImmutableDictionary<string, string>>();
        redacted.ShouldNotBeSameAs(fields);
        redacted["password"].ShouldBe("<redacted>");
        redacted["message"].ShouldBe("token=<redacted>");
        redacted["safe"].ShouldBe("şəffaf");
        fields["password"].ShouldBe("secret");
        fields["message"].ShouldBe("token=abc");
    }
}
