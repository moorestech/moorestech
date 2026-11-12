using System;
using Client.Starter.Initialization.Scene;
using Client.Ui.Foundation.Composite;
using NUnit.Framework;

namespace Client.Tests.Starter.Initialization
{
    // 初期化の失敗の行き先（アプリ終了／メインメニュー）の判定を固定する（S5-gate D-18 裁定 b）
    // Pins where an initialization failure leads (quit the app / main menu; S5-gate D-18 ruling b)
    public class InitializationFailureRouteTest
    {
        [Test]
        public void sRGB合成のRTを作れない失敗はアプリを終了する()
        {
            var exception = new UiSrgbPanelRenderTextureUnavailableException(1920, 1080);
            Assert.AreEqual(InitializationFailureExit.QuitApplication, InitializationFailureRoute.Decide(exception));
        }

        [Test]
        public void ほかの初期化の失敗はメインメニューへ戻る()
        {
            var exception = new InvalidOperationException("other initialization failure");
            Assert.AreEqual(InitializationFailureExit.ReturnToMainMenu, InitializationFailureRoute.Decide(exception));
        }
    }
}
