namespace DeadMemberAudit.Model;

// asmdefの所在から導く配置サイド。server宣言・client参照のみの型を配置ミスとして検出するために使う
// Placement side derived from where the asmdef lives, used to flag server-declared types used only by the client
public enum AssemblySide
{
    Server,
    Client,
    Unknown,
}

public static class AssemblySideExtensions
{
    public static string Label(this AssemblySide side)
    {
        return side switch
        {
            AssemblySide.Server => "server",
            AssemblySide.Client => "client",
            _ => "unknown",
        };
    }
}
