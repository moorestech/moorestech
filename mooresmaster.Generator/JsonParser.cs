using System;
using System.Collections.Generic;

namespace mooresmaster.Generator;

public record JsonNode;

public record JsonObject(Dictionary<string, JsonNode> Nodes) : JsonNode
{
    public readonly Dictionary<string, JsonNode> Nodes = Nodes;
}

public record JsonArray(JsonNode[] Nodes) : JsonNode
{
    public readonly JsonNode[] Nodes = Nodes;
}

public record JsonString(string Literal) : JsonNode
{
    public readonly string Literal = Literal;
}

public static class JsonParser
{
    public static JsonNode Parse(Token[] tokens)
    {
        var iterator = new Iterator(tokens);
        return Parse(ref iterator);
    }
    
    private static JsonNode Parse(ref Iterator iterator)
    {
        return iterator.CurrentToken.Type switch
        {
            TokenType.String => ParseString(ref iterator),
            TokenType.LBrace => ParseObject(ref iterator),
            TokenType.LSquare => ParseArray(ref iterator),
            _ => throw new Exception($"""Unexpected token: {iterator.CurrentToken.Type} "{iterator.CurrentToken.Literal}" """)
        };
    }
    
    private static JsonString ParseString(ref Iterator iterator)
    {
        var value = iterator.CurrentToken.Literal;
        iterator.CurrentIndex++; // skip string
        return new JsonString(value);
    }
    
    private static JsonArray ParseArray(ref Iterator iterator)
    {
        var nodes = new List<JsonNode>();
        iterator.CurrentIndex++; // skip '['
        
        while (iterator.CurrentToken.Type != TokenType.RSquare)
        {
            nodes.Add(Parse(ref iterator));
            if (iterator.CurrentToken.Type == TokenType.RSquare) break;
            iterator.CurrentIndex++; // skip ','
        }
        
        iterator.CurrentIndex++; // skip ']'
        
        return new JsonArray(nodes.ToArray());
    }
    
    private static JsonObject ParseObject(ref Iterator iterator)
    {
        var nodes = new Dictionary<string, JsonNode>();
        iterator.CurrentIndex++; // skip '{'
        
        while (iterator.CurrentToken.Type != TokenType.RBrace)
        {
            var key = ParseString(ref iterator);
            iterator.CurrentIndex++; // skip ':'
            var value = Parse(ref iterator);
            nodes.Add(key.Literal, value);
            if (iterator.CurrentToken.Type == TokenType.RBrace) break;
            iterator.CurrentIndex++; // skip ','
        }
        
        iterator.CurrentIndex++; // skip '}'
        
        return new JsonObject(nodes);
    }
    
    public struct Iterator(Token[] tokens)
    {
        public int CurrentIndex;
        public Token CurrentToken => tokens.Length > CurrentIndex ? tokens[CurrentIndex] : new Token(TokenType.Illegal, "");
        public Token NextToken => tokens.Length > CurrentIndex + 1 ? tokens[CurrentIndex + 1] : new Token(TokenType.Illegal, "");
    }
}
