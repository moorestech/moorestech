using mooresmaster.Generator.Json;
using mooresmaster.Generator.Semantic;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     RootSemanticsTableにRootIdが見つからない場合のDiagnostics
/// </summary>
public class RootSemanticsNotFoundDiagnostics : IDiagnostics
{
    public RootSemanticsNotFoundDiagnostics(RootId rootId)
    {
        RootId = rootId;
        Locations = new Location[0];
    }

    public RootId RootId { get; }
    public Location[] Locations { get; }

    public string Message => $"Root semantics for RootId '{RootId}' not found in table.";
}
