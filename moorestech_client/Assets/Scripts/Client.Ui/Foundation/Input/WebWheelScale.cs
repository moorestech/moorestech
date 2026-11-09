namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// Web（CEF）のホイール 1 ノッチの px（A-71）。ノッチを px に換算する UITK の読み手はすべてここを読む
    /// Pixels per wheel notch on the Web (CEF) (A-71); every UITK reader converting notches to pixels reads this
    /// </summary>
    public static class WebWheelScale
    {
        // CefUnity の ScrollInputPipeline.WheelPixelsPerStep と同じ値
        // Same value as CefUnity ScrollInputPipeline.WheelPixelsPerStep
        public const float PixelsPerNotch = 60f;
    }
}
