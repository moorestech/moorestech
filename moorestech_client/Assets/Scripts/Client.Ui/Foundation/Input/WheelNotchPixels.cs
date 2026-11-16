namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// ホイール 1 ノッチの px（A-71）
    /// Pixels per wheel notch (A-71)
    /// </summary>
    public static class WheelNotchPixels
    {
        // 撤去前の Web 版のホイール 1 ノッチと同じ 60px。スクロール量・ズームの感度はこの値で揃う
        // 60px, the same as one notch of the former Web version; scroll amounts and zoom sensitivity line up on it
        public const float PixelsPerNotch = 60f;
    }
}
