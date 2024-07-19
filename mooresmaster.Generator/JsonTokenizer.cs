using System;
using System.Collections.Generic;

namespace mooresmaster.Generator;

public record struct Token(TokenType Type, string Literal);

public enum TokenType
{
    String,
    Colon,
    RBrace,
    LBrace,
    RSquare,
    LSquare,
    Comma
}

public struct Iterator(string sourceText)
{
    public int CurrentIndex = 0;
    public readonly string SourceText = sourceText;
    
    public char CurrentChar => SourceText.Length > CurrentIndex ? SourceText[CurrentIndex] : '\0';
    public char NextChar => SourceText.Length > CurrentIndex + 1 ? SourceText[CurrentIndex + 1] : '\0';
}

public static class JsonTokenizer
{
    public static Token[] GetTokens(string json)
    {
        var tokens = new List<Token>();
        
        var iterator = new Iterator(json);
        
        while (json.Length > iterator.CurrentIndex)
        {
            // skip whitespace
            while (char.IsWhiteSpace(iterator.CurrentChar))
                iterator.CurrentIndex++;
            
            // tokenize
            switch (iterator.CurrentChar)
            {
                case ':':
                    tokens.Add(new Token(TokenType.Colon, ":"));
                    break;
                case '{':
                    tokens.Add(new Token(TokenType.LBrace, "{"));
                    break;
                case '}':
                    tokens.Add(new Token(TokenType.RBrace, "}"));
                    break;
                case '[':
                    tokens.Add(new Token(TokenType.LSquare, "["));
                    break;
                case ']':
                    tokens.Add(new Token(TokenType.RSquare, "]"));
                    break;
                case ',':
                    tokens.Add(new Token(TokenType.Comma, ","));
                    break;
                case '"':
                    var literal = "";
                    
                    while (iterator.NextChar != '"')
                    {
                        iterator.CurrentIndex++;
                        if (iterator.CurrentChar == '\\')
                            throw new Exception("not implemented \\");
                        literal += iterator.CurrentChar;
                    }
                    
                    iterator.CurrentIndex++;
                    
                    tokens.Add(new Token(TokenType.String, literal));
                    break;
                default:
                    throw new Exception($"not implemented: {iterator.CurrentChar} {iterator.NextChar}");
            }
            
            iterator.CurrentIndex++;
        }
        
        return tokens.ToArray();
    }
}
