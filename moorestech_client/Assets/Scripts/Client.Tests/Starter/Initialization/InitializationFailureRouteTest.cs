using System;
using Client.Starter.Initialization.Scene;
using Client.Ui.Foundation.Composite;
using NUnit.Framework;

namespace Client.Tests.Starter.Initialization
{
    // 初期化の失敗がアプリを終了させるかと、その終了ログの文を固定する（S5-gate D-18 裁定 b）
    // Pins whether an initialization failure quits the app and its quit log line (S5-gate D-18 ruling b)
    public class InitializationFailureRouteTest
    {
        [Test]
        public void sRGB合成のRTを作れない失敗はRTの寸法入りの終了ログでアプリを終了する()
        {
            var exception = new UiSrgbPanelRenderTextureUnavailableException(1920, 1080);

            Assert.IsTrue(InitializationFailureRoute.TryGetQuitLog(exception, out var quitLog));
            Assert.AreEqual("sRGB 合成の RT（1920x1080）を作れないため起動を止めてアプリを終了します: [UiSrgbPanelTarget] failed to create the 1920x1080 panel RT\n", quitLog);
        }

        [Test]
        public void ほかの初期化の失敗は終了せずメインメニューへ戻る()
        {
            var exception = new InvalidOperationException("other initialization failure");

            Assert.IsFalse(InitializationFailureRoute.TryGetQuitLog(exception, out var quitLog));
            Assert.IsNull(quitLog);
        }
    }
}
