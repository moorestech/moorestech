using System;
using mooresmaster.Generator.JsonSchema;
using mooresmaster.Generator.NameResolve;
using mooresmaster.Generator.Semantic;

namespace mooresmaster.Generator.Definitions;

public record Type
{
    public static Type GetType(NameTable nameTable, Guid? typeId, ISchema schema, Semantics semantics, SchemaTable schemaTable)
    {
        return schema switch
        {
            ArraySchema arraySchema => arraySchema.Pattern?.Literal switch
            {
                "@vector2" => new Vector2Type(),
                _ => new ArrayType(GetType(
                    nameTable,
                    semantics.SchemaTypeSemanticsTable.ContainsKey(schemaTable.Table[arraySchema.Items])
                        ? semantics.SchemaTypeSemanticsTable[schemaTable.Table[arraySchema.Items]]
                        : null,
                    schemaTable.Table[arraySchema.Items],
                    semantics,
                    schemaTable
                ))
            },
            BooleanSchema => new BooleanType(),
            IntegerSchema => new IntType(),
            NumberSchema => new FloatType(),
            StringSchema => new StringType(),
            ObjectSchema => new CustomType(nameTable.Names[typeId!.Value].GetName()),
            OneOfSchema => new CustomType(nameTable.Names[typeId!.Value].GetName()),
            RefSchema refSchema => new CustomType(refSchema.Ref),
            _ => throw new ArgumentOutOfRangeException(nameof(schema))
        };
    }
}

public record BuiltinType : Type;

public record StringType : BuiltinType;

public record BooleanType : BuiltinType;

public record IntType : BuiltinType;

public record FloatType : BuiltinType;

public record ArrayType(Type InnerType) : BuiltinType
{
    public Type InnerType = InnerType;
}

public record Vector2Type : BuiltinType;

public record DictionaryType(Type KeyType, Type ValueType) : BuiltinType
{
    public Type KeyType = KeyType;
    public Type ValueType = ValueType;
}

public record CustomType(string Name) : Type
{
    public string Name = Name;
}
