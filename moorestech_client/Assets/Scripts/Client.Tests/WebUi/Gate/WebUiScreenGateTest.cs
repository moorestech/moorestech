using Client.Game.InGame.UI.UIState;
using NUnit.Framework;

namespace Client.Tests.WebUi.Gate
{
    public class WebUiScreenGateTest
    {
        [Test]
        public void WebUiModeIsPermanentlyOnRegardlessOfHostAvailability()
        {
            // uGUI廃止Phase1: ホスト起動成否に関わらずWebモード恒久ON（uGUIフォールバック廃止）
            // uGUI-retirement Phase1: web mode stays ON regardless of host availability (uGUI fallback removed)
            WebUiScreenGate.SetHostAvailable(false);
            Assert.IsTrue(WebUiScreenGate.IsWebUiMode);

            WebUiScreenGate.SetHostAvailable(true);
            Assert.IsTrue(WebUiScreenGate.IsWebUiMode);
        }
    }
}
