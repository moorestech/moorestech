namespace DeadMemberAudit.Cancellation;

// キャンセル周りの指摘種別。裁定規範はreviewers/core-cs-async-cancellation.mdと揃える
// Kind of cancellation issue; the adjudication norm matches reviewers/core-cs-async-cancellation.md
public enum CancellationIssue
{
    TokenNotPassed,
    AsyncVoid,
    CancellationTokenSourceNotReleased,
}

// キャンセル周りの指摘1件
// One cancellation finding
public sealed class CancellationFinding
{
    public readonly string AssemblyName;
    public readonly string DeclaringTypeFullName;
    public readonly string MemberDisplay;
    public readonly string SourceLocation;
    public readonly CancellationIssue Issue;

    // 呼び先や、トークンを持っている根拠など、裁定に必要な文脈
    // The context a verifier needs, such as the callee and why the caller is considered to hold a token
    public readonly string Detail;

    public CancellationFinding(string assemblyName, string declaringTypeFullName, string memberDisplay,
        string sourceLocation, CancellationIssue issue, string detail)
    {
        AssemblyName = assemblyName;
        DeclaringTypeFullName = declaringTypeFullName;
        MemberDisplay = memberDisplay;
        SourceLocation = sourceLocation;
        Issue = issue;
        Detail = detail;
    }
}
