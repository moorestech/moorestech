using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     スキーマにtypeが定義されていない場合のDiagnostics（refでもswitchでもない場合）
/// </summary>
public class TypeNotFoundDiagnostics : IDiagnostics
{
    public TypeNotFoundDiagnostics(JsonObject json, string? propertyName)
    {
        Json = json;
        PropertyName = propertyName;
        Locations = new[] { json.Location };
    }

    public JsonObject Json { get; }
    public string? PropertyName { get; }
    public Location[] Locations { get; }

    public string Message => PropertyName != null
        ? $"Property '{PropertyName}' must have a 'type', 'ref', or 'switch' field."
        : "Schema must have a 'type', 'ref', or 'switch' field.";
}
