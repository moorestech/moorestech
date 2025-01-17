using System.Collections.Immutable;
using System.Linq;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator.Analyze.Analyzers;

public class DefineInterfaceAnalyzer : IPostJsonSchemaLayerAnalyzer
{
    public void PostJsonSchemaLayerAnalyze(Analysis analysis, ImmutableArray<SchemaFile> schemaFiles, SchemaTable schemaTable)
    {
        foreach (var (schemaFile, defineInterface) in schemaFiles.SelectMany(f => f.Schema.Interfaces.Select(i => (f, i)))) analysis.ReportDiagnostics(new DefineInterfaceDiagnostics(defineInterface));
    }
    
    private class DefineInterfaceDiagnostics(DefineInterface targetDefineInterface) : IDiagnostics
    {
        public string Message => $"ErrorInterfaceName {targetDefineInterface.InterfaceName}";
    }
}
