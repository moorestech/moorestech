using System;
using System.Linq;
using mooresmaster.Generator.Definitions;
using mooresmaster.Generator.NameResolve;
using Type = mooresmaster.Generator.Definitions.Type;

namespace mooresmaster.Generator.LoaderGenerate;

public record LoaderFile(string FileName, string Code)
{
    public string Code = Code;
    public string FileName = FileName;
}

public static class LoaderGenerator
{
    public static LoaderFile[] Generate(Definition definition)
    {
        return definition
            .TypeDefinitions
            .Select(GenerateTypeLoaderCode)
            .Select(value => new LoaderFile(value.fileName, value.code))
            .ToArray();
    }

    private static (string fileName, string code) GenerateTypeLoaderCode(TypeDefinition typeDefinition)
    {
        var targetType = typeDefinition.TypeName;
        var targetTypeName = targetType.GetModelName();

        return (
            $"mooresmaster.loader.{typeDefinition.TypeName.ModuleName}.{typeDefinition.TypeName.Name}.g.cs",
            $$$"""
               namespace Mooresmaster.Loader.{{{typeDefinition.TypeName.ModuleName}}}
               {
                   public static class {{{typeDefinition.TypeName.Name}}}Loader
                   {
                       public static {{{targetTypeName}}} Load()
                       {
                           {{{GeneratePropertyLoaderCode(typeDefinition)}}}
                       
                           return new {{{targetTypeName}}}({{{string.Join(", ", typeDefinition.PropertyTable.Select(property => property.Key))}}});
                       }
                   }
               }
               """
        );
    }

    private static string GeneratePropertyLoaderCode(TypeDefinition typeDefinition)
    {
        return string.Join(
            "\n",
            typeDefinition
                .PropertyTable
                .Select(property => $$$"""
                                       var {{{property.Key}}} = {{{GetLoaderName(property.Value)}}}();
                                       """)
        );
    }

    private static string GetLoaderName(Type type)
    {
        return $"Mooresmaster.Loader.{
            type switch
            {
                BooleanType booleanType => "BuiltinLoader.LoadBoolean",
                ArrayType arrayType => "BuiltinLoader.LoadArray",
                DictionaryType dictionaryType => "BuiltinLoader.LoadDictionary",
                FloatType floatType => "BuiltinLoader.LoadBoolean",
                IntType intType => "BuiltinLoader.LoadInt",
                StringType stringType => "BuiltinLoader.LoadString",
                UUIDType uuidType => "BuiltinLoader.LoadUUID",
                Vector2IntType vector2IntType => "BuiltinLoader.LoadVector2Int",
                Vector2Type vector2Type => "BuiltinLoader.LoadVector2",
                Vector3IntType vector3IntType => "BuiltinLoader.LoadVector3Int",
                Vector3Type vector3Type => "BuiltinLoader.LoadVector3",
                Vector4Type vector4Type => "BuiltinLoader.LoadVector4",
                CustomType customType => $"{customType.Name}Loader.Load",
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            }
        }";
    }

    public static string GenerateBuiltinLoaderCode()
    {
        return """
               namespace Mooresmaster.Loader
               {
                   public static class BuiltinLoader
                   {
                       public static bool LoadBoolean()
                       {
                           return default;
                       }
                   }
               }
               """;
    }

    private static string Indent(this string code, bool firstLine = false, int level = 1)
    {
        var indent = new string(' ', 4 * level);
        return firstLine ? indent : "" + code.Replace("\n", $"\n{indent}");
    }
}
