using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     Switchケースがenumの全てのオプションを網羅していない場合のDiagnostics
/// </summary>
public class SwitchCasesNotExhaustiveDiagnostics : IDiagnostics
{
    public SwitchCasesNotExhaustiveDiagnostics(SwitchSchema switchSchema, string[] missingCases, string[] allEnumOptions, Location location)
    {
        SwitchSchema = switchSchema;
        MissingCases = missingCases;
        AllEnumOptions = allEnumOptions;
        Locations = new[] { location };
    }
    
    public SwitchSchema SwitchSchema { get; }
    public string[] MissingCases { get; }
    public string[] AllEnumOptions { get; }
    public Location[] Locations { get; }
    
    public string Message => $"Switch cases are not exhaustive. Missing cases: [{string.Join(", ", MissingCases)}]. All enum options: [{string.Join(", ", AllEnumOptions)}]";
}