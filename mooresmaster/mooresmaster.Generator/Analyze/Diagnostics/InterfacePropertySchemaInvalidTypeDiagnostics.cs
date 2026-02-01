using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     defineInterfaceのpropertyのschemaがIDefineInterfacePropertySchemaでない場合のDiagnostics
/// </summary>
public class InterfacePropertySchemaInvalidTypeDiagnostics : IDiagnostics
{
    public InterfacePropertySchemaInvalidTypeDiagnostics(string interfaceName, string propertyKey, ISchema actualSchema, JsonObject propertyNode, bool isGlobal)
    {
        InterfaceName = interfaceName;
        PropertyKey = propertyKey;
        ActualSchema = actualSchema;
        PropertyNode = propertyNode;
        IsGlobal = isGlobal;
        Locations = new[] { propertyNode.Location };
    }

    public string InterfaceName { get; }
    public string PropertyKey { get; }
    public ISchema ActualSchema { get; }
    public JsonObject PropertyNode { get; }
    public bool IsGlobal { get; }
    public Location[] Locations { get; }

    public string Message => $"Property '{PropertyKey}' in interface '{InterfaceName}' has invalid type '{ActualSchema.GetType().Name}'. Interface properties must be primitive types (string, integer, number, boolean, uuid, enum).";
}
