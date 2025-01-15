using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator.Analyze.Analyzers;

public class DefineInterfaceAnalyzer : IPostJsonSchemaLayerAnalyzer
{
    public void PostJsonSchemaLayerAnalyze(Analysis analysis, SchemaTable schemaTable)
    {
        analysis.ReportDiagnostics(new DefineInterfaceDiagnostics());
    }
    
    private class DefineInterfaceDiagnostics : IDiagnostics
    {
    }
}
