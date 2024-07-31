using System;
using System.Collections.Generic;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator.Semantic;

public class Semantics
{
    public readonly List<(Guid interfaceId, Guid typeId)> InheritList = new(); // (InterfaceId, TypeId)
    public readonly Dictionary<Guid, InterfaceSemantics> InterfaceSemanticsTable = new();
    public readonly Dictionary<OneOfSchema, Guid> OneOfInterfaceSemanticsTable = new();
    public readonly Dictionary<Guid, RootSemantics> RootSemanticsTable = new();
    public readonly Dictionary<ISchema, Guid> SchemaTypeSemanticsTable = new();
    public readonly Dictionary<Guid, TypeSemantics> TypeSemanticsTable = new();
    public readonly Dictionary<Guid, PropertySemantics> PropertySemanticsTable = new();

    public Guid AddInterfaceSemantics(InterfaceSemantics interfaceSemantics)
    {
        var id = Guid.NewGuid();
        InterfaceSemanticsTable.Add(id, interfaceSemantics);
        OneOfInterfaceSemanticsTable.Add(interfaceSemantics.Schema, id);
        return id;
    }

    public Guid AddTypeSemantics(TypeSemantics typeSemantics)
    {
        var id = Guid.NewGuid();
        TypeSemanticsTable.Add(id, typeSemantics);
        SchemaTypeSemanticsTable.Add(typeSemantics.Schema, id);
        return id;
    }

    public Guid AddRootSemantics(RootSemantics rootSemantics)
    {
        var id = Guid.NewGuid();
        RootSemanticsTable.Add(id, rootSemantics);
        return id;
    }

    public Guid AddPropertySemantics(PropertySemantics propertySemantics)
    {
        var id = Guid.NewGuid();
        PropertySemanticsTable.Add(id, propertySemantics);
        return id;
    }

    public Semantics Merge(Semantics other)
    {
        foreach (var inherit in other.InheritList) InheritList.Add(inherit);
        foreach (var interfaceSemantics in other.InterfaceSemanticsTable) InterfaceSemanticsTable.Add(interfaceSemantics.Key, interfaceSemantics.Value);
        foreach (var rootSemantics in other.RootSemanticsTable) RootSemanticsTable.Add(rootSemantics.Key, rootSemantics.Value);
        foreach (var typeSemantics in other.TypeSemanticsTable) TypeSemanticsTable.Add(typeSemantics.Key, typeSemantics.Value);
        foreach (var kvp in other.SchemaTypeSemanticsTable) SchemaTypeSemanticsTable.Add(kvp.Key, kvp.Value);
        foreach (var kvp in other.OneOfInterfaceSemanticsTable) OneOfInterfaceSemanticsTable.Add(kvp.Key, kvp.Value);
        foreach (var kvp in other.PropertySemanticsTable) PropertySemanticsTable.Add(kvp.Key, kvp.Value);

        return this;
    }

    public Semantics AddTo(Semantics other)
    {
        return other.Merge(this);
    }
}

public record RootSemantics(Schema Root, Guid TypeId)
{
    public Schema Root = Root;
    public Guid TypeId = TypeId;
}

/// <summary>
/// </summary>
/// <param name="ParentType">
///     親のTypeId
///     存在しない場合は親がrootということ
///     親要素がinterfaceになることはない
/// </param>
/// <param name="Schema"></param>
public record TypeSemantics(Guid[] Properties, ISchema Schema)
{
    public Guid[] Properties = Properties;
    public ISchema Schema = Schema;
}

public record PropertySemantics(Guid ParentTypeId, string PropertyName, Guid? PropertyType, ISchema Schema)
{
    public Guid ParentTypeId = ParentTypeId;
    public string PropertyName = PropertyName;
    public Guid? PropertyType = PropertyType;
    public ISchema Schema = Schema;
}

public record InterfaceSemantics(OneOfSchema Schema)
{
    public OneOfSchema Schema = Schema;
}

[UnitOf(typeof(Guid))]
public readonly partial struct PropertyId;

[UnitOf(typeof(Guid))]
public readonly partial struct TypeId;

[UnitOf(typeof(Guid))]
public readonly partial struct InterfaceId;
