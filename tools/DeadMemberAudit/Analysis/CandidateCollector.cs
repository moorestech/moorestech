using DeadMemberAudit.Loading;
using DeadMemberAudit.Metadata;
using DeadMemberAudit.Model;
using Mono.Cecil;

namespace DeadMemberAudit.Analysis;

// productionアセンブリのpublicメンバーを母集団として集める
// Collects the public members of production assemblies as the audited population
public static class CandidateCollector
{
    public static List<MemberCandidate> Collect(IReadOnlyList<LoadedAssembly> assemblies)
    {
        var candidates = new List<MemberCandidate>();
        foreach (var assembly in assemblies.Where(assembly => assembly.Category.IsProduction()))
        {
            foreach (var type in TypeEnumerator.AllTypes(assembly.Assembly))
            {
                // デリゲート型のInvoke等はコンパイラ生成なので型ごと飛ばす
                // A delegate type's Invoke and friends are compiler-provided, so skip the whole type
                if (type.IsEnum || IsDelegate(type)) continue;
                CollectFromType(assembly.Name, type, candidates);
            }
        }

        return candidates;
    }

    private static void CollectFromType(string assemblyName, TypeDefinition type, List<MemberCandidate> candidates)
    {
        // プロパティはget/setを1件に束ねるため、アクセサをメソッド走査から除く
        // Properties bundle their get and set into one entry, so accessors are kept out of the method sweep
        var accessorNames = BuildAccessorNames(type);

        foreach (var method in type.Methods)
        {
            if (!method.IsPublic || accessorNames.Contains(method.Name)) continue;
            var display = $"{type.Name}.{FormatMethodName(method)}";
            candidates.Add(new MemberCandidate(assemblyName, type, display, FormatSignature(method), new[] { method }, null));
        }

        foreach (var property in type.Properties)
        {
            var accessors = new[] { property.GetMethod, property.SetMethod }.Where(accessor => accessor != null).ToArray()!;
            if (accessors.Length == 0 || !accessors.Any(accessor => accessor!.IsPublic)) continue;
            var display = $"{type.Name}.{property.Name}";
            candidates.Add(new MemberCandidate(assemblyName, type, display, FormatPropertySignature(property), accessors!, property));
        }
    }

    private static HashSet<string> BuildAccessorNames(TypeDefinition type)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in type.Properties)
        {
            if (property.GetMethod != null) names.Add(property.GetMethod.Name);
            if (property.SetMethod != null) names.Add(property.SetMethod.Name);
        }

        foreach (var declaredEvent in type.Events)
        {
            if (declaredEvent.AddMethod != null) names.Add(declaredEvent.AddMethod.Name);
            if (declaredEvent.RemoveMethod != null) names.Add(declaredEvent.RemoveMethod.Name);
        }

        return names;
    }

    private static bool IsDelegate(TypeDefinition type)
    {
        return type.BaseType != null && (type.BaseType.FullName == "System.MulticastDelegate" || type.BaseType.FullName == "System.Delegate");
    }

    private static string FormatMethodName(MethodDefinition method)
    {
        return method.IsConstructor ? "ctor" : method.Name;
    }

    private static string FormatSignature(MethodDefinition method)
    {
        var parameters = method.Parameters.Select(parameter => parameter.ParameterType.Name);
        return $"{FormatMethodName(method)}({string.Join(", ", parameters)})";
    }

    private static string FormatPropertySignature(PropertyDefinition property)
    {
        var sides = new List<string>();
        if (property.GetMethod != null) sides.Add("get");
        if (property.SetMethod != null) sides.Add("set");
        return $"{property.Name} {{ {string.Join("; ", sides)} }}";
    }
}
