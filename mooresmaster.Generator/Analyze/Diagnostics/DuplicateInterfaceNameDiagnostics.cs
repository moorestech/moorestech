using Location = mooresmaster.Generator.Json.Location;

namespace mooresmaster.Generator.Analyze.Diagnostics;

public class DuplicateInterfaceNameDiagnostics(string interfaceName, Location[] locations) : IDiagnostics
{
    public readonly string InterfaceName = interfaceName;
    public string Message => $"Interface '{InterfaceName}' is defined more than once.";
    public Location[] Locations { get; } = locations;
}