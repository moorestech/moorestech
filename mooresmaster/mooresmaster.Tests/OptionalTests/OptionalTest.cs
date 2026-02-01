using Mooresmaster.Loader.OptionalTestSchemaModule;
using Xunit;

namespace mooresmaster.Tests.OptionalTests;

public class OptionalTest
{
    [Fact]
    public void OptionalLoaderTest()
    {
        var yamlSchema = """
                         id: test
                         type: object
                         properties:
                         - key: data0
                           type: string
                           optional: true
                         - key: data1
                           type: number
                           optional: true
                         - key: data2
                           type: object
                           optional: true
                           properties:
                           - key: data3
                             type: number
                             optional: true
                         """;
        
        var (schemaTable, nameTable, semantics, definition, analysis) = Test.Generate(yamlSchema);
        
        // 全てのプロパティがoptionalのはず
        foreach (var propertySemantics in semantics.PropertySemanticsTable.Values) Assert.True(propertySemantics.IsNullable);
    }
    
    [Fact]
    public void PropertyExistOptionalTest()
    {
        var value = OptionalTestSchemaLoader.Load(Test.GetJson("OptionalTests/OptionalTestSchema1"))!;
        
        Assert.NotNull(value.Data0);
    }
    
    [Fact]
    public void PropertyNotExistOptionalTest()
    {
        var value = OptionalTestSchemaLoader.Load(Test.GetJson("OptionalTests/OptionalTestSchema2"))!;
        
        Assert.Null(value.Data0);
    }
}