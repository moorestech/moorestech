using System;
using Client.Ui.Foundation.Composite;

namespace Client.Starter.Initialization.Scene
{
    /// <summary>
    /// 初期化の失敗がアプリを終了させるものかを判定し、終了ログの文を作る
    /// Decides whether an initialization failure quits the app and builds the quit log line
    /// </summary>
    /// <remarks>
    /// sRGB 合成の RT を作れないときは、色の外れた UI を出し続けることも、無操作監視の無いメインメニューで止まることもさせず終了する（S5-gate D-18 裁定 b）。それ以外は今どおりメインメニューへ戻す。型の知識はここ 1 か所に閉じ、呼び手はキャストしない
    /// When the sRGB composite RT cannot be created, quit rather than show off-colour UI or idle at a main menu without the idle watch (S5-gate D-18 ruling b); every other failure still returns to the main menu. The type knowledge stays here so callers never cast
    /// </remarks>
    public static class InitializationFailureRoute
    {
        public static bool TryGetQuitLog(Exception exception, out string quitLog)
        {
            if (exception is UiSrgbPanelRenderTextureUnavailableException unavailableTexture)
            {
                quitLog = $"sRGB 合成の RT（{unavailableTexture.Width}x{unavailableTexture.Height}）を作れないため起動を止めてアプリを終了します: {unavailableTexture.Message}\n{unavailableTexture.StackTrace}";
                return true;
            }

            quitLog = null;
            return false;
        }
    }
}
