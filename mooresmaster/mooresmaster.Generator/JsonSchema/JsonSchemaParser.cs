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

        for (var i = 0; i < defineJsons.Nodes.Length; i++)
        {
            var defineJsonNode = defineJsons.Nodes[i];
            if (defineJsonNode is not JsonObject defineJson)
            {
                analysis.ReportDiagnostics(new DefineInterfaceElementNotObjectDiagnostics(Tokens.DefineInterface, defineJsonNode, i, false));
                continue;
            }

            var result = ParseDefineInterface(id, defineJson, schemaTable, false, analysis);
            if (result.IsValid)
                interfaces.Add(result.Value!);
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

        for (var i = 0; i < defineJsons.Nodes.Length; i++)
        {
            var defineJsonNode = defineJsons.Nodes[i];
            if (defineJsonNode is not JsonObject defineJson)
            {
                analysis.ReportDiagnostics(new DefineInterfaceElementNotObjectDiagnostics(Tokens.GlobalDefineInterface, defineJsonNode, i, true));
                continue;
            }

            var result = ParseDefineInterface(id, defineJson, schemaTable, true, analysis);
            if (result.IsValid)
                interfaces.Add(result.Value!);
        }

        return interfaces.ToArray();
    }

    private static Falliable<DefineInterface> ParseDefineInterface(string id, JsonObject node, SchemaTable schemaTable, bool isGlobal, Analysis analysis)
    {
        // interfaceNameの取得とバリデーション
        if (!node.Nodes.TryGetValue(Tokens.InterfaceNameKey, out var interfaceNameNodeRaw))
        {
            analysis.ReportDiagnostics(new InterfaceNameNotStringDiagnostics(node, null, isGlobal));
            return Falliable<DefineInterface>.Failure();
        }

        if (interfaceNameNodeRaw is not JsonString interfaceNameNode)
        {
            analysis.ReportDiagnostics(new InterfaceNameNotStringDiagnostics(node, interfaceNameNodeRaw, isGlobal));
            return Falliable<DefineInterface>.Failure();
        }

        var interfaceName = interfaceNameNode.Literal;
        var properties = new Dictionary<string, Falliable<IDefineInterfacePropertySchema>>();

        if (node.Nodes.TryGetValue(Tokens.PropertiesKey, out var propertiesNode))
        {
            // propertiesが配列でない場合はDiagnosticsを報告
            if (propertiesNode is not JsonArray propertiesArray)
            {
                analysis.ReportDiagnostics(new InterfacePropertiesNotArrayDiagnostics(interfaceName, propertiesNode, isGlobal));
                // propertiesが不正でも処理を続行（空のpropertiesとして扱う）
            }
            else
            {
                var propertyIndex = 0;
                foreach (var propertyNode in propertiesArray.Nodes.OfType<JsonObject>())
                {
                    // keyの取得とバリデーション
                    var keyNode = propertyNode[Tokens.PropertyNameKey];
                    if (keyNode is not JsonString key)
                    {
                        analysis.ReportDiagnostics(new InterfacePropertyKeyNotStringDiagnostics(interfaceName, propertyNode, keyNode, propertyIndex, isGlobal));
                        propertyIndex++;
                        continue;
                    }

                    var propertySchemaIdResult = Parse(propertyNode, null, true, schemaTable, analysis);

                    if (!propertySchemaIdResult.IsValid)
                    {
                        properties[key.Literal] = Falliable<IDefineInterfacePropertySchema>.Failure();
                        propertyIndex++;
                        continue;
                    }

                    var schema = schemaTable.Table[propertySchemaIdResult.Value!];
                    if (schema is IDefineInterfacePropertySchema propertySchema)
                    {
                        properties[key.Literal] = Falliable<IDefineInterfacePropertySchema>.Success(propertySchema);
                    }
                    else
                    {
                        analysis.ReportDiagnostics(new InterfacePropertySchemaInvalidTypeDiagnostics(interfaceName, key.Literal, schema, propertyNode, isGlobal));
                        properties[key.Literal] = Falliable<IDefineInterfacePropertySchema>.Failure();
                    }

                    propertyIndex++;
                }
            }
        }

        // interfaceの継承情報を取得
        var implementationNodes = new Dictionary<string, JsonString>();
        var duplicateImplementationLocations = new Dictionary<string, List<Location>>();
        if (node.Nodes.TryGetValue(Tokens.ImplementationInterfaceKey, out var implementationInterfacesNode) && implementationInterfacesNode is JsonArray nodesArray)
        {
            for (var i = 0; i < nodesArray.Nodes.Length; i++)
            {
                var implementationInterfaceNode = nodesArray.Nodes[i];
                if (implementationInterfaceNode is not JsonString name)
                {
                    analysis.ReportDiagnostics(new ImplementsElementNotStringDiagnostics(interfaceName, implementationInterfaceNode, i, isGlobal));
                    continue;
                }

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
        }

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

        return Falliable<DefineInterface>.Success(defineInterface);
    }
    
    private static Falliable<SchemaId> Parse(JsonObject root, SchemaId? parent, bool isInterfaceProperty, SchemaTable schemaTable, Analysis analysis)
    {
        if (root.Nodes.ContainsKey(Tokens.SwitchKey)) return ParseSwitch(root, parent, isInterfaceProperty, schemaTable, analysis);
        if (root.Nodes.ContainsKey(Tokens.RefKey)) return ParseRef(root, parent, isInterfaceProperty, schemaTable, analysis);

        // typeが存在しない場合はDiagnosticsを報告してFailureを返す
        if (!root.Nodes.TryGetValue(Tokens.TypeKey, out var typeNode) || typeNode is not JsonString typeString)
        {
            var propertyName = (root[Tokens.PropertyNameKey] as JsonString)?.Literal;
            analysis.ReportDiagnostics(new TypeNotFoundDiagnostics(root, propertyName));
            return Falliable<SchemaId>.Failure();
        }

        var type = typeString.Literal;

        Falliable<SchemaId>? schemaIdResult = type switch
        {
            Tokens.ObjectType => ParseObject(root, parent, isInterfaceProperty, schemaTable, analysis),
            Tokens.ArrayType => ParseArray(root, parent, isInterfaceProperty, schemaTable, analysis),
            Tokens.StringType => ParseString(root, parent, isInterfaceProperty, schemaTable, analysis),
            Tokens.EnumType => ParseEnum(root, parent, isInterfaceProperty, schemaTable, analysis),
            Tokens.NumberType => Falliable<SchemaId>.Success(ParseNumber(root, parent, isInterfaceProperty, schemaTable, analysis)),
            Tokens.IntegerType => Falliable<SchemaId>.Success(ParseInteger(root, parent, isInterfaceProperty, schemaTable, analysis)),
            Tokens.BooleanType => Falliable<SchemaId>.Success(ParseBoolean(root, parent, isInterfaceProperty, schemaTable, analysis)),
            Tokens.UuidType => Falliable<SchemaId>.Success(ParseUuid(root, parent, isInterfaceProperty, schemaTable, analysis)),
            Tokens.Vector2Type => Falliable<SchemaId>.Success(ParseVector2(root, parent, isInterfaceProperty, schemaTable, analysis)),
            Tokens.Vector3Type => Falliable<SchemaId>.Success(ParseVector3(root, parent, isInterfaceProperty, schemaTable, analysis)),
            Tokens.Vector4Type => Falliable<SchemaId>.Success(ParseVector4(root, parent, isInterfaceProperty, schemaTable, analysis)),
            Tokens.Vector2IntType => Falliable<SchemaId>.Success(ParseVector2Int(root, parent, isInterfaceProperty, schemaTable, analysis)),
            Tokens.Vector3IntType => Falliable<SchemaId>.Success(ParseVector3Int(root, parent, isInterfaceProperty, schemaTable, analysis)),
            _ => null
        };

        if (schemaIdResult == null)
        {
            var propertyName = (root[Tokens.PropertyNameKey] as JsonString)?.Literal;
            analysis.ReportDiagnostics(new UnknownTypeDiagnostics(root, propertyName, type, typeString.Location));
            return Falliable<SchemaId>.Failure();
        }

        return schemaIdResult.Value;
    }
    
    private static Falliable<SchemaId> ParseObject(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
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

        var objectName = (json[Tokens.PropertyNameKey] as JsonString)?.Literal;
        var duplicateLocationsDict = duplicateImplementationLocations.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());

        if (!json.Nodes.TryGetValue(Tokens.PropertiesKey, out var propertiesNode))
            return Falliable<SchemaId>.Success(table.Add(new ObjectSchema(objectName, parent, new Dictionary<string, Falliable<SchemaId>>(), new Dictionary<string, JsonObject>(), [], IsNullable(json, analysis), implementationNodes.Keys.ToArray(), implementationNodes, duplicateLocationsDict, isInterfaceProperty, json)));

        if (propertiesNode is not JsonArray propertiesJson)
        {
            analysis.ReportDiagnostics(new ObjectPropertiesNotArrayDiagnostics(objectName, propertiesNode));
            return Falliable<SchemaId>.Failure();
        }

        var required = json["required"] is not JsonArray requiredJson ? [] : requiredJson.Nodes.OfType<JsonString>().Select(str => str.Literal).ToArray();
        var objectSchemaId = SchemaId.New();

        Dictionary<string, Falliable<SchemaId>> properties = [];
        Dictionary<string, JsonObject> propertiesJsonDict = [];
        var propertyIndex = 0;
        foreach (var propertyNode in propertiesJson.Nodes.OfType<JsonObject>())
        {
            // keyの取得とバリデーション
            propertyNode.Nodes.TryGetValue(Tokens.PropertyNameKey, out var keyNode);
            if (keyNode is not JsonString keyString)
            {
                analysis.ReportDiagnostics(new ObjectPropertyKeyNotStringDiagnostics(objectName, propertyNode, keyNode, propertyIndex));
                propertyIndex++;
                continue;
            }

            var schemaIdResult = Parse(propertyNode, objectSchemaId, false, table, analysis);
            properties.Add(keyString.Literal, schemaIdResult);
            propertiesJsonDict.Add(keyString.Literal, propertyNode);
            propertyIndex++;
        }

        table.Add(objectSchemaId, new ObjectSchema(objectName, parent, properties, propertiesJsonDict, required, IsNullable(json, analysis), implementationNodes.Keys.ToArray(), implementationNodes, duplicateLocationsDict, isInterfaceProperty, json));

        return Falliable<SchemaId>.Success(objectSchemaId);
    }
    
    private static Falliable<SchemaId> ParseArray(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        var overrideCodeGeneratePropertyName = json[Tokens.OverrideCodeGeneratePropertyNameKey] as JsonString;
        var arraySchemaId = SchemaId.New();
        var key = json[Tokens.PropertyNameKey] as JsonString;

        Falliable<SchemaId> items;
        JsonObject? itemsJsonObject = null;
        if (json["items"] is JsonObject itemsJson)
        {
            itemsJsonObject = itemsJson;
            items = Parse(itemsJson, arraySchemaId, false, table, analysis);
        }
        else
        {
            analysis.ReportDiagnostics(new ArrayItemsNotFoundDiagnostics(json, arraySchemaId, key?.Literal));
            items = Falliable<SchemaId>.Failure();
        }

        table.Add(arraySchemaId, new ArraySchema(key?.Literal, parent, items, itemsJsonObject, overrideCodeGeneratePropertyName, IsNullable(json, analysis), isInterfaceProperty, json));
        return Falliable<SchemaId>.Success(arraySchemaId);
    }
    
    private static Falliable<SchemaId> ParseSwitch(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        var schemaId = SchemaId.New();
        var key = json[Tokens.PropertyNameKey] as JsonString;

        // switchの値がJsonStringでない場合
        var switchNode = json[Tokens.SwitchKey];
        if (switchNode is not JsonString switchReferencePathJson)
        {
            analysis.ReportDiagnostics(new SwitchKeyNotStringDiagnostics(key?.Literal, switchNode));
            return Falliable<SchemaId>.Failure();
        }

        // switchパスのパース
        var switchPathResult = SwitchPathParser.Parse(switchReferencePathJson.Literal);
        if (!switchPathResult.IsValid)
        {
            analysis.ReportDiagnostics(new InvalidSwitchPathDiagnostics(key?.Literal, switchReferencePathJson.Literal, switchReferencePathJson.Location));
            return Falliable<SchemaId>.Failure();
        }

        Falliable<SwitchCaseSchema[]> ifThenArray;
        var hasOptionalCase = false;

        if (json["cases"] is JsonArray casesArray)
        {
            var ifThenList = new List<SwitchCaseSchema>();
            var hasError = false;

            for (var i = 0; i < casesArray.Nodes.Length; i++)
            {
                var node = casesArray.Nodes[i];

                // caseがJsonObjectでない場合
                if (node is not JsonObject jsonObject)
                {
                    analysis.ReportDiagnostics(new SwitchCaseNotObjectDiagnostics(key?.Literal, node, i));
                    hasError = true;
                    continue;
                }

                // whenがJsonStringでない場合
                var whenNode = jsonObject["when"];
                if (whenNode is not JsonString whenJson)
                {
                    analysis.ReportDiagnostics(new SwitchCaseWhenNotStringDiagnostics(key?.Literal, jsonObject, whenNode, i));
                    hasError = true;
                    continue;
                }

                var caseSchemaResult = Parse(jsonObject, schemaId, false, table, analysis);

                ifThenList.Add(new SwitchCaseSchema(switchPathResult.Value!, whenJson.Literal, caseSchemaResult, jsonObject));
            }

            if (hasError && ifThenList.Count == 0)
            {
                ifThenArray = Falliable<SwitchCaseSchema[]>.Failure();
            }
            else
            {
                hasOptionalCase = ifThenList.Any(c => c.Schema.IsValid && table.Table[c.Schema.Value!].IsNullable);
                ifThenArray = Falliable<SwitchCaseSchema[]>.Success(ifThenList.ToArray());
            }
        }
        else
        {
            analysis.ReportDiagnostics(new SwitchCasesNotFoundDiagnostics(json, schemaId, key?.Literal, switchReferencePathJson.Literal));
            ifThenArray = Falliable<SwitchCaseSchema[]>.Failure();
        }

        table.Add(schemaId, new SwitchSchema(key?.Literal, parent, ifThenArray, IsNullable(json, analysis), hasOptionalCase, isInterfaceProperty, switchReferencePathJson.Location, json));
        return Falliable<SchemaId>.Success(schemaId);
    }
    
    private static Falliable<SchemaId> ParseRef(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        var refNode = json[Tokens.RefKey];
        var propertyName = (json[Tokens.PropertyNameKey] as JsonString)?.Literal;

        if (refNode is not JsonString refJson)
        {
            analysis.ReportDiagnostics(new RefKeyNotStringDiagnostics(propertyName, refNode));
            return Falliable<SchemaId>.Failure();
        }

        return Falliable<SchemaId>.Success(table.Add(new RefSchema(propertyName, parent, refJson.Literal, refJson.Location, IsNullable(json, analysis), isInterfaceProperty, json)));
    }
    
    private static Falliable<SchemaId> ParseString(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        var enumJson = json["enum"];
        var propertyName = (json[Tokens.PropertyNameKey] as JsonString)?.Literal;
        List<string>? enums = null;
        var hasError = false;

        if (enumJson is JsonArray enumArray)
        {
            enums = new List<string>();
            for (var i = 0; i < enumArray.Nodes.Length; i++)
            {
                var enumNode = enumArray.Nodes[i];
                if (enumNode is not JsonString enumString)
                {
                    analysis.ReportDiagnostics(new EnumElementNotStringDiagnostics(propertyName, enumNode, i, false));
                    hasError = true;
                    continue;
                }

                enums.Add(enumString.Literal);
            }
        }

        if (hasError && (enums == null || enums.Count == 0))
            return Falliable<SchemaId>.Failure();

        return Falliable<SchemaId>.Success(table.Add(new StringSchema(propertyName, parent, IsNullable(json, analysis), enums?.ToArray(), isInterfaceProperty, json)));
    }
    
    private static Falliable<SchemaId> ParseEnum(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        var nameJson = json[Tokens.PropertyNameKey] as JsonString;
        var propertyName = nameJson?.Literal;

        var options = new List<string>();
        var hasError = false;

        if (json["options"] is JsonArray enumArray)
        {
            for (var i = 0; i < enumArray.Nodes.Length; i++)
            {
                var enumNode = enumArray.Nodes[i];
                if (enumNode is not JsonString enumString)
                {
                    analysis.ReportDiagnostics(new EnumElementNotStringDiagnostics(propertyName, enumNode, i, true));
                    hasError = true;
                    continue;
                }

                options.Add(enumString.Literal);
            }
        }

        if (hasError && options.Count == 0)
            return Falliable<SchemaId>.Failure();

        return Falliable<SchemaId>.Success(table.Add(new StringSchema(propertyName, parent, IsNullable(json, analysis), options.ToArray(), isInterfaceProperty, json)));
    }
    
    private static SchemaId ParseNumber(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        return table.Add(new NumberSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json, analysis), isInterfaceProperty, json));
    }

    private static SchemaId ParseInteger(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        return table.Add(new IntegerSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json, analysis), isInterfaceProperty, json));
    }

    private static SchemaId ParseBoolean(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        return table.Add(new BooleanSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json, analysis), isInterfaceProperty, json));
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
        return table.Add(new UuidSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json, analysis), isInterfaceProperty, json));
    }

    private static SchemaId ParseVector2(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        return table.Add(new Vector2Schema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json, analysis), isInterfaceProperty, json));
    }

    private static SchemaId ParseVector3(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        return table.Add(new Vector3Schema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json, analysis), isInterfaceProperty, json));
    }

    private static SchemaId ParseVector4(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        return table.Add(new Vector4Schema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json, analysis), isInterfaceProperty, json));
    }

    private static SchemaId ParseVector2Int(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        return table.Add(new Vector2IntSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json, analysis), isInterfaceProperty, json));
    }

    private static SchemaId ParseVector3Int(JsonObject json, SchemaId? parent, bool isInterfaceProperty, SchemaTable table, Analysis analysis)
    {
        return table.Add(new Vector3IntSchema((json[Tokens.PropertyNameKey] as JsonString)?.Literal, parent, IsNullable(json, analysis), isInterfaceProperty, json));
    }
}