#if UNITY_EDITOR
internal enum DeveloperTokenWindowResultKind
{
    InvalidToken, Saved, SaveFailed, Deleted, DeleteFailed,
    CredentialRequired, Unreachable, Refused, MalformedResponse, Connected,
}

#endif
