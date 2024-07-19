using System;
using System.Collections.Generic;
using System.Linq;
using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.JsonSchema;

public interface ISchema;

public record ObjectSchema(Dictionary<string, ISchema> Properties, string[] Required) : ISchema
{
    public Dictionary<string, ISchema> Properties = Properties;
    public string[] Required = Required;
}

public record ArraySchema(ISchema Items) : ISchema
{
    public ISchema Items = Items;
}

public record StringSchema : ISchema;

public record OneOfSchema : ISchema;

public record NumberSchema : ISchema;

public record IntegerSchema : ISchema;

public record BooleanSchema : ISchema;

public static class JsonSchemaParser
{
    public static ISchema Parse(JsonObject root)
    {
        if (root.Nodes.ContainsKey("oneOf")) return new OneOfSchema();
        var type = (root["type"] as JsonString)!.Literal;
        return type switch
        {
            "object" => ParseObject(root),
            "array" => ParseArray(root),
            "string" => ParseString(root),
            "number" => ParseNumber(root),
            "integer" => ParseInteger(root),
            "boolean" => ParseBoolean(root),
            _ => throw new Exception($"Unknown type: {type}")
        };
    }
    
    private static ObjectSchema ParseObject(JsonObject json)
    {
        if (!json.Nodes.ContainsKey("properties")) return new ObjectSchema(new Dictionary<string, ISchema>(), []);
        
        var propertiesJson = (json["properties"] as JsonObject)!;
        var requiredJson = propertiesJson["required"] as JsonArray;
        var required = requiredJson is null ? [] : requiredJson.Nodes.OfType<JsonString>().Select(str => str.Literal).ToArray();
        var properties = propertiesJson.Nodes
            .Where(node => node.Key != "required")
            .Select(kvp => (kvp.Key, Parse((kvp.Value as JsonObject)!)))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Item2);
        
        return new ObjectSchema(properties, required);
    }
    
    private static ArraySchema ParseArray(JsonObject json)
    {
        var items = Parse((json["items"] as JsonObject)!);
        return new ArraySchema(items);
    }
    
    private static StringSchema ParseString(JsonObject json)
    {
        return new StringSchema();
    }
    
    private static NumberSchema ParseNumber(JsonObject json)
    {
        return new NumberSchema();
    }
    
    private static IntegerSchema ParseInteger(JsonObject json)
    {
        return new IntegerSchema();
    }
    
    private static BooleanSchema ParseBoolean(JsonObject json)
    {
        return new BooleanSchema();
}
