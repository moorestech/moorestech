// S/Client.Tests/Ui/Screens/Skit/SkitScreenViewTest.cs
using System.Text.RegularExpressions;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.Skit;
using Client.Ui.Screens.Skit.Parts.Window;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Skit
{
    /// <summary>
    /// 会話窓の打ち出し・全文表示・送り・選択と、背景の 1 行・UI 非表示・演出中・暗転の出し分けを確かめる（L1。Web skit.spec・phaseC4.spec・hotbar.spec）
    /// Verifies typing, reveal, advance and select on the window, and how the background line, hidden UI, staging and transition show (L1; Web skit.spec, phaseC4.spec and hotbar.spec)
    /// </summary>
    public class SkitScreenViewTest : ScreenTestHost
    {
        private const string SecondBody = "Second message";

        private RecordingSkitCommands _commands;
        private FakeUiOsCursorApplier _cursorApplier;
        private SkitScreenView _view;

        [SetUp]
        public void MountScreen()
        {
            _commands = new RecordingSkitCommands();
            _cursorApplier = new FakeUiOsCursorApplier();
            _view = Mount(new SkitScreenView(_commands, Toaster, new UiOsCursorRequestStack(_cursorApplier)));
        }

        /// <web-test file="e2e/tests/skit.spec.ts">blocking skit reveals, advances, and selects by choiceId</web-test>
        [Test]
        public void 打ち出し中のクリックは全文を出し次のクリックで送り選択肢はchoiceIdで選ぶ()
        {
            // 最初の描画は復元なので全文、次の本文から打ち出す
            // The first draw is a restore and shows everything; the next body is typed
            _view.Render(SkitTestData.Text(SkitTestData.SessionId, 1, "Blocking message", SkitTestData.TextIntents));
            _view.Render(SkitTestData.Text(SkitTestData.SessionId, 2, SecondBody, SkitTestData.TextIntents));
            simulate.FrameUpdateMs(120);
            Assert.AreNotEqual(Expected(SecondBody, 14), BodyText(), "打ち出しが始まっていない");

            simulate.Click(Find<VisualElement>(_view, "blocking-skit"));
            Assert.AreEqual(Expected(SecondBody, 14), BodyText());
            Assert.AreEqual(0, _commands.Issued.Count, "全文表示のクリックで送った");

            simulate.Click(Find<VisualElement>(_view, "blocking-skit"));
            Assert.AreEqual(new SkitCommandRecord(SkitCommandKind.Advance, SkitTestData.SessionId, 2, null, false), _commands.Issued[0]);

            _view.Render(SkitTestData.Choices(3, SkitTestData.ChoiceIntents));
            simulate.FrameUpdate();
            simulate.Click(Find<Button>(_view, "skit-choice-route-b"));
            Assert.AreEqual(new SkitCommandRecord(SkitCommandKind.Select, SkitTestData.SessionId, 3, "route-b", false), _commands.Issued[1]);
        }

        [Test]
        public void 送りの印は全文が出て送りが許可されたときだけ出る()
        {
            _view.Render(SkitTestData.Text(SkitTestData.SessionId, 1, "Blocking message", SkitTestData.TextIntents));
            _view.Render(SkitTestData.Text(SkitTestData.SessionId, 2, SecondBody, SkitTestData.TextIntents));
            simulate.FrameUpdateMs(60);
            Assert.IsFalse(IsShown(Find<VisualElement>(_view, "skit-advance-marker")));

            simulate.FrameUpdateMs(2000);
            Assert.IsTrue(IsShown(Find<VisualElement>(_view, "skit-advance-marker")));

            _view.Render(SkitTestData.Text(SkitTestData.SessionId, 3, SecondBody, new[] { "set-auto" }));
            Assert.IsFalse(IsShown(Find<VisualElement>(_view, "skit-advance-marker")));
        }

        /// <web-test file="e2e/tests/phaseC4.spec.ts">background skit snapshot renders speaker and body</web-test>
        [Test]
        public void 背景スキットの提示は話者と本文を1行で出し窓は出さない()
        {
            _view.Render(SkitTestData.Background(1, "Moore", "Background message"));
            simulate.FrameUpdate();

            var line = Find<Label>(_view, "background-skit");
            Assert.IsTrue(IsShown(line));
            Assert.AreEqual(SkitRichText.Literal("Moore : Background message"), line.text);
            Assert.AreEqual(PickingMode.Ignore, line.pickingMode);
            Assert.IsFalse(IsShown(Find<VisualElement>(_view, "skit-window")));
            Assert.IsFalse(IsShown(Find<VisualElement>(_view, "skit-tool-auto")));
        }

        [Test]
        public void 背景の空本文と提示なしでは何も出さない()
        {
            _view.Render(SkitTestData.Background(1, "Moore", ""));
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(Find<Label>(_view, "background-skit")));

            _view.Render(SkitTestData.None(2));
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(Find<VisualElement>(_view, "skit-window")));
            Assert.IsFalse(IsShown(Find<VisualElement>(_view, "skit-tool-auto")));
            Assert.IsFalse(IsShown(Find<VisualElement>(_view, "skit-show-ui")));
        }

        [Test]
        public void UI非表示の間は復帰ボタンだけを出す()
        {
            _view.Render(SkitTestData.Controls(1, true, false, false, false, true, SkitTestData.TextIntents));
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(Find<VisualElement>(_view, "skit-window")));
            Assert.IsFalse(IsShown(Find<VisualElement>(_view, "skit-tool-auto")));
            Assert.IsTrue(IsShown(Find<VisualElement>(_view, "skit-show-ui")));
        }

        [Test]
        public void 演出で本文の窓が閉じている間はツールだけを出す()
        {
            _view.Render(SkitTestData.Controls(1, false, false, false, false, false, SkitTestData.TextIntents));
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(Find<VisualElement>(_view, "skit-window")));
            Assert.IsTrue(IsShown(Find<VisualElement>(_view, "skit-tool-auto")));
            Assert.IsFalse(IsShown(Find<VisualElement>(_view, "skit-show-ui")));
        }

        [Test]
        public void 暗転は実画面いっぱいの黒を即座に出し入力を奪わない()
        {
            var transition = Mount(new SkitTransitionView());
            Assert.IsFalse(IsShown(transition));

            transition.SetShown(true);
            Assert.IsTrue(IsShown(transition));
            Assert.AreEqual(new Rect(0, 0, 1280, 720), transition.worldBound);
            Assert.AreEqual(1f, transition.resolvedStyle.backgroundColor.a, 0.001f);
            Assert.AreEqual(PickingMode.Ignore, transition.pickingMode);
        }

        /// <web-test file="e2e/tests/hotbar/hotbar.spec.ts">blockingスキット中はホットバーHUDだけが退き、会話窓は残る</web-test>
        [Test]
        public void blockingスキットの間は会話窓が出て終われば消える()
        {
            // ホットバーが退く側は HotbarHudViewTest が見る
            // That the hotbar retreats is checked by HotbarHudViewTest
            _view.Render(SkitTestData.Text(SkitTestData.SessionId, 1, "Blocking message", SkitTestData.TextIntents));
            Assert.IsTrue(IsShownWithAncestors(Find<VisualElement>(_view, "blocking-skit")));

            _view.Render(SkitTestData.None(2));
            Assert.IsFalse(IsShownWithAncestors(Find<VisualElement>(_view, "blocking-skit")));
        }

        [Test]
        public void ポーズに入ると乗っていた会話窓と選択肢の指のカーソルを外す()
        {
            _view.Render(SkitTestData.Choices(1, SkitTestData.ChoiceIntents));
            simulate.FrameUpdate();
            simulate.MouseMove(Find<Button>(_view, "skit-choice-route-a").worldBound.center);
            Assert.AreEqual(UiOsCursorShape.Pointer, _cursorApplier.Current, "Web .choice の cursor: pointer");
            _view.SetInteractive(false);
            simulate.FrameUpdate();
            Assert.AreEqual(UiOsCursorShape.Default, _cursorApplier.Current, "ポーズに入っても選択肢の指が残る");

            // 会話窓の文の区画も同じく、乗ったままポーズに入ったら指を外す
            // The window's text section likewise drops the finger when paused while hovered
            _view.SetInteractive(true);
            simulate.MouseMove(Find<VisualElement>(_view, "blocking-skit").worldBound.center);
            Assert.AreEqual(UiOsCursorShape.Pointer, _cursorApplier.Current, "Web .window の cursor: pointer");
            _view.SetInteractive(false);
            simulate.FrameUpdate();
            Assert.AreEqual(UiOsCursorShape.Default, _cursorApplier.Current, "ポーズに入っても会話窓の指が残る");
        }

        private string BodyText()
        {
            return Find<LocalizedLabel>(_view, "skit-body").text;
        }

        // 本文の Label は行高のタグを前置するので、その前置きだけを許して比べる。透明な残りは全文で終わるので末尾の一致だけでは打ち出し中も一致する
        // The body Label prefixes a line-height tag, so allow only that prefix; the transparent tail ends with the full text, so a bare suffix match would also hit while typing
        private string Expected(string body, int visibleCodePoints)
        {
            var expected = SkitRichText.TypewriterBody(body, visibleCodePoints);
            var actual = BodyText();
            if (!actual.EndsWith(expected)) return expected;
            var prefix = actual.Substring(0, actual.Length - expected.Length);
            return Regex.IsMatch(prefix, "^(<line-height=[^>]*>)?$") ? actual : expected;
        }

        // 祖先のどれかが display:none なら画面には出ていない
        // Not on screen when any ancestor is display:none
        private bool IsShownWithAncestors(VisualElement element)
        {
            simulate.FrameUpdate();
            for (var current = element; current != null; current = current.hierarchy.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None) return false;
            }
            return element.panel != null;
        }
    }
}
