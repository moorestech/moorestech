using System;
using System.Collections.Generic;
using System.Linq;
using mooresmaster.Generator.Definitions;
using mooresmaster.Generator.NameResolve;
using mooresmaster.Generator.Semantic;
using Type = mooresmaster.Generator.Definitions.Type;

namespace mooresmaster.Generator.CodeGenerate;

public record CodeFile(string FileName, string Code)
{
    public string Code = Code;
    public string FileName = FileName;
}

public static class CodeGenerator
{
    public static CodeFile[] Generate(Definition definition, Semantics semantics)
    {
        var files = new Dictionary<string, List<string>>();

        foreach (var typeDefinition in definition.TypeDefinitions)
        {
            if (!files.TryGetValue(typeDefinition.FileName, out _)) files[typeDefinition.FileName] = [];

            var isArrayInnerType = false;
            if (definition.TypeNameToClassId.TryGetValue(typeDefinition.TypeName, out var classId))
            {
                isArrayInnerType = semantics.TypeSemanticsTable[classId].IsArrayInnerType;
            }

            files[typeDefinition.FileName].Add(GenerateTypeDefinitionCode(typeDefinition, isArrayInnerType));
        }

        foreach (var interfaceDefinition in definition.InterfaceDefinitions)
        {
            if (!files.TryGetValue(interfaceDefinition.FileName, out _)) files[interfaceDefinition.FileName] = [];

            files[interfaceDefinition.FileName].Add(GenerateInterfaceCode(interfaceDefinition));
        }

        return files
            .Select(file =>
                new CodeFile(
                    file.Key,
                    string.Join("\n", file.Value)
                )
            )
            .Select(file =>
                file with
                {
                    Code = $"{file.Code}"
                })
            .ToArray();
    }

    private static string GenerateTypeDefinitionCode(TypeDefinition typeDef, bool isArrayInnerType)
    {
        return $$$"""
                  namespace Mooresmaster.Model.{{{typeDef.TypeName.ModuleName}}}
                  {
                      public class {{{typeDef.TypeName.Name}}} {{{GenerateInterfaceImplementationCode(typeDef.InheritList)}}}
                      {
                          {{{GeneratePropertiesCode(typeDef, isArrayInnerType).Indent(level: 2)}}}

                          {{{GenerateTypeConstructorCode(typeDef, isArrayInnerType).Indent(level: 2)}}}

                          {{{GenerateConstEnumCode(typeDef).Indent(level: 2)}}}
                      }
                  }
                  """;
    }

    private static string GeneratePropertiesCode(TypeDefinition typeDef, bool isArrayInnerType)
    {
        var properties = typeDef
            .PropertyTable
            .Select(kvp => $"public {GenerateTypeCode(kvp.Value.Type)} {kvp.Key} {{ get; }}")
            .ToList();

        if (isArrayInnerType)
        {
            properties.Insert(0, "public int Index { get; }");
        }

        return string.Join("\n", properties);
    }

    private static string GenerateTypeConstructorCode(TypeDefinition typeDef, bool isArrayInnerType)
    {
        var parameters = typeDef.PropertyTable.Select(kvp => $"{GenerateTypeCode(kvp.Value.Type)} {kvp.Key}").ToList();
        var assignments = typeDef.PropertyTable.Select(kvp => $"this.{kvp.Key} = {kvp.Key};").ToList();

        if (isArrayInnerType)
        {
            parameters.Insert(0, "int Index");
            assignments.Insert(0, "this.Index = Index;");
        }

        return $$$"""
                  public {{{typeDef.TypeName.Name}}}({{{string.Join(", ", parameters)}}})
                  {
                      {{{string.Join("\n", assignments).Indent()}}}
                  }
                  """;
    }

    private static string GenerateConstEnumCode(TypeDefinition typeDef)
    {
        var enumCodes = new List<string>();

        foreach (var kvp in typeDef.PropertyTable)
        {
            var name = kvp.Key;
            var property = kvp.Value;
            if (property.Enums is null)
                continue;

            enumCodes.Add($$$"""
                             public static class {{{name}}}Const
                             {
                             {{{string.Join("\n", property.Enums.Select(e => $"    public const string {e} = \"{e}\";"))}}}
                             }
                             """);
        }

        return $$$"""
                  {{{string.Join("\n", enumCodes)}}}
                  """;
    }

    private static string GenerateTypeCode(Type type)
    {
        return type switch
        {
            BooleanType => "bool",
            ArrayType arrayType => $"{GenerateTypeCode(arrayType.InnerType)}[]",
            DictionaryType dictionaryType => $"global::System.Collections.Generic.Dictionary<{GenerateTypeCode(dictionaryType.KeyType)}, {GenerateTypeCode(dictionaryType.ValueType)}>",
            FloatType => "float",
            IntType => "int",
            StringType => "string",
            Vector2Type => "global::UnityEngine.Vector2",
            Vector3Type => "global::UnityEngine.Vector3",
            Vector4Type => "global::UnityEngine.Vector4",
            Vector2IntType => "global::UnityEngine.Vector2Int",
            Vector3IntType => "global::UnityEngine.Vector3Int",
            UUIDType => "global::System.Guid",
            CustomType customType => $"{customType.Name.GetModelName()}",
            NullableType nullableType => $"{GenerateTypeCode(nullableType.InnerType)}?",
            _ => throw new ArgumentOutOfRangeException(type.GetType().Name)
        };
    }

    private static string GenerateInterfaceCode(InterfaceDefinition interfaceDef)
    {
        return $$$"""
                  namespace Mooresmaster.Model.{{{interfaceDef.TypeName.ModuleName}}}
                  {
                      public interface {{{interfaceDef.TypeName.Name}}}{{{GenerateInterfaceImplementationCode(interfaceDef.ImplementationList)}}}
                      {
                          {{{GenerateInterfacePropertiesCode(interfaceDef).Indent(level: 2)}}}
                      }
                  }
                  """;
    }

    private static string GenerateInterfaceImplementationCode(IEnumerable<TypeName> implementations)
    {
        var implementationsArray = implementations as TypeName[] ?? implementations.ToArray();
        if (!implementationsArray.Any()) return "";

        return $" : {string.Join(", ", implementationsArray.Select(i => i.GetModelName()))}";
    }

    private static string GenerateInterfacePropertiesCode(InterfaceDefinition interfaceDefinition)
    {
        var codes = new List<string>();

        foreach (var kvp in interfaceDefinition.PropertyTable)
        {
            var name = kvp.Key;
            var interfacePropertyDefinition = kvp.Value;

            codes.Add($"public {GenerateTypeCode(interfacePropertyDefinition.Type)} {name} {{ get; }}");
        }

        return string.Join("\n", codes);
    }

    private static string Indent(this string code, bool firstLine = false, int level = 1)
    {
        var indent = new string(' ', 4 * level);
        return firstLine ? indent : "" + code.Replace("\n", $"\n{indent}");
    }
}
