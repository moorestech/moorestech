using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.Skit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.Screens.Skit
{
    /// <summary>
    /// Enter が会話窓のクリックと同じ評価器を通り、Space・全体非表示中・窓の無い提示では使われないことを View 単位で確かめる（L1。Web skit.spec）
    /// Verifies on the view that Enter goes through the window-click evaluator and is unused for Space, while the whole UI is hidden, or without a window (L1; Web skit.spec)
    /// </summary>
    public class SkitEnterKeyTest : ScreenTestHost
    {
        private const string WholeUiHiddenLog = "[skit.text] Enter ignored: the whole UI is hidden";

        private RecordingSkitCommands _commands;
        private SkitScreenView _view;

        [SetUp]
        public void MountScreen()
        {
            _commands = new RecordingSkitCommands();
            _view = Mount(new SkitScreenView(_commands, Toaster, new UiOsCursorRequestStack(new FakeUiOsCursorApplier())));
        }

        /// <web-test file="e2e/tests/skit.spec.ts">会話窓にフォーカスがあってもSpaceでは送らずEnterで送る</web-test>
        [Test]
        public void Spaceでは送らずEnterは打ち出し中なら全文を出し全文なら送る()
        {
            _view.Render(SkitTestData.Text(SkitTestData.SessionId, 1, "Blocking message", SkitTestData.TextIntents));
            _view.Render(SkitTestData.Text(SkitTestData.SessionId, 2, "Second message", SkitTestData.TextIntents));
            simulate.FrameUpdateMs(60);

            simulate.KeyDown(KeyCode.Space);
            Assert.AreEqual(0, _commands.Issued.Count, "Space で送った");

            _view.HandleEnterKey();
            Assert.AreEqual(0, _commands.Issued.Count, "打ち出し中の Enter で送った");
            _view.HandleEnterKey();
            Assert.AreEqual(new SkitCommandRecord(SkitCommandKind.Advance, SkitTestData.SessionId, 2, null, false), _commands.Issued[0]);
        }

        [Test]
        public void 全体非表示の間はEnterで送らずログを出し戻すと送る()
        {
            _view.Render(SkitTestData.Text(SkitTestData.SessionId, 1, "Blocking message", SkitTestData.TextIntents));
            _view.SetWholeUiShown(false);
            LogAssert.Expect(LogType.Log, WholeUiHiddenLog);
            _view.HandleEnterKey();
            Assert.AreEqual(0, _commands.Issued.Count);

            _view.SetWholeUiShown(true);
            _view.HandleEnterKey();
            Assert.AreEqual(1, _commands.Issued.Count);
        }

        [Test]
        public void 会話窓の無い提示ではEnterを使わず警告も出さない()
        {
            _view.Render(SkitTestData.Background(1, "Moore", "Background message"));
            _view.SetInteractive(false);
            _view.HandleEnterKey();

            Assert.AreEqual(0, _commands.Issued.Count);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
