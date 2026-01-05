using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator.Analyze.Diagnostics;

public class ArrayItemsNotFoundDiagnostics : IDiagnostics
{
    public readonly JsonObject ArrayJson;
    public readonly SchemaId ArraySchemaId;
    public readonly string? PropertyName;
    
    public ArrayItemsNotFoundDiagnostics(JsonObject arrayJson, SchemaId arraySchemaId, string? propertyName)
    {
        ArrayJson = arrayJson;
        ArraySchemaId = arraySchemaId;
        PropertyName = propertyName;
    }
    
    public string Message => PropertyName != null
        ? $"Array '{PropertyName}' is missing required 'items' field."
        : "Array is missing required 'items' field.";
    
    public Location[] Locations => new[] { ArrayJson.Location };
}