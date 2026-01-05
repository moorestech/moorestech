using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using mooresmaster.Generator;
using mooresmaster.Generator.Analyze;
using mooresmaster.Generator.Definitions;
using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;
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
    
    public static (SchemaTable schemaTable, NameTable nameTable, Semantics semantics, Definition definition, Analysis analysis) Generate(params string[] yamlTexts)
    {
        var analysis = new Analysis();
        var analyzer = new Analyzer().AddAllAnalyzer();
        
        var schemaTable = new SchemaTable();
        var schemaFileList = new List<SchemaFile>();
        
        analyzer.PreJsonSchemaLayerAnalyze(analysis, yamlTexts.ToAnalyzerTextFiles());
        
        foreach (var yaml in yamlTexts)
        {
            var json = YamlParser.Parse("TestDummyFilePath", yaml);
            var schema = JsonSchemaParser.ParseSchema(json, schemaTable, analysis);
            schemaFileList.Add(new SchemaFile("", schema));
        }
        
        var schemaFiles = schemaFileList.ToImmutableArray();
        
        analyzer.PostJsonSchemaLayerAnalyze(analysis, schemaFiles, schemaTable);
        
        analyzer.PreSemanticsLayerAnalyze(analysis, schemaFiles, schemaTable);
        var semantics = SemanticsGenerator.Generate([..schemaFileList.Select(s => s.Schema)], schemaTable, analysis);
        analyzer.PostSemanticsLayerAnalyze(analysis, semantics, schemaFiles, schemaTable);
        
        var nameTable = NameResolver.Resolve(semantics, schemaTable);
        analyzer.PreDefinitionLayerAnalyze(analysis, semantics, schemaFiles, schemaTable);
        var definition = DefinitionGenerator.Generate(semantics, nameTable, schemaTable);
        analyzer.PostDefinitionLayerAnalyze(analysis, semantics, schemaFiles, schemaTable, definition);
        
        return (schemaTable, nameTable, semantics, definition, analysis);
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
}