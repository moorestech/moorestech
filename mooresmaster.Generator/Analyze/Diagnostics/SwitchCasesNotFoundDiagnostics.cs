using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator.Analyze.Diagnostics;

public class SwitchCasesNotFoundDiagnostics : IDiagnostics
{
    public readonly JsonObject SwitchJson;
    public readonly SchemaId SwitchSchemaId;
    public readonly string? PropertyName;
    public readonly string SwitchPath;

    public SwitchCasesNotFoundDiagnostics(JsonObject switchJson, SchemaId switchSchemaId, string? propertyName, string switchPath)
    {
        SwitchJson = switchJson;
        SwitchSchemaId = switchSchemaId;
        PropertyName = propertyName;
        SwitchPath = switchPath;
    }

    public string Message => PropertyName != null
        ? $"Switch '{PropertyName}' (path: {SwitchPath}) is missing required 'cases' field."
        : $"Switch (path: {SwitchPath}) is missing required 'cases' field.";

    public Location[] Locations => new[] { SwitchJson.Location };
}
