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
    public void LocalInterfaceDependLocalInterface()
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
}
