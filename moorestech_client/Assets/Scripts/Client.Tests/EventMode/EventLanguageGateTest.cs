using Client.Localization;
using Client.WebUiHost.Game.EventMode;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.EventMode
{
    public class EventLanguageGateTest
    {
        [SetUp]
        public void SetUp()
        {
            // TrySetLanguageは公開snapshotの実言語を判定基準にするため、辞書を張ってから検証する
            // TrySetLanguage judges against the published snapshot, so the dictionaries must be loaded first
            Localize.Initialize();
        }

        [Test]
        public void 生成直後は選択待ちである()
        {
            var gate = new EventLanguageGate(true);

            Assert.IsTrue(gate.IsWaitingSelection);
            Assert.IsFalse(gate.WaitForSelectionAsync().Status.IsCompleted());
        }

        [Test]
        public void 選択可能な言語を選ぶと待機が解けて待ち合わせが完了する()
        {
            var gate = new EventLanguageGate(true);

            Assert.AreEqual(EventLanguageSelectionResult.Applied, gate.TrySelectLanguage("japanese"));

            Assert.IsFalse(gate.IsWaitingSelection);
            Assert.IsTrue(gate.WaitForSelectionAsync().Status.IsCompleted());
            Assert.AreEqual("japanese", Localize.GetCurrentLanguageCode());
        }

        [Test]
        public void 未知の言語は拒否しゲートを開かない()
        {
            var gate = new EventLanguageGate(true);

            Assert.AreEqual(EventLanguageSelectionResult.UnknownLanguage, gate.TrySelectLanguage("klingon"));

            Assert.IsTrue(gate.IsWaitingSelection);
        }

        // 二重クリック・再送でも二度開かない保証
        // Guarantees a double click or a resend cannot open the gate twice
        [Test]
        public void 二度目の選択は既に選択済みを返し状態を変えない()
        {
            var gate = new EventLanguageGate(true);
            gate.TrySelectLanguage("japanese");

            var changedCount = 0;
            gate.OnWaitingChanged.Subscribe(_ => changedCount++);

            Assert.AreEqual(EventLanguageSelectionResult.AlreadySelected, gate.TrySelectLanguage("english"));
            Assert.IsFalse(gate.IsWaitingSelection);
            Assert.AreEqual(0, changedCount);
        }

        [Test]
        public void 選択で待機変化が一度だけ通知される()
        {
            var gate = new EventLanguageGate(true);
            var changedCount = 0;
            gate.OnWaitingChanged.Subscribe(_ => changedCount++);

            gate.TrySelectLanguage("english");

            Assert.AreEqual(1, changedCount);
        }
    }
}
