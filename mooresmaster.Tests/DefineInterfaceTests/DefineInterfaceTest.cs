using System;
using System.Linq;
using Xunit;

namespace mooresmaster.Tests.DefineInterfaceTests;

public class DefineInterfaceTest
{
    [Fact]
    public void LocalDefineInterfaceTest()
    {
        var (schemaTable, nameTable, semantics, definition) = Test.Generate(Test.GetSchema(@"DefineInterfaceTests/DefineInterfaceTestSchema.yml"));
        
        var interfaceTable = semantics.InterfaceSemanticsTable;
        
        var firstLocalInterface = interfaceTable.Select(i => i.Value.Interface).First(i => i.InterfaceName == "ILocalInterface");
        
        Assert.False(firstLocalInterface.IsGlobal);
        Assert.True(firstLocalInterface.Properties.ContainsKey("test0"));
        Assert.True(firstLocalInterface.Properties.ContainsKey("test1"));
    }
    
    [Fact]
    public void GlobalDefineInterfaceTest()
    {
        var (schemaTable, nameTable, semantics, definition) = Test.Generate(Test.GetSchema(@"DefineInterfaceTests/DefineInterfaceTestSchema.yml"));
        
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
        Assert.ThrowsAny<Exception>(() => { _ = Test.Generate(defineInterfaceTestSchemaText, failedTestText); });

        // globalであれば問題ない
        Test.Generate(defineInterfaceTestSchemaText, passedTestText);
    }
}
