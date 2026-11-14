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
            Assert.AreEqual(Expected(SecondBody, 0), BodyText(), "120ms ではまだ 0 字（全文が即時に出ていない）");

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

        [Test]
        public void 打ち出し中に同じ本文で版と許可だけが変わっても頭から打ち直さない()
        {
            _view.Render(SkitTestData.Text(SkitTestData.SessionId, 1, "Blocking message", SkitTestData.TextIntents));
            _view.Render(SkitTestData.Text(SkitTestData.SessionId, 2, SecondBody, SkitTestData.TextIntents));
            // 時計は最初のスケジューラ呼び出しで開始するので、先に 1 フレーム回してから途中まで進める
            // The clock starts on the first scheduler call, so run one frame first and then advance partway
            simulate.FrameUpdate();
            simulate.FrameUpdateMs(300);
            var typedSoFar = BodyText();
            Assert.AreEqual(Expected(SecondBody, 6), typedSoFar, "前提: 300ms で 6 字まで出ている");

            // 自動・ポーズ・UI 非表示の往復は版と許可 intent だけが進むので、続きから打つ
            // Auto, pause and UI-hide round trips only advance the revision and intents, so typing continues
            _view.Render(SkitTestData.Text(SkitTestData.SessionId, 3, SecondBody, new[] { "set-auto", "skip" }));
            Assert.AreEqual(typedSoFar, BodyText(), "同じ本文の新しい版で 0 字から打ち直した");
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
            // 退く側は HotbarHudViewTest
            // The hotbar retreat is checked by HotbarHudViewTest
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

            // 文の区画も、乗ったままポーズなら指を外す
            // The text section also drops the finger when paused while hovered
            _view.SetInteractive(true);
            simulate.MouseMove(Find<VisualElement>(_view, "blocking-skit").worldBound.center);
            Assert.AreEqual(UiOsCursorShape.Pointer, _cursorApplier.Current, "Web .window の cursor: pointer");
            _view.SetInteractive(false);
            simulate.FrameUpdate();
            Assert.AreEqual(UiOsCursorShape.Default, _cursorApplier.Current, "ポーズに入っても会話窓の指が残る");
        }

        private string BodyText()
        {
            // 表示文は行送り・字間の印を前置するので、文の中身は原文で読む
            // The display text prefixes line-height and spacing tags, so read the words from the source
            return Find<LocalizedLabel>(_view, "skit-body").SourceText;
        }

        private static string Expected(string body, int visibleCodePoints)
        {
            return SkitRichText.TypewriterBody(body, visibleCodePoints);
        }

        // 祖先が display:none なら非表示
        // Hidden when any ancestor is display:none
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
