using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using mooresmaster.Generator.Definitions;
using mooresmaster.Generator.JsonSchema;
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
        var inheritTable = new Dictionary<SwitchId, List<ClassId>>();
        foreach (var (interfaceId, classId) in semantics.SwitchInheritList)
            if (inheritTable.ContainsKey(interfaceId))
                inheritTable[interfaceId].Add(classId);
            else
                inheritTable[interfaceId] = [classId];
        var loaderFiles = definition
            .TypeDefinitions
            .Select(typeDefinition => GenerateTypeLoaderCode(typeDefinition, semantics, nameTable, definition))
            .Concat(
                inheritTable
                    .Select(inherit => GenerateSwitchLoaderCode(inherit.Key, semantics, nameTable)
                    )
            )
            .Append(GenerateGlobalLoaderCode(semantics, nameTable))
            .Select(value => new LoaderFile(value.fileName, value.code))
            .ToList();
        for (var i = loaderFiles.Count - 1; i >= 0; i--)
        for (var j = 0; j < i; j++)
            if (loaderFiles[j].FileName == loaderFiles[i].FileName)
            {
                var removeLoaderFile = loaderFiles[i];
                var loaderFile = loaderFiles[j];
                loaderFile.Code += $"\n{removeLoaderFile.Code}";
                loaderFiles.RemoveAt(i);
                break;
            }
        
        return loaderFiles.ToArray();
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
            Tokens.LoaderFileName,
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
    
    private static (string fileName, string code) GenerateTypeLoaderCode(TypeDefinition typeDefinition, Semantics semantics, NameTable nameTable, Definition definition)
    {
        var targetType = typeDefinition.TypeName;
        var targetTypeName = targetType.GetModelName();
        // プロパティIdが全てnullならroot
        var isRoot = typeDefinition.PropertyTable.All(property => property.Value.PropertyId == null);
        var propertyLoaderCode = isRoot
            ? GenerateRootPropertyLoaderCode(typeDefinition, semantics, definition)
            : GeneratePropertiesLoaderCode(typeDefinition, semantics, definition);

        // IsArrayInnerTypeかどうかを判定
        var isArrayInnerType = false;
        if (definition.TypeNameToClassId.TryGetValue(typeDefinition.TypeName, out var classId))
        {
            isArrayInnerType = semantics.TypeSemanticsTable[classId].IsArrayInnerType;
        }

        var loadMethodParams = isArrayInnerType ? "int Index, global::Newtonsoft.Json.Linq.JToken json" : "global::Newtonsoft.Json.Linq.JToken json";
        var constructorArgs = isArrayInnerType
            ? string.Join(", ", new[] { "Index" }.Concat(typeDefinition.PropertyTable.Select(property => property.Key)))
            : string.Join(", ", typeDefinition.PropertyTable.Select(property => property.Key));

        return (
            $"mooresmaster.loader.{typeDefinition.TypeName.ModuleName}.{typeDefinition.TypeName.Name}.g.cs",
            $$$"""
               namespace Mooresmaster.Loader.{{{typeDefinition.TypeName.ModuleName}}}
               {
                   public static class {{{typeDefinition.TypeName.Name}}}Loader
                   {
                       public static {{{targetTypeName}}} Load({{{loadMethodParams}}})
                       {
                           {{{GenerateNullCheckCode(typeDefinition, semantics).Indent(level: 3)}}}

                           {{{propertyLoaderCode.Indent(level: 3)}}}

                           return new {{{targetTypeName}}}({{{constructorArgs}}});
                       }
                   }
               }
               """
        );
    }
    
    private static string GenerateNullCheckCode(TypeDefinition typeDefinition, Semantics semantics)
    {
        StringBuilder builder = new();
        foreach (var propertyDefinition in typeDefinition.PropertyTable.Values
                     .Where(v => v.PropertyId.HasValue)
                     .Where(p => !p.IsNullable)
                     .Where(v => !semantics.PropertySemanticsTable[v.PropertyId!.Value].IsNullable)
                     .Where(v =>
                     {
                         var schema = semantics.PropertySemanticsTable[v.PropertyId!.Value].Schema;
                         if (schema is not SwitchSchema switchSchema) return true;
                         return !switchSchema.HasOptionalCase;
                     })
                )
        {
            var jsonPropertyName = semantics.PropertySemanticsTable[propertyDefinition.PropertyId!.Value].PropertyName;
            var typeName = typeDefinition.TypeName.GetModelName();
            builder.AppendLine(
                $$$$"""
                    if (json["{{{{jsonPropertyName}}}}"] == null)
                    {
                        var propertyPath = json.Parent == null ? "{{{{jsonPropertyName}}}}" : $"{json.Path}.{{{{jsonPropertyName}}}}";
                        throw new global::Mooresmaster.Loader.MooresmasterLoaderException(propertyPath, typeof({{{{typeName}}}}).Name, "{{{{propertyDefinition.PropertyName}}}}");
                    }
                    """
            );
        }
        
        return builder.ToString();
    }
    
    private static (string fileName, string code) GenerateSwitchLoaderCode(SwitchId switchId, Semantics semantics, NameTable nameTable)
    {
        var switchSemantics = semantics.SwitchSemanticsTable[switchId];
        var name = nameTable.TypeNames[switchId];
        var hasOptionalCase = switchSemantics.Schema.HasOptionalCase;
        return (
            $"mooresmaster.loader.{name.ModuleName}.{name.Name}.g.cs",
            $$$"""
               namespace Mooresmaster.Loader.{{{name.ModuleName}}}
               {
                   public static class {{{name.Name}}}Loader
                   {
                       public static global::Mooresmaster.Model.{{{name.ModuleName}}}.{{{name.Name}}}{{{(hasOptionalCase ? "?" : "")}}} Load(global::Newtonsoft.Json.Linq.JToken parentJson)
                       {
                           {{{string.Join("\n", switchSemantics.Types.Select(value => GenerateSwitchInheritedTypeLoaderCode("parentJson", switchSemantics, semantics, value.switchReferencePath, value.constValue, value.classId, nameTable))).Indent(level: 3)}}}
                           
                           throw new global::System.NotImplementedException(parentJson.Path);
                       }
                   }
               }
               """
        );
    }
    
    private static string GenerateSwitchCheckCode(string currentJson, SwitchPath switchReferencePath, string constValue)
    {
        var path = "";
        switch (switchReferencePath.Type)
        {
            case SwitchPathType.Absolute:
                path += ".Root";
                break;
            case SwitchPathType.Relative:
                path += "";
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        
        foreach (var element in switchReferencePath.Elements)
            switch (element)
            {
                case NormalSwitchPathElement normalSwitchPathElement:
                    path += $"[\"{normalSwitchPathElement.Path}\"]";
                    break;
                case ParentSwitchPathElement parentSwitchPathElement:
                    path += ".Parent";
                    break;
            }
        
        return $$$"""
                  (string)({{{currentJson}}}{{{path}}}) == "{{{constValue}}}"
                  """;
    }
    
    private static string GenerateSwitchInheritedTypeLoaderCode(string parentJsonName, SwitchSemantics switchSemantics, Semantics semantics, SwitchPath switchReferencePath, string constValue, ClassId classId, NameTable nameTable)
    {
        var name = nameTable.TypeNames[classId];
        var typeSemantics = semantics.TypeSemanticsTable[classId];
        var propertyName = switchSemantics.Schema.PropertyName;
        
        return $$$"""
                  if ({{{GenerateSwitchCheckCode(parentJsonName, switchReferencePath, constValue)}}})
                  {
                      if ({{{parentJsonName}}}["{{{propertyName}}}"] == null) {{{(typeSemantics.Schema.IsNullable ? "return null;" : $"throw new global::Mooresmaster.Loader.MooresmasterLoaderException({parentJsonName}.Path, typeof({name.GetModelName()}).Name, \"{propertyName}\");")}}}
                      else return {{{name.GetLoaderName()}}}.Load({{{parentJsonName}}}["{{{propertyName}}}"]);
                  }
                  """;
    }
    
    private static string GenerateRootPropertyLoaderCode(TypeDefinition typeDefinition, Semantics semantics, Definition definition)
    {
        if (typeDefinition.PropertyTable.Count == 0) return string.Empty;
        var property = typeDefinition.PropertyTable.First();
        return $"{property.Value.Type.GetName()} {property.Key} = {GeneratePropertyLoaderCode(property.Value.Type, "json", definition, semantics)};";
    }
    
    private static string GeneratePropertiesLoaderCode(TypeDefinition typeDefinition, Semantics semantics, Definition definition)
    {
        return string.Join(
            "\n",
            typeDefinition
                .PropertyTable
                .Select(property =>
                    {
                        var propertySemantics = semantics.PropertySemanticsTable[property.Value.PropertyId!.Value];

                        return $"{property.Value.Type.GetName()} {property.Key} = {GeneratePropertyLoaderCode(
                            property.Value.Type,
                            propertySemantics.Schema is SwitchSchema ?
                                "json" :
                                $"json[\"{propertySemantics.PropertyName}\"]",
                            definition,
                            semantics)};";
                    }
                )
        );
    }
    
    private static string GeneratePropertyLoaderCode(Type type, string json, Definition definition, Semantics semantics)
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
            ArrayType arrayType => GenerateArrayLoaderCode(arrayType, json, definition, semantics),
            DictionaryType dictionaryType => $$$"""
                                                new global::System.Collections.Generic.Dictionary<{{{dictionaryType.KeyType.GetName()}}}, {{{dictionaryType.ValueType.GetName()}}}>()
                                                {{{json}}}.ToDictionary(key => {{{GeneratePropertyLoaderCode(dictionaryType.KeyType, "key", definition, semantics)}}}, value => {{{GeneratePropertyLoaderCode(dictionaryType.ValueType, "value", definition, semantics)}}})
                                                """,
            NullableType nullableType => $$$"""
                                            (({{{json}}} == null) ? null : {{{GeneratePropertyLoaderCode(nullableType.InnerType, json, definition, semantics)}}})
                                            """,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    private static string GenerateArrayLoaderCode(ArrayType arrayType, string json, Definition definition, Semantics semantics)
    {
        var innerType = arrayType.InnerType;

        // InnerTypeがCustomTypeかつIsArrayInnerTypeの場合、インデックス付きのSelectを使用
        if (innerType is CustomType customType &&
            definition.TypeNameToClassId.TryGetValue(customType.Name, out var classId) &&
            semantics.TypeSemanticsTable[classId].IsArrayInnerType)
        {
            return $$$"""
                      global::System.Linq.Enumerable.ToArray(global::System.Linq.Enumerable.Select({{{json}}}, (value, __Index__) => {{{GetLoaderName(innerType)}}}(__Index__, value)))
                      """;
        }

        return $$$"""
                  global::System.Linq.Enumerable.ToArray(global::System.Linq.Enumerable.Select({{{json}}}, value => {{{GeneratePropertyLoaderCode(innerType, "value", definition, semantics)}}}))
                  """;
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
                           return string.IsNullOrEmpty((string)json) ? global::System.Guid.Empty : new global::System.Guid((string)json);
                       }
                   }
               }
               """;
    }
    
    public static string GenerateLoaderExceptionTypeCode()
    {
        return """"
               namespace Mooresmaster.Loader
               {
                   public class MooresmasterLoaderException : global::System.Exception
                   {
                       public string ErrorProperty;
                       public string ErrorType;
                       public string PropertyPath;
                   
                       public MooresmasterLoaderException(string propertyPath, string errorType, string errorProperty)
                       {
                           PropertyPath = propertyPath;
                           ErrorType = errorType;
                           ErrorProperty = errorProperty;
                       }
                   
                       public override string Message => ToString();
                   
                       public override string ToString()
                       {
                           return $"PropertyPath: {PropertyPath}\nErrorProperty: {ErrorType}.{ErrorProperty}";
                       }
                   }
               }
               """";
    }
    
    private static string Indent(this string code, bool firstLine = false, int level = 1)
    {
        var indent = new string(' ', 4 * level);
        return firstLine ? indent : "" + code.Replace("\n", $"\n{indent}");
    }
}