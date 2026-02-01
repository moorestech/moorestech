using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator.Analyze.Diagnostics;

public class RefNotFoundDiagnostics : IDiagnostics
{
    public RefNotFoundDiagnostics(RefSchema refSchema, string[] availableSchemaIds)
    {
        RefSchema = refSchema;
        AvailableSchemaIds = availableSchemaIds;
        Locations = new[] { refSchema.RefLocation };
    }
    
    public RefSchema RefSchema { get; }
    public string[] AvailableSchemaIds { get; }
    public Location[] Locations { get; }
    
    public string Message
    {
        get
        {
            var propertyInfo = RefSchema.PropertyName != null ? $" (property: {RefSchema.PropertyName})" : "";
            var availableInfo = AvailableSchemaIds.Length > 0
                ? $" Available schemas: [{string.Join(", ", AvailableSchemaIds)}]"
                : " No schemas available.";
            return $"Referenced schema '{RefSchema.Ref}' not found{propertyInfo}.{availableInfo}";
        }
    }
}