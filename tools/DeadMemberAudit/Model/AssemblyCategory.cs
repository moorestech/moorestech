namespace DeadMemberAudit.Model;

// アセンブリの役割分類。Productionのみが監査対象母集団になる
// Role of an assembly; only Production contributes to the audited population
public enum AssemblyCategory
{
    Production,
    Test,
    Debug,
    Editor,
    Default,
    External,
}

public static class AssemblyCategoryExtensions
{
    // productionからの参照だけが「生きている」根拠になる
    // Only a reference from production counts as evidence that a member is alive
    public static bool IsProduction(this AssemblyCategory category)
    {
        return category == AssemblyCategory.Production;
    }

    // 解析対象のmoorestechアセンブリか。外部DLLは参照解決にしか使わない
    // Whether this is a moorestech assembly to analyse; external DLLs are only used to resolve references
    public static bool IsMoorestech(this AssemblyCategory category)
    {
        return category != AssemblyCategory.External;
    }
}
