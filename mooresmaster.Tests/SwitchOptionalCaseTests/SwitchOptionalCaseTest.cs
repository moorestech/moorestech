using System.IO;
using System.Linq;
using mooresmaster.Generator.JsonSchema;
using Mooresmaster.Loader;
using Mooresmaster.Loader.SwitchOptionalCaseTestSchemaModule;
using Mooresmaster.Model.SwitchOptionalCaseTestSchemaModule;
using Xunit;

namespace mooresmaster.Tests.SwitchOptionalCaseTests;

public class SwitchOptionalCaseTest
{
    [Fact]
    public void OptionalCaseTest()
    {
        var yaml = File.ReadAllText("./SwitchOptionalCaseTests/SwitchOptionalCaseTestSchema.yml");
        
        var (schemaTable, nameTable, semantics, definition, analysis) = Test.Generate(yaml);
        
        var rootSemantics = semantics.RootSemanticsTable.Values.First();
        var rootInnerSchema = schemaTable.Table[rootSemantics.Root.InnerSchema.Value!];
        var rootInnerClassId = semantics.SchemaTypeSemanticsTable[rootInnerSchema];
        var rootInnerTypeSemantics = semantics.TypeSemanticsTable[rootInnerClassId];
        var rootInnerTypeObjectSchema = (ObjectSchema)rootInnerTypeSemantics.Schema;
        var switchSchemaId = rootInnerTypeObjectSchema.Properties.Values.First(v => v.IsValid && schemaTable.Table[v.Value!].PropertyName == "data");
        var switchProperty = (SwitchSchema)schemaTable.Table[switchSchemaId.Value!];
        
        Assert.True(switchProperty.HasOptionalCase);
    }
    
    /// <summary>
    ///     optionalなcaseの場合、dataがなくてもエラーにならない
    /// </summary>
    [Fact]
    public void OptionalCaseLoaderTestOptionalCaseNull()
    {
        var json = """
                   {
                     "type": "A"
                   }
                   """;
        
        var data = SwitchOptionalCaseTestSchemaLoader.Load(Test.ToJson(json));
        Assert.NotNull(data);
        Assert.Null(data.Data);
    }
    
    /// <summary>
    ///     optionalなcaseの場合、dataがあったら読み取れる
    /// </summary>
    [Fact]
    public void OptionalCaseLoaderTestOptionalCaseNotNull()
    {
        var json = """
                   {
                     "type": "A",
                     "data": {
                       "child0": 1,
                       "child1": "test"
                     }
                   }
                   """;
        
        var data = SwitchOptionalCaseTestSchemaLoader.Load(Test.ToJson(json));
        Assert.NotNull(data);
        Assert.NotNull(data.Data);
        Assert.True(data.Data is AData);
        var aData = (AData)data.Data;
        Assert.Equal(1, aData.Child0);
        Assert.Equal("test", aData.Child1);
    }
    
    /// <summary>
    ///     optionalなcaseがあり、かつ今回はoptionalでない場合、dataがないとエラーになる
    /// </summary>
    [Fact]
    public void OptionalCaseLoaderTestNotOptionalCaseNull()
    {
        var json = """
                   {
                     "type": "B"
                   }
                   """;
        
        Assert.Throws<MooresmasterLoaderException>(() => { SwitchOptionalCaseTestSchemaLoader.Load(Test.ToJson(json)); }
        );
    }
    
    /// <summary>
    ///     optionalなcaseがあり、かつ今回はoptionalでない場合、dataがあったら読み取れる
    /// </summary>
    [Fact]
    public void OptionalCaseLoaderTestNotOptionalCaseNotNull()
    {
        var json = """
                   {
                     "type": "B",
                     "data": {
                       "child0": 2,
                       "child1": "test2"
                     }
                   }
                   """;
        
        var data = SwitchOptionalCaseTestSchemaLoader.Load(Test.ToJson(json));
        Assert.NotNull(data);
        Assert.NotNull(data.Data);
        Assert.True(data.Data is BData);
        var bData = (BData)data.Data;
        Assert.Equal(2, bData.Child0);
        Assert.Equal("test2", bData.Child1);
    }
}