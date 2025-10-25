using System;
using System.IO;
using System.Linq;
using mooresmaster.Generator.CodeGenerate;
using mooresmaster.Generator.JsonSchema;
using mooresmaster.Generator.LoaderGenerate;
using Xunit;

namespace mooresmaster.Tests.SwitchOptionalCaseTests;

public class SwitchOptionalCaseTest
{
    [Fact]
    public void RecursiveOptionalTest()
    {
        var yaml = File.ReadAllText("./SwitchOptionalCaseTests/SwitchOptionalCaseTestSchema.yml");
        
        var (schemaTable, nameTable, semantics, definition) = Test.Generate(yaml);
        
        var rootSemantics = semantics.RootSemanticsTable.Values.First();
        var rootInnerSchema = schemaTable.Table[rootSemantics.Root.InnerSchema];
        var rootInnerClassId = semantics.SchemaTypeSemanticsTable[rootInnerSchema];
        var rootInnerTypeSemantics = semantics.TypeSemanticsTable[rootInnerClassId];
        var rootInnerTypeObjectSchema = (ObjectSchema)rootInnerTypeSemantics.Schema;
        var switchSchemaId = rootInnerTypeObjectSchema.Properties.Values.First(v => schemaTable.Table[v].PropertyName == "data");
        var switchProperty = (SwitchSchema)schemaTable.Table[switchSchemaId];
        
        Assert.True(switchProperty.IsNullable);
        
        var codeFiles = CodeGenerator.Generate(definition);
        var loaderFiles = LoaderGenerator.Generate(definition, semantics, nameTable);
        Console.WriteLine(codeFiles);
    }
}