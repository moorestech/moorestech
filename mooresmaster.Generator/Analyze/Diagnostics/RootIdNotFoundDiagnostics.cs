using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     ルートスキーマにidが定義されていない場合のDiagnostics
/// </summary>
public class RootIdNotFoundDiagnostics : IDiagnostics
{
    public RootIdNotFoundDiagnostics(JsonObject rootJson)
    {
        RootJson = rootJson;
        Locations = new[] { rootJson.Location };
    }

    public JsonObject RootJson { get; }
    public Location[] Locations { get; }

    public string Message => "Root schema must have an 'id' field.";
}
