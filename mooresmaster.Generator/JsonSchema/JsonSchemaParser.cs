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
        var defineInterfaces = ParseDefineInterfaces(id, root, schemaTable);
        return new Schema(id, Parse(root, null, false, schemaTable), defineInterfaces);
    }
    
    private static DefineInterface[] ParseDefineInterfaces(string id, JsonObject root, SchemaTable schemaTable)
    {
        var localDefineInterfaces = ParseLocalDefineInterfaces(id, root, schemaTable);
        var globalDefineInterfaces = ParseGlobalDefineInterfaces(id, root, schemaTable);
        
        return localDefineInterfaces.Concat(globalDefineInterfaces).ToArray();
    }
    
    private static DefineInterface[] ParseLocalDefineInterfaces(string id, JsonObject root, SchemaTable schemaTable)
    {
        if (!root.Nodes.ContainsKey(Tokens.DefineInterface)) return [];
        
        List<DefineInterface> interfaces = new();
        var defineJsons = root[Tokens.DefineInterface] as JsonArray;
        
        foreach (var defineJsonNode in defineJsons!.Nodes)
        {
            var defineJson = defineJsonNode as JsonObject ?? throw new InvalidOperationException();
            
            interfaces.Add(ParseDefineInterface(id, defineJson, schemaTable, false));
        }
        
        return interfaces.ToArray();
    }
    
    private static DefineInterface[] ParseGlobalDefineInterfaces(string id, JsonObject root, SchemaTable schemaTable)
    {
        if (!root.Nodes.ContainsKey(Tokens.GlobalDefineInterface)) return [];
        
        var interfaces = new List<DefineInterface>();
        var defineJsons = root[Tokens.GlobalDefineInterface] as JsonArray;
        
        foreach (var defineJsonNode in defineJsons!.Nodes)
        {
            var defineJson = defineJsonNode as JsonObject ?? throw new InvalidOperationException();
            
            interfaces.Add(ParseDefineInterface(id, defineJson, schemaTable, true));
        }
        
        return interfaces.ToArray();
    }
    
    private static DefineInterface ParseDefineInterface(string id, JsonObject node, SchemaTable schemaTable, bool isGlobal)
    {
        var interfaceNameNode = node[Tokens.InterfaceNameKey] as JsonString ?? throw new InvalidOperationException();
        var interfaceName = interfaceNameNode.Literal;

        var properties = new Dictionary<string, IDefineInterfacePropertySchema>();

        if (node.Nodes.TryGetValue(Tokens.PropertiesKey, out var propertiesNode))
        {
            var propertiesArray = propertiesNode as JsonArray;
            foreach (var propertyNode in propertiesArray.Nodes.OfType<JsonObject>())
            {
                // valueがtypeとかdefaultとか
                // keyがプロパティ名

                var propertySchemaId = Parse(propertyNode, null, true, schemaTable);
                var key = (propertyNode[Tokens.PropertyNameKey] as JsonString)!;
                if (schemaTable.Table[propertySchemaId] is IDefineInterfacePropertySchema propertySchema)
                    properties[key.Literal] = propertySchema;
                else throw new InvalidOperationException();
            }
        }

        // interfaceの継承情報を取得
        var implementationNodes = new Dictionary<string, JsonString>();
        if (node.Nodes.TryGetValue(Tokens.ImplementationInterfaceKey, out var implementationInterfacesNode) && implementationInterfacesNode is JsonArray nodesArray)
            foreach (var implementationInterfaceNode in nodesArray.Nodes)
            {
                var name = (JsonString)implementationInterfaceNode;
                implementationNodes[name.Literal] = name;
            }

        if (interfaceName == null) throw new Exception("interfaceName is null");
        if (properties == null) throw new Exception("properties is null");

        var defineInterface = new DefineInterface(
            id,
            interfaceName,
            properties,
            implementationNodes.Keys.ToArray(),
            implementationNodes,
            isGlobal,
            interfaceNameNode.Location
        );

        return defineInterface;
    }
    
    private static SchemaId Parse(JsonObject root, SchemaId? parent, bool isInterfaceProperty, SchemaTable schemaTable)
    {
        if (root.Nodes.ContainsKey(Tokens.SwitchKey)) return ParseSwitch(root, parent, isInterfaceProperty, schemaTable);
        if (root.Nodes.ContainsKey(Tokens.RefKey)) return ParseRef(root, parent, isInterfaceProperty, schemaTable);
        var type = (root[Tokens.TypeKey] as JsonString)!.Literal;
        return type switch
        {
            Tokens.ObjectType => ParseObject(root, parent, isInterfaceProperty, schemaTable),
            Tokens.ArrayType => ParseArray(root, parent, isInterfaceProperty, schemaTable),
            Tokens.StringType => ParseString(root, parent, isInterfaceProperty, schemaTable),
            Tokens.EnumType => ParseEnum(root, parent, isInterfaceProperty, schemaTable),
            Tokens.NumberType => ParseNumber(root, parent, isInterfaceProperty, schemaTable),
            Tokens.IntegerType => ParseInteger(root, parent, isInterfaceProperty, schemaTable),
            Tokens.BooleanType => ParseBoolean(root, parent, isInterfaceProperty, schemaTable),
            Tokens.UuidType => ParseUuid(root, parent, isInterfaceProperty, schemaTable),
            Tokens.Vector2Type => ParseVector2(root, parent, isInterfaceProperty, schemaTable),
            Tokens.Vector3Type => ParseVector3(root, parent, isInterfaceProperty, schemaTable),
            Tokens.Vector4Type => ParseVector4(root, parent, isInterfaceProperty, schemaTable),
            Tokens.Vector2IntType => ParseVector2Int(root, parent, isInterfaceProperty, schemaTable),
            Tokens.Vector3IntType => ParseVector3Int(root, parent, isInterfaceProperty, schemaTable),
            _ => throw new Exception($"Unknown type: {type}")
        };
    }
    
    private static SchemaId ParseObject(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table)
    {
        var interfaceImplementations = new List<string>();
        var implementationNodes = new Dictionary<string, JsonString>();
        if (json.Nodes.TryGetValue(Tokens.ImplementationInterfaceKey, out var node) && node is JsonArray array)
            foreach (var implementation in array.Nodes)
                if (implementation is JsonString name)
                {
                    interfaceImplementations.Add(name.Literal);
                    implementationNodes[name.Literal] = name;
                }

        var objectName = json.Nodes.ContainsKey(Tokens.PropertyNameKey) ? (json[Tokens.PropertyNameKey] as JsonString)!.Literal : null;

        if (!json.Nodes.ContainsKey(Tokens.PropertiesKey)) return table.Add(new ObjectSchema(objectName, parent, new Dictionary<string, SchemaId>(), [], IsNullable(json), interfaceImplementations.ToArray(), implementationNodes, isInterfaceProperty));

        var propertiesJson = (json[Tokens.PropertiesKey] as JsonArray)!;
        var requiredJson = json["required"] as JsonArray;
        var required = requiredJson is null ? [] : requiredJson.Nodes.OfType<JsonString>().Select(str => str.Literal).ToArray();
        var objectSchemaId = SchemaId.New();

        Dictionary<string, SchemaId> properties = [];
        foreach (var propertyNode in propertiesJson.Nodes.OfType<JsonObject>())
        {
            var key = propertyNode.Nodes[Tokens.PropertyNameKey] as JsonString;
            var value = propertyNode;
            var schemaId = Parse(value, objectSchemaId, false, table);

            properties.Add(key?.Literal, schemaId);
        }

        table.Add(objectSchemaId, new ObjectSchema(objectName, parent, properties, required, IsNullable(json), interfaceImplementations.ToArray(), implementationNodes, isInterfaceProperty));

        return objectSchemaId;
    }
    
    private static SchemaId ParseArray(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table)
    {
        var overrideCodeGeneratePropertyName = json["overrideCodeGeneratePropertyName"] as JsonString;
        var arraySchemaId = SchemaId.New();
        var key = json[Tokens.PropertyNameKey] as JsonString;
        var items = Parse((json["items"] as JsonObject)!, arraySchemaId, false, table);
        table.Add(arraySchemaId, new ArraySchema(key?.Literal, parent, items, overrideCodeGeneratePropertyName, IsNullable(json), isInterfaceProperty));
        return arraySchemaId;
    }
    
    private static SchemaId ParseSwitch(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table)
    {
        var ifThenList = new List<SwitchCaseSchema>();
        var schemaId = SchemaId.New();

        var switchReferencePathJson = (json[Tokens.SwitchKey] as JsonString)!;

        foreach (var node in (json["cases"] as JsonArray)!.Nodes)
        {
            var jsonObject = (node as JsonObject)!;
            var whenJson = (JsonString)jsonObject["when"];
            var thenJson = jsonObject;

            var switchPath = SwitchPathParser.Parse(switchReferencePathJson.Literal);

            ifThenList.Add(new SwitchCaseSchema(switchPath, whenJson.Literal, Parse(thenJson, schemaId, false, table)));
        }

        var hasOptionalCase = ifThenList.Any(c => table.Table[c.Schema].IsNullable);

        table.Add(schemaId, new SwitchSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, ifThenList.ToArray(), IsNullable(json), hasOptionalCase, isInterfaceProperty, switchReferencePathJson.Location));
        return schemaId;
    }
    
    private static SchemaId ParseRef(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table)
    {
        var refJson = json[Tokens.RefKey] as JsonString;
        return table.Add(new RefSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, refJson.Literal, IsNullable(json), isInterfaceProperty));
    }
    
    private static SchemaId ParseString(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table)
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
        
        return table.Add(new StringSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json), enums?.ToArray(), isInterfaceProperty));
    }
    
    private static SchemaId ParseEnum(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table)
    {
        var nameJson = json[Tokens.PropertyNameKey] as JsonString;
        
        var options = new List<string>();
        
        if (json["options"] is JsonArray enumArray)
            foreach (var enumNode in enumArray.Nodes)
            {
                if (enumNode is not JsonString enumString) throw new Exception("Enum must be an array of strings");
                
                options.Add(enumString.Literal);
            }
        
        return table.Add(new StringSchema(nameJson?.Literal, parent, IsNullable(json), options.ToArray(), isInterfaceProperty));
    }
    
    private static SchemaId ParseNumber(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table)
    {
        return table.Add(new NumberSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json), isInterfaceProperty));
    }
    
    private static SchemaId ParseInteger(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table)
    {
        return table.Add(new IntegerSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json), isInterfaceProperty));
    }
    
    private static SchemaId ParseBoolean(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table)
    {
        return table.Add(new BooleanSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json), isInterfaceProperty));
    }
    
    private static bool IsNullable(JsonObject json)
    {
        return json["optional"] is JsonBoolean { Literal: true } || json["optional"] is JsonString { Literal: "true" };
    }
    
    private static SchemaId ParseUuid(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table)
    {
        return table.Add(new UUIDSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json), isInterfaceProperty));
    }
    
    private static SchemaId ParseVector2(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table)
    {
        return table.Add(new Vector2Schema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json), isInterfaceProperty));
    }
    
    private static SchemaId ParseVector3(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table)
    {
        return table.Add(new Vector3Schema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json), isInterfaceProperty));
    }
    
    private static SchemaId ParseVector4(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table)
    {
        return table.Add(new Vector4Schema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json), isInterfaceProperty));
    }
    
    private static SchemaId ParseVector2Int(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table)
    {
        return table.Add(new Vector2IntSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json), isInterfaceProperty));
    }
    
    private static SchemaId ParseVector3Int(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table)
    {
        return table.Add(new Vector3IntSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json), isInterfaceProperty));
    }
}