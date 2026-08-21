using Mono.Cecil;

namespace DeadMemberAudit.Loading;

// 型フォワーダが循環する参照セットでは、Cecilの型解決（Resolve→ExportedType.Resolve→Resolve）が無限再帰しstack overflowで落ちる
// Cecil's type resolution recurses forever (Resolve -> ExportedType.Resolve -> Resolve) when type forwarders form a cycle
//
// 解決中の型を「移送先スコープ名＋型名」で記録し、同じ型へ再入した時点で循環と判定してnullを返す。
// 循環したフォワーダ鎖は実際に解決不能なので、nullは打ち切りではなく正しい答えである。
//
// In-flight types are tracked by target scope name plus type name, and re-entering the same type means a cycle, which returns null.
// A cyclic forwarder chain is genuinely unresolvable, so null is the truthful answer rather than a suppression.
public sealed class ForwarderCycleGuardResolver : MetadataResolver
{
    private readonly HashSet<string> _resolving = new(StringComparer.Ordinal);

    // 解決が縮退した事実をレポートへ出すための件数。異常な多さは検索ディレクトリ構成の疑い
    // Count of degraded resolutions surfaced in the report; an unusually high count hints at the search-directory setup
    public int BrokenCycleCount { get; private set; }

    public ForwarderCycleGuardResolver(IAssemblyResolver assemblyResolver) : base(assemblyResolver)
    {
    }

    public override TypeDefinition? Resolve(TypeReference type)
    {
        var key = ResolutionKey(type);
        if (key == null) return base.Resolve(type);

        if (!_resolving.Add(key))
        {
            BrokenCycleCount++;
            return null;
        }

        // 解析は単一スレッドのため、追跡集合の排他は不要
        // The analysis is single-threaded, so the tracking set needs no locking
        var resolved = base.Resolve(type);
        _resolving.Remove(key);
        return resolved;
    }

    // ExportedTypeは解決のたびに新しいTypeReferenceを作るため、参照の同一性では循環を検知できない
    // ExportedType builds a fresh TypeReference per resolution, so reference identity cannot detect the cycle
    private static string? ResolutionKey(TypeReference type)
    {
        var element = type.GetElementType();
        var scope = element.Scope;
        return scope == null ? null : $"{scope.Name}|{element.FullName}";
    }
}
