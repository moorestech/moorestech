using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;
using mooresmaster.Generator.Semantic;

namespace mooresmaster.Generator.Analyze.Diagnostics;

/// <summary>
///     RootSemanticsTableにRootIdが見つからない場合のDiagnostics
/// </summary>
public class RootSemanticsNotFoundDiagnostics : IDiagnostics
{
    public RootSemanticsNotFoundDiagnostics(RootId rootId, ISchema schema)
    {
        RootId = rootId;
        Schema = schema;
        Locations = new[] { schema.Json.Location };
    }

    public RootId RootId { get; }
    public ISchema Schema { get; }
    public Location[] Locations { get; }

    public string Message => $"Root semantics for RootId '{RootId}' not found in table.";
}
