using Location = mooresmaster.Generator.Json.Location;

namespace mooresmaster.Generator.Analyze.Diagnostics;

public class InterfaceNotFoundDiagnostics(string interfaceName, Location location) : IDiagnostics
{
    public readonly string InterfaceName = interfaceName;
    public string Message => $"Interface '{InterfaceName}' not found.";
    public Location Location { get; } = location;
}