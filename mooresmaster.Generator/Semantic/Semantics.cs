using System;
using System.Collections.Generic;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator.Semantic;

public class Semantics
{
    public readonly Dictionary<ClassId, List<InterfaceId>> ClassInterfaceImplementationTable = new();
    public readonly Dictionary<InterfaceId, List<InterfaceId>> InterfaceInterfaceImplementationTable = new();
    public readonly Dictionary<InterfacePropertySemantics, InterfacePropertyId> InterfacePropertyIdTable = new();
    public readonly Dictionary<InterfacePropertyId, InterfacePropertySemantics> InterfacePropertySemanticsTable = new();
    public readonly Dictionary<InterfaceId, InterfaceSemantics> InterfaceSemanticsTable = new();
    public readonly Dictionary<PropertyId, PropertySemantics> PropertySemanticsTable = new();
    public readonly Dictionary<RootId, RootSemantics> RootSemanticsTable = new();
    public readonly Dictionary<ISchema, ClassId> SchemaTypeSemanticsTable = new();
    public readonly List<(SwitchId switchId, ClassId typeId)> SwitchInheritList = new();
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
    
    public void AddInterfaceInterfaceImplementation(InterfaceId target, InterfaceId other)
    {
        if (!InterfaceInterfaceImplementationTable.TryGetValue(target, out var list))
        {
            list = new List<InterfaceId>();
            InterfaceInterfaceImplementationTable[target] = list;
        }
        
        list.Add(other);
    }
    
    public void AddClassInterfaceImplementation(ClassId target, InterfaceId other)
    {
        if (!ClassInterfaceImplementationTable.TryGetValue(target, out var list))
        {
            list = new List<InterfaceId>();
            ClassInterfaceImplementationTable[target] = list;
        }
        
        list.Add(other);
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
        foreach (var kvp in other.InterfaceInterfaceImplementationTable) InterfaceInterfaceImplementationTable.Add(kvp.Key, kvp.Value);
        foreach (var kvp in other.ClassInterfaceImplementationTable) ClassInterfaceImplementationTable.Add(kvp.Key, kvp.Value);
        
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
public record TypeSemantics(PropertyId[] Properties, ISchema Schema, RootId RootId)
{
    public PropertyId[] Properties = Properties;
    public RootId RootId = RootId;
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

public record SwitchSemantics(SwitchSchema Schema, (SwitchPath switchReferencePath, string constValue, ClassId classId)[] Types)
{
    public SwitchSchema Schema = Schema;
    public (SwitchPath switchReferencePath, string constValue, ClassId classId)[] Types = Types;
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

public readonly struct RootId : IEquatable<RootId>, IComparable<RootId>
{
    private readonly MasterId _value;
    
    public RootId(MasterId value)
    {
        _value = value;
    }
    
    public static RootId New()
    {
        return new RootId(new MasterId());
    }
    
    public bool Equals(RootId other)
    {
        return _value.Equals(other._value);
    }
    
    public override bool Equals(object? obj)
    {
        return obj is RootId other && Equals(other);
    }
    
    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }
    
    public int CompareTo(RootId other)
    {
        return _value.CompareTo(other._value);
    }
    
    public override string ToString()
    {
        return _value.ToString();
    }
    
    public static bool operator ==(RootId left, RootId right)
    {
        return left.Equals(right);
    }
    
    public static bool operator !=(RootId left, RootId right)
    {
        return !left.Equals(right);
    }
    
    public static bool operator <(RootId left, RootId right)
    {
        return left.CompareTo(right) < 0;
    }
    
    public static bool operator <=(RootId left, RootId right)
    {
        return left.CompareTo(right) <= 0;
    }
    
    public static bool operator >(RootId left, RootId right)
    {
        return left.CompareTo(right) > 0;
    }
    
    public static bool operator >=(RootId left, RootId right)
    {
        return left.CompareTo(right) >= 0;
    }
}

public readonly struct PropertyId : IEquatable<PropertyId>, IComparable<PropertyId>
{
    private readonly MasterId _value;
    
    public PropertyId(MasterId value)
    {
        _value = value;
    }
    
    public static PropertyId New()
    {
        return new PropertyId(new MasterId());
    }
    
    public bool Equals(PropertyId other)
    {
        return _value.Equals(other._value);
    }
    
    public override bool Equals(object? obj)
    {
        return obj is PropertyId other && Equals(other);
    }
    
    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }
    
    public int CompareTo(PropertyId other)
    {
        return _value.CompareTo(other._value);
    }
    
    public override string ToString()
    {
        return _value.ToString();
    }
    
    public static bool operator ==(PropertyId left, PropertyId right)
    {
        return left.Equals(right);
    }
    
    public static bool operator !=(PropertyId left, PropertyId right)
    {
        return !left.Equals(right);
    }
    
    public static bool operator <(PropertyId left, PropertyId right)
    {
        return left.CompareTo(right) < 0;
    }
    
    public static bool operator <=(PropertyId left, PropertyId right)
    {
        return left.CompareTo(right) <= 0;
    }
    
    public static bool operator >(PropertyId left, PropertyId right)
    {
        return left.CompareTo(right) > 0;
    }
    
    public static bool operator >=(PropertyId left, PropertyId right)
    {
        return left.CompareTo(right) >= 0;
    }
}

public interface ITypeId;

public readonly struct ClassId : ITypeId, IEquatable<ClassId>, IComparable<ClassId>
{
    private readonly MasterId _value;
    
    public ClassId(MasterId value)
    {
        _value = value;
    }
    
    public static ClassId New()
    {
        return new ClassId(new MasterId());
    }
    
    public bool Equals(ClassId other)
    {
        return _value.Equals(other._value);
    }
    
    public override bool Equals(object? obj)
    {
        return obj is ClassId other && Equals(other);
    }
    
    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }
    
    public int CompareTo(ClassId other)
    {
        return _value.CompareTo(other._value);
    }
    
    public override string ToString()
    {
        return _value.ToString();
    }
    
    public static bool operator ==(ClassId left, ClassId right)
    {
        return left.Equals(right);
    }
    
    public static bool operator !=(ClassId left, ClassId right)
    {
        return !left.Equals(right);
    }
    
    public static bool operator <(ClassId left, ClassId right)
    {
        return left.CompareTo(right) < 0;
    }
    
    public static bool operator <=(ClassId left, ClassId right)
    {
        return left.CompareTo(right) <= 0;
    }
    
    public static bool operator >(ClassId left, ClassId right)
    {
        return left.CompareTo(right) > 0;
    }
    
    public static bool operator >=(ClassId left, ClassId right)
    {
        return left.CompareTo(right) >= 0;
    }
}

public readonly struct SwitchId : ITypeId, IEquatable<SwitchId>, IComparable<SwitchId>
{
    private readonly MasterId _value;
    
    public SwitchId(MasterId value)
    {
        _value = value;
    }
    
    public static SwitchId New()
    {
        return new SwitchId(new MasterId());
    }
    
    public bool Equals(SwitchId other)
    {
        return _value.Equals(other._value);
    }
    
    public override bool Equals(object? obj)
    {
        return obj is SwitchId other && Equals(other);
    }
    
    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }
    
    public int CompareTo(SwitchId other)
    {
        return _value.CompareTo(other._value);
    }
    
    public override string ToString()
    {
        return _value.ToString();
    }
    
    public static bool operator ==(SwitchId left, SwitchId right)
    {
        return left.Equals(right);
    }
    
    public static bool operator !=(SwitchId left, SwitchId right)
    {
        return !left.Equals(right);
    }
    
    public static bool operator <(SwitchId left, SwitchId right)
    {
        return left.CompareTo(right) < 0;
    }
    
    public static bool operator <=(SwitchId left, SwitchId right)
    {
        return left.CompareTo(right) <= 0;
    }
    
    public static bool operator >(SwitchId left, SwitchId right)
    {
        return left.CompareTo(right) > 0;
    }
    
    public static bool operator >=(SwitchId left, SwitchId right)
    {
        return left.CompareTo(right) >= 0;
    }
}

public readonly struct InterfaceId : ITypeId, IEquatable<InterfaceId>, IComparable<InterfaceId>
{
    private readonly MasterId _value;
    
    public InterfaceId(MasterId value)
    {
        _value = value;
    }
    
    public static InterfaceId New()
    {
        return new InterfaceId(new MasterId());
    }
    
    public bool Equals(InterfaceId other)
    {
        return _value.Equals(other._value);
    }
    
    public override bool Equals(object? obj)
    {
        return obj is InterfaceId other && Equals(other);
    }
    
    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }
    
    public int CompareTo(InterfaceId other)
    {
        return _value.CompareTo(other._value);
    }
    
    public override string ToString()
    {
        return _value.ToString();
    }
    
    public static bool operator ==(InterfaceId left, InterfaceId right)
    {
        return left.Equals(right);
    }
    
    public static bool operator !=(InterfaceId left, InterfaceId right)
    {
        return !left.Equals(right);
    }
    
    public static bool operator <(InterfaceId left, InterfaceId right)
    {
        return left.CompareTo(right) < 0;
    }
    
    public static bool operator <=(InterfaceId left, InterfaceId right)
    {
        return left.CompareTo(right) <= 0;
    }
    
    public static bool operator >(InterfaceId left, InterfaceId right)
    {
        return left.CompareTo(right) > 0;
    }
    
    public static bool operator >=(InterfaceId left, InterfaceId right)
    {
        return left.CompareTo(right) >= 0;
    }
}

public readonly struct InterfacePropertyId : ITypeId, IEquatable<InterfacePropertyId>, IComparable<InterfacePropertyId>
{
    private readonly MasterId _value;
    
    public InterfacePropertyId(MasterId value)
    {
        _value = value;
    }
    
    public static InterfacePropertyId New()
    {
        return new InterfacePropertyId(new MasterId());
    }
    
    public bool Equals(InterfacePropertyId other)
    {
        return _value.Equals(other._value);
    }
    
    public override bool Equals(object? obj)
    {
        return obj is InterfacePropertyId other && Equals(other);
    }
    
    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }
    
    public int CompareTo(InterfacePropertyId other)
    {
        return _value.CompareTo(other._value);
    }
    
    public override string ToString()
    {
        return _value.ToString();
    }
    
    public static bool operator ==(InterfacePropertyId left, InterfacePropertyId right)
    {
        return left.Equals(right);
    }
    
    public static bool operator !=(InterfacePropertyId left, InterfacePropertyId right)
    {
        return !left.Equals(right);
    }
    
    public static bool operator <(InterfacePropertyId left, InterfacePropertyId right)
    {
        return left.CompareTo(right) < 0;
    }
    
    public static bool operator <=(InterfacePropertyId left, InterfacePropertyId right)
    {
        return left.CompareTo(right) <= 0;
    }
    
    public static bool operator >(InterfacePropertyId left, InterfacePropertyId right)
    {
        return left.CompareTo(right) > 0;
    }
    
    public static bool operator >=(InterfacePropertyId left, InterfacePropertyId right)
    {
        return left.CompareTo(right) >= 0;
    }
}