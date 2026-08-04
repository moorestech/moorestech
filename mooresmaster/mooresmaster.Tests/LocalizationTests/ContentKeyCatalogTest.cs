using System;
using System.Reflection;
using Mooresmaster.LocalizationCsv;
using mooresmaster.Generator.Localization;
using Xunit;
using static mooresmaster.Tests.LocalizationTests.LocalizationGeneratedCodeCompiler;

namespace mooresmaster.Tests.LocalizationTests;

public class ContentKeyCatalogTest
{
    private const string CatalogHeader = "namespace,field,sourceMaster\n";
    private static readonly Guid ContentGuid = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

    [Fact]
    public void 宣言表の行をnamespaceとfieldと供給Masterへ写像する()
    {
        var definitions = ContentKeyCatalogParser.Parse(
            CatalogHeader +
            "item,name,ItemMaster\n" +
            "research,description,ResearchMaster\n");

        Assert.Equal(2, definitions.Length);
        Assert.Equal("item", definitions[0].Namespace);
        Assert.Equal("name", definitions[0].Field);
        Assert.Equal("ItemMaster", definitions[0].SourceMaster);
        Assert.Equal("research", definitions[1].Namespace);
        Assert.Equal("description", definitions[1].Field);
        Assert.Equal("ResearchMaster", definitions[1].SourceMaster);
    }

    [Theory]
    [InlineData("item", "name", "ItemName", "item.01234567-89ab-cdef-0123-456789abcdef.name")]
    [InlineData("fluid", "name", "FluidName", "fluid.01234567-89ab-cdef-0123-456789abcdef.name")]
    [InlineData("connectTool", "name", "ConnectToolName", "connectTool.01234567-89ab-cdef-0123-456789abcdef.name")]
    [InlineData("challengeTutorial", "text", "ChallengeTutorialText", "challengeTutorial.01234567-89ab-cdef-0123-456789abcdef.text")]
    [InlineData("research", "description", "ResearchDescription", "research.01234567-89ab-cdef-0123-456789abcdef.description")]
    public void 宣言表の各行を型付きビルダーとして生成する(
        string contentNamespace,
        string field,
        string builderName,
        string expectedKey)
    {
        var keysType = CompileContentKeys(
            CatalogHeader + $"{contentNamespace},{field},SomeMaster\n");

        // 実assemblyのビルダー戻り値からキー文字列を取り出す
        // Read the key string from the builder return value in a real assembly
        var key = keysType.GetMethod(builderName)!.Invoke(null, new object[] { ContentGuid })!;
        var keyField = key.GetType().GetField("Key")!;

        Assert.Equal(expectedKey, keyField.GetValue(key));
    }

    [Fact]
    public void ビルダーはstring非互換のreadonly構造体を返す()
    {
        var keysType = CompileContentKeys(CatalogHeader + "item,name,ItemMaster\n");
        var returnType = keysType.GetMethod("ItemName")!.ReturnType;

        Assert.Equal("Mooresmaster.Localization.Generated.ContentLocalizationKey", returnType.FullName);
        Assert.True(returnType.IsValueType);
        Assert.Equal(typeof(string), returnType.GetField("Key")!.FieldType);
        Assert.True(returnType.GetField("Key")!.Attributes.HasFlag(FieldAttributes.InitOnly));
    }

    [Theory]
    [InlineData("field,namespace,sourceMaster\n")]
    [InlineData("namespace,field\n")]
    [InlineData("")]
    public void 宣言表の見出し契約違反は明示例外(string csvText)
    {
        Assert.Throws<LocalizationCsvException>(() => ContentKeyCatalogParser.Parse(csvText));
    }

    [Theory]
    [InlineData("item,name\n")]
    [InlineData("item,name,ItemMaster,extra\n")]
    [InlineData("item,name,\n")]
    [InlineData("Item,name,ItemMaster\n")]
    [InlineData("item,Name,ItemMaster\n")]
    [InlineData("build-menu,name,ItemMaster\n")]
    [InlineData("item,1name,ItemMaster\n")]
    public void 宣言表の行契約違反は明示例外(string row)
    {
        Assert.Throws<LocalizationCsvException>(() => ContentKeyCatalogParser.Parse(CatalogHeader + row));
    }

    [Fact]
    public void 同一namespaceとfieldの重複行は明示例外()
    {
        Assert.Throws<LocalizationCsvException>(() => ContentKeyCatalogParser.Parse(
            CatalogHeader +
            "item,name,ItemMaster\n" +
            "item,name,BlockMaster\n"));
    }

    private static Type CompileContentKeys(string catalogText)
    {
        var csv = LocalizationCsvParser.Parse("key,Source,english\nui.menu.close,Close,Close\n");
        var settings = new[] { new LanguageSetting("english", "English", "en") };
        var code = LocalizationCodeGenerator.Generate(csv, settings, ContentKeyCatalogParser.Parse(catalogText));

        return CompileTable(code).Assembly.GetType("Mooresmaster.Localization.Generated.ContentLocalizationKeys")!;
    }
}
