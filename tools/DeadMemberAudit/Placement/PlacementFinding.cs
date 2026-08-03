namespace DeadMemberAudit.Placement;

// 配置ミスの種別。DI登録のみのものは移設ではなく「そもそも要るのか」から問い直す必要がある
// Kind of misplacement; a registration-only type needs the question "is it needed at all" before any relocation
public enum PlacementIssue
{
    ClientOnlyUsage,
    RegistrationWithoutResolver,
}

// server側で宣言されたのにserver側に解決者がいない型1件
// One type declared on the server side that has no resolver on the server side
public sealed class PlacementFinding
{
    public readonly string AssemblyName;
    public readonly string TypeFullName;
    public readonly string SourceLocation;
    public readonly PlacementIssue Issue;

    // 参照元の内訳。`Client.Game:3` の形で並べる
    // Breakdown of referencing assemblies, listed as `Client.Game:3`
    public readonly string ClientUsage;
    public readonly string ServerRegistration;
    public readonly string ServerImplementation;

    public PlacementFinding(string assemblyName, string typeFullName, string sourceLocation, PlacementIssue issue,
        string clientUsage, string serverRegistration, string serverImplementation)
    {
        AssemblyName = assemblyName;
        TypeFullName = typeFullName;
        SourceLocation = sourceLocation;
        Issue = issue;
        ClientUsage = clientUsage;
        ServerRegistration = serverRegistration;
        ServerImplementation = serverImplementation;
    }
}
