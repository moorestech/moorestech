using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using mooresmaster.Generator.Analyze;
using mooresmaster.Generator.Analyze.Diagnostics;
using mooresmaster.Generator.JsonSchema;

// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator

namespace mooresmaster.Generator.Semantic;

public static class SemanticsGenerator
{
    public static Semantics Generate(ImmutableArray<Schema> schemaArray, SchemaTable table, Analysis analysis)
    {
        var semantics = new Semantics();
        
        foreach (var schema in schemaArray)
        {
            var rootId = RootId.New();
            
            // ファイルに分けられているルートの要素はclassになる
            // ただし、objectSchemaだった場合のちのGenerateで生成されるため、ここでは生成しない
            if (table.Table[schema.InnerSchema] is ObjectSchema objectSchema)
            {
                var (innerSemantics, id) = Generate(objectSchema, table, rootId, analysis);
                semantics.RootSemanticsTable.Add(rootId, new RootSemantics(schema, id));
                innerSemantics.AddTo(semantics);
            }
            else
            {
                var typeSemantics = new TypeSemantics([], table.Table[schema.InnerSchema], rootId);
                var typeId = semantics.AddTypeSemantics(typeSemantics);
                semantics.RootSemanticsTable.Add(rootId, new RootSemantics(schema, typeId));
                
                Generate(table.Table[schema.InnerSchema], table, rootId, analysis).AddTo(semantics);
            }
            
            foreach (var defineInterface in schema.Interfaces)
                GenerateInterfaceSemantics(defineInterface, schema, table, rootId, analysis).AddTo(semantics);
        }
        
        ResolveInterfaceInterfaceImplementations(semantics, analysis);
        ResolveClassInterfaceImplementations(semantics, analysis);
        
        return semantics;
    }
    
    private static void ResolveInterfaceInterfaceImplementations(Semantics semantics, Analysis analysis)
    {
        var allInterfaceTable = new Dictionary<string, InterfaceId>();
        foreach (var kvp in semantics.InterfaceSemanticsTable)
        {
            var interfaceName = kvp.Value.Interface.InterfaceName;
            if (!allInterfaceTable.ContainsKey(interfaceName))
                allInterfaceTable.Add(interfaceName, kvp.Key);
        }
        
        var globalInterfaceTable = new Dictionary<string, InterfaceId>();
        foreach (var kvp in semantics.InterfaceSemanticsTable.Where(i => i.Value.Interface.IsGlobal))
        {
            var interfaceName = kvp.Value.Interface.InterfaceName;
            if (!globalInterfaceTable.ContainsKey(interfaceName))
                globalInterfaceTable.Add(interfaceName, kvp.Key);
        }
        
        foreach (var kvp in semantics.InterfaceSemanticsTable)
        {
            var target = kvp.Key;
            var localInterfaceTable = new Dictionary<string, InterfaceId>();
            foreach (var i in semantics.InterfaceSemanticsTable
                         .Where(i => i.Value.Schema.SchemaId == kvp.Value.Schema.SchemaId)
                         .Where(i => !i.Value.Interface.IsGlobal))
            {
                var name = i.Value.Interface.InterfaceName;
                if (!localInterfaceTable.ContainsKey(name))
                    localInterfaceTable.Add(name, i.Key);
            }
            
            foreach (var interfaceName in kvp.Value.Interface.ImplementationInterfaces)
                if (localInterfaceTable.TryGetValue(interfaceName, out var localOther))
                    semantics.AddInterfaceInterfaceImplementation(target, localOther);
                else if (globalInterfaceTable.TryGetValue(interfaceName, out var globalOther))
                    semantics.AddInterfaceInterfaceImplementation(target, globalOther);
                else if (allInterfaceTable.TryGetValue(interfaceName, out var allOther))
                    semantics.AddInterfaceInterfaceImplementation(target, allOther);
                else
                    analysis.ReportDiagnostics(new InterfaceNotFoundDiagnostics(interfaceName, kvp.Value.Interface.ImplementationNodes[interfaceName].Location));
        }
    }
    
    private static void ResolveClassInterfaceImplementations(Semantics semantics, Analysis analysis)
    {
        var globalInterfaceTable = new Dictionary<string, InterfaceId>();
        foreach (var kvp in semantics.InterfaceSemanticsTable.Where(i => i.Value.Interface.IsGlobal))
        {
            var interfaceName = kvp.Value.Interface.InterfaceName;
            if (!globalInterfaceTable.ContainsKey(interfaceName))
                globalInterfaceTable.Add(interfaceName, kvp.Key);
        }
        
        foreach (var kvp in semantics.TypeSemanticsTable)
        {
            if (kvp.Value.Schema is not ObjectSchema objectSchema) continue;
            var localInterfaceTable = new Dictionary<string, InterfaceId>();
            foreach (var i in semantics.InterfaceSemanticsTable
                         .Where(i => i.Value.Schema.SchemaId == semantics.RootSemanticsTable[kvp.Value.RootId].Root.SchemaId)
                         .Where(i => !i.Value.Interface.IsGlobal))
            {
                var interfaceName = i.Value.Interface.InterfaceName;
                if (!localInterfaceTable.ContainsKey(interfaceName))
                    localInterfaceTable.Add(interfaceName, i.Key);
            }
            
            var target = kvp.Key;
            
            foreach (var interfaceName in objectSchema.InterfaceImplementations)
                if (localInterfaceTable.TryGetValue(interfaceName, out var localOther))
                {
                    semantics.AddClassInterfaceImplementation(target, localOther);
                }
                else
                {
                    if (globalInterfaceTable.TryGetValue(interfaceName, out var globalOther))
                        semantics.AddClassInterfaceImplementation(target, globalOther);
                    else
                        analysis.ReportDiagnostics(new InterfaceNotFoundDiagnostics(interfaceName, objectSchema.ImplementationNodes[interfaceName].Location));
                }
        }
    }
    
    private static Semantics GenerateInterfaceSemantics(DefineInterface defineInterface, Schema schema, SchemaTable table, RootId rootId, Analysis analysis)
    {
        var semantics = new Semantics();
        
        var interfaceId = InterfaceId.New();
        
        List<InterfacePropertyId> propertyIds = new();
        foreach (var property in defineInterface.Properties)
        {
            var propertySchema = property.Value;
            
            Generate(propertySchema, table, rootId, analysis).AddTo(semantics);
            
            var propertyId = semantics.AddInterfacePropertySemantics(new InterfacePropertySemantics(propertySchema, interfaceId));
            propertyIds.Add(propertyId);
        }
        
        semantics.InterfaceSemanticsTable[interfaceId] = new InterfaceSemantics(
            schema,
            defineInterface,
            propertyIds.ToArray()
        );
        
        return semantics;
    }
    
    private static Semantics Generate(ISchema schema, SchemaTable table, RootId rootId, Analysis analysis)
    {
        var semantics = new Semantics();
        
        switch (schema)
        {
            case ArraySchema arraySchema:
                if (arraySchema.Items.IsValid)
                {
                    var itemsSchema = table.Table[arraySchema.Items.Value!];
                    if (itemsSchema is ObjectSchema arrayItemObjectSchema)
                    {
                        var (arrayItemSemantics, _) = Generate(arrayItemObjectSchema, table, rootId, analysis, true);
                        arrayItemSemantics.AddTo(semantics);
                    }
                    else
                    {
                        Generate(itemsSchema, table, rootId, analysis).AddTo(semantics);
                    }
                }
                
                break;
            case ObjectSchema objectSchema:
                var (innerSemantics, _) = Generate(objectSchema, table, rootId, analysis);
                innerSemantics.AddTo(semantics);
                break;
            case SwitchSchema oneOfSchema:
                var (oneOfInnerSemantics, _) = Generate(oneOfSchema, table, rootId, analysis);
                oneOfInnerSemantics.AddTo(semantics);
                break;
            case RefSchema:
            case BooleanSchema:
            case IntegerSchema:
            case NumberSchema:
            case StringSchema:
            case UuidSchema:
            case Vector2Schema:
            case Vector3Schema:
            case Vector4Schema:
            case Vector2IntSchema:
            case Vector3IntSchema:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(schema));
        }
        
        return semantics;
    }
    
    private static (Semantics, SwitchId) Generate(SwitchSchema switchSchema, SchemaTable table, RootId rootId, Analysis analysis)
    {
        var semantics = new Semantics();
        
        var interfaceId = SwitchId.New();
        List<(SwitchPath, string, ClassId)> thenList = new();
        
        if (switchSchema.IfThenArray.IsValid)
            foreach (var ifThen in switchSchema.IfThenArray.Value!)
            {
                Generate(table.Table[ifThen.Schema], table, rootId, analysis).AddTo(semantics);
                
                var then = semantics.SchemaTypeSemanticsTable[table.Table[ifThen.Schema]];
                semantics.SwitchInheritList.Add((interfaceId, then));
                thenList.Add((ifThen.SwitchReferencePath, ifThen.When, then));
            }
        
        semantics.SwitchSemanticsTable.Add(interfaceId, new SwitchSemantics(switchSchema, thenList.ToArray()));
        
        return (semantics, interfaceId);
    }
    
    private static (Semantics, ClassId) Generate(ObjectSchema objectSchema, SchemaTable table, RootId rootId, Analysis analysis, bool isArrayInnerType = false)
    {
        var semantics = new Semantics();
        var typeId = ClassId.New();
        var properties = new List<PropertyId>();
        foreach (var property in objectSchema.Properties)
        {
            var schema = table.Table[property.Value];
            switch (schema)
            {
                case ObjectSchema innerObjectSchema:
                    var (objectInnerSemantics, objectInnerTypeId) = Generate(innerObjectSchema, table, rootId, analysis);
                    objectInnerSemantics.AddTo(semantics);
                    properties.Add(semantics.AddPropertySemantics(
                        new PropertySemantics(
                            typeId,
                            property.Key,
                            objectInnerTypeId,
                            schema,
                            schema.IsNullable
                        )
                    ));
                    break;
                case SwitchSchema oneOfSchema:
                    var (oneOfInnerSemantics, oneOfInnerTypeId) = Generate(oneOfSchema, table, rootId, analysis);
                    oneOfInnerSemantics.AddTo(semantics);
                    properties.Add(semantics.AddPropertySemantics(
                        new PropertySemantics(
                            typeId,
                            property.Key,
                            oneOfInnerTypeId,
                            schema,
                            schema.IsNullable
                        )
                    ));
                    break;
                default:
                    Generate(table.Table[property.Value], table, rootId, analysis).AddTo(semantics);
                    properties.Add(semantics.AddPropertySemantics(
                        new PropertySemantics(
                            typeId,
                            property.Key,
                            null,
                            schema,
                            schema.IsNullable
                        )
                    ));
                    break;
            }
        }
        
        var typeSemantics = new TypeSemantics(properties.ToArray(), objectSchema, rootId, isArrayInnerType);
        semantics.TypeSemanticsTable[typeId] = typeSemantics;
        semantics.SchemaTypeSemanticsTable[typeSemantics.Schema] = typeId;
        
        return (semantics, typeId);
    }
}