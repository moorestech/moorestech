using System.Text.RegularExpressions;
using Client.Ui.Screens.Tutorial.DragGuide;
using Client.Ui.Screens.Tutorial.Overlay;
using Client.Ui.State.Tutorial.Logic;
using Client.Ui.State.Tutorial.Overlay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Tutorial.Overlay
{
    /// <summary>
    /// 2 つのアンカーを結ぶドラッグ誘導の出し入れと置き場所を確かめる（L1。Web TutorialOverlay.test.ts の drag guides）
    /// Verifies the drag guide between two anchors shows, hides and sits at the origin (L1; Web TutorialOverlay.test.ts drag guides)
    /// </summary>
    public class TutorialOverlayDragGuideTest : TutorialOverlayTestHost
    {
        private FakeTutorialOverlayModel _model;
        private TutorialOverlayView _view;

        [SetUp]
        public void MountOverlay()
        {
            _model = new FakeTutorialOverlayModel();
            _view = TutorialOverlayPresenter.Mount(rootVisualElement, rootVisualElement, _model.Current);
        }

        private static TutorialOverlayViewModel Guide()
        {
            return new TutorialOverlayViewModel(System.Array.Empty<TutorialOutlineCue>(), new[] { new TutorialDragGuideCue("s1:d1", "pause.save", "pause.settings") });
        }

        private int ShownGuides()
        {
            return _view.Query<TutorialDragGuideView>().Where(guide => IsShown(guide)).ToList().Count;
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">両anchorがreadyのときだけ tutorial-drag-guide を1件描画する</web-test>
        [Test]
        public void 両端がreadyのときだけDragGuideを1件出す()
        {
            MountAnchor(rootVisualElement, "pause.save", 100, 100, 40, 40);
            LogAssert.Expect(LogType.Warning, new Regex("anchor is missing: pause.settings"));
            _model.Set(Guide());
            simulate.FrameUpdate();
            Assert.AreEqual(0, ShownGuides());

            MountAnchor(rootVisualElement, "pause.settings", 500, 300, 40, 40);
            simulate.FrameUpdate();
            Assert.AreEqual(1, ShownGuides());
            var guide = _view.Q<TutorialDragGuideView>();
            Assert.AreEqual(120f, guide.worldBound.center.x - guide.resolvedStyle.translate.x, 0.5f);
        }

        [Test]
        public void 両端が最初から揃っていても寸法の解決後に中心へ置く()
        {
            // 寸法の解決より先に Show が来ても、解決後に置き直して起点の中心に合わせる
            // Even if Show arrives before the size resolves, re-place after resolution to center on the origin
            MountAnchor(rootVisualElement, "pause.save", 100, 100, 40, 40);
            MountAnchor(rootVisualElement, "pause.settings", 500, 300, 40, 40);
            // 両端のレイアウトを先に済ませ、Set の同期の測定で ready にして寸法の解決前の Show を起こす
            // Lay out both ends first so Set's synchronous measurement is ready and Show runs before the size resolves
            simulate.FrameUpdate();
            _model.Set(Guide());
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            var guide = _view.Q<TutorialDragGuideView>();
            Assert.AreEqual(120f, guide.worldBound.center.x - guide.resolvedStyle.translate.x, 0.5f);
            Assert.AreEqual(120f, guide.worldBound.center.y - guide.resolvedStyle.translate.y, 0.5f);
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">起点が祖先クリップの外へスクロールしたら矢印を描かない</web-test>
        [Test]
        public void 起点がクリップの外へスクロールしたら出さない()
        {
            var scroll = new ScrollView { style = { position = Position.Absolute, left = 0, top = 100, width = 400, height = 200 } };
            rootVisualElement.Add(scroll);
            var content = new VisualElement { style = { height = 1000 } };
            scroll.Add(content);
            MountAnchor(content, "pause.save", 50, 20, 40, 40);
            MountAnchor(rootVisualElement, "pause.settings", 600, 300, 40, 40);
            _model.Set(Guide());
            simulate.FrameUpdate();
            Assert.AreEqual(1, ShownGuides());

            scroll.scrollOffset = new Vector2(0, 200);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.AreEqual(0, ShownGuides());
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">片方のanchorが未解決に戻ると非表示になる</web-test>
        [Test]
        public void 片方のアンカーが消えると隠す()
        {
            MountAnchor(rootVisualElement, "pause.save", 100, 100, 40, 40);
            var to = MountAnchor(rootVisualElement, "pause.settings", 500, 300, 40, 40);
            _model.Set(Guide());
            simulate.FrameUpdate();
            Assert.AreEqual(1, ShownGuides());

            LogAssert.Expect(LogType.Warning, new Regex("anchor is missing: pause.settings"));
            to.RemoveFromHierarchy();
            simulate.FrameUpdate();
            Assert.AreEqual(0, ShownGuides());
        }

        [Test]
        public void 隠して出し直すと1周の始めから動く()
        {
            MountAnchor(rootVisualElement, "pause.save", 100, 100, 40, 40);
            var to = MountAnchor(rootVisualElement, "pause.settings", 500, 300, 40, 40);
            _model.Set(Guide());
            // 寸法・周期の解決と、ループの最初の更新（その時刻が起点）を済ませてから半周進める
            // Resolve size and duration and run the loop's first update (its head time) before advancing half a loop
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs((long)(Ms("--tutorial-drag-guide-duration") / 2f));
            var guide = _view.Q<TutorialDragGuideView>();
            Assert.Greater(guide.resolvedStyle.translate.x, 1f);

            // 終点が消えて隠れた後に戻ると、途中の位相ではなく起点・透明から流れ直す（Web は 0% から）
            // When the target returns after hiding, restart at the origin and transparent, not mid-phase (Web restarts at 0%)
            LogAssert.Expect(LogType.Warning, new Regex("anchor is missing: pause.settings"));
            to.RemoveFromHierarchy();
            simulate.FrameUpdate();
            Assert.AreEqual(0, ShownGuides());
            simulate.FrameUpdateMs((long)(Ms("--tutorial-drag-guide-duration") / 4f));
            rootVisualElement.Add(to);
            simulate.FrameUpdateMs(0);
            Assert.AreEqual(1, ShownGuides());
            Assert.AreEqual(0f, guide.resolvedStyle.translate.x, 0.5f);
            Assert.AreEqual(0f, guide.resolvedStyle.opacity, 0.05f);
        }

        [Test]
        public void パネルから外れている間にShowされても載せ直せば流れ始める()
        {
            MountAnchor(rootVisualElement, "pause.save", 100, 100, 40, 40);
            MountAnchor(rootVisualElement, "pause.settings", 500, 300, 40, 40);
            _model.Set(Guide());
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(0);
            var guide = _view.Q<TutorialDragGuideView>();
            var parent = guide.parent;

            // 外れている間の Show は始めるものが無い（ここでは取消済みハンドルを残さず、載せ直しの解決が始める）
            // Show while detached has nothing to start (leave no cancelled handle; the re-attach resolution starts it)
            guide.HideGuide();
            guide.RemoveFromHierarchy();
            guide.Show(new TutorialDragGuidePlacement(120f, 120f, 400f, 200f));
            parent.Add(guide);
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs((long)(Ms("--tutorial-drag-guide-duration") / 2f));
            Assert.Greater(guide.resolvedStyle.translate.x, 1f);
        }

        [Test]
        public void モデルから誘導が消えると誘導のViewも消える()
        {
            MountAnchor(rootVisualElement, "pause.save", 100, 100, 40, 40);
            MountAnchor(rootVisualElement, "pause.settings", 500, 300, 40, 40);
            _model.Set(Guide());
            simulate.FrameUpdate();
            Assert.AreEqual(1, _view.Query<TutorialDragGuideView>().ToList().Count);

            _model.Set(TutorialOverlayViewModel.Empty);
            simulate.FrameUpdate();
            Assert.AreEqual(0, _view.Query<TutorialDragGuideView>().ToList().Count);
        }
    }
}
