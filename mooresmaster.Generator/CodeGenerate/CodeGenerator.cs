using System;
using System.Collections.Generic;
using System.Linq;
using mooresmaster.Generator.Definitions;
using mooresmaster.Generator.NameResolve;
using Type = mooresmaster.Generator.Definitions.Type;

namespace mooresmaster.Generator.CodeGenerate;

public record CodeFile(string FileName, string Code)
{
    public string Code = Code;
    public string FileName = FileName;
}

public static class CodeGenerator
{
    public static CodeFile[] Generate(Definition definition)
    {
        var files = new Dictionary<string, List<string>>();

        foreach (var typeDefinition in definition.TypeDefinitions)
        {
            if (!files.TryGetValue(typeDefinition.FileName, out _)) files[typeDefinition.FileName] = [];

            files[typeDefinition.FileName].Add(GenerateTypeDefinitionCode(typeDefinition));
        }

        foreach (var interfaceDefinition in definition.InterfaceDefinitions)
        {
            if (!files.TryGetValue(interfaceDefinition.FileName, out _)) files[interfaceDefinition.FileName] = [];

            files[interfaceDefinition.FileName].Add(GenerateInterfaceCode(interfaceDefinition));
        }

//         $$$"""
//                {{{string.Join("\n", definition.TypeDefinitions.Select(GenerateTypeDefinitionCode))}}}
//                {{{string.Join("\n", definition.InterfaceDefinitions.Select(GenerateInterfaceCode))}}}
//                """

        return files.Select(file => new CodeFile(file.Key, string.Join("\n", file.Value))).ToArray();
    }

    private static string GenerateTypeDefinitionCode(TypeDefinition typeDef)
    {
        return $$$"""
                  namespace Mooresmaster.Model.{{{typeDef.TypeName.ModuleName}}}
                  {
                      public class {{{typeDef.TypeName.Name}}}{{{GenerateInheritCode(typeDef)}}}
                      {
                          {{{GeneratePropertiesCode(typeDef).Indent(level: 2)}}}
                          
                          {{{GenerateTypeConstructorCode(typeDef).Indent(level: 2)}}}
                      }
                  }
                  """;
    }

    private static string GeneratePropertiesCode(TypeDefinition typeDef)
    {
        return string.Join(
            "\n",
            typeDef
                .PropertyTable
                .Select(kvp => $"public readonly {GenerateTypeCode(kvp.Value.Type)} {kvp.Key};")
        );
    }

    private static string GenerateTypeConstructorCode(TypeDefinition typeDef)
    {
        return $$$"""
                  public {{{typeDef.TypeName.Name}}}({{{string.Join(", ", typeDef.PropertyTable.Select(kvp => $"{GenerateTypeCode(kvp.Value.Type)} {kvp.Key}"))}}})
                  {
                      {{{string.Join("\n", typeDef.PropertyTable.Select(kvp => $"this.{kvp.Key} = {kvp.Key};")).Indent()}}}
                  }
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
            _ => throw new ArgumentOutOfRangeException(type.GetType().Name)
        };
    }

    private static string GenerateInterfaceCode(InterfaceDefinition interfaceDef)
    {
        return $$$"""
                  namespace Mooresmaster.Model.{{{interfaceDef.TypeName.ModuleName}}}
                  {
                      public interface {{{interfaceDef.TypeName.Name}}} { }
                  }
                  """;
    }

    private static string GenerateInheritCode(TypeDefinition type)
    {
        return type.InheritList.Length > 0 ? $": {string.Join(", ", type.InheritList.Select(t => t.GetModelName()))}" : "";
    }

    private static string Indent(this string code, bool firstLine = false, int level = 1)
    {
        var indent = new string(' ', 4 * level);
        return firstLine ? indent : "" + code.Replace("\n", $"\n{indent}");
    }
}