using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     defineInterfaceのpropertyのkeyがJsonStringでない場合のDiagnostics
/// </summary>
public class InterfacePropertyKeyNotStringDiagnostics : IDiagnostics
{
    public InterfacePropertyKeyNotStringDiagnostics(string interfaceName, JsonObject propertyNode, IJsonNode? actualKeyNode, int propertyIndex, bool isGlobal)
    {
        InterfaceName = interfaceName;
        PropertyNode = propertyNode;
        ActualKeyNode = actualKeyNode;
        PropertyIndex = propertyIndex;
        IsGlobal = isGlobal;
        Locations = actualKeyNode != null ? new[] { actualKeyNode.Location } : new[] { propertyNode.Location };
    }

    public string InterfaceName { get; }
    public JsonObject PropertyNode { get; }
    public IJsonNode? ActualKeyNode { get; }
    public int PropertyIndex { get; }
    public bool IsGlobal { get; }
    public Location[] Locations { get; }

    public string Message => ActualKeyNode == null
        ? $"Property at index {PropertyIndex} in interface '{InterfaceName}' is missing required 'key' field."
        : $"Property 'key' at index {PropertyIndex} in interface '{InterfaceName}' must be a string.";
}
