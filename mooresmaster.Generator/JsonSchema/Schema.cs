using System;
using System.Collections.Generic;
using mooresmaster.Generator.Json;
using mooresmaster.Generator.NameResolve;
using UnitGenerator;

namespace mooresmaster.Generator.JsonSchema;

public class SchemaTable
{
    public readonly Dictionary<SchemaId, ISchema> Table = new();

    public SchemaId Add(ISchema schema)
    {
        var id = SchemaId.New();
        Table.Add(id, schema);
        return id;
    }

    public void Add(SchemaId id, ISchema schema)
    {
        Table.Add(id, schema);
    }
}

public interface ISchema
{
    string? PropertyName { get; }
    bool IsNullable { get; }
    SchemaId? Parent { get; }
}

public record Schema(string SchemaId, SchemaId InnerSchema)
{
    public SchemaId InnerSchema = InnerSchema;
    public string SchemaId = SchemaId;
}

public record ObjectSchema(string? PropertyName, SchemaId? Parent, Dictionary<string, SchemaId> Properties, string[] Required, bool IsNullable) : ISchema
{
    public Dictionary<string, SchemaId> Properties = Properties;
    public string[] Required = Required;
    public string? PropertyName { get; } = PropertyName;
    public bool IsNullable { get; } = IsNullable;
    public SchemaId? Parent { get; } = Parent;
}

public record ArraySchema(string? PropertyName, SchemaId? Parent, SchemaId Items, JsonString? Pattern, JsonString? OverrideCodeGeneratePropertyName, bool IsNullable) : ISchema
{
    public SchemaId Items = Items;
    public JsonString? OverrideCodeGeneratePropertyName = OverrideCodeGeneratePropertyName;
    public JsonString? Pattern = Pattern;
    public bool IsNullable { get; } = IsNullable;
    public SchemaId? Parent { get; } = Parent;
    public string? PropertyName { get; } = PropertyName;
}

public static class ArraySchemaExtension
{
    public static string GetPropertyName(this ArraySchema arraySchema)
    {
        if (arraySchema.OverrideCodeGeneratePropertyName != null) return arraySchema.OverrideCodeGeneratePropertyName.Literal;

        return $"{arraySchema.PropertyName!}Element";
    }
}

public record OneOfSchema(string? PropertyName, SchemaId? Parent, IfThenSchema[] IfThenArray, bool IsNullable) : ISchema
{
    public IfThenSchema[] IfThenArray = IfThenArray;
    public string? PropertyName { get; } = PropertyName;
    public bool IsNullable { get; } = IsNullable;
    public SchemaId? Parent { get; } = Parent;
}

public record IfThenSchema(JsonObject If, SchemaId Then)
{
    public JsonObject If = If;
    public SchemaId Then = Then;
}

public record StringSchema(string? PropertyName, SchemaId? Parent, JsonString? Format, bool IsNullable, string[]? Enums) : ISchema
{
    public string[]? Enums = Enums;
    public JsonString? Format = Format;
    public string? PropertyName { get; } = PropertyName;
    public bool IsNullable { get; } = IsNullable;
    public SchemaId? Parent { get; } = Parent;
}

public record NumberSchema(string? PropertyName, SchemaId? Parent, bool IsNullable) : ISchema
{
    public string? PropertyName { get; } = PropertyName;
    public bool IsNullable { get; } = IsNullable;
    public SchemaId? Parent { get; } = Parent;
}

public record IntegerSchema(string? PropertyName, SchemaId? Parent, bool IsNullable) : ISchema
{
    public string? PropertyName { get; } = PropertyName;
    public bool IsNullable { get; } = IsNullable;
    public SchemaId? Parent { get; } = Parent;
}

public record BooleanSchema(string? PropertyName, SchemaId? Parent, bool IsNullable) : ISchema
{
    public string? PropertyName { get; } = PropertyName;
    public bool IsNullable { get; } = IsNullable;
    public SchemaId? Parent { get; } = Parent;
}

public record RefSchema(string? PropertyName, SchemaId? Parent, string Ref, bool IsNullable) : ISchema
{
    public string Ref = Ref;
    public string? PropertyName { get; } = PropertyName;
    public bool IsNullable { get; } = IsNullable;
    public SchemaId? Parent { get; } = Parent;

    public TypeName GetRefName()
    {
        return new TypeName(Ref, $"{Ref}Module");
    }
}

[UnitOf(typeof(Guid))]
public readonly partial struct SchemaId;
