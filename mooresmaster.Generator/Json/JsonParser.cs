using System;
using System.Collections.Generic;

namespace mooresmaster.Generator.Json;

public interface IJsonNode
{
    IJsonNode? Parent { get; }
}

public record JsonObject(Dictionary<string, IJsonNode> Nodes, IJsonNode? Parent) : IJsonNode
{
    public readonly Dictionary<string, IJsonNode> Nodes = Nodes;
    public IJsonNode? this[string key] => Nodes.ContainsKey(key) ? Nodes[key] : null;
    public IJsonNode? Parent { get; } = Parent;
}

public record JsonArray(IJsonNode[] Nodes, IJsonNode? Parent) : IJsonNode
{
    public IJsonNode[] Nodes = Nodes;
    public IJsonNode? this[int index] => Nodes.Length > index ? Nodes[index] : null;
    public IJsonNode? Parent { get; } = Parent;
}

public record JsonString(string Literal, IJsonNode? Parent) : IJsonNode
{
    public readonly string Literal = Literal;
    public IJsonNode? Parent { get; } = Parent;
}

public record JsonBoolean(bool Literal, IJsonNode? Parent) : IJsonNode
{
    public readonly bool Literal = Literal;
    public IJsonNode? Parent { get; } = Parent;
}

public static class JsonParser
{
    public static IJsonNode Parse(Token[] tokens)
    {
        var iterator = new Iterator(tokens);
        return Parse(ref iterator, null);
    }
    
    private static IJsonNode Parse(ref Iterator iterator, IJsonNode? parent)
    {
        return iterator.CurrentToken.Type switch
        {
            TokenType.String => ParseString(ref iterator, parent),
            TokenType.LBrace => ParseObject(ref iterator, parent),
            TokenType.LSquare => ParseArray(ref iterator, parent),
            TokenType.True or TokenType.False => ParseBoolean(ref iterator, parent),
            _ => throw new Exception($"""Unexpected token: {iterator.CurrentToken.Type} "{iterator.CurrentToken.Literal}" """)
        };
    }
    
    private static JsonBoolean ParseBoolean(ref Iterator iterator, IJsonNode? parent)
    {
        var value = iterator.CurrentToken.Literal;
        iterator.CurrentIndex++; // skip boolean
        return new JsonBoolean(value == "true", parent);
    }
    
    private static JsonString ParseString(ref Iterator iterator, IJsonNode? parent)
    {
        var value = iterator.CurrentToken.Literal;
        iterator.CurrentIndex++; // skip string
        return new JsonString(value, parent);
    }
    
    private static JsonArray ParseArray(ref Iterator iterator, IJsonNode? parent)
    {
        var nodes = new List<IJsonNode>();
        iterator.CurrentIndex++; // skip '['
        
        var jsonNode = new JsonArray([], parent);
        
        while (iterator.CurrentToken.Type != TokenType.RSquare)
        {
            nodes.Add(Parse(ref iterator, jsonNode));
            if (iterator.CurrentToken.Type == TokenType.RSquare) break;
            iterator.CurrentIndex++; // skip ','
        }
        
        iterator.CurrentIndex++; // skip ']'
        
        jsonNode.Nodes = nodes.ToArray();
        
        return jsonNode;
    }
    
    private static JsonObject ParseObject(ref Iterator iterator, IJsonNode? parent)
    {
        var nodes = new Dictionary<string, IJsonNode>();
        iterator.CurrentIndex++; // skip '{'
        
        var jsonNode = new JsonObject(nodes, parent);
        
        while (iterator.CurrentToken.Type != TokenType.RBrace)
        {
            var key = ParseString(ref iterator, jsonNode);
            iterator.CurrentIndex++; // skip ':'
            var value = Parse(ref iterator, jsonNode);
            nodes.Add(key.Literal, value);
            if (iterator.CurrentToken.Type == TokenType.RBrace) break;
            iterator.CurrentIndex++; // skip ','
        }
        
        iterator.CurrentIndex++; // skip '}'
        
        return jsonNode;
    }
    
    public struct Iterator(Token[] tokens)
    {
        public int CurrentIndex;
        public Token CurrentToken => tokens.Length > CurrentIndex ? tokens[CurrentIndex] : new Token(TokenType.Illegal, "");
        public Token NextToken => tokens.Length > CurrentIndex + 1 ? tokens[CurrentIndex + 1] : new Token(TokenType.Illegal, "");
    }
}
