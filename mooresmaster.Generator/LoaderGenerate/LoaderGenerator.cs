using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using mooresmaster.Generator.CodeGenerate;
using mooresmaster.Generator.Definitions;
using mooresmaster.Generator.Json;
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
    public static LoaderFile[] Generate(Definition definition, Semantics semantics, NameTable nameTable)
    {
        var inheritTable = new Dictionary<InterfaceId, List<ClassId>>();

        foreach (var (interfaceId, classId) in semantics.InheritList)
            if (inheritTable.ContainsKey(interfaceId))
                inheritTable[interfaceId].Add(classId);
            else
                inheritTable[interfaceId] = [classId];

        return definition
            .TypeDefinitions
            .Select(typeDefinition => GenerateTypeLoaderCode(typeDefinition, semantics, nameTable))
            .Concat(
                inheritTable
                    .Select(
                        inherit => GenerateInterfaceLoaderCode(inherit.Key, semantics, nameTable)
                    )
            )
            .Append(GenerateGlobalLoaderCode(semantics, nameTable))
            .Select(value => new LoaderFile(value.fileName, value.code.GetPreprocessedCode()))
            .ToArray();
    }

    private static (string fileName, string code) GenerateGlobalLoaderCode(Semantics semantics, NameTable nameTable)
    {
        try
        {
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        return (
            "mooresmaster.loader.g.cs",
            $$$"""
               namespace Mooresmaster.Loader
               {
                   public static class GlobalLoader
                   {
                       public static object Load(global::Newtonsoft.Json.Linq.JToken json)
                       {
                           {{{string.Join(
                               "\n",
                               semantics
                                   .RootSemanticsTable
                                   .Select(root => {
                                           var name = nameTable.TypeNames[root.Value.ClassId];

                                           return $$$"""
                                                     try
                                                     {
                                                         // code
                                                         return Mooresmaster.Loader.{{{name.ModuleName}}}.{{{name.Name}}}Loader.Load(json);
                                                     }
                                                     catch
                                                     {
                                                     }
                                                     """;
                                       }
                                   )
                           ).Indent(level: 3)}}}
                           
                           throw new global::System.NotImplementedException();
                       }
                   }
               }
               """
        );
    }

    private static (string fileName, string code) GenerateTypeLoaderCode(TypeDefinition typeDefinition, Semantics semantics, NameTable nameTable)
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
                           {{{GenerateNullCheckCode(typeDefinition, semantics).Indent(level: 3)}}}
                       
                           {{{propertyLoaderCode.Indent(level: 3)}}}
                       
                           return new {{{targetTypeName}}}({{{string.Join(", ", typeDefinition.PropertyTable.Select(property => property.Key))}}});
                       }
                   }
               }
               """
        );
    }

    private static string GenerateNullCheckCode(TypeDefinition typeDefinition, Semantics semantics)
    {
        StringBuilder builder = new();

        foreach (var propertyDefinition in typeDefinition.PropertyTable.Values.Where(v => v.PropertyId.HasValue).Where(v => !semantics.PropertySemanticsTable[v.PropertyId!.Value].IsNullable))
        {
            var propertyName = semantics.PropertySemanticsTable[propertyDefinition.PropertyId!.Value].PropertyName;

            builder.AppendLine(
                $$$"""
                   if (json["{{{propertyName}}}"] == null)
                   {
                       var errorMessage = $"SchemaLoadError\nErrorPath: {json.Path}\nTargetProperty: {{{propertyName}}}\n\n";
                       
                       var parent = json.Parent;
                       while (parent != null)
                       {
                       //    errorMessage += parent.ToString() + "\n";
                           parent = parent.Parent;
                       }
                   
                       throw new global::System.Exception(errorMessage);
                   }

                   """
            );
        }

        return builder.ToString();
    }

    private static (string fileName, string code) GenerateInterfaceLoaderCode(InterfaceId interfaceId, Semantics semantics, NameTable nameTable)
    {
        var interfaceSemantics = semantics.InterfaceSemanticsTable[interfaceId];

        var name = nameTable.TypeNames[interfaceId];

        return (
            $"mooresmaster.loader.{name.ModuleName}.{name.Name}.g.cs",
            $$$"""
               namespace Mooresmaster.Loader.{{{name.ModuleName}}}
               {
                   public static class {{{name.Name}}}Loader
                   {
                       public static global::Mooresmaster.Model.{{{name.ModuleName}}}.{{{name.Name}}} Load(global::Newtonsoft.Json.Linq.JToken json)
                       {
                           {{{string.Join("\n", interfaceSemantics.Types.Select(value => GenerateInterfaceInheritedTypeLoaderCode(value.Item1, value.Item2, nameTable))).Indent(level: 3)}}}
                           
                           throw new global::System.NotImplementedException();
                       }
                   }
               }
               """
        );
    }

    private static string GenerateInterfaceCheckCode(string currentJson, string propertyName, string constValue)
    {
        return $$$"""
                  (string)({{{currentJson}}}.Parent.Parent["{{{propertyName}}}"]) == "{{{constValue}}}"
                  """;
    }

    private static string GenerateInterfaceInheritedTypeLoaderCode(JsonObject ifObject, ClassId classId, NameTable nameTable)
    {
        var name = nameTable.TypeNames[classId];
        var properties = (JsonObject)ifObject.Nodes["properties"];
        var firstProperty = properties.Nodes.First();
        var propertyName = firstProperty.Key;
        var constValue = ((JsonString)((JsonObject)firstProperty.Value).Nodes["const"]).Literal;


        return $$$"""
                  if ({{{GenerateInterfaceCheckCode("json", propertyName, constValue)}}})
                  {
                      return {{{name.GetLoaderName()}}}.Load(json);
                  }

                  """;
    }

    private static string GenerateRootPropertyLoaderCode(TypeDefinition typeDefinition, Semantics semantics)
    {
        if (typeDefinition.PropertyTable.Count == 0) return string.Empty;
        var property = typeDefinition.PropertyTable.First();

        return $"{property.Value.Type.GetName()} {property.Key} = {GeneratePropertyLoaderCode(property.Value.Type, "json")};";
    }

    private static string GeneratePropertiesLoaderCode(TypeDefinition typeDefinition, Semantics semantics)
    {
        return string.Join(
            "\n",
            typeDefinition
                .PropertyTable
                .Select(property => $"{property.Value.Type.GetName()} {property.Key} = {GeneratePropertyLoaderCode(
                    property.Value.Type,
                    $$$"""
                       json["{{{semantics.PropertySemanticsTable[property.Value.PropertyId.Value].PropertyName}}}"]
                       """)};"
                )
        );
    }

    private static string GeneratePropertyLoaderCode(Type type, string json)
    {
        return type switch
        {
            BooleanType
                or FloatType
                or IntType
                or StringType
                or UUIDType
                => $$$"""
                      {{{GetLoaderName(type)}}}({{{json}}})
                      """,

            Vector2Type
                or Vector2IntType
                or Vector3Type
                or Vector3IntType
                or Vector4Type => $$$"""
                                     {{{GetLoaderName(type)}}}({{{json}}})
                                     """,

            CustomType => $$$"""
                             {{{GetLoaderName(type)}}}({{{json}}})
                             """,

            ArrayType arrayType => $$$"""
                                      global::System.Linq.Enumerable.ToArray(global::System.Linq.Enumerable.Select({{{json}}}, value => {{{GeneratePropertyLoaderCode(arrayType.InnerType, "value")}}}))
                                      """,

            DictionaryType dictionaryType => $$$"""
                                                new global::System.Collections.Generic.Dictionary<{{{dictionaryType.KeyType.GetName()}}}, {{{dictionaryType.ValueType.GetName()}}}>()
                                                {{{json}}}.ToDictionary(key => {{{GeneratePropertyLoaderCode(dictionaryType.KeyType, "key")}}}, value => {{{GeneratePropertyLoaderCode(dictionaryType.ValueType, "value")}}})
                                                """,

            NullableType nullableType => $$$"""
                                            (({{{json}}} == null) ? null : {{{GeneratePropertyLoaderCode(nullableType.InnerType, json)}}})
                                            """,

            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    private static string GenerateNullableUnwrapCode(Type type)
    {
        return type switch
        {
            BooleanType or FloatType or IntType or UUIDType or Vector2IntType or Vector2Type or Vector3IntType or Vector3Type or Vector4Type => "!.Value", // 値型のため!.Valueを追加
            ArrayType or DictionaryType or StringType or CustomType => "", // 参照型のためそのまま
            _ => throw new ArgumentOutOfRangeException(nameof(type)) // nullableは来てはいけない
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
                NullableType nullableType => GetLoaderName(nullableType.InnerType),
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
                           return (float)json;
                       }
                       
                       public static int LoadInt(global::Newtonsoft.Json.Linq.JToken json)
                       {
                           return (int)json;
                       }
                       
                       public static string LoadString(global::Newtonsoft.Json.Linq.JToken json)
                       {
                           return (string)json;
                       }
                       
                       public static global::UnityEngine.Vector2Int LoadVector2Int(global::Newtonsoft.Json.Linq.JToken json)
                       {
                           return new global::UnityEngine.Vector2Int((int)json[0], (int)json[1]);
                       }
                       
                       public static global::UnityEngine.Vector2 LoadVector2(global::Newtonsoft.Json.Linq.JToken json)
                       {
                           return new global::UnityEngine.Vector2((float)json[0], (float)json[1]);
                       }
                       
                       public static global::UnityEngine.Vector3Int LoadVector3Int(global::Newtonsoft.Json.Linq.JToken json)
                       {
                           return new global::UnityEngine.Vector3Int((int)json[0], (int)json[1], (int)json[2]);
                       }
                       
                       public static global::UnityEngine.Vector3 LoadVector3(global::Newtonsoft.Json.Linq.JToken json)
                       {
                           return new global::UnityEngine.Vector3((float)json[0], (float)json[1], (float)json[2]);
                       }
                       
                       public static global::UnityEngine.Vector4 LoadVector4(global::Newtonsoft.Json.Linq.JToken json)
                       {
                           return new global::UnityEngine.Vector4((float)json[0], (float)json[1], (float)json[2], (float)json[3]);
                       }
                       
                       public static global::System.Guid LoadUUID(global::Newtonsoft.Json.Linq.JToken json)
                       {
                           return new global::System.Guid((string)json);
                       }
                   }
               }
               """.GetPreprocessedCode();
    }

    private static string Indent(this string code, bool firstLine = false, int level = 1)
    {
        var indent = new string(' ', 4 * level);
        return firstLine ? indent : "" + code.Replace("\n", $"\n{indent}");
    }
}
