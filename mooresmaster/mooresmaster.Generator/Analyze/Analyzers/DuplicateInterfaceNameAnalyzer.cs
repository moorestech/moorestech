using System.Collections.Immutable;
using System.Linq;
using mooresmaster.Generator.Analyze.Diagnostics;
using mooresmaster.Generator.JsonSchema;
using mooresmaster.Generator.Semantic;

namespace mooresmaster.Generator.Analyze.Analyzers;

public class DuplicateInterfaceNameAnalyzer : IPostSemanticsLayerAnalyzer
{
    public void PostSemanticsLayerAnalyze(Analysis analysis, Semantics semantics, ImmutableArray<SchemaFile> schemaFiles, SchemaTable schemaTable)
    {
        var interfaceGroups = semantics.InterfaceSemanticsTable
            .GroupBy(kvp => kvp.Value.Interface.InterfaceName)
            .Where(g => g.Count() > 1);
        
        foreach (var group in interfaceGroups)
        {
            var locations = group.Select(kvp => kvp.Value.Interface.Location).ToArray();
            analysis.ReportDiagnostics(new DuplicateInterfaceNameDiagnostics(group.Key, locations));
        }
    }
}