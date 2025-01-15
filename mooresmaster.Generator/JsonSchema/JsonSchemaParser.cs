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
        return new Schema(id, Parse(root, null, schemaTable), defineInterfaces);
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
        var interfaceName = (node[Tokens.InterfaceNameKey] as JsonString)?.Literal ?? throw new InvalidOperationException();
        
        var properties = new Dictionary<string, IDefineInterfacePropertySchema>();
        
        var propertiesNode = node.Nodes[Tokens.PropertiesKey] as JsonArray;
        foreach (var propertyNode in propertiesNode.Nodes.OfType<JsonObject>())
        {
            // valueがtypeとかdefaultとか
            // keyがプロパティ名
            
            var propertySchemaId = Parse(propertyNode, null, schemaTable);
            var key = propertyNode[Tokens.PropertyNameKey] as JsonString;
            properties[key.Literal] = schemaTable.Table[propertySchemaId] as IDefineInterfacePropertySchema;
        }
        
        // interfaceの継承情報を取得
        var implementationInterfaces = new List<string>();
        if (node.Nodes.TryGetValue(Tokens.ImplementationInterfaceKey, out var implementationInterfacesNode) && implementationInterfacesNode is JsonArray nodesArray)
            foreach (var implementationInterfaceNode in nodesArray.Nodes)
            {
                var name = (JsonString)implementationInterfaceNode;
                implementationInterfaces.Add(name.Literal);
            }
        
        if (interfaceName == null) throw new Exception("interfaceName is null");
        if (properties == null) throw new Exception("properties is null");
        
        return new DefineInterface(id, interfaceName, properties, implementationInterfaces.ToArray(), isGlobal);
    }
    
    private static SchemaId Parse(JsonObject root, SchemaId? parent, SchemaTable schemaTable)
    {
        if (root.Nodes.ContainsKey(Tokens.SwitchKey)) return ParseSwitch(root, parent, schemaTable);
        if (root.Nodes.ContainsKey(Tokens.RefKey)) return ParseRef(root, parent, schemaTable);
        var type = (root[Tokens.TypeKey] as JsonString)!.Literal;
        return type switch
        {
            Tokens.ObjectType => ParseObject(root, parent, schemaTable),
            Tokens.ArrayType => ParseArray(root, parent, schemaTable),
            Tokens.StringType => ParseString(root, parent, schemaTable),
            Tokens.NumberType => ParseNumber(root, parent, schemaTable),
            Tokens.IntegerType => ParseInteger(root, parent, schemaTable),
            Tokens.BooleanType => ParseBoolean(root, parent, schemaTable),
            Tokens.UuidType => ParseUUID(root, parent, schemaTable),
            Tokens.Vector2Type => ParseVector2(root, parent, schemaTable),
            Tokens.Vector3Type => ParseVector3(root, parent, schemaTable),
            Tokens.Vector4Type => ParseVector4(root, parent, schemaTable),
            Tokens.Vector2IntType => ParseVector2Int(root, parent, schemaTable),
            Tokens.Vector3IntType => ParseVector3Int(root, parent, schemaTable),
            _ => throw new Exception($"Unknown type: {type}")
        };
    }
    
    private static SchemaId ParseObject(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        var interfaceImplementations = new List<string>();
        if (json.Nodes.TryGetValue(Tokens.ImplementationInterfaceKey, out var node) && node is JsonArray array)
            foreach (var implementation in array.Nodes)
                if (implementation is JsonString name)
                    interfaceImplementations.Add(name.Literal);
        
        var objectName = json.Nodes.ContainsKey(Tokens.PropertyNameKey) ? (json[Tokens.PropertyNameKey] as JsonString)!.Literal : null;
        
        if (!json.Nodes.ContainsKey(Tokens.PropertiesKey)) return table.Add(new ObjectSchema(objectName, parent, new Dictionary<string, SchemaId>(), [], IsNullable(json), interfaceImplementations.ToArray()));
        
        var propertiesJson = (json[Tokens.PropertiesKey] as JsonArray)!;
        var requiredJson = json["required"] as JsonArray;
        var required = requiredJson is null ? [] : requiredJson.Nodes.OfType<JsonString>().Select(str => str.Literal).ToArray();
        var objectSchemaId = SchemaId.New();
        
        Dictionary<string, SchemaId> properties = [];
        foreach (var propertyNode in propertiesJson.Nodes.OfType<JsonObject>())
        {
            var key = propertyNode.Nodes[Tokens.PropertyNameKey] as JsonString;
            var value = propertyNode;
            var schemaId = Parse(value, objectSchemaId, table);
            
            properties.Add(key?.Literal, schemaId);
        }
        
        table.Add(objectSchemaId, new ObjectSchema(objectName, parent, properties, required, IsNullable(json), interfaceImplementations.ToArray()));
        
        return objectSchemaId;
    }
    
    private static SchemaId ParseArray(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        var overrideCodeGeneratePropertyName = json["overrideCodeGeneratePropertyName"] as JsonString;
        var arraySchemaId = SchemaId.New();
        var key = json[Tokens.PropertyNameKey] as JsonString;
        var items = Parse((json["items"] as JsonObject)!, arraySchemaId, table);
        table.Add(arraySchemaId, new ArraySchema(key?.Literal, parent, items, overrideCodeGeneratePropertyName, IsNullable(json)));
        return arraySchemaId;
    }
    
    private static SchemaId ParseSwitch(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        var ifThenList = new List<SwitchCaseSchema>();
        var schemaId = SchemaId.New();
        
        var switchReferencePath = (json[Tokens.SwitchKey] as JsonString)!;
        
        foreach (var node in (json["cases"] as JsonArray)!.Nodes)
        {
            var jsonObject = (node as JsonObject)!;
            var whenJson = (JsonString)jsonObject["when"];
            var thenJson = jsonObject;
            
            var switchPath = SwitchPathParser.Parse(switchReferencePath.Literal);
            
            ifThenList.Add(new SwitchCaseSchema(switchPath, whenJson.Literal, Parse(thenJson, schemaId, table)));
        }
        
        table.Add(schemaId, new SwitchSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, ifThenList.ToArray(), IsNullable(json)));
        return schemaId;
    }
    
    private static SchemaId ParseRef(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        var refJson = json[Tokens.RefKey] as JsonString;
        return table.Add(new RefSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, refJson.Literal, IsNullable(json)));
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
        
        return table.Add(new StringSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json), enums?.ToArray()));
    }
    
    private static SchemaId ParseNumber(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        return table.Add(new NumberSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json)));
    }
    
    private static SchemaId ParseInteger(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        return table.Add(new IntegerSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json)));
    }
    
    private static SchemaId ParseBoolean(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        return table.Add(new BooleanSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json)));
    }
    
    private static bool IsNullable(JsonObject json)
    {
        return json["optional"] is JsonBoolean { Literal: true } || json["optional"] is JsonString { Literal: "true" };
    }
    
    private static SchemaId ParseUUID(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        return table.Add(new UUIDSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json)));
    }
    
    private static SchemaId ParseVector2(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        return table.Add(new Vector2Schema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json)));
    }
    
    private static SchemaId ParseVector3(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        return table.Add(new Vector3Schema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json)));
    }
    
    private static SchemaId ParseVector4(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        return table.Add(new Vector4Schema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json)));
    }
    
    private static SchemaId ParseVector2Int(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        return table.Add(new Vector2IntSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json)));
    }
    
    private static SchemaId ParseVector3Int(JsonObject json, SchemaId? parent, SchemaTable table)
    {
        return table.Add(new Vector3IntSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json)));
    }
}
