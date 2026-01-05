using System.Collections.Immutable;
using mooresmaster.Generator.Analyze.Diagnostics;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator.Analyze.Analyzers;

public class DuplicateImplementationInterfaceAnalyzer : IPostJsonSchemaLayerAnalyzer
{
    public void PostJsonSchemaLayerAnalyze(Analysis analysis, ImmutableArray<SchemaFile> schemaFiles, SchemaTable schemaTable)
    {
        // DefineInterfaceの重複継承をチェック
        foreach (var schemaFile in schemaFiles)
        foreach (var defineInterface in schemaFile.Schema.Interfaces)
        foreach (var kvp in defineInterface.DuplicateImplementationLocations)
            analysis.ReportDiagnostics(new DuplicateImplementationInterfaceDiagnostics(
                defineInterface.InterfaceName,
                kvp.Key,
                kvp.Value
            ));
        
        // ObjectSchemaの重複継承をチェック
        foreach (var kvp in schemaTable.Table)
            if (kvp.Value is ObjectSchema objectSchema)
                foreach (var duplicateKvp in objectSchema.DuplicateImplementationLocations)
                {
                    var targetName = objectSchema.PropertyName ?? "(anonymous object)";
                    analysis.ReportDiagnostics(new DuplicateImplementationInterfaceDiagnostics(
                        targetName,
                        duplicateKvp.Key,
                        duplicateKvp.Value
                    ));
                }
    }
}