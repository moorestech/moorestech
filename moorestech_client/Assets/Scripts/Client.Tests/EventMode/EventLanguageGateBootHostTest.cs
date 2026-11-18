using Client.Starter.EventMode;
using Client.Tests.Ui.Screens.EventMode;
using Client.Ui.Screens.EventMode;
using Client.Ui.State.EventMode;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.EventMode
{
    /// <summary>
    /// DI 前の宿主がゲートを最前面の層へ載せること（L1）
    /// The pre-DI host mounts the gate on the frontmost layer (L1)
    /// </summary>
    public class EventLanguageGateBootHostTest : EventLanguageGateTestHost
    {
        [Test]
        public void 宿主は待機中のゲートを最前面の層へ載せる()
        {
            new EventLanguageGateBootHost(GateLayer, Scale(), LanguageCatalog.Languages).Open(new EventLanguageGate(true));
            simulate.FrameUpdate();

            Assert.IsNotNull(GateLayer.Q(EventLanguageGateView.RootTestId));
        }
    }
}
