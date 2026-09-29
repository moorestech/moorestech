namespace Game.Paths
{
    // 起動セッション名の形式を記録と遠隔実行の台帳で共有する
    // Share the boot-session name format between recording and remote execution ledgers
    public static class ProcessSessionName
    {
        public const string Prefix = "session_";
        public const string NumericSuffixPattern = "[0-9]+";
    }
}
