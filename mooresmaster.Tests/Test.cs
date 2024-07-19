using System.Collections.Generic;
using mooresmaster.Generator.Json;
using Xunit;

namespace mooresmaster.Tests;

public class Test
{
    private const string BlockConfigJson = """
                                           {
                                             "type": "object",
                                             "properties": {
                                               "required": [
                                                 "blocks"
                                               ],
                                               "blocks": {
                                                 "type": "array",
                                                 "items": {
                                                   "type": "object",
                                                   "properties": {
                                                     "required": [
                                                       "blockId",
                                                       "itemId",
                                                       "blockType"
                                                     ],
                                                     "blockId": {
                                                       "type": "string",
                                                       "pattern": "@guid"
                                                     },
                                                     "itemId": {
                                                       "type": "string",
                                                       "pattern": "@guid",
                                                       "format": "@foreignKey:itemId"
                                                     },
                                                     "blockType": {
                                                       "type": "string"
                                                     },
                                                     "blockParam": {
                                                       "oneOf": [
                                                         {
                                                           "if": {
                                                             "properties": {
                                                               "blockType": {
                                                                 "const": "TypeA"
                                                               }
                                                             }
                                                           },
                                                           "then": {
                                                             "type": "object",
                                                             "properties": {
                                                               "paramA": {
                                                                 "type": "string"
                                                               },
                                                               "paramB": {
                                                                 "type": "integer"
                                                               }
                                                             },
                                                             "required": [
                                                               "paramA",
                                                               "paramB"
                                                             ]
                                                           }
                                                         },
                                                         {
                                                           "if": {
                                                             "properties": {
                                                               "blockType": {
                                                                 "const": "TypeB"
                                                               }
                                                             }
                                                           },
                                                           "then": {
                                                             "type": "object",
                                                             "properties": {
                                                               "paramA": {
                                                                 "type": "boolean"
                                                               },
                                                               "paramB": {
                                                                 "type": "number"
                                                               }
                                                             },
                                                             "required": [
                                                               "paramA",
                                                               "paramB"
                                                             ]
                                                           }
                                                         }
                                                       ]
                                                     },
                                                     "arrayTest": {
                                                       "type": "array",
                                                       "items": {
                                                         "type": "string"
                                                       }
                                                     },
                                                     "objectTest": {
                                                       "type": "object"
                                                     },
                                                     "vector2Test": {
                                                       "type": "array",
                                                       "pattern": "@vector2",
                                                       "items": {
                                                         "type": "number"
                                                       }
                                                     },
                                                     "vector3IntTest": {
                                                       "type": "array",
                                                       "pattern": "@vector3Int",
                                                       "items": {
                                                         "type": "integer"
                                                       }
                                                     },
                                                     "vector4Test": {
                                                       "type": "array",
                                                       "pattern": "@vector4",
                                                       "items": {
                                                         "type": "number"
                                                       }
                                                     },
                                                     "boolTest": {
                                                       "type": "boolean"
                                                     },
                                                     "intTest": {
                                                       "type": "integer"
                                                     },
                                                     "floatTest": {
                                                       "type": "number"
                                                     }
                                                   }
                                                 }
                                               }
                                             }
                                           }
                                           
                                           """;
    
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
        var node = JsonParser.Parse(JsonTokenizer.GetTokens(json));
        var answer = new JsonObject(new Dictionary<string, IJsonNode>
        {
            ["hoge"] = new JsonString("fuga"),
            ["piyo"] = new JsonArray([
                new JsonString("puyo"),
                new JsonString("poyo")
            ])
        });
        
        Assert.Equivalent(node, answer, true);
    }
    
    [Fact]
    public void BlockJsonParseTest()
    {
        var tokens = JsonTokenizer.GetTokens(BlockConfigJson);
        JsonParser.Parse(tokens);
    }
}
