namespace Client.Starter.Playtest.TitleGates
{
    /// <summary>
    /// 開始の関所の判定。断り方を2つに分け、テスター向けの文言を出すかを呼び出し側がswitchで決める（ADR 0065）。
    /// The start checkpoint's verdict; refusals are split in two so each caller decides by switch whether to show tester-facing text (ADR 0065).
    /// </summary>
    public enum PlaytestStartVerdict
    {
        Passed,

        // 画面に答えるべき確認が無い拒否。PlaytestStartRefusal の文言をテスターへ出す
        // A refusal with no confirmation on screen; the PlaytestStartRefusal text is shown to the tester
        RefusedWithNotice,

        // 答えるべき確認が画面に出ている拒否。文言を重ねると確認が隠れるので何も出さない
        // A refusal while the confirmation to answer is on screen; stacking text would hide it, so nothing is shown
        RefusedWhileConfirmationVisible,
    }
}
