using System;
using System.Collections.Generic;
using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;
using UnitGenerator;

namespace mooresmaster.Generator.Semantic;

public class Semantics
{
    public readonly Dictionary<InterfacePropertySemantics, InterfacePropertyId> InterfacePropertyIdTable = new();
    public readonly Dictionary<InterfacePropertyId, InterfacePropertySemantics> InterfacePropertySemanticsTable = new();
    public readonly Dictionary<InterfaceId, InterfaceSemantics> InterfaceSemanticsTable = new();
    public readonly Dictionary<PropertyId, PropertySemantics> PropertySemanticsTable = new();
    public readonly Dictionary<RootId, RootSemantics> RootSemanticsTable = new();
    public readonly Dictionary<ISchema, ClassId> SchemaTypeSemanticsTable = new();
    public readonly List<(SwitchId switchId, ClassId typeId)> SwitchInheritList = new(); // (SwitchId, TypeId)
    public readonly Dictionary<SwitchId, SwitchSemantics> SwitchSemanticsTable = new();
    public readonly Dictionary<ClassId, TypeSemantics> TypeSemanticsTable = new();

    public SwitchId AddSwitchSemantics(SwitchSemantics switchSemantics)
    {
        var id = SwitchId.New();
        SwitchSemanticsTable.Add(id, switchSemantics);
        return id;
    }

    public InterfaceId AddInterfaceSemantics(InterfaceSemantics interfaceSemantics)
    {
        var id = InterfaceId.New();
        InterfaceSemanticsTable.Add(id, interfaceSemantics);
        return id;
    }

    public ClassId AddTypeSemantics(TypeSemantics typeSemantics)
    {
        var id = ClassId.New();
        TypeSemanticsTable.Add(id, typeSemantics);
        SchemaTypeSemanticsTable.Add(typeSemantics.Schema, id);
        return id;
    }

    public RootId AddRootSemantics(RootSemantics rootSemantics)
    {
        var id = RootId.New();
        RootSemanticsTable.Add(id, rootSemantics);
        return id;
    }

    public PropertyId AddPropertySemantics(PropertySemantics propertySemantics)
    {
        var id = PropertyId.New();
        PropertySemanticsTable.Add(id, propertySemantics);
        return id;
    }

    public InterfacePropertyId AddInterfacePropertySemantics(InterfacePropertySemantics interfacePropertySemantics)
    {
        var id = InterfacePropertyId.New();
        InterfacePropertySemanticsTable.Add(id, interfacePropertySemantics);
        InterfacePropertyIdTable.Add(interfacePropertySemantics, id);
        return id;
    }

    public Semantics Merge(Semantics other)
    {
        foreach (var inherit in other.SwitchInheritList) SwitchInheritList.Add(inherit);
        foreach (var interfaceSemantics in other.InterfaceSemanticsTable) InterfaceSemanticsTable.Add(interfaceSemantics.Key, interfaceSemantics.Value);
        foreach (var interfaceSemantics in other.SwitchSemanticsTable) SwitchSemanticsTable.Add(interfaceSemantics.Key, interfaceSemantics.Value);
        foreach (var rootSemantics in other.RootSemanticsTable) RootSemanticsTable.Add(rootSemantics.Key, rootSemantics.Value);
        foreach (var typeSemantics in other.TypeSemanticsTable) TypeSemanticsTable.Add(typeSemantics.Key, typeSemantics.Value);
        foreach (var kvp in other.SchemaTypeSemanticsTable) SchemaTypeSemanticsTable.Add(kvp.Key, kvp.Value);
        foreach (var kvp in other.PropertySemanticsTable) PropertySemanticsTable.Add(kvp.Key, kvp.Value);
        foreach (var kvp in other.InterfacePropertySemanticsTable) InterfacePropertySemanticsTable.Add(kvp.Key, kvp.Value);
        foreach (var kvp in other.InterfacePropertyIdTable) InterfacePropertyIdTable.Add(kvp.Key, kvp.Value);

        return this;
    }

    public Semantics AddTo(Semantics other)
    {
        return other.Merge(this);
    }
}

public record RootSemantics(Schema Root, ClassId ClassId)
{
    public ClassId ClassId = ClassId;
    public Schema Root = Root;
}

/// <summary>
/// </summary>
/// <param name="ParentType">
///     親のTypeId
///     存在しない場合は親がrootということ
///     親要素がinterfaceになることはない
/// </param>
/// <param name="Schema"></param>
public record TypeSemantics(PropertyId[] Properties, ISchema Schema)
{
    public PropertyId[] Properties = Properties;
    public ISchema Schema = Schema;
}

public record PropertySemantics(ITypeId ParentTypeId, string PropertyName, ITypeId? PropertyType, ISchema Schema, bool IsNullable)
{
    public bool IsNullable = IsNullable;
    public ITypeId ParentTypeId = ParentTypeId;
    public string PropertyName = PropertyName;
    public ITypeId? PropertyType = PropertyType;
    public ISchema Schema = Schema;
}

public record SwitchSemantics(SwitchSchema Schema, (JsonObject, ClassId)[] Types)
{
    public SwitchSchema Schema = Schema;
    public (JsonObject, ClassId)[] Types = Types;
}

public record InterfaceSemantics(Schema Schema, DefineInterface Interface, InterfacePropertyId[] Properties)
{
    public DefineInterface Interface = Interface;
    public InterfacePropertyId[] Properties = Properties;
    public Schema Schema = Schema;
}

public record InterfacePropertySemantics(IDefineInterfacePropertySchema PropertySchema, InterfaceId InterfaceId)
{
    public InterfaceId InterfaceId = InterfaceId;
    public IDefineInterfacePropertySchema PropertySchema = PropertySchema;
}

[UnitOf(typeof(Guid))]
public readonly partial struct RootId;

[UnitOf(typeof(Guid))]
public readonly partial struct PropertyId;

public interface ITypeId;

[UnitOf(typeof(Guid))]
public readonly partial struct ClassId : ITypeId;

[UnitOf(typeof(Guid))]
public readonly partial struct SwitchId : ITypeId;

[UnitOf(typeof(Guid))]
public readonly partial struct InterfaceId : ITypeId;

[UnitOf(typeof(Guid))]
public readonly partial struct InterfacePropertyId : ITypeId;
