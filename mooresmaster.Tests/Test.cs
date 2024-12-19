using System;
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

    public static (SchemaTable schemaTable, NameTable nameTable, Semantics semantics, Definition definition) Generate(string yaml)
    {
        var jsonSchema = Yaml.ToJson(yaml);
        var schemaTable = new SchemaTable();
        var json = JsonParser.Parse(JsonTokenizer.GetTokens(jsonSchema));
        var schema = JsonSchemaParser.ParseSchema(json as JsonObject, schemaTable);
        var semantics = SemanticsGenerator.Generate([schema], schemaTable);
        var nameTable = NameResolver.Resolve(semantics, schemaTable);
        var definition = DefinitionGenerator.Generate(semantics, nameTable, schemaTable);

        return (schemaTable, nameTable, semantics, definition);
    }

    public static JToken GetJson(string name)
    {
        var blockJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{name}.json");
        var blockJson = File.ReadAllText(blockJsonPath);
        return (JToken)JsonConvert.DeserializeObject(blockJson)!;
    }
}
