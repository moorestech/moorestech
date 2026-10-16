#if UNITY_EDITOR
internal enum DeveloperTokenWindowResultKind
{
    InvalidToken, Saved, SaveFailed, Deleted, DeleteFailed,
    CredentialRequired, Unreachable, Refused, HttpFailed, MalformedResponse, Connected,
}

#endif
