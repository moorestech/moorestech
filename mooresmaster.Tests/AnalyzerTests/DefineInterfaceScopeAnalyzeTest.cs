using mooresmaster.Generator.Analyze;
using mooresmaster.Generator.Analyze.Analyzers;
using Xunit;

namespace mooresmaster.Tests.AnalyzerTests;

public class DefineInterfaceScopeAnalyzeTest
{
    /// <summary>
    ///     LocalInterfaceから別schemaのLocalInterfaceへの依存時のエラーのテスト
    /// </summary>
    [Fact]
    public void LocalInterfaceDependOtherSchemaLocalInterface()
    {
        const string localInterface0 =
            """
            id: localInterface0Schema
            type: object
            
            defineInterface:
              - interfaceName: ILocalInterface0
                properties:
                  - key: test0
                    type: integer
            """;
        
        const string localInterface1 =
            """
            id: localInterface1Schema
            type: object
            
            defineInterface:
              - interfaceName: ILocalInterface1
                implementationInterface:
                  - ILocalInterface0
                properties:
                  - key: test1
                    type: integer
            """;
        
        var analyzeException = Assert.ThrowsAny<AnalyzeException>(() => Test.Generate(localInterface0, localInterface1));
        var diagnosticsArray = analyzeException.DiagnosticsArray;
        
        // エラーがDefineInterfaceLocalScopeDiagnosticsのみであることの確認
        Assert.Single(diagnosticsArray);
        Assert.Equal(typeof(DefineInterfaceScopeAnalyzer.DefineInterfaceLocalScopeDiagnostics), diagnosticsArray[0].GetType());
    }
    
    [Fact]
    public void GlobalInterfaceDependOtherSchemaLocalInterface()
    {
        const string localInterfaceSchema =
            """
            id: localInterfaceSchema
            type: object
            
            defineInterface:
              - interfaceName: ILocalInterface0
                properties:
                  - key: test0
                    type: integer
            """;
        
        const string globalInterfaceSchema =
            """
            id: globalInterfaceSchema
            type: object
            
            globalDefineInterface:
              - interfaceName: ILocalInterface1
                implementationInterface:
                  - ILocalInterface0
                properties:
                  - key: test1
                    type: integer
            """;
        
        var analyzerException = Assert.ThrowsAny<AnalyzeException>(() => Test.Generate(localInterfaceSchema, globalInterfaceSchema));
        var diagnosticsArray = analyzerException.DiagnosticsArray;
        
        // エラーがDefineInterfaceGlobalScopeDiagnosticsのみであることの確認
        Assert.Single(diagnosticsArray);
        Assert.Equal(typeof(DefineInterfaceScopeAnalyzer.DefineInterfaceGlobalScopeDiagnostics), diagnosticsArray[0].GetType());
    }
}
