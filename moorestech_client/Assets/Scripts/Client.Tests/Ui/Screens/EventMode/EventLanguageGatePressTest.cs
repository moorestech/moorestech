using Client.Localization;
using Client.Ui.Screens.EventMode;
using Client.Ui.State.EventMode;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.EventMode
{
    /// <summary>
    /// 言語ゲートの押下（L1）
    /// Language-gate presses (L1)
    /// </summary>
    public class EventLanguageGatePressTest : EventLanguageGateTestHost
    {
        /// <web-test file="src/features/eventLanguageGate/EventLanguageGate.test.ts">ボタン押下でlocale付きのselect_languageを送る</web-test>
        [Test]
        public void 言語のボタンを押すとその言語を選びゲートを外す()
        {
            var gate = OpenWaitingGate();

            simulate.Click(Option("japanese").worldBound.center);

            Assert.IsFalse(gate.IsWaitingSelection);
            Assert.AreEqual("japanese", Localize.GetCurrentLanguageCode());
            Assert.IsNull(GateLayer.Q(EventLanguageGateView.RootTestId));
        }

        // UITK の受理は同期で応答待ちが無い。受理のその押下でゲートを外し、二度目の押下を届かせない（D-8）
        // UITK accepts synchronously with no pending state; the accepting press removes the gate so a second press never lands (D-8)
        /// <web-test file="src/features/eventLanguageGate/EventLanguageGate.test.ts">応答待ちの間は選択ボタンを押せなくする</web-test>
        [Test]
        public void 受理したその押下でゲートを外し二度目の押下を通さない()
        {
            var gate = OpenWaitingGate();
            var waitingChanges = 0;
            using var subscription = gate.OnWaitingChanged.Subscribe(_ => waitingChanges++);
            var germanPoint = Option("german").worldBound.center;

            simulate.Click(Option("japanese").worldBound.center);
            simulate.Click(germanPoint);

            Assert.AreEqual(1, waitingChanges);
            Assert.AreEqual("japanese", Localize.GetCurrentLanguageCode());
            Assert.IsNull(GateLayer.Q(EventLanguageGateView.RootTestId));
        }

        // 本番の Presenter は待機中しか載せないので拒否は起きない。View が拒否を黙らずに残すこと（fail-closed のログ）だけを、待機しないゲートに直接載せて見る
        // The production presenter mounts only while waiting, so no rejection occurs; mount the view directly on a non-waiting gate to check only that it logs rejections (fail-closed log)
        [Test]
        public void 待機しないゲートへの押下は言語を変えず拒否をToasterと同じ書式でログに残す()
        {
            MountView(new EventLanguageGate(false));
            LogAssert.Expect(LogType.Warning, "[EventLanguageGate] event_mode.select_language failed: already_selected");

            simulate.Click(Option("japanese").worldBound.center);

            Assert.AreEqual("english", Localize.GetCurrentLanguageCode(), "a rejected press changes no language");
        }
    }
}
