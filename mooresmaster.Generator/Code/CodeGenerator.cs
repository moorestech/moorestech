using System;
using System.Linq;
using mooresmaster.Generator.Definitions;
using Type = mooresmaster.Generator.Definitions.Type;

namespace mooresmaster.Generator.Code;

public static class CodeGenerator
{
    public static string Generate(Definition definition)
    {
        return $$$"""
                    {{{string.Join("\n", definition.TypeDefinitions.Select(GenerateTypeDefinitionCode)).Indent()}}}
                    {{{string.Join("\n", definition.InterfaceDefinitions.Select(GenerateInterfaceCode)).Indent()}}}
                  """;
    }

    private static string GenerateTypeDefinitionCode(TypeDefinition typeDef)
    {
        return $$$"""
                  namespace {{{typeDef.TypeName.NameSpace}}}
                  {
                      public class {{{typeDef.TypeName.Name}}}{{{GenerateInheritCode(typeDef)}}}
                      {
                          {{{GeneratePropertiesCode(typeDef).Indent()}}}
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
                .Select(kvp => $"public {GenerateTypeCode(kvp.Value)} {kvp.Key};")
        );
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
            CustomType customType => customType.Name,
            _ => throw new ArgumentOutOfRangeException(type.GetType().Name)
        };
    }

    private static string GenerateInterfaceCode(InterfaceDefinition interfaceDef)
    {
        return $$$"""
                  namespace {{{interfaceDef.TypeName.NameSpace}}}
                  {
                      public interface {{{interfaceDef.TypeName.Name}}} { }
                  }
                  """;
    }

    private static string GenerateInheritCode(TypeDefinition type)
    {
        return type.InheritList.Length > 0 ? $": {string.Join(", ", type.InheritList)}" : "";
    }

    private static string Indent(this string code, bool firstLine = false, int level = 1)
    {
        var indent = new string(' ', 4 * level);
        return firstLine ? indent : "" + code.Replace("\n", $"\n{indent}");
    }
}
