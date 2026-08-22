using Dobby.Diagnostics;
using Shouldly;
using System.Collections.Immutable;
using System.Text.Json;

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

    [Fact]
    public void Redacts_nested_structured_json_by_field_semantics()
    {
        var input = """
            {"safe":"şəffaf","nested":{"apiToken":"abc","items":[{"private_key":"pem"}]}}
            """;

        var redacted = DiagnosticRedactor.Redact(input);

        using var document = JsonDocument.Parse(redacted);
        document.RootElement.GetProperty("safe").GetString().ShouldBe("şəffaf");
        document.RootElement.GetProperty("nested").GetProperty("apiToken").GetString().ShouldBe("<redacted>");
        document.RootElement.GetProperty("nested").GetProperty("items")[0].GetProperty("private_key").GetString().ShouldBe("<redacted>");
        redacted.ShouldNotContain("abc");
        redacted.ShouldNotContain("pem");
    }

    [Fact]
    public void Redacts_paths_private_material_connection_sql_stack_and_unicode_separators()
    {
        var input = string.Join('\n',
            "connectionString=Data Source=C:\\private\\dobby.db;Password=secret",
            "-----BEGIN PRIVATE KEY-----raw-key-----END PRIVATE KEY-----",
            "C:\\Users\\Owner\\secret.txt and \\\\server\\share\\secret.txt",
            "SELECT PasswordHash FROM Users WHERE UserId='owner';",
            "   at Product.Type.Method() in C:\\src\\Secret.cs:line 42",
            "token=abc\u2028safe=value");

        var redacted = DiagnosticRedactor.Redact(input);

        redacted.ShouldNotContain("secret");
        redacted.ShouldNotContain("raw-key");
        redacted.ShouldNotContain("Owner");
        redacted.ShouldNotContain("server");
        redacted.ShouldNotContain("PasswordHash");
        redacted.ShouldNotContain("Product.Type.Method");
        redacted.ShouldNotContain("abc");
        redacted.ShouldContain("<path>");
        redacted.ShouldContain("safe=value");
    }

    [Fact]
    public void Oversized_diagnostic_text_is_dropped_instead_of_partially_copied()
    {
        DiagnosticRedactor.Redact(new string('x', 16 * 1024 + 1))
            .ShouldBe("<redacted:oversized>");
    }
}
