using System;
using System.Collections.Generic;
using System.Linq;
using mooresmaster.Generator.Common;

namespace mooresmaster.Generator.JsonSchema;

public static class SwitchPathParser
{
    public static Falliable<SwitchPath> Parse(string path)
    {
        List<ISwitchPathElement> elements = [];
        var typeResult = GetType(path);
        if (!typeResult.IsValid)
            return Falliable<SwitchPath>.Failure();

        var type = typeResult.Value!.Value;
        path = GetTopPathExcludedPath(path, type);

        var currentPosition = 0;

        while (true)
        {
            var (currentChar, nextChar) = GetCurrentAndNextChar(path, currentPosition);
            if (currentChar == '\0') break;

            switch (currentChar)
            {
                case '.': // ../ parentPath
                    if (nextChar == '.')
                    {
                        currentPosition++;
                        (currentChar, nextChar) = GetCurrentAndNextChar(path, currentPosition);

                        if (nextChar == '/')
                        {
                            elements.Add(new ParentSwitchPathElement());
                            currentPosition++;
                        }
                    }

                    break;
                default:
                    if (IsChar(currentChar))
                    {
                        List<char> chars = [];

                        while (IsChar(currentChar))
                        {
                            chars.Add(currentChar);
                            currentPosition++;
                            (currentChar, nextChar) = GetCurrentAndNextChar(path, currentPosition);
                        }

                        elements.Add(new NormalSwitchPathElement(new string(chars.ToArray())));
                    }
                    else
                    {
                        return Falliable<SwitchPath>.Failure();
                    }

                    break;
            }

            currentPosition++;
        }

        return Falliable<SwitchPath>.Success(new SwitchPath(elements.ToArray(), type));
    }

    private static bool IsChar(char c)
    {
        return c != '.' && c != '/' && c != '\0';
    }

    private static (char currentChar, char nextChar) GetCurrentAndNextChar(string path, int currentPosition)
    {
        var currentChar = currentPosition < path.Length ? path[currentPosition] : (char)0;
        var nextChar = currentPosition + 1 < path.Length ? path[currentPosition + 1] : (char)0;

        return (currentChar, nextChar);
    }

    private static Falliable<SwitchPathType?> GetType(string path)
    {
        if (path.StartsWith("/")) return Falliable<SwitchPathType?>.Success(SwitchPathType.Absolute);

        if (path.StartsWith("./")) return Falliable<SwitchPathType?>.Success(SwitchPathType.Relative);

        return Falliable<SwitchPathType?>.Failure();
    }

    private static string GetTopPathExcludedPath(string path, SwitchPathType type)
    {
        var newPath = path.Substring(type == SwitchPathType.Absolute ? 1 : 2);
        return newPath;
    }
}

public class SwitchPath(ISwitchPathElement[] Elements, SwitchPathType Type) : IEquatable<SwitchPath>
{
    public readonly SwitchPathType Type = Type;
    public ISwitchPathElement[] Elements = Elements;
    
    public bool Equals(SwitchPath? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Type == other.Type && Elements.SequenceEqual(other.Elements);
    }
    
    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((SwitchPath)obj);
    }
    
    public override int GetHashCode()
    {
        unchecked
        {
            return ((int)Type * 397) ^ Elements.GetHashCode();
        }
    }
}

public interface ISwitchPathElement;

public enum SwitchPathType
{
    Absolute,
    Relative
}

public record struct NormalSwitchPathElement(string Path) : ISwitchPathElement
{
    public string Path = Path;
}

public record struct ParentSwitchPathElement : ISwitchPathElement;