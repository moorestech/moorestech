using DeadMemberAudit.Analysis;
using DeadMemberAudit.Loading;
using DeadMemberAudit.Metadata;
using DeadMemberAudit.Model;
using Mono.Cecil;

namespace DeadMemberAudit.PrivateHelper;

// privateメソッドを呼び出し元メソッド数で仕分ける。1ならローカル関数へ畳む候補、0なら死にコード
// Sorts private methods by their caller count: one means a fold-into-local-function candidate, zero means dead code
public sealed class PrivateHelperScanner
{
    private readonly IlScanResult _scan;
    private readonly SourceLocator _sourceLocator;
    private readonly InterfaceImplementationIndex _interfaceIndex = new();

    public PrivateHelperScanner(IlScanResult scan, SourceLocator sourceLocator)
    {
        _scan = scan;
        _sourceLocator = sourceLocator;
    }

    public List<PrivateHelperFinding> Scan(IReadOnlyList<LoadedAssembly> assemblies)
    {
        var findings = new List<PrivateHelperFinding>();
        foreach (var assembly in assemblies.Where(assembly => assembly.Category.IsProduction()))
        {
            // 外部Mod向けAPIアセンブリはIL上の呼び出し元が原理的に足りないので母集団から外す
            // Assemblies exposing an API to external mods structurally lack IL call sites, so they leave the population
            if (AuditConstants.ExternalApiAssemblies.Contains(assembly.Name)) continue;

            foreach (var type in TypeEnumerator.AllTypes(assembly.Assembly))
            {
                if (!HelperMethodFilter.IsAuditableType(type)) continue;
                foreach (var method in type.Methods) Collect(method, assembly.Name, findings);
            }
        }

        return findings;
    }

    private void Collect(MethodDefinition method, string assemblyName, List<PrivateHelperFinding> findings)
    {
        if (!HelperMethodFilter.IsAuditableMethod(method, _interfaceIndex, _sourceLocator)) return;

        // デリゲート化されたメソッドは呼び出しサイトが遅延するので、畳む・消すのどちらの候補にもしない
        // A method turned into a delegate has its call site deferred, so it is neither a folding nor a deletion candidate
        var memberKey = MemberKey.For(method);
        if (_scan.DelegateBoundMethods.Contains(memberKey)) return;

        if (!_scan.CallersByMember.TryGetValue(memberKey, out var callers))
        {
            findings.Add(Create(method, assemblyName, PrivateHelperIssue.NeverCalled, string.Empty));
            return;
        }

        // 入れ子型からの呼び出しも同一型として扱う。C#では入れ子間でprivateが見えるため
        // A call from a nested type still counts as the same type, because C# lets nested scopes see each other's private members
        if (callers.Count != 1) return;
        var caller = callers.First();
        if (caller.TypeFullName != TypeTraits.OutermostFullName(method.DeclaringType)) return;
        findings.Add(Create(method, assemblyName, PrivateHelperIssue.SingleCaller, caller.DisplayName));
    }

    private PrivateHelperFinding Create(MethodDefinition method, string assemblyName, PrivateHelperIssue issue, string callerDisplayName)
    {
        return new PrivateHelperFinding(assemblyName, method.DeclaringType.FullName,
            $"{method.DeclaringType.Name}.{method.Name}", HelperMethodFilter.Signature(method),
            MethodLocation(method), issue, callerDisplayName);
    }

    // asyncのスタブ本体は隠しシーケンスポイントしか持たないことがあるので、状態機械側の位置へ落とす
    // An async stub can carry only hidden sequence points, so the state machine's location is used as a fallback
    private string MethodLocation(MethodDefinition method)
    {
        var direct = _sourceLocator.DisplayLocation(method);
        if (direct.Length > 0) return direct;

        var stateMachine = TypeTraits.StateMachineType(method);
        var moveNext = stateMachine?.Methods.FirstOrDefault(candidate => candidate.Name == "MoveNext");
        return moveNext == null ? string.Empty : _sourceLocator.DisplayLocation(moveNext);
    }
}
