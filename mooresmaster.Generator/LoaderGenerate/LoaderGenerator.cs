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
        var propertyLoaderCode = isRoot
            ? GenerateRootPropertyLoaderCode(typeDefinition, semantics)
            : GeneratePropertiesLoaderCode(typeDefinition, semantics);

        return (
            $"mooresmaster.loader.{typeDefinition.TypeName.ModuleName}.{typeDefinition.TypeName.Name}.g.cs",
            $$$"""
               namespace Mooresmaster.Loader.{{{typeDefinition.TypeName.ModuleName}}}
               {
                   public static class {{{typeDefinition.TypeName.Name}}}Loader
                   {
                       public static {{{targetTypeName}}} Load(global::Newtonsoft.Json.Linq.JToken json)
                       {
                           {{{propertyLoaderCode.Indent(level: 3)}}}
                       
                           return new {{{targetTypeName}}}({{{string.Join(", ", typeDefinition.PropertyTable.Select(property => property.Key))}}});
                       }
                   }
               }
               """
        );
    }

    private static string GenerateRootPropertyLoaderCode(TypeDefinition typeDefinition, Semantics semantics)
    {
        var property = typeDefinition.PropertyTable.First();

        return $"var {property.Key} = {GeneratePropertyLoaderCode(property.Value, semantics, "json")};";
    }

    private static string GeneratePropertiesLoaderCode(TypeDefinition typeDefinition, Semantics semantics)
    {
        return string.Join(
            "\n",
            typeDefinition
                .PropertyTable
                .Select(property => $"var {property.Key} = {GeneratePropertyLoaderCode(
                    property.Value,
                    semantics,
                    $$$"""
                       json["{{{semantics.PropertySemanticsTable[property.Value.PropertyId.Value].PropertyName}}}"]
                       """)};"
                )
        );
    }

    private static string GeneratePropertyLoaderCode(PropertyDefinition propertyDefinition, Semantics semantics, string json)
    {
        return propertyDefinition.Type switch
        {
            BooleanType
                or FloatType
                or IntType
                or StringType
                => $$$"""
                      {{{GetLoaderName(propertyDefinition.Type)}}}((global::Newtonsoft.Json.Linq.JValue){{{json}}})
                      """,

            UUIDType
                or Vector2Type
                or Vector2IntType
                or Vector3Type
                or Vector3IntType
                or Vector4Type => $$$"""
                                     {{{GetLoaderName(propertyDefinition.Type)}}}((global::Newtonsoft.Json.Linq.JArray){{{json}}})
                                     """,
            CustomType => $$$"""
                             {{{GetLoaderName(propertyDefinition.Type)}}}((global::Newtonsoft.Json.Linq.JObject){{{json}}})
                             """,

            ArrayType arrayType => $$$"""
                                      new {{{arrayType.InnerType.GetName()}}}[{{{json}}}.Count()]
                                      """,
            DictionaryType dictionaryType => $$$"""
                                                new global::System.Collections.Generic.Dictionary<{{{dictionaryType.KeyType.GetName()}}}, {{{dictionaryType.ValueType.GetName()}}}>()
                                                """,

            _ => throw new ArgumentOutOfRangeException(nameof(propertyDefinition.Type))
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
                       public static bool LoadBoolean(global::Newtonsoft.Json.Linq.JToken json)
                       {
                           return (bool)json;
                       }
                   
                       public static float LoadFloat(global::Newtonsoft.Json.Linq.JToken json)
                       {
                           return default;
                       }
                       
                       public static int LoadInt(global::Newtonsoft.Json.Linq.JToken json)
                       {
                           return default;
                       }
                       
                       public static string LoadString(global::Newtonsoft.Json.Linq.JToken json)
                       {
                           return default;
                       }
                       
                       public static global::UnityEngine.Vector2Int LoadVector2Int(global::Newtonsoft.Json.Linq.JToken json)
                       {
                           return default;
                       }
                       
                       public static global::UnityEngine.Vector2 LoadVector2(global::Newtonsoft.Json.Linq.JToken json)
                       {
                           return default;
                       }
                       
                       public static global::UnityEngine.Vector3Int LoadVector3Int(global::Newtonsoft.Json.Linq.JToken json)
                       {
                           return default;
                       }
                       
                       public static global::UnityEngine.Vector3 LoadVector3(global::Newtonsoft.Json.Linq.JToken json)
                       {
                           return default;
                       }
                       
                       public static global::UnityEngine.Vector4 LoadVector4(global::Newtonsoft.Json.Linq.JToken json)
                       {
                           return default;
                       }
                       
                       public static global::System.Guid LoadUUID(global::Newtonsoft.Json.Linq.JToken json)
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
