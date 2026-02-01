using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.Analyze.Diagnostics;

public class InvalidOptionalValueDiagnostics : IDiagnostics
{
    public InvalidOptionalValueDiagnostics(JsonObject parentJson, IJsonNode optionalNode, string? propertyName)
    {
        ParentJson = parentJson;
        OptionalNode = optionalNode;
        PropertyName = propertyName;
        Locations = new[] { optionalNode.Location };
    }
    
    public JsonObject ParentJson { get; }
    public IJsonNode OptionalNode { get; }
    public string? PropertyName { get; }
    public Location[] Locations { get; }
    
    public string Message
    {
        get
        {
            var actualValue = OptionalNode switch
            {
                JsonString str => $"\"{str.Literal}\"",
                JsonNumber num => num.Literal.ToString(),
                JsonInt intNum => intNum.Literal.ToString(),
                JsonBoolean b => b.Literal.ToString().ToLower(),
                JsonArray => "array",
                JsonObject => "object",
                _ => "unknown"
            };
            
            var propertyInfo = PropertyName != null ? $" (property: {PropertyName})" : "";
            return $"Invalid 'optional' value{propertyInfo}. Expected 'true' or 'false', but got {actualValue}.";
        }
    }
}