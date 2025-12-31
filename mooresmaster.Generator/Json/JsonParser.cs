using System.Collections.Generic;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace mooresmaster.Generator.Json;

public readonly struct Location(string filePath, long startLine, long startColumn, long endLine, long endColumn)
{
    public readonly string FilePath = filePath;
    public readonly long StartLine = startLine;
    public readonly long StartColumn = startColumn;
    public readonly long EndLine = endLine;
    public readonly long EndColumn = endColumn;
    
    public static Location Create(string filePath, Mark start, Mark end)
    {
        return new Location
            (filePath, start.Line, start.Column, end.Line, end.Column);
    }
    
    public static Location Create(string filePath, YamlNode yamlNode)
    {
        return Create(filePath, yamlNode.Start, yamlNode.End);
    }
    
    public override string ToString()
    {
        return $"({StartLine}:{StartColumn} - {EndLine}:{EndColumn}) in {FilePath}";
    }
}

public interface IJsonNode
{
    IJsonNode? Parent { get; }
    string? PropertyName { get; }
    Location Location { get; }
}

public record JsonObject(Dictionary<string, IJsonNode> Nodes, IJsonNode? Parent, string? PropertyName, Location Location) : IJsonNode
{
    public readonly Dictionary<string, IJsonNode> Nodes = Nodes;
    public IJsonNode? this[string key] => Nodes.ContainsKey(key) ? Nodes[key] : null;
    public IJsonNode? Parent { get; } = Parent;
    public string? PropertyName { get; } = PropertyName;
    public Location Location { get; } = Location;
}

public record JsonArray(IJsonNode[] Nodes, IJsonNode? Parent, string? PropertyName, Location Location) : IJsonNode
{
    public IJsonNode[] Nodes = Nodes;
    public IJsonNode? this[int index] => Nodes.Length > index ? Nodes[index] : null;
    public IJsonNode? Parent { get; } = Parent;
    public string? PropertyName { get; } = PropertyName;
    public Location Location { get; } = Location;
}

public record JsonString(string Literal, IJsonNode? Parent, string? PropertyName, Location Location) : IJsonNode
{
    public readonly string Literal = Literal;
    public string? PropertyName { get; } = PropertyName;
    public Location Location { get; } = Location;
    public IJsonNode? Parent { get; } = Parent;
}

public record JsonBoolean(bool Literal, IJsonNode? Parent, string? PropertyName, Location Location) : IJsonNode
{
    public readonly bool Literal = Literal;
    public IJsonNode? Parent { get; } = Parent;
    public string? PropertyName { get; } = PropertyName;
    public Location Location { get; } = Location;
}

public record JsonNumber(double Literal, IJsonNode? Parent, string? PropertyName, Location Location) : IJsonNode
{
    public readonly double Literal = Literal;
    public IJsonNode? Parent { get; } = Parent;
    public string? PropertyName { get; } = PropertyName;
    public Location Location { get; } = Location;
}

public record JsonInt(long Literal, IJsonNode? Parent, string? PropertyName, Location Location) : IJsonNode
{
    public readonly long Literal = Literal;
    public IJsonNode? Parent { get; } = Parent;
    public string? PropertyName { get; } = PropertyName;
    public Location Location { get; } = Location;
}