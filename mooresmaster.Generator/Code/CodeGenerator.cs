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
                  namespace mooresmaster.Generator
                  {
                      {{{string.Join("\n", definition.TypeDefinitions.Select(GenerateTypeDefinitionCode)).Indent()}}}
                      {{{string.Join("\n", definition.InterfaceDefinitions.Select(GenerateInterfaceCode)).Indent()}}}
                  }
                  """;
    }
    
    private static string GenerateTypeDefinitionCode(TypeDefinition typeDef)
    {
        return $$$"""
                  public class {{{typeDef.Name}}} {{{GenerateInheritCode(typeDef)}}}
                  {
                      {{{GeneratePropertiesCode(typeDef).Indent()}}}
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
            BooleanType booleanType => "bool",
            ArrayType arrayType => $"{GenerateTypeCode(arrayType.InnerType)}[]",
            DictionaryType dictionaryType => $"global::System.Collections.Generic.Dictionary<{GenerateTypeCode(dictionaryType.KeyType)}, {GenerateTypeCode(dictionaryType.ValueType)}>",
            FloatType floatType => "float",
            IntType intType => "int",
            StringType stringType => "string",
            CustomType customType => customType.Name,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }
    
    private static string GenerateInterfaceCode(InterfaceDefinition interfaceDef)
    {
        return $$$"""public interface {{{interfaceDef.Name}}} { }""";
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
