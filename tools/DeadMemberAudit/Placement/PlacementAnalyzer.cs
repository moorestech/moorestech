using DeadMemberAudit.Loading;
using DeadMemberAudit.Metadata;
using DeadMemberAudit.Model;
using Mono.Cecil;

namespace DeadMemberAudit.Placement;

// server側で宣言された型のうち、server側に解決者がいないものを配置ミスとして挙げる
// Reports server-declared types that have no resolver on the server side as misplaced
public sealed class PlacementAnalyzer
{
    private readonly AssemblyClassifier _classifier;
    private readonly TypeSourceLocator _typeSourceLocator;

    public PlacementAnalyzer(AssemblyClassifier classifier, TypeSourceLocator typeSourceLocator)
    {
        _classifier = classifier;
        _typeSourceLocator = typeSourceLocator;
    }

    public List<PlacementFinding> Analyze(IReadOnlyList<LoadedAssembly> assemblies)
    {
        var serverTypes = CollectServerTypes(assemblies);
        var index = new TypeUsageIndex(serverTypes.Select(type => type.FullName));
        new TypeUsageCollector(index).Collect(assemblies);

        var findings = new List<PlacementFinding>();
        foreach (var type in serverTypes)
        {
            var usage = index.Usage(type.FullName);
            if (ServerResolverUsage(index, type.FullName) > 0) continue;

            var clientUsage = SumBySide(usage.ResolverUsageByAssembly, AssemblySide.Client);
            var serverRegistration = SumBySide(usage.DiRegistrationByAssembly, AssemblySide.Server);
            if (clientUsage == 0 && serverRegistration == 0) continue;

            // DI登録だけがserver側の接点なら、移設より先に「誰も解決していない」ことを指摘する
            // When registration is the only server-side touch point, the missing resolver outranks any relocation advice
            var issue = serverRegistration > 0 ? PlacementIssue.RegistrationWithoutResolver : PlacementIssue.ClientOnlyUsage;
            findings.Add(new PlacementFinding(
                type.Module.Assembly.Name.Name, type.FullName, _typeSourceLocator.DisplayLocation(type), issue,
                Format(usage.ResolverUsageByAssembly, AssemblySide.Client),
                Format(usage.DiRegistrationByAssembly, AssemblySide.Server),
                Format(usage.ImplementationByAssembly, AssemblySide.Server)));
        }

        findings.Sort((left, right) => string.CompareOrdinal(left.TypeFullName, right.TypeFullName));
        return findings;
    }

    // 母集団はserver側productionのトップレベル型のみ。ネスト型は親と一緒に動くので単独の配置対象にしない
    // The population is top-level production types on the server side; nested types move with their parent and are not placed alone
    private List<TypeDefinition> CollectServerTypes(IReadOnlyList<LoadedAssembly> assemblies)
    {
        var types = new List<TypeDefinition>();
        foreach (var assembly in assemblies)
        {
            if (!assembly.Category.IsProduction() || _classifier.SideOf(assembly.Name) != AssemblySide.Server) continue;
            if (AuditConstants.ExternalApiAssemblies.Contains(assembly.Name)) continue;

            foreach (var type in TypeEnumerator.AllTypes(assembly.Assembly))
            {
                if (type.DeclaringType != null || type.Name.Contains('<')) continue;
                if (TypeTraits.IsGeneratedCode(type) || TypeTraits.HasAnyAttribute(type, new[] { "CompilerGeneratedAttribute" })) continue;
                types.Add(type);
            }
        }

        return types;
    }

    // 同じ登録に並んだサービス型が解決されていれば、実装型もserver側で使われている
    // If a service type from the same registration is resolved, its implementation is in use on the server side too
    private int ServerResolverUsage(TypeUsageIndex index, string typeFullName)
    {
        var direct = SumBySide(index.Usage(typeFullName).ResolverUsageByAssembly, AssemblySide.Server);
        return direct + index.PeersOf(typeFullName).Sum(peer => SumBySide(index.Usage(peer).ResolverUsageByAssembly, AssemblySide.Server));
    }

    private int SumBySide(Dictionary<string, int> countsByAssembly, AssemblySide side)
    {
        return countsByAssembly.Where(entry => _classifier.SideOf(entry.Key) == side).Sum(entry => entry.Value);
    }

    private string Format(Dictionary<string, int> countsByAssembly, AssemblySide side)
    {
        var parts = countsByAssembly
            .Where(entry => _classifier.SideOf(entry.Key) == side)
            .OrderByDescending(entry => entry.Value)
            .Select(entry => $"{entry.Key}:{entry.Value}");
        return string.Join(", ", parts);
    }
}
