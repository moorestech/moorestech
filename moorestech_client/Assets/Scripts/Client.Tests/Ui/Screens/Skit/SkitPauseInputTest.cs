using System.Text.RegularExpressions;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.Skit;
using Client.Ui.Screens.Skit.Parts.Window;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Skit
{
    /// <summary>
    /// 入力不可（ポーズ中）の間は会話窓・ツール・選択肢・Enter のどれも効かず表示は続き、戻すと操作できることを確かめる（L1。Web skitPauseInput.spec・skit.spec）
    /// Verifies that while input is refused (paused) the window, tools, choices and Enter do nothing yet stay drawn, and work again on resume (L1; Web skitPauseInput.spec and skit.spec)
    /// </summary>
    public class SkitPauseInputTest : ScreenTestHost
    {
        private const string Body = "Blocking message";

        private RecordingSkitCommands _commands;
        private SkitScreenView _view;

        [SetUp]
        public void MountScreen()
        {
            _commands = new RecordingSkitCommands();
            _view = Mount(new SkitScreenView(_commands, Toaster, new UiOsCursorRequestStack(new FakeUiOsCursorApplier())));
        }

        /// <web-test file="e2e/tests/skitPauseInput.spec.ts">ポーズ中は会話窓とツールを実クリックしても全文表示も操作も出さず、再開後は操作できる</web-test>
        [Test]
        public void ポーズ中は会話窓とツールを押しても全文表示も操作も出さず再開後は操作できる()
        {
            StartTyping(SkitTestData.TextIntents);
            _view.SetInteractive(false);
            var window = Find<VisualElement>(_view, "blocking-skit");
            var auto = Find<VisualElement>(_view, "skit-tool-auto");
            var typingText = BodyText();

            // 各要素の中心を押しても拾われない
            // Pressing each element center picks no skit element
            foreach (var testid in new[] { "blocking-skit", "skit-tool-auto", "skit-tool-skip", "skit-tool-hide-ui" })
            {
                var center = Find<VisualElement>(_view, testid).worldBound.center;
                Assert.IsFalse(PicksInsideSkit(center), $"{testid} がポーズ中に拾われた");
                simulate.Click(center);
            }
            Assert.AreEqual(typingText, BodyText(), "ポーズ中のクリックで全文が出た");
            Assert.AreEqual(0, _commands.Issued.Count);

            // 再開後は窓で全文表示、ツールで送信
            // On resume the window reveals the body and the tool sends
            _view.SetInteractive(true);
            Assert.IsTrue(PicksInsideSkit(window.worldBound.center));
            simulate.Click(window.worldBound.center);
            Assert.IsTrue(ShowsWholeBody());
            simulate.Click(auto.worldBound.center);
            Assert.AreEqual(SkitCommandKind.SetAuto, _commands.Issued[0].Kind);
        }

        /// <web-test file="e2e/tests/skitPauseInput.spec.ts">ポーズ中は選択肢を実クリックしても選ばず、再開後は選べる</web-test>
        [Test]
        public void ポーズ中は選択肢を押しても選ばず再開後は選べる()
        {
            _view.Render(SkitTestData.Choices(3, SkitTestData.ChoiceIntents));
            simulate.FrameUpdate();
            var routeB = Find<Button>(_view, "skit-choice-route-b").worldBound.center;

            _view.SetInteractive(false);
            Assert.IsFalse(PicksInsideSkit(routeB));
            simulate.Click(routeB);
            Assert.IsTrue(IsShown(Find<Button>(_view, "skit-choice-route-b")));
            Assert.AreEqual(0, _commands.Issued.Count);

            _view.SetInteractive(true);
            simulate.Click(routeB);
            Assert.AreEqual(new SkitCommandRecord(SkitCommandKind.Select, SkitTestData.SessionId, 3, "route-b", false), _commands.Issued[0]);
        }

        /// <web-test file="e2e/tests/skitPauseInput.spec.ts">フォーカス後のポーズ中はEnterで全文表示しない（許可空=${emptyIntents}）</web-test>
        [TestCase(false)]
        [TestCase(true)]
        public void ポーズ中はEnterで全文表示せず警告を出す(bool emptyIntents)
        {
            // 許可が空でも全文表示の入口は閉じる
            // With an empty allowance the reveal entry stays closed
            StartTyping(emptyIntents ? new string[0] : SkitTestData.TextIntents);
            _view.SetInteractive(false);
            var typingText = BodyText();

            LogAssert.Expect(LogType.Warning, "[skit.text] input rejected: presentation is not interactive");
            _view.HandleEnterKey();

            Assert.AreEqual(typingText, BodyText());
            Assert.AreEqual(0, _commands.Issued.Count);

            // 再開後の Enter は全文表示のみ（Web の同テスト後半）
            // After resume Enter only reveals the body (Web test, second half)
            _view.SetInteractive(true);
            _view.HandleEnterKey();
            Assert.IsTrue(ShowsWholeBody(), "再開後の Enter で全文が出ない");
            Assert.AreEqual(0, _commands.Issued.Count, "再開後の Enter で操作を送った");
        }

        /// <web-test file="e2e/tests/skit.spec.ts">Escでポーズメニューを開いても会話UIは背後で表示され続ける</web-test>
        [Test]
        public void ポーズ中も会話窓とツールは背後で表示され続ける()
        {
            _view.Render(SkitTestData.Text(SkitTestData.SessionId, 1, Body, SkitTestData.TextIntents));
            _view.SetInteractive(false);
            simulate.FrameUpdate();

            Assert.IsTrue(IsShown(Find<VisualElement>(_view, "skit-window")));
            Assert.IsTrue(IsShown(Find<VisualElement>(_view, "skit-tool-auto")));
            Assert.AreEqual(1f, Find<VisualElement>(_view, "skit-window").resolvedStyle.opacity);
            Assert.IsTrue(ShowsWholeBody());
        }

        // 復元後、別 session で打ち出しを進める
        // After restore, advance typing in another session
        private void StartTyping(string[] intents)
        {
            _view.Render(SkitTestData.Text("restore-0", 0, "Restored line", intents));
            _view.Render(SkitTestData.Text(SkitTestData.SessionId, 1, Body, intents));
            simulate.FrameUpdateMs(120);
            Assert.IsFalse(ShowsWholeBody(), "打ち出しが始まっていない");
        }

        private string BodyText()
        {
            return Find<LocalizedLabel>(_view, "skit-body").text;
        }

        // 本文の Label は行高のタグを前置するので、その前置きだけを許して全文と比べる。透明な残りも全文で終わるので末尾の一致では判定しない
        // The body Label prefixes a line-height tag, so allow only that prefix against the full text; the transparent tail also ends with the full text, so a suffix match is not used
        private bool ShowsWholeBody()
        {
            return Regex.IsMatch(BodyText(), "^(<line-height=[^>]*>)?" + Regex.Escape(SkitRichText.TypewriterBody(Body, Body.Length)) + "$");
        }

        private bool PicksInsideSkit(Vector2 point)
        {
            var picked = _view.panel.Pick(point);
            return picked != null && (picked == _view || _view.Contains(picked));
        }
    }
}
