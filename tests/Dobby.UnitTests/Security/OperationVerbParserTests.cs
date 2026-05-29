using Dobby.Core;
using Dobby.Security.Authorization;
using Shouldly;

namespace Dobby.UnitTests.Security;

public sealed class OperationVerbParserTests
{
    public static TheoryData<string, OperationVerb> CanonicalVerbs => new()
    {
        { "view", OperationVerb.View },
        { "create", OperationVerb.Create },
        { "update", OperationVerb.Update },
        { "delete", OperationVerb.Delete },
        { "execute", OperationVerb.Execute },
        { "import", OperationVerb.Import },
        { "export", OperationVerb.Export },
        { "print", OperationVerb.Print },
        { "configure", OperationVerb.Configure },
        { "grant", OperationVerb.Grant }
    };

    [Theory]
    [MemberData(nameof(CanonicalVerbs))]
    public void Parse_accepts_each_canonical_sprint6_verb(string wireValue, OperationVerb expected)
    {
        OperationVerbParser.TryParse(wireValue, out var verb).ShouldBeTrue();

        verb.ShouldBe(expected);
        OperationVerbParser.ToWireValue(verb).ShouldBe(wireValue);
    }

    [Fact]
    public void Operation_key_splits_target_on_last_dot()
    {
        var result = OperationKey.Parse("com.dobby.sample.greet.execute");

        result.IsSuccess.ShouldBeTrue();
        result.Value.TargetId.ShouldBe("com.dobby.sample.greet");
        result.Value.Verb.ShouldBe(OperationVerb.Execute);
        result.Value.ToString().ShouldBe("com.dobby.sample.greet.execute");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("users.management")]
    [InlineData(".view")]
    [InlineData("users.management.")]
    [InlineData("users.management.unknown")]
    public void Parse_rejects_unknown_or_malformed_operation_keys(string value)
    {
        var result = OperationKey.Parse(value);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.InvalidInput);
    }
}
