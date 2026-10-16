namespace Client.Game.InGame.BugReport
{
    internal enum CommitBundleResultKind
    {
        SkippedNoCommit,
        SkippedNoRepository,
        AlreadyPublished,
        CommitNotFound,
        UpdateRefFailed,
        Created,
        BundleCreateFailed,
    }
}
