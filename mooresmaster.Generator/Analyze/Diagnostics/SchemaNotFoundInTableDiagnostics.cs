using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     SchemaTableにスキーマIDが見つからない場合のDiagnostics
/// </summary>
public class SchemaNotFoundInTableDiagnostics : IDiagnostics
{
    public SchemaNotFoundInTableDiagnostics(SchemaId schemaId, string? propertyName, string context, ISchema? parentSchema)
    {
        SchemaId = schemaId;
        PropertyName = propertyName;
        Context = context;
        ParentSchema = parentSchema;
        Locations = parentSchema != null ? new[] { parentSchema.Json.Location } : new Location[0];
    }

    public SchemaId SchemaId { get; }
    public string? PropertyName { get; }
    public string Context { get; }
    public ISchema? ParentSchema { get; }
    public Location[] Locations { get; }

    public string Message => PropertyName != null
        ? $"Schema ID '{SchemaId}' for property '{PropertyName}' not found in table during {Context}."
        : $"Schema ID '{SchemaId}' not found in table during {Context}.";
}
