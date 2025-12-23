using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using mooresmaster.Generator;
using mooresmaster.Generator.Analyze;
using mooresmaster.Generator.CodeGenerate;
using mooresmaster.Generator.Definitions;
using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;
using mooresmaster.Generator.LoaderGenerate;
using mooresmaster.Generator.NameResolve;
using mooresmaster.Generator.Semantic;
using mooresmaster.Generator.Yaml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace mooresmaster.Tests;

public class Test
{
    [Fact]
    public void JsonTokenizerTest()
    {
        var json = """
                   {
                       "hoge": "fuga", 
                       "piyo": [
                           "puyo", 
                           "poyo"
                       ]
                   }
                   """;
        
        Token[] answer =
        [
            new(TokenType.LBrace, "{"),
            new(TokenType.String, "hoge"),
            new(TokenType.Colon, ":"),
            new(TokenType.String, "fuga"),
            new(TokenType.Comma, ","),
            new(TokenType.String, "piyo"),
            new(TokenType.Colon, ":"),
            new(TokenType.LSquare, "["),
            new(TokenType.String, "puyo"),
            new(TokenType.Comma, ","),
            new(TokenType.String, "poyo"),
            new(TokenType.RSquare, "]"),
            new(TokenType.RBrace, "}")
        ];
        var tokens = JsonTokenizer.GetTokens(json);
        Assert.Equivalent(tokens, answer, true);
    }
    
    [Fact]
    public void JsonParserTest()
    {
        var json = """
                   {
                       "hoge": "fuga", 
                       "piyo": [
                           "puyo", 
                           "poyo"
                       ]
                   }
                   """;
        // TODO あとで直す
        // var node = JsonParser.Parse(JsonTokenizer.GetTokens(json));
        // var answer = new JsonObject(new Dictionary<string, IJsonNode>
        // {
        //     ["hoge"] = new JsonString("fuga"),
        //     ["piyo"] = new JsonArray([
        //         new JsonString("puyo"),
        //         new JsonString("poyo")
        //     ])
        // });
        //
        // Assert.Equivalent(node, answer, true);
    }
    
    public static (SchemaTable schemaTable, NameTable nameTable, Semantics semantics, Definition definition) Generate(params string[] yamlTexts)
    {
        var analysis = new Analysis();
        var analyzer = new Analyzer().AddAllAnalyzer();
        
        var schemaTable = new SchemaTable();
        var schemaFileList = new List<SchemaFile>();
        
        analyzer.PreJsonSchemaLayerAnalyze(analysis, yamlTexts.ToAnalyzerTextFiles());
        
        foreach (var yaml in yamlTexts)
        {
            var json = YamlParser.Parse("TestDummyFilePath", yaml);
            var schema = JsonSchemaParser.ParseSchema(json, schemaTable);
            schemaFileList.Add(new SchemaFile("", schema));
        }
        
        var schemaFiles = schemaFileList.ToImmutableArray();
        
        analyzer.PostJsonSchemaLayerAnalyze(analysis, schemaFiles, schemaTable);
        
        analyzer.PreSemanticsLayerAnalyze(analysis, schemaFiles, schemaTable);
        var semantics = SemanticsGenerator.Generate([..schemaFileList.Select(s => s.Schema)], schemaTable);
        analyzer.PostSemanticsLayerAnalyze(analysis, semantics, schemaFiles, schemaTable);
        
        var nameTable = NameResolver.Resolve(semantics, schemaTable);
        analyzer.PreDefinitionLayerAnalyze(analysis, semantics, schemaFiles, schemaTable);
        var definition = DefinitionGenerator.Generate(semantics, nameTable, schemaTable);
        analyzer.PostDefinitionLayerAnalyze(analysis, semantics, schemaFiles, schemaTable, definition);
        
        analysis.ThrowDiagnostics();
        
        return (schemaTable, nameTable, semantics, definition);
    }
    
    public static string GetSchema(string relativePath)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{relativePath}");
        return File.ReadAllText(path);
    }
    
    public static JToken GetJson(string name)
    {
        var blockJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{name}.json");
        var blockJson = File.ReadAllText(blockJsonPath);
        return (JToken)JsonConvert.DeserializeObject(blockJson)!;
    }
    
    public static JToken ToJson(string json)
    {
        return (JToken)JsonConvert.DeserializeObject(json)!;
    }

    [Fact]
    public void ArrayInnerTypeHasIndexProperty()
    {
        var yaml = """
                   id: test
                   type: object
                   properties:
                   - key: items
                     type: array
                     items:
                       type: object
                       properties:
                       - key: name
                         type: string
                   """;

        var (schemaTable, nameTable, semantics, definition) = Generate(yaml);
        var codeFiles = CodeGenerator.Generate(definition, semantics);
        var loaderFiles = LoaderGenerator.Generate(definition, semantics, nameTable);

        // 配列のInnerTypeのコードを確認
        var itemsElementCode = codeFiles.FirstOrDefault(f => f.Code.Contains("class ItemsElement"));
        Assert.NotNull(itemsElementCode);

        // Indexプロパティが生成されているか確認
        Assert.Contains("public int Index { get; }", itemsElementCode.Code);

        // コンストラクタにIndexが含まれているか確認
        Assert.Contains("int Index", itemsElementCode.Code);
        Assert.Contains("this.Index = Index;", itemsElementCode.Code);

        // ItemsElementLoaderがIndexパラメータを受け取るか確認
        var itemsElementLoaderCode = loaderFiles.FirstOrDefault(f => f.Code.Contains("ItemsElementLoader"));
        Assert.NotNull(itemsElementLoaderCode);
        Assert.Contains("Load(int Index,", itemsElementLoaderCode.Code);

        // TestLoaderでSelect with Indexが使われているか確認
        var testLoaderCode = loaderFiles.FirstOrDefault(f => f.Code.Contains("TestLoader"));
        Assert.NotNull(testLoaderCode);
        Assert.Contains("(value, __Index__)", testLoaderCode.Code);

        // Loaderが__Index__をLoad関数に渡しているか確認
        Assert.Contains("Load(__Index__, value)", testLoaderCode.Code);

        // ItemsElementLoaderがIndexをコンストラクタに渡しているか確認
        Assert.Contains("return new", itemsElementLoaderCode.Code);
        Assert.Contains("(Index,", itemsElementLoaderCode.Code);
    }

    [Fact]
    public void ArrayInnerTypeIndexIsAssignedCorrectly()
    {
        // 生成されたコードの構造を詳細に確認するテスト
        var yaml = """
                   id: test
                   type: object
                   properties:
                   - key: data
                     type: array
                     items:
                       type: object
                       properties:
                       - key: id
                         type: integer
                       - key: value
                         type: string
                   """;

        var (schemaTable, nameTable, semantics, definition) = Generate(yaml);
        var codeFiles = CodeGenerator.Generate(definition, semantics);
        var loaderFiles = LoaderGenerator.Generate(definition, semantics, nameTable);

        // Model側: DataElementクラスにIndexプロパティがある
        var dataElementCode = codeFiles.FirstOrDefault(f => f.Code.Contains("class DataElement"));
        Assert.NotNull(dataElementCode);
        Assert.Contains("public int Index { get; }", dataElementCode.Code);

        // Model側: コンストラクタの最初の引数がIndex
        Assert.Contains("public DataElement(int Index,", dataElementCode.Code);
        Assert.Contains("this.Index = Index;", dataElementCode.Code);

        // Loader側: DataElementLoaderのLoad関数がint Indexを最初の引数として受け取る
        var dataElementLoader = loaderFiles.FirstOrDefault(f => f.Code.Contains("DataElementLoader"));
        Assert.NotNull(dataElementLoader);
        Assert.Contains("public static", dataElementLoader.Code);
        Assert.Contains("Load(int Index, global::Newtonsoft.Json.Linq.JToken json)", dataElementLoader.Code);

        // Loader側: new DataElement(Index, ...) の形式でIndexを渡している
        Assert.Contains("new", dataElementLoader.Code);
        Assert.Contains("(Index, Id, Value)", dataElementLoader.Code);

        // 親Loader側: Select((value, __Index__) => ...) の形式でインデックス付きSelectを使用
        var testLoader = loaderFiles.FirstOrDefault(f => f.Code.Contains("TestLoader"));
        Assert.NotNull(testLoader);
        Assert.Contains("Select(", testLoader.Code);
        Assert.Contains("(value, __Index__)", testLoader.Code);
        Assert.Contains("DataElementLoader.Load(__Index__, value)", testLoader.Code);
    }
}