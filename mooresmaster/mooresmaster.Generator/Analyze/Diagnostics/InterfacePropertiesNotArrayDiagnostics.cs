using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     defineInterfaceのpropertiesがJsonArrayでない場合のDiagnostics
/// </summary>
public class InterfacePropertiesNotArrayDiagnostics : IDiagnostics
{
    public InterfacePropertiesNotArrayDiagnostics(string interfaceName, IJsonNode actualNode, bool isGlobal)
    {
        InterfaceName = interfaceName;
        ActualNode = actualNode;
        IsGlobal = isGlobal;
        Locations = new[] { actualNode.Location };
    }

    public string InterfaceName { get; }
    public IJsonNode ActualNode { get; }
    public bool IsGlobal { get; }
    public Location[] Locations { get; }

    public string Message => $"'properties' in interface '{InterfaceName}' must be an array.";
}
