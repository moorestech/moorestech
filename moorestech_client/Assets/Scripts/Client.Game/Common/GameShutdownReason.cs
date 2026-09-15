namespace Client.Game.Common
{
    // 終了パイプラインを流れる終了理由。「意図的な終了だけ」に反応したい購読者がこれで分岐する
    // The reason travelling through the shutdown pipeline; subscribers that must react only to a deliberate exit branch on it
    public enum GameShutdownReason
    {
        // プレイヤーが選んだ終了。ポーズメニューの終了・メインメニューへの復帰・出展モードのアイドル終了
        // An exit the player chose: quit from the pause menu, return to the main menu, or the event-mode idle quit
        IntentionalExit,

        // 初期化に失敗して畳む経路。拾いたいクラッシュ側なので正常終了として記録してはいけない
        // The fold-up after a failed initialization; this is the crash side we want to keep, never a clean exit
        InitializationFailed,

        // 書き出し完了を待てない終了（EditorのPlay停止・破棄・終了要求を遅らせられない環境）。完了を観測できないので意思表明の時点が最後の記録点になる
        // An exit that cannot await the flush (Editor Play stop, teardown, or a platform that ignores quit deferral); the intent is the last point that can be recorded
        UnawaitableExit,
    }
}
