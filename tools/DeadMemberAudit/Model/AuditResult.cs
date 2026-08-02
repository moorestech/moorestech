namespace DeadMemberAudit.Model;

// 監査の最終結果。レポート生成はこれだけを見る
// Final audit result; report generation looks at nothing else
public sealed class AuditResult
{
    public readonly List<MemberCandidate> NeverReferenced = new();
    public readonly List<MemberCandidate> NonProductionOnly = new();
    public readonly Dictionary<ExclusionReason, int> ExclusionCounts = new();
    public readonly Dictionary<AssemblyCategory, int> AssemblyCountsByCategory = new();

    public int PopulationCount { get; private set; }
    public int ScannedMethodCount { get; private set; }
    public int LiveCount { get; private set; }

    // 生成コード判定はPDBに依存するため、シンボル無しで読んだ数を明示する
    // The generated-code check depends on PDBs, so the symbol-less count is reported explicitly
    public int SymbolLessAssemblyCount { get; private set; }
    public int SkippedFileCount { get; private set; }

    public void SetCounts(int populationCount, int scannedMethodCount, int liveCount)
    {
        PopulationCount = populationCount;
        ScannedMethodCount = scannedMethodCount;
        LiveCount = liveCount;
    }

    public void SetLoadDiagnostics(int symbolLessAssemblyCount, int skippedFileCount)
    {
        SymbolLessAssemblyCount = symbolLessAssemblyCount;
        SkippedFileCount = skippedFileCount;
    }

    public void CountExclusion(ExclusionReason reason)
    {
        ExclusionCounts.TryGetValue(reason, out var current);
        ExclusionCounts[reason] = current + 1;
    }

    public void CountAssembly(AssemblyCategory category)
    {
        AssemblyCountsByCategory.TryGetValue(category, out var current);
        AssemblyCountsByCategory[category] = current + 1;
    }

    public int ExcludedTotal()
    {
        return ExclusionCounts.Values.Sum();
    }
}
