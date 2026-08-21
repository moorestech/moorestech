namespace DeadMemberAudit.PrivateHelper;

// privateメソッドの扱い方の種別。呼び出し元が1つなら畳む、0なら消すで、出口が違う
// What to do with a private method; one caller means fold it in, zero means delete it
public enum PrivateHelperIssue
{
    SingleCaller,
    NeverCalled,
}

// 呼び出し元が1メソッドだけ、あるいはどこからも呼ばれていないprivateメソッド1件
// One private method called from a single method, or from nowhere at all
public sealed class PrivateHelperFinding
{
    public readonly string AssemblyName;
    public readonly string DeclaringTypeFullName;
    public readonly string DisplayName;
    public readonly string Signature;
    public readonly string SourceLocation;
    public readonly PrivateHelperIssue Issue;

    // 唯一の呼び出し元。`Type.Method` の形で、ラムダ・ローカル関数は元のメソッドへ寄せてある
    // The only caller, as `Type.Method`, with lambdas and local functions folded back into their owner
    public readonly string CallerDisplayName;

    public PrivateHelperFinding(string assemblyName, string declaringTypeFullName, string displayName,
        string signature, string sourceLocation, PrivateHelperIssue issue, string callerDisplayName)
    {
        AssemblyName = assemblyName;
        DeclaringTypeFullName = declaringTypeFullName;
        DisplayName = displayName;
        Signature = signature;
        SourceLocation = sourceLocation;
        Issue = issue;
        CallerDisplayName = callerDisplayName;
    }
}
