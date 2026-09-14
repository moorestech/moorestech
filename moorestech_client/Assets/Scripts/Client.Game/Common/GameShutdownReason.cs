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
    }
}
