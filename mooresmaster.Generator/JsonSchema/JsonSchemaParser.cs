using System;
using System.Collections.Generic;
using System.Linq;
using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.JsonSchema;

public static class JsonSchemaParser
{
    public static Schema ParseSchema(JsonObject root, SchemaTable schemaTable)
    {
        var id = (root["$id"] as JsonString)!.Literal;
        return new Schema(id, Parse(root, null, schemaTable));
    }

    private static Guid Parse(JsonObject root, Guid? parent, SchemaTable schemaTable)
    {
        if (root.Nodes.ContainsKey("oneOf")) return ParseOneOf(root, parent, schemaTable);
        if (root.Nodes.ContainsKey("$ref")) return ParseRef((root["$ref"] as JsonString)!, parent, schemaTable);
        var type = (root["type"] as JsonString)!.Literal;
        return type switch
        {
            "object" => ParseObject(root, parent, schemaTable),
            "array" => ParseArray(root, parent, schemaTable),
            "string" => ParseString(root, parent, schemaTable),
            "number" => ParseNumber(root, parent, schemaTable),
            "integer" => ParseInteger(root, parent, schemaTable),
            "boolean" => ParseBoolean(root, parent, schemaTable),
            _ => throw new Exception($"Unknown type: {type}")
        };
    }

    private static Guid ParseObject(JsonObject json, Guid? parent, SchemaTable table)
    {
        if (!json.Nodes.ContainsKey("properties")) return table.Add(new ObjectSchema(json.PropertyName, parent, new Dictionary<string, Guid>(), []));

        var propertiesJson = (json["properties"] as JsonObject)!;
        var requiredJson = json["required"] as JsonArray;
        var required = requiredJson is null ? [] : requiredJson.Nodes.OfType<JsonString>().Select(str => str.Literal).ToArray();
        var objectSchemaId = Guid.NewGuid();
        var properties = propertiesJson.Nodes
            .Select(kvp => (kvp.Key, Parse((kvp.Value as JsonObject)!, objectSchemaId, table)))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Item2);

        table.Add(objectSchemaId, new ObjectSchema(json.PropertyName, parent, properties, required));

        return objectSchemaId;
    }

    private static Guid ParseArray(JsonObject json, Guid? parent, SchemaTable table)
    {
        var pattern = json["pattern"] as JsonString;
        var arraySchemaId = Guid.NewGuid();
        var items = Parse((json["items"] as JsonObject)!, arraySchemaId, table);
        table.Add(arraySchemaId, new ArraySchema(json.PropertyName, parent, items, pattern));
        return arraySchemaId;
    }

    private static Guid ParseOneOf(JsonObject json, Guid? parent, SchemaTable table)
    {
        var ifThenList = new List<IfThenSchema>();
        var schemaId = Guid.NewGuid();

        foreach (var node in (json["oneOf"] as JsonArray)!.Nodes)
        {
            var jsonObject = (node as JsonObject)!;
            var ifJson = (jsonObject["if"] as JsonObject)!;
            var thenJson = (jsonObject["then"] as JsonObject)!;

            ifThenList.Add(new IfThenSchema(ifJson, Parse(thenJson, schemaId, table)));
        }

        table.Add(schemaId, new OneOfSchema(json.PropertyName, parent, ifThenList.ToArray()));
        return schemaId;
    }

    private static Guid ParseRef(JsonString json, Guid? parent, SchemaTable table)
    {
        return table.Add(new RefSchema(json.PropertyName, parent, json.Literal));
    }

    private static Guid ParseString(JsonObject json, Guid? parent, SchemaTable table)
    {
        var format = json["format"] as JsonString;
        return table.Add(new StringSchema(json.PropertyName, parent, format));
    }

    private static Guid ParseNumber(JsonObject json, Guid? parent, SchemaTable table)
    {
        return table.Add(new NumberSchema(json.PropertyName, parent));
    }

    private static Guid ParseInteger(JsonObject json, Guid? parent, SchemaTable table)
    {
        return table.Add(new IntegerSchema(json.PropertyName, parent));
    }

    private static Guid ParseBoolean(JsonObject json, Guid? parent, SchemaTable table)
    {
        return table.Add(new BooleanSchema(json.PropertyName, parent));
    }
}
