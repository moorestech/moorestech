using Location = mooresmaster.Generator.Json.Location;

namespace mooresmaster.Generator.Analyze.Diagnostics;

public class DuplicateInterfaceNameDiagnostics(string interfaceName) : IDiagnostics
{
    public readonly string InterfaceName = interfaceName;
    public string Message => $"Interface '{InterfaceName}' is defined more than once.";
    public Location Location { get; } = default;
}
