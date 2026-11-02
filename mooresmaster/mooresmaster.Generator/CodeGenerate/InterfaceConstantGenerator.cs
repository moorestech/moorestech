using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using mooresmaster.Generator.Definitions;
using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator.CodeGenerate;

// JSONに存在しない固定interface値をスキーマの明示指定から生成する
// Generate fixed interface values absent from JSON only when explicitly declared in the schema
internal static class InterfaceConstantGenerator
{
    internal static string Generate(ISchema schema, TypeDefinition type, Definition definition)
    {
        if (schema is not ObjectSchema obj || !obj.Json.Nodes.TryGetValue("interfacePropertyConstants", out var node)) return "";
        if (node is not JsonObject constants) throw new InvalidOperationException("interfacePropertyConstants must be an object");
        var interfaces = definition.InterfaceDefinitions.Where(item => type.InheritList.Contains(item.TypeName)).ToArray();
        var lines = new List<string>();
        foreach (var pair in constants.Nodes)
        {
            var name = char.ToUpperInvariant(pair.Key[0]) + pair.Key.Substring(1);
            if (type.PropertyTable.ContainsKey(name)) throw new InvalidOperationException($"Constant duplicates JSON property: {name}");
            var matches = interfaces.Where(item => item.PropertyTable.ContainsKey(name)).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException($"Constant must belong to one implemented interface: {name}");
            var property = matches[0].PropertyTable[name];

            // 任意のC#文字列を埋め込まず、宣言型に合う数値だけを生成する
            // Emit only numbers matching the declared type, never arbitrary C# text
            var literal = (property.Type, pair.Value) switch
            {
                (IntType, JsonInt integer) when int.MinValue <= integer.Literal && integer.Literal <= int.MaxValue => integer.Literal.ToString(CultureInfo.InvariantCulture),
                (FloatType, JsonInt integer) => integer.Literal.ToString(CultureInfo.InvariantCulture) + "f",
                (FloatType, JsonNumber number) when !double.IsInfinity(number.Literal) && !double.IsNaN(number.Literal) => number.Literal.ToString("R", CultureInfo.InvariantCulture) + "f",
                _ => throw new InvalidOperationException($"Constant type does not match numeric interface property: {name}"),
            };
            var keyword = property.Type is IntType ? "int" : "float";
            lines.Add($"public {keyword} {name} => {literal};");
        }
        return string.Join("\n", lines);
    }
}
