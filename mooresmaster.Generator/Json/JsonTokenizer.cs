using System;
using System.Collections.Generic;
using System.Linq;

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
    
    True,
    False,
    
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
            while (new[] { ' ', '\t', '\n', '\r' }.Contains(iterator.CurrentChar))
                iterator.CurrentIndex++;
            
            // end
            if (iterator.CurrentChar == '\0') break;
            
            // tokenize
            Console.WriteLine(iterator.CurrentChar);
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
                case '/':
                    if (iterator.NextChar != '/') throw new Exception("not implemented");
                    
                    // skip comment
                    iterator.CurrentIndex++;
                    while (iterator.NextChar != '\n') iterator.CurrentIndex++;
                    
                    break;
                default:
                    var identifier = "" + iterator.CurrentChar;
                    while (char.IsLetter(iterator.NextChar))
                    {
                        iterator.CurrentIndex++;
                        identifier += iterator.CurrentChar;
                    }
                    
                    switch (identifier)
                    {
                        case "true":
                            tokens.Add(new Token(TokenType.True, "true"));
                            break;
                        case "false":
                            tokens.Add(new Token(TokenType.False, "false"));
                            break;
                        default:
                            throw new Exception($"not implemented: \"{identifier}\"");
                    }
                    
                    break;
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
