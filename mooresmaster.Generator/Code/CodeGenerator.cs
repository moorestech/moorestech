using System.Linq;
using mooresmaster.Generator.Definitions;

namespace mooresmaster.Generator.Code;

public static class CodeGenerator
{
    public static string Generate(Definition definition)
    {
        return $$$"""
                  namespace mooresmaster.Generator
                  {
                      {{{string.Join("\n", definition.TypeDefinitions.Select(GenerateType)).Indent()}}}
                      {{{string.Join("\n", definition.InterfaceDefinitions.Select(GenerateInterface)).Indent()}}}
                  }
                  """;
    }
    
    private static string GenerateType(TypeDefinition typeDef)
    {
        return $$$"""
                  public class {{{typeDef.Name}}} {{{GenerateInheritCode(typeDef)}}}
                  {
                      
                  }
                  """;
    }
    
    private static string GenerateInterface(InterfaceDefinition interfaceDef)
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
