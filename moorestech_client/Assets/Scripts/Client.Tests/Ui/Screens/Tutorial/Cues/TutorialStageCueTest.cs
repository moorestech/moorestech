using System.Text.RegularExpressions;
using Client.Game.InGame.UI.UIState;
using Client.Tests.Ui.Screens.Tutorial.Overlay;
using Client.Ui.Components.Text;
using Client.Ui.Screens.Tutorial.KeyControlHint;
using Client.Ui.Screens.Tutorial.Overlay;
using Client.Ui.State.Tutorial.KeyControlHint;
using Client.Ui.State.Tutorial.Overlay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Tutorial.TutorialOverlayProjectionTest;

namespace Client.Tests.Ui.Screens.Tutorial.Cues
{
    /// <summary>
    /// Web e2e の tutorial.spec を、枠とキー操作ヒントを同じパネルに載せて写す（L1）
    /// Copies Web e2e tutorial.spec with the outline and key hint mounted on one panel (L1)
    /// </summary>
    public class TutorialStageCueTest : TutorialOverlayTestHost
    {
        private FakeTutorialOverlayModel _overlay;
        private FakeKeyControlHintModel _keyControl;
        private TutorialOverlayView _overlayView;
        private KeyControlHintView _keyControlView;

        [SetUp]
        public void MountCues()
        {
            _overlay = new FakeTutorialOverlayModel();
            _keyControl = new FakeKeyControlHintModel();
            _overlayView = TutorialOverlayPresenter.Mount(rootVisualElement, rootVisualElement, _overlay.Current);
            _keyControlView = KeyControlHintPresenter.Mount(rootVisualElement, _keyControl.Current);
        }

        private void ShowKeyHint()
        {
            _keyControl.Set(new KeyControlHintProjection().Project(Data(1, KeyControlHintTest.Hint("k1", "Tab", "GameScreen")), UIStateEnum.GameScreen, false));
            simulate.FrameUpdate();
        }

        private void ShowOutline(string labelGuid)
        {
            MountAnchor(rootVisualElement, "pause.save", 400, 300, 200, 40);
            _overlay.Set(new TutorialOverlayProjection().Project(Data(1, Outline("o1", "pause.save", labelGuid))));
            simulate.FrameUpdate();
        }

        /// <web-test file="e2e/tests/system/tutorial.spec.ts">tutorial outline highlights the target without dimming the rest of the screen</web-test>
        [Test]
        public void 枠は対象を囲み画面の残りを暗くしない()
        {
            ShowOutline(null);

            var ring = _overlayView.Q(className: "tutorial-outline__ring").worldBound;
            Assert.AreEqual(new Rect(392, 292, 216, 56), new Rect(Mathf.Round(ring.x), Mathf.Round(ring.y), Mathf.Round(ring.width), Mathf.Round(ring.height)));
            // 全面の幕を持たない（オーバーレイ自身は透明で拾わない）
            // No full-screen scrim (the overlay itself is transparent and not pickable)
            Assert.AreEqual(0f, _overlayView.resolvedStyle.backgroundColor.a);
            Assert.AreEqual(PickingMode.Ignore, _overlayView.pickingMode);
        }

        /// <web-test file="e2e/tests/system/tutorial.spec.ts">outline with a label renders the label text beside the outline</web-test>
        [Test]
        public void ラベル付きの枠は枠の脇に文を出す()
        {
            ShowOutline(LabelGuid.ToString());
            simulate.FrameUpdate();

            var label = _overlayView.Q<TutorialHighlightLabelView>();
            Assert.IsTrue(IsShown(label));
            Assert.IsNotEmpty(label.text);
            Assert.AreEqual(400f - 8f, label.worldBound.xMin, 0.5f);
        }

        /// <web-test file="e2e/tests/system/tutorial.spec.ts">keyControl hint renders a kbd and text above the hotbar while uiState matches</web-test>
        [Test]
        public void 画面状態が一致する間はホットバーの上にkbdと文を出す()
        {
            ShowKeyHint();

            var hint = _keyControlView.Q<ShortcutHintLabel>("key-control-hint");
            Assert.AreEqual("Tab", hint.Q<KeyCap>().text);
            var floor = Px("--hotbar-floor-offset") + Px("--tutorial-key-hint-hotbar-gap");
            Assert.AreEqual(720f - floor, _keyControlView.worldBound.yMax, 1f);
            Assert.AreEqual(640f, hint.worldBound.center.x, 1f);
        }

        /// <web-test file="e2e/tests/system/tutorial.spec.ts">keyControl hint actually plays the strong 1.08 pulse in the browser</web-test>
        [Test]
        public void キー操作ヒントは1_08の脈動を実際に流す()
        {
            ShowKeyHint();
            // 最初の呼び出しを経過 0 として開始する（UiMotionHandle）
            // The first scheduler call starts the motion at elapsed 0 (UiMotionHandle)
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs((long)(Ms("--tutorial-pulse-duration") / 2f));
            Assert.AreEqual(1.08f, _keyControlView.Q<ShortcutHintLabel>("key-control-hint").resolvedStyle.scale.value.x, 0.005f);
        }

        /// <web-test file="e2e/tests/system/tutorial.spec.ts">tutorial outline actually plays the subtle 1.03 pulse in the browser</web-test>
        [Test]
        public void 枠は1_03の脈動を実際に流しラベルは脈動しない()
        {
            ShowOutline(LabelGuid.ToString());
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs((long)(Ms("--tutorial-pulse-duration") / 2f));

            Assert.AreEqual(1.03f, _overlayView.Q(className: "tutorial-outline__clip").resolvedStyle.scale.value.x, 0.005f);
            Assert.AreEqual(1f, _overlayView.Q<TutorialHighlightLabelView>().resolvedStyle.scale.value.x, 0.0001f);
        }

        /// <web-test file="e2e/tests/system/tutorial.spec.ts">outline never outlives its anchor even when no frame arrives</web-test>
        [Test]
        public void 演出の更新が来なくてもアンカーが消えたフレームで枠も消える()
        {
            var anchor = MountAnchor(rootVisualElement, "pause.save", 400, 300, 200, 40);
            _overlay.Set(new TutorialOverlayProjection().Project(Data(1, Outline("o1", "pause.save", null))));
            simulate.FrameUpdate();
            Assert.IsTrue(IsShown(_overlayView.Q(className: "tutorial-outline__clip")));

            LogAssert.Expect(LogType.Warning, new Regex("anchor is missing: pause.save"));
            anchor.RemoveFromHierarchy();
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_overlayView.Q(className: "tutorial-outline__clip")));
        }
    }
}
