using System.Linq;
using mooresmaster.Generator.Definitions;

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
        return (
            $"mooresmaster.loader.{typeDefinition.TypeName.ModuleName}.{typeDefinition.TypeName.Name}.g.cs",
            $$$"""
               namespace Mooresmaster.Loader.{{{typeDefinition.TypeName.ModuleName}}}
               {
                   public static class {{{typeDefinition.TypeName.Name}}}Loader
                   {
                       
                   }
               }
               """
        );
    }

    private static string Indent(this string code, bool firstLine = false, int level = 1)
    {
        var indent = new string(' ', 4 * level);
        return firstLine ? indent : "" + code.Replace("\n", $"\n{indent}");
    }
}
