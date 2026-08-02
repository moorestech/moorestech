namespace DeadMemberAudit.Analysis;

// IL全走査の成果物。参照表と、反射的に生成される型の集合を持つ
// Product of the full IL scan: the reference table plus the sets of reflectively constructed types
public sealed class IlScanResult
{
    // メソッド定義キー -> (参照元アセンブリ名 -> 回数)
    // Method definition key -> (referencing assembly name -> count)
    public readonly Dictionary<string, Dictionary<string, int>> ReferencesByMember = new(StringComparer.Ordinal);

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

    public void AddReference(string memberKey, string fromAssembly)
    {
        if (!ReferencesByMember.TryGetValue(memberKey, out var perAssembly))
        {
            perAssembly = new Dictionary<string, int>(StringComparer.Ordinal);
            ReferencesByMember[memberKey] = perAssembly;
        }

        perAssembly.TryGetValue(fromAssembly, out var current);
        perAssembly[fromAssembly] = current + 1;
    }
}
