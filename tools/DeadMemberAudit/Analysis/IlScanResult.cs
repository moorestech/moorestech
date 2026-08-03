namespace DeadMemberAudit.Analysis;

// 参照元がどこまで外に出たか。公開範囲を縮小できるかの判定材料になる
// How far outward the references reached, which decides whether the accessibility can be narrowed
public sealed class ReferenceScope
{
    public bool HasOutsideDeclaringType { get; private set; }
    public bool HasOutsideDeclaringAssembly { get; private set; }

    public void Observe(bool sameOutermostType, bool sameAssembly)
    {
        if (!sameOutermostType) HasOutsideDeclaringType = true;
        if (!sameAssembly) HasOutsideDeclaringAssembly = true;
    }
}

// IL全走査の成果物。参照表と、反射的に生成される型の集合を持つ
// Product of the full IL scan: the reference table plus the sets of reflectively constructed types
public sealed class IlScanResult
{
    // メソッド定義キー -> (参照元アセンブリ名 -> 回数)
    // Method definition key -> (referencing assembly name -> count)
    public readonly Dictionary<string, Dictionary<string, int>> ReferencesByMember = new(StringComparer.Ordinal);

    // メソッド定義キー -> 参照が宣言型・宣言アセンブリの外へ出たか
    // Method definition key -> whether references escaped the declaring type or assembly
    public readonly Dictionary<string, ReferenceScope> ScopeByMember = new(StringComparer.Ordinal);

    // DIコンテナ登録のジェネリック引数に現れた型。コンテナがコンストラクタを反射的に呼ぶ
    // Types appearing as generic arguments of container registration; the container calls their constructors reflectively
    public readonly HashSet<string> DiConstructedTypes = new(StringComparer.Ordinal);

    // typeof / シリアライザのジェネリック引数に現れた型。反射経路が疑われる
    // Types appearing in typeof or serializer generic arguments, so a reflection route is suspected
    public readonly HashSet<string> ReflectivelyReferencedTypes = new(StringComparer.Ordinal);

    public int ScannedMethodCount { get; private set; }

    public void CountScannedMethod()
    {
        ScannedMethodCount++;
    }

    public void AddReference(string memberKey, string fromAssembly, bool sameOutermostType, bool sameAssembly)
    {
        if (!ReferencesByMember.TryGetValue(memberKey, out var perAssembly))
        {
            perAssembly = new Dictionary<string, int>(StringComparer.Ordinal);
            ReferencesByMember[memberKey] = perAssembly;
        }

        perAssembly.TryGetValue(fromAssembly, out var current);
        perAssembly[fromAssembly] = current + 1;

        if (!ScopeByMember.TryGetValue(memberKey, out var scope))
        {
            scope = new ReferenceScope();
            ScopeByMember[memberKey] = scope;
        }

        scope.Observe(sameOutermostType, sameAssembly);
    }
}
