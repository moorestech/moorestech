using System;
using System.Collections.Generic;
using System.Linq;
using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.JsonSchema;

public static class JsonSchemaParser
{
    public static Schema ParseSchema(JsonObject root, SchemaTable schemaTable)
    {
        var id = (root["id"] as JsonString)!.Literal;
        var defineInterfaces = ParseDefineInterfaces(root, schemaTable);
        return new Schema(id, Parse(root, null, schemaTable), defineInterfaces);
    }

    private static DefineInterface[] ParseDefineInterfaces(JsonObject root, SchemaTable schemaTable)
    {
        if (!root.Nodes.ContainsKey("defineInterface")) return [];

        List<DefineInterface> interfaces = new();
        var defineJsons = root["defineInterface"] as JsonArray;

        foreach (var defineJsonNode in defineJsons!.Nodes)
        {
            var defineJson = defineJsonNode as JsonObject ?? throw new InvalidOperationException();

            interfaces.Add(ParseDefineInterface(defineJson, schemaTable));
        }

        return interfaces.ToArray();
    }

    private static DefineInterface ParseDefineInterface(JsonObject node, SchemaTable schemaTable)
    {
        var interfaceName = (node["interfaceName"] as JsonString)?.Literal ?? throw new InvalidOperationException();

        var properties = new Dictionary<string, IDefineInterfacePropertySchema>();

        var propertiesNode = node.Nodes["properties"] as JsonObject;
        foreach (var propertyNode in propertiesNode.Nodes)
        {
            var schemaId = Parse(propertyNode.Value as JsonObject, null, schemaTable);
            properties[propertyNode.Key] = schemaTable.Table[schemaId] as IDefineInterfacePropertySchema;
        }

        // interfaceの継承情報を取得
        var implementationInterfaces = new List<string>();
        if (node.Nodes.TryGetValue("implementationInterface", out var implementationInterfacesNode) && implementationInterfacesNode is JsonArray nodesArray)
            foreach (var implementationInterfaceNode in nodesArray.Nodes)
            {
                var name = (JsonString)implementationInterfaceNode;
                implementationInterfaces.Add(name.Literal);
            }

        return new DefineInterface(interfaceName, properties, implementationInterfaces.ToArray());
    }

    private static SchemaId Parse(JsonObject root, SchemaId? parent, SchemaTable schemaTable)
    {
        if (root.Nodes.ContainsKey("switch")) return ParseSwitch(root, parent, schemaTable);
        if (root.Nodes.ContainsKey("ref")) return ParseRef(root, parent, schemaTable);
        var type = (root["type"] as JsonString)!.Literal;
        return type switch
        {
            "object" => ParseObject(root, parent, schemaTable),
            "array" => ParseArray(root, parent, schemaTable),
            "string" => ParseString(root, parent, schemaTable),
            "number" => ParseNumber(root, parent, schemaTable),
            "integer" => ParseInteger(root, parent, schemaTable),
            "boolean" => ParseBoolean(root, parent, schemaTable),
            "uuid" => ParseUUID(root, parent, schemaTable),
            "vector2" => ParseVector2(root, parent, schemaTable),
            "vector3" => ParseVector3(root, parent, schemaTable),
            "vector4" => ParseVector4(root, parent, schemaTable),
            "vector2Int" => ParseVector2Int(root, parent, schemaTable),
            "vector3Int" => ParseVector3Int(root, parent, schemaTable),
            _ => throw new Exception($"Unknown type: {type}")
        };
    }

    private static SchemaId ParseObject(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        var interfaceImplementations = new List<string>();
        if (json.Nodes.TryGetValue("implementationInterface", out var node) && node is JsonArray array)
            foreach (var implementation in array.Nodes)
                if (implementation is JsonString name)
                    interfaceImplementations.Add(name.Literal);

        if (!json.Nodes.ContainsKey("properties")) return table.Add(new ObjectSchema(json.PropertyName, parent, new Dictionary<string, SchemaId>(), [], IsNullable(json), interfaceImplementations.ToArray()));

        var propertiesJson = (json["properties"] as JsonArray)!;
        var requiredJson = json["required"] as JsonArray;
        var required = requiredJson is null ? [] : requiredJson.Nodes.OfType<JsonString>().Select(str => str.Literal).ToArray();
        var objectSchemaId = SchemaId.New();

        Dictionary<string, SchemaId> properties = [];
        foreach (var propertyNode in propertiesJson.Nodes.OfType<JsonObject>())
        {
            var key = propertyNode.Nodes["key"] as JsonString;
            var value = propertyNode;
            var schemaId = Parse(value, objectSchemaId, table);
            properties.Add(key.Literal, schemaId);
        }

        table.Add(objectSchemaId, new ObjectSchema(json.PropertyName, parent, properties, required, IsNullable(json), interfaceImplementations.ToArray()));

        return objectSchemaId;
    }

    private static SchemaId ParseArray(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        var overrideCodeGeneratePropertyName = json["overrideCodeGeneratePropertyName"] as JsonString;
        var arraySchemaId = SchemaId.New();
        var items = Parse((json["items"] as JsonObject)!, arraySchemaId, table);
        table.Add(arraySchemaId, new ArraySchema(json.PropertyName, parent, items, overrideCodeGeneratePropertyName, IsNullable(json)));
        return arraySchemaId;
    }

    private static SchemaId ParseSwitch(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        var ifThenList = new List<IfThenSchema>();
        var schemaId = SchemaId.New();

        var switchReferencePath = (json["switch"] as JsonString)!;

        foreach (var node in (json["cases"] as JsonArray)!.Nodes)
        {
            var jsonObject = (node as JsonObject)!;
            var whenJson = (JsonString)jsonObject["when"];
            var thenJson = jsonObject;

            ifThenList.Add(new IfThenSchema(switchReferencePath, whenJson, Parse(thenJson, schemaId, table)));
        }

        table.Add(schemaId, new SwitchSchema(json.PropertyName, parent, ifThenList.ToArray(), IsNullable(json)));
        return schemaId;
    }

    private static SchemaId ParseRef(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        var refJson = json["ref"] as JsonString;
        return table.Add(new RefSchema(json.PropertyName, parent, refJson.Literal, IsNullable(json)));
    }

    private static SchemaId ParseString(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        var enumJson = json["enum"];
        List<string>? enums = null;
        if (enumJson is JsonArray enumArray)
        {
            enums = new List<string>();
            foreach (var enumNode in enumArray.Nodes)
            {
                if (enumNode is not JsonString enumString)
                    throw new Exception("Enum must be an array of strings");

                enums.Add(enumString.Literal);
            }
        }

        return table.Add(new StringSchema(json.PropertyName, parent, IsNullable(json), enums?.ToArray()));
    }

    private static SchemaId ParseNumber(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        return table.Add(new NumberSchema(json.PropertyName, parent, IsNullable(json)));
    }

    private static SchemaId ParseInteger(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        return table.Add(new IntegerSchema(json.PropertyName, parent, IsNullable(json)));
    }

    private static SchemaId ParseBoolean(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        return table.Add(new BooleanSchema(json.PropertyName, parent, IsNullable(json)));
    }

    private static bool IsNullable(JsonObject json)
    {
        return json["optional"] is JsonBoolean { Literal: true };
    }

    private static SchemaId ParseUUID(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        return table.Add(new UUIDSchema(json.PropertyName, parent, IsNullable(json)));
    }

    private static SchemaId ParseVector2(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        return table.Add(new Vector2Schema(json.PropertyName, parent, IsNullable(json)));
    }

    private static SchemaId ParseVector3(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        return table.Add(new Vector3Schema(json.PropertyName, parent, IsNullable(json)));
    }

    private static SchemaId ParseVector4(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        return table.Add(new Vector4Schema(json.PropertyName, parent, IsNullable(json)));
    }

    private static SchemaId ParseVector2Int(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        return table.Add(new Vector2IntSchema(json.PropertyName, parent, IsNullable(json)));
    }

    private static SchemaId ParseVector3Int(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        return table.Add(new Vector3IntSchema(json.PropertyName, parent, IsNullable(json)));
    }
}
