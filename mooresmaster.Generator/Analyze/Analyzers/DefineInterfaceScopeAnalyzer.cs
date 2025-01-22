using System.Collections.Immutable;
using System.Linq;
using mooresmaster.Generator.JsonSchema;
using mooresmaster.Generator.Semantic;

namespace mooresmaster.Generator.Analyze.Analyzers;

public class DefineInterfaceScopeAnalyzer : IPostSemanticsLayerAnalyzer
{
    public void PostSemanticsLayerAnalyze(Analysis analysis, Semantics semantics, ImmutableArray<SchemaFile> schemaFiles, SchemaTable schemaTable)
    {
        foreach (var (id, interfaceSemantics) in semantics.InterfaceSemanticsTable.Select(kvp => (kvp.Key, kvp.Value)))
        {
            var implementations = semantics.GetImplementations(id);
            
            // 依存対象のinterfaceも後で調査するため再起的に全て調査する必要はない
            foreach (var (implementationInterfaceId, implementation) in implementations.Select(i => (i, semantics.InterfaceSemanticsTable[i])))
                if (interfaceSemantics.Interface.IsGlobal)
                {
                    // グローバルなら依存対象も全てglobalでないといけない
                    if (implementation.Interface.IsGlobal) continue;
                    analysis.ReportDiagnostics(new DefineInterfaceGlobalScopeDiagnostics(id, implementationInterfaceId, semantics));
                }
                else
                {
                    // ローカルなら依存対象はglobalか同じscopeのlocalでないといけない
                    if (implementation.Interface.IsGlobal) continue;
                    if (implementation.Schema.SchemaId == interfaceSemantics.Schema.SchemaId) continue;
                    
                    analysis.ReportDiagnostics(new DefineInterfaceLocalScopeDiagnostics(id, implementationInterfaceId, semantics));
                }
        }
    }
    
    public class DefineInterfaceGlobalScopeDiagnostics(InterfaceId targetInterfaceId, InterfaceId implementationInterfaceId, Semantics semantics) : IDiagnostics
    {
        public string Message => $"""
                                  Global interface cannot depend on local interface.
                                  
                                  TargetInterface {semantics.InterfaceSemanticsTable[targetInterfaceId].Interface.InterfaceName}
                                  ImplementationInterface {semantics.InterfaceSemanticsTable[implementationInterfaceId].Interface.InterfaceName}
                                  """;
    }
    
    public class DefineInterfaceLocalScopeDiagnostics(InterfaceId targetInterfaceId, InterfaceId implementationInterfaceId, Semantics semantics) : IDiagnostics
    {
        public string Message => $"""
                                  Local interface can only depend on global interface or same scope local interface.
                                  
                                  TargetInterface {semantics.InterfaceSemanticsTable[targetInterfaceId].Interface.InterfaceName}
                                  
                                  ImplementationInterface {semantics.InterfaceSemanticsTable[implementationInterfaceId].Interface.InterfaceName}
                                  """;
    }
}
