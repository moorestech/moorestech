using Client.Game.Common;

namespace Client.Game.InGame.Playtest.Progress
{
    // 記録が閉じられた理由。集計側が「どう終わったセッションか」で分ける契約値（shared-contracts §3）
    // Why a record was closed; the contract value the digest side splits sessions by (shared-contracts §3)
    public static class ProgressEndReason
    {
        public const string Quit = "quit";
        public const string CrashRecovered = "crash-recovered";

        // 初期化に失敗して畳んだ終了。プレイヤーが選んだ終了と混ぜると「起動できていない」事実が quit に埋もれる
        // The fold-up after a failed initialization; merging it into a deliberate quit would bury "it never started" inside quit
        public const string InitializationFailed = "init-failed";

        public static string FromShutdownReason(GameShutdownReason reason)
        {
            return reason == GameShutdownReason.InitializationFailed ? InitializationFailed : Quit;
        }
    }
}
