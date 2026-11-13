using System;
using Client.Ui.Foundation.Composite;

namespace Client.Starter.Initialization.Scene
{
    // 主シーンの初期化に失敗したあとの行き先
    // Where the boot goes after main-scene initialization failed
    public enum InitializationFailureExit
    {
        QuitApplication,
        ReturnToMainMenu,
    }

    /// <summary>
    /// 失敗の行き先（終了／メニュー）を決める
    /// Decides the failure route (quit or menu)
    /// </summary>
    /// <remarks>
    /// sRGB 合成の RT を作れないときは、色の外れた UI を出し続けることも、無操作監視の無いメインメニューで止まることもさせず終了する（S5-gate D-18 裁定 b）。それ以外は今どおりメインメニューへ戻す
    /// When the sRGB composite RT cannot be created, quit rather than show off-colour UI or idle at a main menu without the idle watch (S5-gate D-18 ruling b); every other failure still returns to the main menu
    /// </remarks>
    public static class InitializationFailureRoute
    {
        public static InitializationFailureExit Decide(Exception exception)
        {
            return exception is UiSrgbPanelRenderTextureUnavailableException ? InitializationFailureExit.QuitApplication : InitializationFailureExit.ReturnToMainMenu;
        }
    }
}
