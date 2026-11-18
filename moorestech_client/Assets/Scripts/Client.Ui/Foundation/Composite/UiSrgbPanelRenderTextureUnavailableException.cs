using System;

namespace Client.Ui.Foundation.Composite
{
    /// <summary>
    /// 画面の大きさの sRGB 合成の RT を作れなかった失敗。起動中なら起動で止めてアプリを終了する（S5-gate D-18 裁定 b）
    /// Failure to create the screen-sized sRGB composite RT; during boot the app stops and quits (S5-gate D-18 ruling b)
    /// </summary>
    public sealed class UiSrgbPanelRenderTextureUnavailableException : InvalidOperationException
    {
        public int Width { get; private set; }
        public int Height { get; private set; }

        public UiSrgbPanelRenderTextureUnavailableException(int width, int height)
            : base($"[UiSrgbPanelTarget] failed to create the {width}x{height} panel RT")
        {
            Width = width;
            Height = height;
        }
    }
}
