using Mono.Cecil;

namespace DeadMemberAudit.Metadata;

// ネストした型まで含めてアセンブリ内の全型を列挙する
// Enumerates every type in an assembly, including nested types
public static class TypeEnumerator
{
    public static IEnumerable<TypeDefinition> AllTypes(AssemblyDefinition assembly)
    {
        foreach (var module in assembly.Modules)
        {
            foreach (var type in module.Types)
            {
                foreach (var nested in Expand(type)) yield return nested;
            }
        }
    }

    private static IEnumerable<TypeDefinition> Expand(TypeDefinition type)
    {
        yield return type;
        foreach (var nested in type.NestedTypes)
        {
            foreach (var deeper in Expand(nested)) yield return deeper;
        }
    }
}
