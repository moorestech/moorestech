using DeadMemberAudit.Cancellation;
using DeadMemberAudit.Placement;
using DeadMemberAudit.PrivateHelper;

namespace DeadMemberAudit.Model;

// 監査の最終結果。レポート生成はこれだけを見る
// Final audit result; report generation looks at nothing else
public sealed class AuditResult
{
    public readonly List<MemberCandidate> NeverReferenced = new();
    public readonly List<MemberCandidate> NonProductionOnly = new();

    // 参照は実在するが公開範囲が広すぎるメンバー（リスト3）
    // Members that are referenced yet exposed too widely (list 3)
    public readonly List<MemberCandidate> PrivateCandidates = new();
    public readonly List<MemberCandidate> InternalCandidates = new();

    public readonly List<PlacementFinding> PlacementFindings = new();
    public readonly List<CancellationFinding> CancellationFindings = new();

    // privateメソッドの畳む候補・消す候補（リスト6）。母集団がpublicメンバーではないので独立に持つ
    // Folding and deletion candidates among private methods (list 6), held separately because their population is not the public members
    public readonly List<PrivateHelperFinding> PrivateHelperFindings = new();

    public readonly Dictionary<ExclusionReason, int> ExclusionCounts = new();
    public readonly Dictionary<AssemblyCategory, int> AssemblyCountsByCategory = new();

    // アセンブリ名 -> server/client。asmdefの所在から導いた分類表をレポートに出すために持つ
    // Assembly name -> server or client, kept so the asmdef-derived table can be printed in the report
    public IReadOnlyDictionary<string, AssemblySide> SideTable { get; private set; } = new Dictionary<string, AssemblySide>();

    public void SetSideTable(IReadOnlyDictionary<string, AssemblySide> sideTable)
    {
        SideTable = sideTable;
    }

    public int PopulationCount { get; private set; }
    public int ScannedMethodCount { get; private set; }
    public int LiveCount { get; private set; }

    // 生成コード判定はPDBに依存するため、シンボル無しで読んだ数を明示する
    // The generated-code check depends on PDBs, so the symbol-less count is reported explicitly
    public int SymbolLessAssemblyCount { get; private set; }
    public int SkippedFileCount { get; private set; }

    // 循環フォワーダで解決を打ち切った回数。0でなければ一部の参照が未解決のまま集計されている
    // Resolutions cut short by a forwarder cycle; a non-zero count means some references stayed unresolved
    public int BrokenForwarderCycleCount { get; private set; }

    public void SetCounts(int populationCount, int scannedMethodCount, int liveCount)
    {
        PopulationCount = populationCount;
        ScannedMethodCount = scannedMethodCount;
        LiveCount = liveCount;
    }

    public void SetLoadDiagnostics(int symbolLessAssemblyCount, int skippedFileCount, int brokenForwarderCycleCount)
    {
        SymbolLessAssemblyCount = symbolLessAssemblyCount;
        SkippedFileCount = skippedFileCount;
        BrokenForwarderCycleCount = brokenForwarderCycleCount;
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
