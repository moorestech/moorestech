using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     SemanticsGeneratorで未知のスキーマタイプが渡された場合のDiagnostics
/// </summary>
public class UnknownSchemaTypeDiagnostics : IDiagnostics
{
    public UnknownSchemaTypeDiagnostics(ISchema schema, string actualTypeName)
    {
        Schema = schema;
        ActualTypeName = actualTypeName;
        Locations = new Location[0];
    }

    public ISchema Schema { get; }
    public string ActualTypeName { get; }
    public Location[] Locations { get; }

    public string Message => Schema.PropertyName != null
        ? $"Property '{Schema.PropertyName}' has unknown schema type '{ActualTypeName}' in semantic analysis."
        : $"Schema has unknown schema type '{ActualTypeName}' in semantic analysis.";
}
