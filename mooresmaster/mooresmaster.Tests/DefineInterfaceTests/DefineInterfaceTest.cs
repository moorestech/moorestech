using System.Linq;
using mooresmaster.Generator.Analyze.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace mooresmaster.Tests.DefineInterfaceTests;

public class DefineInterfaceTest
{
    private readonly ITestOutputHelper _testOutputHelper;
    
    public DefineInterfaceTest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }
    
    [Fact]
    public void LocalDefineInterfaceTest()
    {
        var (schemaTable, nameTable, semantics, definition, analysis) = Test.Generate(Test.GetSchema(@"DefineInterfaceTests/DefineInterfaceTestSchema.yml"));
        
        var interfaceTable = semantics.InterfaceSemanticsTable;
        
        var firstLocalInterface = interfaceTable.Select(i => i.Value.Interface).First(i => i.InterfaceName == "ILocalInterface");
        
        Assert.False(firstLocalInterface.IsGlobal);
        Assert.True(firstLocalInterface.Properties.ContainsKey("test0"));
        Assert.True(firstLocalInterface.Properties.ContainsKey("test1"));
    }
    
    [Fact]
    public void GlobalDefineInterfaceTest()
    {
        var (schemaTable, nameTable, semantics, definition, analysis) = Test.Generate(Test.GetSchema(@"DefineInterfaceTests/DefineInterfaceTestSchema.yml"));
        
        var interfaceTable = semantics.InterfaceSemanticsTable;
        
        var firstGlobalInterface = interfaceTable.Select(i => i.Value.Interface).First(i => i.InterfaceName == "IGlobalInterface");
        
        Assert.True(firstGlobalInterface.IsGlobal);
        Assert.True(firstGlobalInterface.Properties.ContainsKey("test2"));
        Assert.True(firstGlobalInterface.Properties.ContainsKey("test3"));
    }
    
    [Fact]
    public void GlobalDefineInterfaceFailedTest()
    {
        const string failedTestText = """
                                      id: defineInterfaceFailedTestSchema
                                      type: object
                                      
                                      implementationInterface:
                                        - ILocalInterface
                                      
                                      properties:
                                        - key: test0
                                          type: integer
                                        - key: test1
                                          type: integer
                                      """;
        
        const string passedTestText = """
                                      id: defineInterfaceFailedTestSchema
                                      type: object
                                      
                                      implementationInterface:
                                        - IGlobalInterface
                                      
                                      properties:
                                        - key: test2
                                          type: integer
                                        - key: test3
                                          type: integer
                                      """;
        
        var defineInterfaceTestSchemaText = Test.GetSchema("DefineInterfaceTests/DefineInterfaceTestSchema.yml");
        
        // ScopeがLocalのInterfaceを別ファイルから参照している場合エラーになる
        Assert.NotEmpty(Test.Generate(defineInterfaceTestSchemaText, failedTestText).analysis.DiagnosticsList);
        
        // globalであれば問題ない
        Test.Generate(defineInterfaceTestSchemaText, passedTestText);
    }
    
    [Fact]
    public void InterfaceNotFoundDiagnosticsReportTest()
    {
        const string source =
            """
            id: interfaceNotFoundDiagnosticsReportTestSchema
            type: object
            
            implementationInterface:
              - INotFoundInterface
            
            properties:
              - key: test2
                type: integer
              - key: test3
                type: integer
            """;
        
        var analysis = Test.Generate(source).analysis;
        _testOutputHelper.WriteLine(analysis.ToString());
        var diagnosticsList = analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsList);
        var diagnostics = diagnosticsList[0];
        var typedDiagnostics = Assert.IsType<InterfaceNotFoundDiagnostics>(diagnostics);
        Assert.Equal("INotFoundInterface", typedDiagnostics.InterfaceName);
    }
    
    [Fact]
    public void DefineInterfaceInterfaceNotFoundDiagnosticsReportTest()
    {
        var source = """
                     id: defineInterfaceInterfaceNotFoundDiagnosticsReportTestSchema
                     type: object
                     
                     defineInterface:
                     - interfaceName: ITestInterface
                       implementationInterface:
                         - INotFoundInterface
                       properties:
                         - key: test0
                           type: integer
                     """;
        
        var analysis = Test.Generate(source).analysis;
        _testOutputHelper.WriteLine(analysis.ToString());
        var diagnosticsList = analysis.DiagnosticsList;
        
        Assert.Single(diagnosticsList);
        var diagnostics = diagnosticsList[0];
        var typedDiagnostics = Assert.IsType<InterfaceNotFoundDiagnostics>(diagnostics);
        Assert.Equal("INotFoundInterface", typedDiagnostics.InterfaceName);
    }
}