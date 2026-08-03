using Mono.Cecil;

namespace DeadMemberAudit.Model;

// Cecilの定義インスタンス同一性に依存せず、メソッド定義を文字列で一意に指す
// Identifies a method definition by string instead of relying on Cecil definition instance identity
public static class MemberKey
{
    public static string For(MethodDefinition method)
    {
        return $"{method.Module.Assembly.Name.Name}|{method.FullName}";
    }

    // 参照側のスコープ名。Resolveを試す価値があるかの足切りに使う
    // Scope name on the referencing side, used to gate whether resolving is worthwhile
    public static string ScopeNameOf(TypeReference type)
    {
        var elementType = type.GetElementType();
        return elementType.Scope switch
        {
            AssemblyNameReference assemblyName => assemblyName.Name,
            ModuleDefinition module => module.Assembly.Name.Name,
            ModuleReference => elementType.Module.Assembly.Name.Name,
            _ => elementType.Module.Assembly.Name.Name,
        };
    }
}
