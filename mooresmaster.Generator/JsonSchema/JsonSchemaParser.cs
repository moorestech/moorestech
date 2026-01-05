using System;
using System.Collections.Generic;
using System.Linq;
using mooresmaster.Generator.Analyze;
using mooresmaster.Generator.Analyze.Diagnostics;
using mooresmaster.Generator.Common;
using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.JsonSchema;

public static class JsonSchemaParser
{
    public static Falliable<Schema> ParseSchema(JsonObject root, SchemaTable schemaTable, Analysis analysis)
    {
        // idが存在しない場合はDiagnosticsを報告してFailureを返す
        if (!root.Nodes.TryGetValue("id", out var idNode) || idNode is not JsonString idString)
        {
            analysis.ReportDiagnostics(new RootIdNotFoundDiagnostics(root));
            return Falliable<Schema>.Failure();
        }

        var id = idString.Literal;
        var innerSchema = Parse(root, null, false, schemaTable, analysis);
        var defineInterfaces = ParseDefineInterfaces(id, root, schemaTable, analysis);
        return Falliable<Schema>.Success(new Schema(id, innerSchema, defineInterfaces));
    }
    
    private static DefineInterface[] ParseDefineInterfaces(string id, JsonObject root, SchemaTable schemaTable, Analysis analysis)
    {
        var localDefineInterfaces = ParseLocalDefineInterfaces(id, root, schemaTable, analysis);
        var globalDefineInterfaces = ParseGlobalDefineInterfaces(id, root, schemaTable, analysis);
        
        return localDefineInterfaces.Concat(globalDefineInterfaces).ToArray();
    }
    
    private static DefineInterface[] ParseLocalDefineInterfaces(string id, JsonObject root, SchemaTable schemaTable, Analysis analysis)
    {
        if (!root.Nodes.TryGetValue(Tokens.DefineInterface, out var defineInterfaceNode)) return [];

        if (defineInterfaceNode is not JsonArray defineJsons)
        {
            analysis.ReportDiagnostics(new DefineInterfaceNotArrayDiagnostics(Tokens.DefineInterface, defineInterfaceNode, false));
            return [];
        }

        List<DefineInterface> interfaces = new();

        foreach (var defineJsonNode in defineJsons.Nodes)
        {
            var defineJson = defineJsonNode as JsonObject ?? throw new InvalidOperationException();

            interfaces.Add(ParseDefineInterface(id, defineJson, schemaTable, false, analysis));
        }

        return interfaces.ToArray();
    }
    
    private static DefineInterface[] ParseGlobalDefineInterfaces(string id, JsonObject root, SchemaTable schemaTable, Analysis analysis)
    {
        if (!root.Nodes.TryGetValue(Tokens.GlobalDefineInterface, out var globalDefineInterfaceNode)) return [];

        if (globalDefineInterfaceNode is not JsonArray defineJsons)
        {
            analysis.ReportDiagnostics(new DefineInterfaceNotArrayDiagnostics(Tokens.GlobalDefineInterface, globalDefineInterfaceNode, true));
            return [];
        }

        var interfaces = new List<DefineInterface>();

        foreach (var defineJsonNode in defineJsons.Nodes)
        {
            var defineJson = defineJsonNode as JsonObject ?? throw new InvalidOperationException();

            interfaces.Add(ParseDefineInterface(id, defineJson, schemaTable, true, analysis));
        }

        return interfaces.ToArray();
    }
    
    private static DefineInterface ParseDefineInterface(string id, JsonObject node, SchemaTable schemaTable, bool isGlobal, Analysis analysis)
    {
        var interfaceNameNode = node[Tokens.InterfaceNameKey] as JsonString ?? throw new InvalidOperationException();
        var interfaceName = interfaceNameNode.Literal;
        
        var properties = new Dictionary<string, Falliable<IDefineInterfacePropertySchema>>();

        if (node.Nodes.TryGetValue(Tokens.PropertiesKey, out var propertiesNode))
        {
            var propertiesArray = propertiesNode as JsonArray;
            foreach (var propertyNode in propertiesArray.Nodes.OfType<JsonObject>())
            {
                // valueがtypeとかdefaultとか
                // keyがプロパティ名

                var key = (propertyNode[Tokens.PropertyNameKey] as JsonString)!;
                var propertySchemaIdResult = Parse(propertyNode, null, true, schemaTable, analysis);

                if (!propertySchemaIdResult.IsValid)
                {
                    properties[key.Literal] = Falliable<IDefineInterfacePropertySchema>.Failure();
                    continue;
                }

                if (schemaTable.Table[propertySchemaIdResult.Value!] is IDefineInterfacePropertySchema propertySchema)
                    properties[key.Literal] = Falliable<IDefineInterfacePropertySchema>.Success(propertySchema);
                else
                    throw new InvalidOperationException();
            }
        }
        
        // interfaceの継承情報を取得
        var implementationNodes = new Dictionary<string, JsonString>();
        var duplicateImplementationLocations = new Dictionary<string, List<Location>>();
        if (node.Nodes.TryGetValue(Tokens.ImplementationInterfaceKey, out var implementationInterfacesNode) && implementationInterfacesNode is JsonArray nodesArray)
            foreach (var implementationInterfaceNode in nodesArray.Nodes)
            {
                var name = (JsonString)implementationInterfaceNode;
                if (implementationNodes.ContainsKey(name.Literal))
                {
                    // 重複を検出
                    if (!duplicateImplementationLocations.ContainsKey(name.Literal)) duplicateImplementationLocations[name.Literal] = new List<Location> { implementationNodes[name.Literal].Location };
                    duplicateImplementationLocations[name.Literal].Add(name.Location);
                }
                else
                {
                    implementationNodes[name.Literal] = name;
                }
            }
        
        if (interfaceName == null) throw new Exception("interfaceName is null");
        if (properties == null) throw new Exception("properties is null");
        
        var defineInterface = new DefineInterface(
            id,
            interfaceName,
            properties,
            implementationNodes.Keys.ToArray(),
            implementationNodes,
            duplicateImplementationLocations.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray()),
            isGlobal,
            interfaceNameNode.Location
        );
        
        return defineInterface;
    }
    
    private static Falliable<SchemaId> Parse(JsonObject root, SchemaId? parent, bool isInterfaceProperty, SchemaTable schemaTable, Analysis analysis)
    {
        if (root.Nodes.ContainsKey(Tokens.SwitchKey)) return Falliable<SchemaId>.Success(ParseSwitch(root, parent, isInterfaceProperty, schemaTable, analysis));
        if (root.Nodes.ContainsKey(Tokens.RefKey)) return Falliable<SchemaId>.Success(ParseRef(root, parent, isInterfaceProperty, schemaTable, analysis));

        // typeが存在しない場合はDiagnosticsを報告してFailureを返す
        if (!root.Nodes.TryGetValue(Tokens.TypeKey, out var typeNode) || typeNode is not JsonString typeString)
        {
            var propertyName = (root[Tokens.PropertyNameKey] as JsonString)?.Literal;
            analysis.ReportDiagnostics(new TypeNotFoundDiagnostics(root, propertyName));
            return Falliable<SchemaId>.Failure();
        }

        var type = typeString.Literal;

        SchemaId? schemaId = type switch
        {
            Tokens.ObjectType => ParseObject(root, parent, isInterfaceProperty, schemaTable, analysis),
            Tokens.ArrayType => ParseArray(root, parent, isInterfaceProperty, schemaTable, analysis),
            Tokens.StringType => ParseString(root, parent, isInterfaceProperty, schemaTable, analysis),
            Tokens.EnumType => ParseEnum(root, parent, isInterfaceProperty, schemaTable, analysis),
            Tokens.NumberType => ParseNumber(root, parent, isInterfaceProperty, schemaTable, analysis),
            Tokens.IntegerType => ParseInteger(root, parent, isInterfaceProperty, schemaTable, analysis),
            Tokens.BooleanType => ParseBoolean(root, parent, isInterfaceProperty, schemaTable, analysis),
            Tokens.UuidType => ParseUuid(root, parent, isInterfaceProperty, schemaTable, analysis),
            Tokens.Vector2Type => ParseVector2(root, parent, isInterfaceProperty, schemaTable, analysis),
            Tokens.Vector3Type => ParseVector3(root, parent, isInterfaceProperty, schemaTable, analysis),
            Tokens.Vector4Type => ParseVector4(root, parent, isInterfaceProperty, schemaTable, analysis),
            Tokens.Vector2IntType => ParseVector2Int(root, parent, isInterfaceProperty, schemaTable, analysis),
            Tokens.Vector3IntType => ParseVector3Int(root, parent, isInterfaceProperty, schemaTable, analysis),
            _ => null
        };

        if (schemaId == null)
        {
            var propertyName = (root[Tokens.PropertyNameKey] as JsonString)?.Literal;
            analysis.ReportDiagnostics(new UnknownTypeDiagnostics(root, propertyName, type, typeString.Location));
            return Falliable<SchemaId>.Failure();
        }

        return Falliable<SchemaId>.Success(schemaId.Value);
    }
    
    private static SchemaId ParseObject(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        var implementationNodes = new Dictionary<string, JsonString>();
        var duplicateImplementationLocations = new Dictionary<string, List<Location>>();
        if (json.Nodes.TryGetValue(Tokens.ImplementationInterfaceKey, out var node) && node is JsonArray array)
            foreach (var implementation in array.Nodes)
                if (implementation is JsonString name)
                {
                    if (implementationNodes.ContainsKey(name.Literal))
                    {
                        // 重複を検出
                        if (!duplicateImplementationLocations.ContainsKey(name.Literal)) duplicateImplementationLocations[name.Literal] = new List<Location> { implementationNodes[name.Literal].Location };
                        duplicateImplementationLocations[name.Literal].Add(name.Location);
                    }
                    else
                    {
                        implementationNodes[name.Literal] = name;
                    }
                }
        
        var objectName = json.Nodes.ContainsKey(Tokens.PropertyNameKey) ? (json[Tokens.PropertyNameKey] as JsonString)!.Literal : null;
        var duplicateLocationsDict = duplicateImplementationLocations.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
        
        if (!json.Nodes.ContainsKey(Tokens.PropertiesKey)) return table.Add(new ObjectSchema(objectName, parent, new Dictionary<string, Falliable<SchemaId>>(), [], IsNullable(json, analysis), implementationNodes.Keys.ToArray(), implementationNodes, duplicateLocationsDict, isInterfaceProperty));

        var propertiesJson = (json[Tokens.PropertiesKey] as JsonArray)!;
        var required = json["required"] is not JsonArray requiredJson ? [] : requiredJson.Nodes.OfType<JsonString>().Select(str => str.Literal).ToArray();
        var objectSchemaId = SchemaId.New();

        Dictionary<string, Falliable<SchemaId>> properties = [];
        foreach (var propertyNode in propertiesJson.Nodes.OfType<JsonObject>())
        {
            var key = propertyNode.Nodes[Tokens.PropertyNameKey] as JsonString;
            var value = propertyNode;
            var schemaIdResult = Parse(value, objectSchemaId, false, table, analysis);

            properties.Add(key?.Literal, schemaIdResult);
        }
        
        table.Add(objectSchemaId, new ObjectSchema(objectName, parent, properties, required, IsNullable(json, analysis), implementationNodes.Keys.ToArray(), implementationNodes, duplicateLocationsDict, isInterfaceProperty));
        
        return objectSchemaId;
    }
    
    private static SchemaId ParseArray(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        var overrideCodeGeneratePropertyName = json[Tokens.OverrideCodeGeneratePropertyNameKey] as JsonString;
        var arraySchemaId = SchemaId.New();
        var key = json[Tokens.PropertyNameKey] as JsonString;
        
        Falliable<SchemaId> items;
        if (json["items"] is JsonObject itemsJson)
        {
            items = Parse(itemsJson, arraySchemaId, false, table, analysis);
        }
        else
        {
            analysis.ReportDiagnostics(new ArrayItemsNotFoundDiagnostics(json, arraySchemaId, key?.Literal));
            items = Falliable<SchemaId>.Failure();
        }
        
        table.Add(arraySchemaId, new ArraySchema(key?.Literal, parent, items, overrideCodeGeneratePropertyName, IsNullable(json, analysis), isInterfaceProperty));
        return arraySchemaId;
    }
    
    private static SchemaId ParseSwitch(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        var schemaId = SchemaId.New();
        var key = json[Tokens.PropertyNameKey] as JsonString;
        var switchReferencePathJson = (json[Tokens.SwitchKey] as JsonString)!;
        
        Falliable<SwitchCaseSchema[]> ifThenArray;
        var hasOptionalCase = false;
        
        if (json["cases"] is JsonArray casesArray)
        {
            var ifThenList = new List<SwitchCaseSchema>();

            foreach (var node in casesArray.Nodes)
            {
                var jsonObject = (node as JsonObject)!;
                var whenJson = (JsonString)jsonObject["when"];
                var thenJson = jsonObject;

                var switchPath = SwitchPathParser.Parse(switchReferencePathJson.Literal);
                var caseSchemaResult = Parse(thenJson, schemaId, false, table, analysis);

                ifThenList.Add(new SwitchCaseSchema(switchPath, whenJson.Literal, caseSchemaResult));
            }

            hasOptionalCase = ifThenList.Any(c => c.Schema.IsValid && table.Table[c.Schema.Value!].IsNullable);
            ifThenArray = Falliable<SwitchCaseSchema[]>.Success(ifThenList.ToArray());
        }
        else
        {
            analysis.ReportDiagnostics(new SwitchCasesNotFoundDiagnostics(json, schemaId, key?.Literal, switchReferencePathJson.Literal));
            ifThenArray = Falliable<SwitchCaseSchema[]>.Failure();
        }
        
        table.Add(schemaId, new SwitchSchema(key?.Literal, parent, ifThenArray, IsNullable(json, analysis), hasOptionalCase, isInterfaceProperty, switchReferencePathJson.Location));
        return schemaId;
    }
    
    private static SchemaId ParseRef(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        var refJson = (json[Tokens.RefKey] as JsonString)!;
        return table.Add(new RefSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, refJson.Literal, refJson.Location, IsNullable(json, analysis), isInterfaceProperty));
    }
    
    private static SchemaId ParseString(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
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
        
        return table.Add(new StringSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json, analysis), enums?.ToArray(), isInterfaceProperty));
    }
    
    private static SchemaId ParseEnum(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        var nameJson = json[Tokens.PropertyNameKey] as JsonString;
        
        var options = new List<string>();
        
        if (json["options"] is JsonArray enumArray)
            foreach (var enumNode in enumArray.Nodes)
            {
                if (enumNode is not JsonString enumString) throw new Exception("Enum must be an array of strings");
                
                options.Add(enumString.Literal);
            }
        
        return table.Add(new StringSchema(nameJson?.Literal, parent, IsNullable(json, analysis), options.ToArray(), isInterfaceProperty));
    }
    
    private static SchemaId ParseNumber(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        return table.Add(new NumberSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json, analysis), isInterfaceProperty));
    }
    
    private static SchemaId ParseInteger(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        return table.Add(new IntegerSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json, analysis), isInterfaceProperty));
    }
    
    private static SchemaId ParseBoolean(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        return table.Add(new BooleanSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json, analysis), isInterfaceProperty));
    }
    
    private static bool IsNullable(JsonObject json, Analysis analysis)
    {
        var propertyName = (json[Tokens.PropertyNameKey] as JsonString)?.Literal;
        
        if (!json.Nodes.TryGetValue("optional", out var optionalNode))
            return false;
        
        switch (optionalNode)
        {
            case JsonBoolean boolNode:
                return boolNode.Literal;
            case JsonString strNode when strNode.Literal == "true":
                return true;
            case JsonString strNode when strNode.Literal == "false":
                return false;
            default:
                analysis.ReportDiagnostics(new InvalidOptionalValueDiagnostics(json, optionalNode, propertyName));
                return false;
        }
    }
    
    private static SchemaId ParseUuid(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        return table.Add(new UuidSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json, analysis), isInterfaceProperty));
    }
    
    private static SchemaId ParseVector2(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        return table.Add(new Vector2Schema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json, analysis), isInterfaceProperty));
    }
    
    private static SchemaId ParseVector3(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        return table.Add(new Vector3Schema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json, analysis), isInterfaceProperty));
    }
    
    private static SchemaId ParseVector4(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        return table.Add(new Vector4Schema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json, analysis), isInterfaceProperty));
    }
    
    private static SchemaId ParseVector2Int(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        return table.Add(new Vector2IntSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json, analysis), isInterfaceProperty));
    }
    
    private static SchemaId ParseVector3Int(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        return table.Add(new Vector3IntSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json, analysis), isInterfaceProperty));
    }
}