using System.Collections.Generic;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace mooresmaster.Generator.Json;

public struct Location
{
    public long StartLine;
    public long StartColumn;
    public long EndLine;
    public long EndColumn;
    
    private static Location Create(Mark start, Mark end)
    {
        return new Location
        {
            StartLine = start.Line,
            StartColumn = start.Column,
            EndLine = end.Line,
            EndColumn = end.Column
        };
    }
    
    public static Location Create(YamlNode yamlNode)
    {
        return Create(yamlNode.Start, yamlNode.End);
    }
    
    public override string ToString()
    {
        return $"({StartLine}:{StartColumn} - {EndLine}:{EndColumn})";
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
