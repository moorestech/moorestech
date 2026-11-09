using Client.Ui.Foundation.Anchor;
using Client.Ui.Screens.Tutorial.Overlay;
using Client.Ui.State.Tutorial.Overlay;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Tutorial.TutorialOverlayProjectionTest;

namespace Client.Tests.Ui.Screens.Tutorial.Overlay
{
    /// <summary>
    /// 祖先クリップで枠を切る・出さないことを確かめる（L1。Web TutorialOverlay.test.ts の clipping）
    /// Verifies outlines are cut or hidden by ancestor clips (L1; Web TutorialOverlay.test.ts clipping)
    /// </summary>
    public class TutorialOverlayClipTest : TutorialOverlayTestHost
    {
        private FakeTutorialOverlayModel _model;
        private TutorialOverlayView _view;

        [SetUp]
        public void MountOverlay()
        {
            _model = new FakeTutorialOverlayModel();
            _view = TutorialOverlayPresenter.Mount(rootVisualElement, rootVisualElement, _model.Current);
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">祖先クリップの外へ出た辺はclip-pathで切られる</web-test>
        [Test]
        public void 祖先クリップの外へ出た辺だけが切られ残りの辺はグローを残す()
        {
            var container = ClipBox();
            MountAnchor(container, "research.node-a", 100, -20, 120, 60);
            _model.Set(new TutorialOverlayProjection().Project(Data(1, Outline("o1", "research.node-a", null))));
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            var clip = UnpulsedWorldBound(_view.Q(className: "tutorial-outline__clip"));
            Assert.AreEqual(200f, clip.yMin, 0.5f);
            Assert.AreEqual(100f - 8f - Glow(), clip.xMin, 0.5f);
            Assert.AreEqual(200f + 40f + 8f + Glow(), clip.yMax, 0.5f);
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">完全にクリップされた枠は要素ごと描かない</web-test>
        [Test]
        public void 完全にクリップされた枠は出さない()
        {
            var container = ClipBox();
            MountAnchor(container, "research.node-a", 100, -200, 120, 60);
            _model.Set(new TutorialOverlayProjection().Project(Data(1, Outline("o1", "research.node-a", null))));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_view.Q(className: "tutorial-outline__clip")));
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">グロー幅未満だけ外れた枠は要素ごと描かない</web-test>
        [Test]
        public void グロー幅に満たない分だけ外れた枠も出さない()
        {
            var container = ClipBox();
            // 箱（アンカー ± 8）の下端が容器の上端より 2px 上（グロー 4px より狭い）
            // The box (anchor ± 8) ends 2px above the container top (narrower than the 4px glow)
            MountAnchor(container, "research.node-a", 100, -70, 120, 60);
            _model.Set(new TutorialOverlayProjection().Project(Data(1, Outline("o1", "research.node-a", null))));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_view.Q(className: "tutorial-outline__clip")));
        }

        private VisualElement ClipBox()
        {
            // overflow: hidden と印を持つ容器（研究ツリーの viewport と同じ役）
            // A container with overflow: hidden and the clip mark (the research tree viewport's role)
            var container = new VisualElement { style = { position = Position.Absolute, left = 0, top = 200, width = 600, height = 300, overflow = Overflow.Hidden } };
            TutorialAnchor.MarkClipContainer(container);
            rootVisualElement.Add(container);
            return container;
        }

        private static float Glow()
        {
            return Px("--tutorial-highlight-glow");
        }
    }
}
