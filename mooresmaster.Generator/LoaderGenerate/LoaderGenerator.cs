using System;
using System.Linq;
using mooresmaster.Generator.Definitions;
using mooresmaster.Generator.NameResolve;
using mooresmaster.Generator.Semantic;
using Type = mooresmaster.Generator.Definitions.Type;

namespace mooresmaster.Generator.LoaderGenerate;

public record LoaderFile(string FileName, string Code)
{
    public string Code = Code;
    public string FileName = FileName;
}

public static class LoaderGenerator
{
    public static LoaderFile[] Generate(Definition definition, Semantics semantics)
    {
        return definition
            .TypeDefinitions
            .Select(typeDefinition => GenerateTypeLoaderCode(typeDefinition, semantics))
            .Select(value => new LoaderFile(value.fileName, value.code))
            .ToArray();
    }

    private static (string fileName, string code) GenerateTypeLoaderCode(TypeDefinition typeDefinition, Semantics semantics)
    {
        var targetType = typeDefinition.TypeName;
        var targetTypeName = targetType.GetModelName();

        // プロパティIdが全てnullならroot
        var isRoot = typeDefinition.PropertyTable.All(property => property.Value.PropertyId == null);
        var args = isRoot
            ? typeDefinition.PropertyTable.First().Value.Type switch
            {
                BooleanType or IntType or FloatType or StringType => "global::Newtonsoft.Json.Linq.JValue json",
                ArrayType => "global::Newtonsoft.Json.Linq.JArray json",
                DictionaryType or CustomType => "global::Newtonsoft.Json.Linq.JObject json",
                _ => throw new ArgumentOutOfRangeException()
            }
            : "global::Newtonsoft.Json.Linq.JObject json";
        var propertyLoaderCode = isRoot
            ? GenerateRootPropertyLoaderCode(typeDefinition)
            : GeneratePropertiesLoaderCode(typeDefinition, semantics);

        return (
            $"mooresmaster.loader.{typeDefinition.TypeName.ModuleName}.{typeDefinition.TypeName.Name}.g.cs",
            $$$"""
               namespace Mooresmaster.Loader.{{{typeDefinition.TypeName.ModuleName}}}
               {
                   public static class {{{typeDefinition.TypeName.Name}}}Loader
                   {
                       public static {{{targetTypeName}}} Load({{{args}}})
                       {
                           {{{propertyLoaderCode.Indent(level: 3)}}}
                       
                           return new {{{targetTypeName}}}({{{string.Join(", ", typeDefinition.PropertyTable.Select(property => property.Key))}}});
                       }
                   }
               }
               """
        );
    }

    private static string GenerateRootPropertyLoaderCode(TypeDefinition typeDefinition)
    {
        var property = typeDefinition.PropertyTable.First();

        return $"var {property.Key} = {GetLoaderName(property.Value.Type)}(json);";
    }

    private static string GeneratePropertiesLoaderCode(TypeDefinition typeDefinition, Semantics semantics)
    {
        return string.Join(
            "\n",
            typeDefinition
                .PropertyTable
                .Select(property => $"var {property.Key} = {GeneratePropertyLoaderCode(property.Value, semantics)};")
        );
    }

    private static string GeneratePropertyLoaderCode(PropertyDefinition propertyDefinition, Semantics semantics)
    {
        return propertyDefinition.Type switch
        {
            BooleanType
                or FloatType
                or IntType
                or StringType
                or UUIDType
                or Vector2Type
                or Vector2IntType
                or Vector3Type
                or Vector3IntType
                or Vector4Type
                or CustomType => $$$"""
                                    {{{GetLoaderName(propertyDefinition.Type)}}}(json["{{{semantics.PropertySemanticsTable[propertyDefinition.PropertyId.Value].PropertyName}}}"])
                                    """,
            ArrayType arrayType => $$$"""
                                      new {{{arrayType.}}}[json]
                                      """,
            DictionaryType dictionaryType => throw new NotImplementedException(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static string GetLoaderName(Type type)
    {
        return $"{
            type switch
            {
                BooleanType booleanType => "Mooresmaster.Loader.BuiltinLoader.LoadBoolean",
                FloatType floatType => "Mooresmaster.Loader.BuiltinLoader.LoadFloat",
                IntType intType => "Mooresmaster.Loader.BuiltinLoader.LoadInt",
                StringType stringType => "Mooresmaster.Loader.BuiltinLoader.LoadString",
                UUIDType uuidType => "Mooresmaster.Loader.BuiltinLoader.LoadUUID",
                Vector2IntType vector2IntType => "Mooresmaster.Loader.BuiltinLoader.LoadVector2Int",
                Vector2Type vector2Type => "Mooresmaster.Loader.BuiltinLoader.LoadVector2",
                Vector3IntType vector3IntType => "Mooresmaster.Loader.BuiltinLoader.LoadVector3Int",
                Vector3Type vector3Type => "Mooresmaster.Loader.BuiltinLoader.LoadVector3",
                Vector4Type vector4Type => "Mooresmaster.Loader.BuiltinLoader.LoadVector4",
                CustomType customType => $"{customType.Name.GetLoaderName()}.Load",
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
                       public static bool LoadBoolean(global::Newtonsoft.Json.Linq.JValue json)
                       {
                           return (bool)json;
                       }
                   
                       public static float LoadFloat(global::Newtonsoft.Json.Linq.JValue json)
                       {
                           return default;
                       }
                       
                       public static string LoadString(global::Newtonsoft.Json.Linq.JValue json)
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
