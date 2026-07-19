using System.Collections.Generic;
using Client.Game.InGame.UI.UIState;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.WebUi.Gate
{
    public class WebUiScreenGateTest
    {
        [SetUp]
        public void SetUp()
        {
            WebUiScreenGate.SetWebUiMode(false);
            WebUiScreenGate.SetHostAvailable(false);
        }

        [TearDown]
        public void TearDown()
        {
            WebUiScreenGate.SetWebUiMode(false);
            WebUiScreenGate.SetHostAvailable(false);
        }

        [Test]
        public void EffectiveModeChangesArePublishedWithoutDuplicates()
        {
            // CEFトグルとホスト稼働状態のANDが変わった場合だけ通知する
            // Publish only when the AND of the CEF toggle and host availability changes
            var changes = new List<bool>();
            using var subscription = WebUiScreenGate.OnWebUiModeChanged.Subscribe(changes.Add);

            WebUiScreenGate.SetWebUiMode(true);
            WebUiScreenGate.SetHostAvailable(true);
            WebUiScreenGate.SetHostAvailable(true);
            WebUiScreenGate.SetWebUiMode(false);

            CollectionAssert.AreEqual(new[] { true, false }, changes);
        }
    }
}
