namespace Client.PlaytestReceiver.Launch
{
    // 起動が配布版か開発者モードか。配布版だけが受け口へ送る（ADR 0070）
    // Whether this boot is a distribution build or developer mode; only a distribution build ships to the receiver (ADR 0070)
    public enum PlaytestLaunchKind
    {
        NotEvaluated,
        DeveloperMode,
        Distribution,
    }
}
