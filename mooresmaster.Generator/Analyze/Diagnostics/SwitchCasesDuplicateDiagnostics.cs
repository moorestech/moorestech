using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     Switchケースに重複したwhen値がある場合のDiagnostics
/// </summary>
public class SwitchCasesDuplicateDiagnostics : IDiagnostics
{
    public SwitchCasesDuplicateDiagnostics(SwitchSchema switchSchema, string duplicateCase, int occurrenceCount, Location location)
    {
        SwitchSchema = switchSchema;
        DuplicateCase = duplicateCase;
        OccurrenceCount = occurrenceCount;
        Locations = new[] { location };
    }
    
    public SwitchSchema SwitchSchema { get; }
    public string DuplicateCase { get; }
    public int OccurrenceCount { get; }
    public Location[] Locations { get; }
    
    public string Message => $"Duplicate switch case '{DuplicateCase}' found {OccurrenceCount} times. Each case must be unique.";
}