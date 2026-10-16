#if UNITY_EDITOR
internal enum DeveloperTokenWindowResultKind
{
    InvalidToken, Saved, SaveFailed, Deleted, DeleteFailed,
    CredentialRequired, Unreachable, Refused, HttpFailed, MalformedResponse, Connected,
}

internal sealed class DeveloperTokenWindowResultData
{
    internal readonly string Detail;
    internal DeveloperTokenWindowResultData(string detail) { Detail = detail; }
}
#endif
