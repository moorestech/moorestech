using Mono.Cecil;

namespace DeadMemberAudit.Model;

// 除外理由。リストには載せないが件数はサマリに出す
// Reason a member was excluded; not listed, but counted in the summary
public enum ExclusionReason
{
    None,
    CompilerGenerated,
    Override,
    ExplicitInterfaceImplementation,
    ImplicitInterfaceImplementation,
    UnityMessageFunction,
    FrameworkInvokedAttribute,
    SerializedMember,
    DiConstructedType,
    ReflectivelyConstructedType,
    GeneratedCode,
    ExternalApiAssembly,
    AttributeType,
    UnityObjectConstructor,
    ImplicitDefaultConstructor,
}

// 監査対象メンバー1件。メソッド・コンストラクタは1メソッド、プロパティはget/set両方を束ねる
// One audited member: a single method or constructor, or a property bundling both accessors
public sealed class MemberCandidate
{
    public readonly string AssemblyName;
    public readonly string DeclaringTypeFullName;
    public readonly string DisplayName;
    public readonly string Signature;
    public readonly IReadOnlyList<MethodDefinition> Methods;

    // プロパティ候補のみ非null。自動実装プロパティのアクセサは[CompilerGenerated]を持つため、判定はこちらで行う
    // Non-null only for property candidates; auto-property accessors carry [CompilerGenerated], so judge on this instead
    public readonly PropertyDefinition? Property;

    // 参照元アセンブリ名 -> 参照回数。self referenceは数えない
    // Referencing assembly name -> count; self references are not counted
    public readonly Dictionary<string, int> ReferencesByAssembly = new();

    public ExclusionReason Exclusion { get; private set; } = ExclusionReason.None;

    // PDB由来の宣言位置（相対パス:行）。シンボルが無ければ空文字
    // Declaration site from the PDB as relative path:line, or empty when symbols are unavailable
    public string SourceLocation { get; private set; } = string.Empty;

    public void SetSourceLocation(string sourceLocation)
    {
        SourceLocation = sourceLocation;
    }

    public readonly TypeDefinition DeclaringType;

    public MemberCandidate(string assemblyName, TypeDefinition declaringType, string displayName, string signature, IReadOnlyList<MethodDefinition> methods, PropertyDefinition? property)
    {
        AssemblyName = assemblyName;
        DeclaringType = declaringType;
        DeclaringTypeFullName = declaringType.FullName;
        DisplayName = displayName;
        Signature = signature;
        Methods = methods;
        Property = property;
    }

    public void SetExclusion(ExclusionReason reason)
    {
        Exclusion = reason;
    }

    public void AddReference(string fromAssembly)
    {
        ReferencesByAssembly.TryGetValue(fromAssembly, out var current);
        ReferencesByAssembly[fromAssembly] = current + 1;
    }

    public int TotalReferenceCount()
    {
        return ReferencesByAssembly.Values.Sum();
    }
}
