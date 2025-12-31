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
        
        var diagnosticsArray = Test.Generate(localInterface0, localInterface1).analysis.DiagnosticsList;
        
        // エラーがDefineInterfaceLocalScopeDiagnosticsのみであることの確認
        Assert.Single(diagnosticsArray);
        Assert.Equal(typeof(DefineInterfaceScopeAnalyzer.DefineInterfaceLocalScopeDiagnostics), diagnosticsArray[0].GetType());
    }
    
    /// <summary>
    ///     LocalInterfaceから別SchemaのGlobalInterfaceへの依存が問題ないことの確認
    /// </summary>
    [Fact]
    public void LocalInterfaceDependOtherSchemaGlobalInterface()
    {
        const string localInterfaceSchema =
            """
            id: localInterfaceSchema
            type: object
            
            defineInterface:
              - interfaceName: ILocalInterface
                implementationInterface:
                  - IGlobalInterface
                properties:
                  - key: test0
                    type: integer
            """;
        
        const string globalInterfaceSchema =
            """
            id: globalInterfaceSchema
            type: object
            
            globalDefineInterface:
              - interfaceName: IGlobalInterface
                properties:
                  - key: test1
                    type: integer
            """;
        
        Test.Generate(localInterfaceSchema, globalInterfaceSchema);
    }
    
    /// <summary>
    ///     GlobalInterfaceからLocalInterfaceへの依存時のエラーのテスト
    /// </summary>
    [Fact]
    public void GlobalInterfaceDependOtherSchemaLocalInterface()
    {
        const string localInterfaceSchema =
            """
            id: localInterfaceSchema
            type: object
            
            defineInterface:
              - interfaceName: ILocalInterface
                properties:
                  - key: test0
                    type: integer
            """;
        
        const string globalInterfaceSchema =
            """
            id: globalInterfaceSchema
            type: object
            
            globalDefineInterface:
              - interfaceName: IGlobalInterface
                implementationInterface:
                  - ILocalInterface
                properties:
                  - key: test1
                    type: integer
            """;
        
        var diagnosticsArray = Test.Generate(localInterfaceSchema, globalInterfaceSchema).analysis.DiagnosticsList;
        
        // エラーがDefineInterfaceGlobalScopeDiagnosticsのみであることの確認
        Assert.Single(diagnosticsArray);
        Assert.Equal(typeof(DefineInterfaceScopeAnalyzer.DefineInterfaceGlobalScopeDiagnostics), diagnosticsArray[0].GetType());
    }
}