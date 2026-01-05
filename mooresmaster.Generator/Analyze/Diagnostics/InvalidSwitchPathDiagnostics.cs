using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     switchパスの構文が不正な場合のDiagnostics
/// </summary>
public class InvalidSwitchPathDiagnostics : IDiagnostics
{
    public InvalidSwitchPathDiagnostics(string? propertyName, string path, Location pathLocation)
    {
        PropertyName = propertyName;
        Path = path;
        Locations = new[] { pathLocation };
    }

    public string? PropertyName { get; }
    public string Path { get; }
    public Location[] Locations { get; }

    public string Message => PropertyName != null
        ? $"Invalid switch path '{Path}' in property '{PropertyName}'. Path must start with '/' (absolute) or './' (relative)."
        : $"Invalid switch path '{Path}'. Path must start with '/' (absolute) or './' (relative).";
}
