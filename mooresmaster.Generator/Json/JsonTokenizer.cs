using System;
using System.Collections.Generic;

namespace mooresmaster.Generator.Json;

public record struct Token(TokenType Type, string Literal);

public enum TokenType
{
    String,
    Colon,
    RBrace,
    LBrace,
    RSquare,
    LSquare,
    Comma,
    
    Illegal
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
            while (char.IsWhiteSpace(iterator.CurrentChar) || iterator.CurrentChar == '\n' || iterator.CurrentChar == '\r' || iterator.CurrentChar == '\t')
                iterator.CurrentIndex++;
            
            // end
            if (iterator.CurrentChar == '\0') break;
            
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
    
    private struct Iterator(string sourceText)
    {
        public int CurrentIndex = 0;
        
        public char CurrentChar => sourceText.Length > CurrentIndex ? sourceText[CurrentIndex] : '\0';
        public char NextChar => sourceText.Length > CurrentIndex + 1 ? sourceText[CurrentIndex + 1] : '\0';
    }
}
