using System;
using System.Collections.Generic;
using System.IO;
using mooresmaster.Generator.Definitions;
using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;
using mooresmaster.Generator.NameResolve;
using mooresmaster.Generator.Semantic;
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
            new Token(TokenType.LBrace, "{"),
            new Token(TokenType.String, "hoge"),
            new Token(TokenType.Colon, ":"),
            new Token(TokenType.String, "fuga"),
            new Token(TokenType.Comma, ","),
            new Token(TokenType.String, "piyo"),
            new Token(TokenType.Colon, ":"),
            new Token(TokenType.LSquare, "["),
            new Token(TokenType.String, "puyo"),
            new Token(TokenType.Comma, ","),
            new Token(TokenType.String, "poyo"),
            new Token(TokenType.RSquare, "]"),
            new Token(TokenType.RBrace, "}")
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
        var schemaTable = new SchemaTable();
        var schemas = new List<Schema>();
        
        foreach (var yaml in yamlTexts)
        {
            var jsonSchema = Yaml.ToJson(yaml);
            var json = JsonParser.Parse(JsonTokenizer.GetTokens(jsonSchema));
            var schema = JsonSchemaParser.ParseSchema(json as JsonObject, schemaTable);
            schemas.Add(schema);
        }
        
        var semantics = SemanticsGenerator.Generate([..schemas], schemaTable);
        var nameTable = NameResolver.Resolve(semantics, schemaTable);
        var definition = DefinitionGenerator.Generate(semantics, nameTable, schemaTable);
        
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
}
