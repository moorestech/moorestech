namespace Client.Ui.Editor.Capture
{
    /// <summary>
    /// 撮影の奥に本番の外殻（ScreenBackdropView）を敷くか。幕は traits の Backdrop 列の画面だけで出る（A-44）
    /// Whether to lay the production shell (ScreenBackdropView) behind the capture; the dim shows only on traits Backdrop screens (A-44)
    /// </summary>
    public enum UiCaptureShell
    {
        None,
        VignetteOnly,
        VignetteAndDim,
    }
}
