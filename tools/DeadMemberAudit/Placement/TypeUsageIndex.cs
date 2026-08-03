namespace DeadMemberAudit.Placement;

// 1つの型に対する使われ方を、性質ごとに分けて数える
// Counts how one type is used, split by the nature of the usage
public sealed class TypeUsage
{
    // 実体を手に入れて使う側（フィールド・引数・戻り値・メソッド呼び出し・GetService等）
    // The consuming side that obtains an instance: fields, parameters, return types, member calls, GetService and friends
    public readonly Dictionary<string, int> ResolverUsageByAssembly = new(StringComparer.Ordinal);

    // DIコンテナへの登録呼び出しサイト。登録は「誰かが解決する」ことの証明にならない
    // Container registration call sites; registering is no proof that anyone resolves it
    public readonly Dictionary<string, int> DiRegistrationByAssembly = new(StringComparer.Ordinal);

    // 継承・interface実装。供給側であって解決者ではない
    // Inheritance and interface implementation, which is the supply side rather than a resolver
    public readonly Dictionary<string, int> ImplementationByAssembly = new(StringComparer.Ordinal);
}

// 追跡対象の型だけを保持する使用箇所インデックス。全型を持つとメモリが跳ねるため対象を絞る
// Usage index limited to tracked types, because holding every type would blow up memory
public sealed class TypeUsageIndex
{
    private readonly Dictionary<string, TypeUsage> _usageByType;

    // 同じDI登録に並んだ型（サービスと実装）。実装は自分の名前でなくサービス名で解決される
    // Types listed side by side in one DI registration; an implementation is resolved under its service's name, not its own
    private readonly Dictionary<string, HashSet<string>> _registrationPeers = new(StringComparer.Ordinal);

    public TypeUsageIndex(IEnumerable<string> trackedTypeFullNames)
    {
        _usageByType = trackedTypeFullNames.Distinct(StringComparer.Ordinal)
            .ToDictionary(name => name, _ => new TypeUsage(), StringComparer.Ordinal);
    }

    public void LinkRegistrationPeers(IReadOnlyList<string> typeFullNames)
    {
        foreach (var name in typeFullNames)
        {
            if (!_registrationPeers.TryGetValue(name, out var peers)) _registrationPeers[name] = peers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var peer in typeFullNames.Where(peer => peer != name)) peers.Add(peer);
        }
    }

    public IReadOnlyCollection<string> PeersOf(string typeFullName)
    {
        return _registrationPeers.TryGetValue(typeFullName, out var peers) ? peers : Array.Empty<string>();
    }

    public bool IsTracked(string typeFullName)
    {
        return _usageByType.ContainsKey(typeFullName);
    }

    public TypeUsage Usage(string typeFullName)
    {
        return _usageByType[typeFullName];
    }

    public void AddResolverUsage(string typeFullName, string fromAssembly)
    {
        Increment(Usage(typeFullName).ResolverUsageByAssembly, fromAssembly);
    }

    public void AddDiRegistration(string typeFullName, string fromAssembly)
    {
        Increment(Usage(typeFullName).DiRegistrationByAssembly, fromAssembly);
    }

    public void AddImplementation(string typeFullName, string fromAssembly)
    {
        Increment(Usage(typeFullName).ImplementationByAssembly, fromAssembly);
    }

    private static void Increment(Dictionary<string, int> counts, string key)
    {
        counts.TryGetValue(key, out var current);
        counts[key] = current + 1;
    }
}
