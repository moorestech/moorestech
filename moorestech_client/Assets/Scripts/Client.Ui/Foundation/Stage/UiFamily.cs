namespace Client.Ui.Foundation.Stage
{
    // 層の中の 2 つの族。Stage は中央の 1280×720 の舞台（Web の --ui-stage-*）、Viewport は実画面いっぱい（--ui-viewport-*・ADR 0013/0017）
    // The two families inside a layer: Stage is the centered 1280x720 stage (Web --ui-stage-*), Viewport fills the real screen (--ui-viewport-*, ADR 0013/0017)
    public enum UiFamily
    {
        Stage,
        Viewport,
    }
}
