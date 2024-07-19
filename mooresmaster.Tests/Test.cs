using System.Collections.Generic;
using mooresmaster.Generator;
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
        var node = JsonParser.Parse(JsonTokenizer.GetTokens(json));
        var answer = new JsonObject(new Dictionary<string, JsonNode>
        {
            ["hoge"] = new JsonString("fuga"),
            ["piyo"] = new JsonArray([
                new JsonString("puyo"),
                new JsonString("poyo")
            ])
        });
        
        Assert.Equivalent(node, answer, true);
    }
}
