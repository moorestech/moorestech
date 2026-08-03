using DeadMemberAudit.Loading;
using Mono.Cecil;

namespace DeadMemberAudit.Metadata;

// 型ごとに「interface経由で呼ばれうるメソッドシグネチャ」の集合を作る
// Builds, per type, the set of method signatures that could be invoked through an interface
public sealed class InterfaceImplementationIndex
{
    private readonly Dictionary<string, HashSet<string>> _signaturesByType = new(StringComparer.Ordinal);

    // 宣言型と基底型がたどれる全interfaceのメンバーを、ジェネリック実引数で置換して登録する
    // Registers members of every interface reachable from the type and its bases, substituted with generic arguments
    public bool IsImplicitInterfaceImplementation(MethodDefinition method)
    {
        var declaringType = method.DeclaringType;
        if (!_signaturesByType.TryGetValue(declaringType.FullName, out var signatures))
        {
            signatures = BuildSignatures(declaringType);
            _signaturesByType[declaringType.FullName] = signatures;
        }

        return signatures.Contains(BuildMethodSignature(method.Name, method.Parameters, new Dictionary<string, string>(StringComparer.Ordinal)));
    }

    private static HashSet<string> BuildSignatures(TypeDefinition type)
    {
        var signatures = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        // 基底型が実装するinterfaceも、派生側の同名メソッドが呼ばれる経路になりうる
        // Interfaces implemented by base types are also a route to a same-named method on the derived type
        for (TypeReference? current = type; current != null;)
        {
            var currentDefinition = current as TypeDefinition ?? AssemblyLoader.TryResolve(current);
            if (currentDefinition == null) break;
            foreach (var implementation in currentDefinition.Interfaces) CollectInterface(implementation.InterfaceType);
            current = currentDefinition.BaseType;
        }

        return signatures;

        #region Internal

        void CollectInterface(TypeReference interfaceReference)
        {
            if (!visited.Add(interfaceReference.FullName)) return;
            var interfaceDefinition = AssemblyLoader.TryResolve(interfaceReference);
            if (interfaceDefinition == null) return;

            var substitution = BuildSubstitution(interfaceDefinition, interfaceReference);
            foreach (var interfaceMethod in interfaceDefinition.Methods)
            {
                signatures.Add(BuildMethodSignature(interfaceMethod.Name, interfaceMethod.Parameters, substitution));
            }

            // interfaceの継承先も同じ実装型が受け持つため、再帰的にたどる
            // Inherited interfaces are served by the same implementing type, so recurse into them
            foreach (var inherited in interfaceDefinition.Interfaces) CollectInterface(inherited.InterfaceType);
        }

        #endregion
    }

    // IFace<Foo> の T を Foo へ写す表。閉じたジェネリックだけが対象
    // Maps T of IFace<Foo> onto Foo; only closed generic instances contribute
    private static Dictionary<string, string> BuildSubstitution(TypeDefinition definition, TypeReference reference)
    {
        var substitution = new Dictionary<string, string>(StringComparer.Ordinal);
        if (reference is not GenericInstanceType genericInstance) return substitution;

        var count = Math.Min(definition.GenericParameters.Count, genericInstance.GenericArguments.Count);
        for (var i = 0; i < count; i++)
        {
            var argument = genericInstance.GenericArguments[i];
            substitution[definition.GenericParameters[i].Name] = argument.ContainsGenericParameter ? "*" : argument.FullName;
        }

        return substitution;
    }

    // 未解決のジェネリック型は * に潰す。取りこぼしより過剰除外のほうが安全なため
    // Unresolved generic types collapse to *, because over-excluding is safer than missing an interface route
    private static string BuildMethodSignature(string name, IEnumerable<ParameterDefinition> parameters, Dictionary<string, string> substitution)
    {
        var parameterNames = parameters.Select(parameter => NormalizeTypeName(parameter.ParameterType, substitution));
        return $"{name}({string.Join(",", parameterNames)})";
    }

    private static string NormalizeTypeName(TypeReference type, Dictionary<string, string> substitution)
    {
        if (type is GenericParameter genericParameter)
        {
            return substitution.TryGetValue(genericParameter.Name, out var mapped) ? mapped : "*";
        }

        return type.ContainsGenericParameter ? "*" : type.FullName;
    }
}
