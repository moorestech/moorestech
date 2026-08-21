using DeadMemberAudit.Cancellation;
using DeadMemberAudit.Loading;
using DeadMemberAudit.Metadata;
using DeadMemberAudit.Model;
using DeadMemberAudit.Placement;
using DeadMemberAudit.PrivateHelper;

namespace DeadMemberAudit.Analysis;

// 監査の本流。分類→読み込み→IL走査→母集団抽出→除外→参照集計→2リストへ振り分け
// Main audit flow: classify, load, scan IL, collect population, exclude, tally references, split into two lists
public sealed class AuditRunner
{
    private readonly string _assembliesDirectory;
    private readonly string _repositoryRoot;

    public AuditRunner(string repositoryRoot, string assembliesDirectory)
    {
        _repositoryRoot = repositoryRoot;
        _assembliesDirectory = assembliesDirectory;
    }

    public AuditResult Run(IEnumerable<string> extraSearchDirectories)
    {
        var classifier = new AssemblyClassifier(_repositoryRoot);
        var loader = new AssemblyLoader(classifier, _assembliesDirectory, extraSearchDirectories);
        var assemblies = loader.LoadMoorestechAssemblies(_assembliesDirectory);

        // 参照は全moorestechアセンブリから集め、母集団はproductionのみから作る
        // References come from every moorestech assembly; the population comes from production only
        var scan = new ReferenceCollector(assemblies).Scan(assemblies);
        var candidates = CandidateCollector.Collect(assemblies);
        var sourceLocator = new SourceLocator(_repositoryRoot);
        var typeSourceLocator = new TypeSourceLocator(_repositoryRoot, sourceLocator);
        var rules = new ExclusionRules(scan, sourceLocator);

        var result = new AuditResult();
        foreach (var assembly in assemblies) result.CountAssembly(assembly.Category);
        result.SetSideTable(classifier.SideTable());

        var liveCount = 0;
        foreach (var candidate in candidates)
        {
            var reason = rules.Evaluate(candidate);
            if (reason != ExclusionReason.None)
            {
                candidate.SetExclusion(reason);
                result.CountExclusion(reason);
                continue;
            }

            AttachReferences(candidate, scan);
            candidate.SetSourceLocation(FirstSourceLocation(candidate, sourceLocator));
            if (HasProductionReference(candidate, classifier))
            {
                liveCount++;
                ClassifyOverPublic(candidate, scan, result);
            }
            else if (candidate.TotalReferenceCount() == 0) result.NeverReferenced.Add(candidate);
            else result.NonProductionOnly.Add(candidate);
        }

        // 配置ミスとキャンセルは母集団がメンバーではなく型・呼び出しサイトなので、独立に走査する
        // Placement and cancellation are populated by types and call sites rather than members, so they scan independently
        result.PlacementFindings.AddRange(new PlacementAnalyzer(classifier, typeSourceLocator).Analyze(assemblies));
        result.CancellationFindings.AddRange(new CancellationScanner(classifier, sourceLocator, typeSourceLocator).Scan(assemblies));

        // privateメソッドの仕分けはpublicメンバーとは母集団が別なので、独立に走査する
        // Sorting private methods runs on its own population, so it scans apart from the public member candidates
        result.PrivateHelperFindings.AddRange(new PrivateHelperScanner(scan, sourceLocator).Scan(assemblies));

        result.SetCounts(candidates.Count, scan.ScannedMethodCount, liveCount);
        result.SetLoadDiagnostics(loader.SymbolLessAssemblyCount, loader.SkippedFileCount, loader.BrokenForwarderCycleCount);
        SortResults(result);
        return result;
    }

    // 縮小候補は生存メンバーの部分集合。参照が宣言型・宣言アセンブリの外へ出たかだけで決まる
    // Narrowing candidates are a subset of live members, decided purely by how far the references escaped
    private static void ClassifyOverPublic(MemberCandidate candidate, IlScanResult scan, AuditResult result)
    {
        var verdict = OverPublicRules.Evaluate(candidate, MergeScopes(candidate, scan));
        candidate.SetOverPublic(verdict);
        if (verdict == OverPublicScope.Private) result.PrivateCandidates.Add(candidate);
        else if (verdict == OverPublicScope.Internal) result.InternalCandidates.Add(candidate);
    }

    // プロパティはget/setで別々にスコープが記録されるため、両方を合わせて広いほうを採る
    // A property records scope per accessor, so both are merged and the wider reach wins
    private static ReferenceScope? MergeScopes(MemberCandidate candidate, IlScanResult scan)
    {
        ReferenceScope? merged = null;
        foreach (var method in candidate.Methods)
        {
            if (!scan.ScopeByMember.TryGetValue(MemberKey.For(method), out var scope)) continue;
            merged ??= new ReferenceScope();
            merged.Observe(!scope.HasOutsideDeclaringType, !scope.HasOutsideDeclaringAssembly);
        }

        return merged;
    }

    // プロパティはget/set両方の参照を合算する。片側だけ生きている場合は死にメンバーではない
    // A property sums references from both accessors, so a member alive on one side is not dead
    private static void AttachReferences(MemberCandidate candidate, IlScanResult scan)
    {
        foreach (var method in candidate.Methods)
        {
            if (!scan.ReferencesByMember.TryGetValue(MemberKey.For(method), out var perAssembly)) continue;
            foreach (var entry in perAssembly)
            {
                for (var i = 0; i < entry.Value; i++) candidate.AddReference(entry.Key);
            }
        }
    }

    // 抽象メソッドやinterfaceメンバーは本体が無いので、位置が取れないことがある
    // Abstract methods and interface members have no body, so a location is not always available
    private static string FirstSourceLocation(MemberCandidate candidate, SourceLocator sourceLocator)
    {
        return candidate.Methods
            .Select(sourceLocator.DisplayLocation)
            .FirstOrDefault(location => location.Length > 0) ?? string.Empty;
    }

    private static bool HasProductionReference(MemberCandidate candidate, AssemblyClassifier classifier)
    {
        return candidate.ReferencesByAssembly.Keys.Any(assemblyName => classifier.Classify(assemblyName).IsProduction());
    }

    private static void SortResults(AuditResult result)
    {
        result.NeverReferenced.Sort(CompareByLocation);
        result.NonProductionOnly.Sort(CompareByLocation);
        result.PrivateCandidates.Sort(CompareByLocation);
        result.InternalCandidates.Sort(CompareByLocation);
        result.PrivateHelperFindings.Sort(CompareHelperByLocation);

        #region Internal

        int CompareByLocation(MemberCandidate left, MemberCandidate right)
        {
            var byAssembly = string.CompareOrdinal(left.AssemblyName, right.AssemblyName);
            if (byAssembly != 0) return byAssembly;
            var byType = string.CompareOrdinal(left.DeclaringTypeFullName, right.DeclaringTypeFullName);
            return byType != 0 ? byType : string.CompareOrdinal(left.Signature, right.Signature);
        }

        int CompareHelperByLocation(PrivateHelperFinding left, PrivateHelperFinding right)
        {
            var byAssembly = string.CompareOrdinal(left.AssemblyName, right.AssemblyName);
            if (byAssembly != 0) return byAssembly;
            var byType = string.CompareOrdinal(left.DeclaringTypeFullName, right.DeclaringTypeFullName);
            return byType != 0 ? byType : string.CompareOrdinal(left.Signature, right.Signature);
        }

        #endregion
    }
}
