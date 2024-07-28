using System;
using System.Collections.Generic;
using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.JsonSchema;

public class SchemaTable
{
    public readonly Dictionary<Guid, ISchema> Table = new();
    
    public Guid Add(ISchema schema)
    {
        var id = Guid.NewGuid();
        Table.Add(id, schema);
        return id;
    }
    
    public void Add(Guid id, ISchema schema)
    {
        Table.Add(id, schema);
    }
}

public interface ISchema
{
    string? PropertyName { get; }
    Guid? Parent { get; }
}

public record Schema(string SchemaId, Guid InnerSchema)
{
    public Guid InnerSchema = InnerSchema;
    public string SchemaId = SchemaId;
}

public record ObjectSchema(string? PropertyName, Guid? Parent, Dictionary<string, Guid> Properties, string[] Required) : ISchema
{
    public Dictionary<string, Guid> Properties = Properties;
    public string[] Required = Required;
    public string? PropertyName { get; } = PropertyName;
    public Guid? Parent { get; } = Parent;
}

public record ArraySchema(string? PropertyName, Guid? Parent, Guid Items, JsonString? Pattern) : ISchema
{
    public Guid Items = Items;
    public JsonString? Pattern = Pattern;
    public string? PropertyName { get; } = PropertyName;
    public Guid? Parent { get; } = Parent;
}

public record OneOfSchema(string? PropertyName, Guid? Parent, IfThenSchema[] IfThenArray) : ISchema
{
    public IfThenSchema[] IfThenArray = IfThenArray;
    public string? PropertyName { get; } = PropertyName;
    public Guid? Parent { get; } = Parent;
}

public record IfThenSchema(JsonObject If, Guid Then)
{
    public JsonObject If = If;
    public Guid Then = Then;
}

public record StringSchema(string? PropertyName, Guid? Parent, JsonString? Format) : ISchema
{
    public JsonString? Format = Format;
    public string? PropertyName { get; } = PropertyName;
    public Guid? Parent { get; } = Parent;
}

public record NumberSchema(string? PropertyName, Guid? Parent) : ISchema
{
    public string? PropertyName { get; } = PropertyName;
    public Guid? Parent { get; } = Parent;
}

public record IntegerSchema(string? PropertyName, Guid? Parent) : ISchema
{
    public string? PropertyName { get; } = PropertyName;
    public Guid? Parent { get; } = Parent;
}

public record BooleanSchema(string? PropertyName, Guid? Parent) : ISchema
{
    public string? PropertyName { get; } = PropertyName;
    public Guid? Parent { get; } = Parent;
}

public record RefSchema(string? PropertyName, Guid? Parent, string Ref) : ISchema
{
    public string Ref = Ref;
    public string? PropertyName { get; } = PropertyName;
    public Guid? Parent { get; } = Parent;
}
