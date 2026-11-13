using Client.Ui.Screens.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Capture
{
    /// <summary>
    /// 撮影の地と幕。地の画像（cover）と本番の ScreenBackdropView（ヴィネット・幕）を中身の奥に敷く（S3-buildmenu D-61）
    /// Capture ground and dim: lays a cover ground image and the production ScreenBackdropView (vignette, dim) behind the content (S3-buildmenu D-61)
    /// </summary>
    /// <remarks>
    /// 地の画像が無いときは Render の ground の色が地。画像の破棄は呼び手
    /// Web の DEMO の地は moorestech_web/webui/public/mock-orange-gradient.png
    /// Without an image the ground is Render's ground colour; the caller disposes the image
    /// The Web DEMO ground is moorestech_web/webui/public/mock-orange-gradient.png
    /// </remarks>
    public sealed class UiCaptureBackdrop
    {
        // 部品の撮影など、地の色だけで撮る
        // Ground colour only, e.g. for component captures
        public static readonly UiCaptureBackdrop GroundColorOnly = new(UiCaptureShell.None, null);

        private readonly UiCaptureShell _shell;
        private readonly Texture2D _groundImage;

        public UiCaptureBackdrop(UiCaptureShell shell, Texture2D groundImage)
        {
            _shell = shell;
            _groundImage = groundImage;
        }

        // 地の色の要素に地の画像と外殻を載せる。中身はこの後に足すので外殻より手前に描かれる
        // Puts the ground image and the shell on the ground-colour element; content is added after, so it draws in front of the shell
        internal void ApplyTo(VisualElement groundElement)
        {
            // 画像は Web の background-size: cover と同じく中央寄せで覆う
            // The image covers centred, like the Web background-size: cover
            if (_groundImage != null)
            {
                groundElement.style.backgroundImage = new StyleBackground(_groundImage);
                groundElement.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                groundElement.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                groundElement.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            }

            // 外殻は本番と同じ部品をそのまま載せる（幕の出し入れも本番の口で）
            // The shell is the production view itself, toggling the dim through its production entry point
            if (_shell == UiCaptureShell.None) return;
            var shellView = new ScreenBackdropView();
            shellView.SetBackdropShown(_shell == UiCaptureShell.VignetteAndDim);
            groundElement.Add(shellView);
        }
    }
}
