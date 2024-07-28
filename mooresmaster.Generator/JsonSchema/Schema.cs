using System.Collections.Generic;
using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.JsonSchema;

public interface ISchema
{
    string? PropertyName { get; }
}

public record Schema(string SchemaId, ISchema InnerSchema)
{
    public ISchema InnerSchema = InnerSchema;
    public string SchemaId = SchemaId;
}

public record ObjectSchema(string? PropertyName, Dictionary<string, ISchema> Properties, string[] Required) : ISchema
{
    public Dictionary<string, ISchema> Properties = Properties;
    public string[] Required = Required;
    public string? PropertyName { get; } = PropertyName;
}

public record ArraySchema(string? PropertyName, ISchema Items, JsonString? Pattern) : ISchema
{
    public ISchema Items = Items;
    public JsonString? Pattern = Pattern;
    public string? PropertyName { get; } = PropertyName;
}

public record OneOfSchema(string? PropertyName, IfThenSchema[] IfThenArray) : ISchema
{
    public IfThenSchema[] IfThenArray = IfThenArray;
    public string? PropertyName { get; } = PropertyName;
}

public record IfThenSchema(JsonObject If, ISchema Then)
{
    public JsonObject If = If;
    public ISchema Then = Then;
}

public record StringSchema(string? PropertyName, JsonString? Format) : ISchema
{
    public JsonString? Format = Format;
    public string? PropertyName { get; } = PropertyName;
}

public record NumberSchema(string? PropertyName) : ISchema
{
    public string? PropertyName { get; } = PropertyName;
}

public record IntegerSchema(string? PropertyName) : ISchema
{
    public string? PropertyName { get; } = PropertyName;
}

public record BooleanSchema(string? PropertyName) : ISchema
{
    public string? PropertyName { get; } = PropertyName;
}

public record RefSchema(string? PropertyName, string Ref) : ISchema
{
    public string Ref = Ref;
    public string? PropertyName { get; } = PropertyName;
}
