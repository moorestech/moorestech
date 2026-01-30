using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator.Analyze.Diagnostics;

public class CircularRefDiagnostics : IDiagnostics
{
    public CircularRefDiagnostics(RefSchema refSchema, string[] circularPath)
    {
        RefSchema = refSchema;
        CircularPath = circularPath;
        Locations = new[] { refSchema.RefLocation };
    }
    
    public RefSchema RefSchema { get; }
    public string[] CircularPath { get; }
    public Location[] Locations { get; }
    
    public string Message
    {
        get
        {
            var propertyInfo = RefSchema.PropertyName != null ? $" (property: {RefSchema.PropertyName})" : "";
            var pathInfo = string.Join(" -> ", CircularPath);
            return $"Circular reference detected{propertyInfo}. Path: {pathInfo}";
        }
    }
}