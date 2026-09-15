namespace Client.Game.InGame.Playtest.Progress
{
    // 記録が閉じられた理由。集計側が「どう終わったセッションか」で分ける。契約値への綴りは ProgressEndReasonJson だけが持つ
    // Why a record was closed, which the digest side splits sessions by; only ProgressEndReasonJson spells it as the contract value
    public enum ProgressEndReason
    {
        Quit,
        CrashRecovered,

        // 初期化に失敗して畳んだ終了。プレイヤーが選んだ終了と混ぜると「起動できていない」事実が quit に埋もれる
        // The fold-up after a failed initialization; merging it into a deliberate quit would bury "it never started" inside quit
        InitializationFailed,
    }
}
