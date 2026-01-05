using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     defineInterfaceのinterfaceNameがJsonStringでない場合のDiagnostics
/// </summary>
public class InterfaceNameNotStringDiagnostics : IDiagnostics
{
    public InterfaceNameNotStringDiagnostics(JsonObject defineInterfaceNode, IJsonNode? actualNode, bool isGlobal)
    {
        DefineInterfaceNode = defineInterfaceNode;
        ActualNode = actualNode;
        IsGlobal = isGlobal;
        Locations = actualNode != null ? new[] { actualNode.Location } : new[] { defineInterfaceNode.Location };
    }

    public JsonObject DefineInterfaceNode { get; }
    public IJsonNode? ActualNode { get; }
    public bool IsGlobal { get; }
    public Location[] Locations { get; }

    public string Message => ActualNode == null
        ? "Interface definition is missing required 'interfaceName' field."
        : "'interfaceName' must be a string.";
}
