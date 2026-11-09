using Client.Ui.Screens.Tutorial.Overlay;
using Client.Ui.State.Tutorial.Overlay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Tutorial.TutorialOverlayProjectionTest;

namespace Client.Tests.Ui.Screens.Tutorial.Overlay
{
    /// <summary>
    /// パンした ScrollView の中のノードの枠が viewport で切られることを確かめる（Web e2e tutorialHighlightClip の写し。研究画面は未移植なので同じ構造を L1 で作る）
    /// Verifies a node outline inside a panned ScrollView is cut at the viewport (copy of Web e2e tutorialHighlightClip; the research screen is unported, so the same structure is built in L1)
    /// </summary>
    public class TutorialHighlightAncestorClipTest : TutorialOverlayTestHost
    {
        private FakeTutorialOverlayModel _model;
        private TutorialOverlayView _view;

        [SetUp]
        public void MountOverlay()
        {
            _model = new FakeTutorialOverlayModel();
            _view = TutorialOverlayPresenter.Mount(rootVisualElement, rootVisualElement, _model.Current);
        }

        /// <web-test file="e2e/tests/system/tutorialHighlightClip.spec.ts">研究ノードのハイライトが祖先のoverflowクリップに合わせてマスクされる</web-test>
        [Test]
        public void スクロールで半分隠れた研究ノードの枠はviewportの境界で切られる()
        {
            // 研究ツリーと同じく ScrollView の中身をずらし、ノードを viewport の上端にまたがらせる
            // Like the research tree, pan the ScrollView content so the node straddles the viewport top
            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal) { style = { position = Position.Absolute, left = 200, top = 150, width = 600, height = 400 } };
            rootVisualElement.Add(scroll);
            var canvas = new VisualElement { style = { width = 2000, height = 2000 } };
            scroll.Add(canvas);
            MountAnchor(canvas, "research.node-a", 300, 480, 160, 80);
            simulate.FrameUpdate();
            scroll.scrollOffset = new Vector2(0, 520);
            _model.Set(new TutorialOverlayProjection().Project(Data(1, Outline("o1", "research.node-a", null))));
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            var viewport = scroll.contentViewport.worldBound;
            var clip = UnpulsedWorldBound(_view.Q(className: "tutorial-outline__clip"));
            Assert.AreEqual(viewport.yMin, clip.yMin, 0.5f);
            Assert.Greater(clip.yMax, viewport.yMin);
            // 切っていない左右はグローの帯まで残る
            // The unclipped left and right sides keep the glow band
            Assert.AreEqual(viewport.xMin + 300f - 8f - Px("--tutorial-highlight-glow"), clip.xMin, 0.5f);
        }
    }
}
