using Location = mooresmaster.Generator.Json.Location;

namespace mooresmaster.Generator.Analyze.Diagnostics;

public class DuplicateImplementationInterfaceDiagnostics(string targetName, string duplicateInterfaceName, Location[] locations) : IDiagnostics
{
    public readonly string DuplicateInterfaceName = duplicateInterfaceName;
    public readonly string TargetName = targetName;
    public string Message => $"Interface '{DuplicateInterfaceName}' is inherited more than once in '{TargetName}'.";
    public Location[] Locations { get; } = locations;
}