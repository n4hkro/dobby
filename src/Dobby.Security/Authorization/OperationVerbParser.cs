namespace Dobby.Security.Authorization;

public static class OperationVerbParser
{
    private static readonly Dictionary<string, OperationVerb> VerbByWireValue = new(StringComparer.Ordinal)
    {
        ["view"] = OperationVerb.View,
        ["create"] = OperationVerb.Create,
        ["update"] = OperationVerb.Update,
        ["delete"] = OperationVerb.Delete,
        ["execute"] = OperationVerb.Execute,
        ["import"] = OperationVerb.Import,
        ["export"] = OperationVerb.Export,
        ["print"] = OperationVerb.Print,
        ["configure"] = OperationVerb.Configure,
        ["grant"] = OperationVerb.Grant
    };

    public static bool TryParse(string value, out OperationVerb verb)
    {
        return VerbByWireValue.TryGetValue(value, out verb);
    }

    public static string ToWireValue(OperationVerb verb)
    {
        return verb switch
        {
            OperationVerb.View => "view",
            OperationVerb.Create => "create",
            OperationVerb.Update => "update",
            OperationVerb.Delete => "delete",
            OperationVerb.Execute => "execute",
            OperationVerb.Import => "import",
            OperationVerb.Export => "export",
            OperationVerb.Print => "print",
            OperationVerb.Configure => "configure",
            OperationVerb.Grant => "grant",
            _ => throw new ArgumentOutOfRangeException(nameof(verb), verb, "Unknown operation verb.")
        };
    }
}
