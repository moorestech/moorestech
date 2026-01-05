using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator.Analyze.Diagnostics;

public class SwitchCasesNotFoundDiagnostics : IDiagnostics
{
    public readonly string? PropertyName;
    public readonly JsonObject SwitchJson;
    public readonly string SwitchPath;
    public readonly SchemaId SwitchSchemaId;
    
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