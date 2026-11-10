namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// Web のホイール 1 ノッチの px（A-71）
    /// Pixels per Web wheel notch (A-71)
    /// </summary>
    public static class WebWheelScale
    {
        // CefUnity の ScrollInputPipeline.WheelPixelsPerStep と同じ値
        // Same value as CefUnity ScrollInputPipeline.WheelPixelsPerStep
        public const float PixelsPerNotch = 60f;
    }
}
