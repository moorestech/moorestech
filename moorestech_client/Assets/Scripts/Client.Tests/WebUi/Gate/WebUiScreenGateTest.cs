using Client.Game.InGame.UI.UIState;
using NUnit.Framework;

namespace Client.Tests.WebUi.Gate
{
    public class WebUiScreenGateTest
    {
        [Test]
        public void WebUiModeIsPermanentlyOnRegardlessOfHostAvailability()
        {
            // ホスト起動成否に関わらずWebモード恒久ON（uGUIフォールバックは撤去済み: ADR 0052）
            // Web mode stays ON regardless of host availability; the uGUI fallback is gone (ADR 0052)
            WebUiScreenGate.SetHostAvailable(false);
            Assert.IsTrue(WebUiScreenGate.IsWebUiMode);

            WebUiScreenGate.SetHostAvailable(true);
            Assert.IsTrue(WebUiScreenGate.IsWebUiMode);
        }
    }
}
