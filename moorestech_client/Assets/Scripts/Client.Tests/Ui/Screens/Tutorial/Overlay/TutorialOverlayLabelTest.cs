using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Tests.Ui.Components;
using Client.Ui.Screens.Tutorial.Overlay;
using Client.Ui.State.Text;
using Client.Ui.State.Tutorial.Overlay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Tutorial.TutorialOverlayProjectionTest;

namespace Client.Tests.Ui.Screens.Tutorial.Overlay
{
    /// <summary>
    /// 枠のラベルの出し分けと置き場所を確かめる（L1。Web TutorialOverlay.test.ts の labels）
    /// Verifies whether and where outline labels appear (L1; Web TutorialOverlay.test.ts labels)
    /// </summary>
    public class TutorialOverlayLabelTest : TutorialOverlayTestHost
    {
        private FakeTutorialOverlayModel _model;
        private TutorialOverlayView _view;

        [SetUp]
        public void MountOverlay()
        {
            _model = new FakeTutorialOverlayModel();
            _view = TutorialOverlayPresenter.Mount(rootVisualElement, rootVisualElement, _model.Current);
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">labelTutorialGuid がある枠線だけラベルを描く</web-test>
        [Test]
        public void ラベルのGUIDがある枠だけラベルを出す()
        {
            MountAnchor(rootVisualElement, "pause.save", 100, 100, 200, 40);
            MountAnchor(rootVisualElement, "pause.back", 100, 300, 200, 40);
            _model.Set(new TutorialOverlayProjection().Project(Data(1, Outline("o1", "pause.save", LabelGuid.ToString()), Outline("o2", "pause.back", null))));
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            var labels = Shown<TutorialHighlightLabelView>();
            Assert.AreEqual(1, labels.Count);
            Assert.AreEqual(Mathf.Round(100f + 40f + 8f + Gap()), Mathf.Round(labels[0].worldBound.yMin));
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">ラベルが下に収まらない時は枠線の上へ反転する</web-test>
        [Test]
        public void 下に収まらないラベルは枠の上へ反転する()
        {
            MountAnchor(rootVisualElement, "pause.save", 100, 680, 200, 30);
            _model.Set(new TutorialOverlayProjection().Project(Data(1, Outline("o1", "pause.save", LabelGuid.ToString()))));
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            var label = Shown<TutorialHighlightLabelView>().Single();
            Assert.AreEqual(680f - 8f - Gap(), label.worldBound.yMax, 0.5f);
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">ラベルが右端を越える時はclip内へ押し戻し、器より広い時は折り返して左端に収まる</web-test>
        [Test]
        public void 右端を越えるラベルはclipへ押し戻し器より広ければ折り返して左端に揃える()
        {
            MountAnchor(rootVisualElement, "pause.save", 1220, 100, 40, 40);
            var shortText = new TutorialOutlineCue.Labeled("s1", "o1", "pause.save", 8, UiText.FromVerbatim("右端のラベル"));
            _model.Set(new TutorialOverlayViewModel(new[] { shortText }, System.Array.Empty<TutorialDragGuideCue>()));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.LessOrEqual(Shown<TutorialHighlightLabelView>().Single().worldBound.xMax, 1280.5f);

            var longText = new TutorialOutlineCue.Labeled("s1", "o1", "pause.save", 8, UiText.FromVerbatim(string.Concat(Enumerable.Repeat("とても長いチュートリアルの説明文", 12))));
            _model.Set(new TutorialOverlayViewModel(new[] { longText }, System.Array.Empty<TutorialDragGuideCue>()));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            var wrapped = Shown<TutorialHighlightLabelView>().Single();
            Assert.AreEqual(0f, wrapped.worldBound.xMin, 0.5f, $"wrapped label rect {wrapped.worldBound}");
            // Web と同じく折り返した面は器の幅いっぱいに広がる（Unity の既定は最長行の幅へ縮む。字形の幅で左端がずれる）
            // As on Web, a wrapped face fills the container width (Unity shrinks to the longest line by default, shifting the left edge by glyph width)
            Assert.AreEqual(1280f, wrapped.worldBound.width, 0.5f);
            Assert.Greater(wrapped.worldBound.height, 30f);
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">anchor未解決の枠線はラベルも描かない</web-test>
        [Test]
        public void アンカーが解けない枠はラベルも出さない()
        {
            LogAssert.Expect(LogType.Warning, new Regex("anchor is missing: pause.save"));
            _model.Set(new TutorialOverlayProjection().Project(Data(1, Outline("o1", "pause.save", LabelGuid.ToString()))));
            simulate.FrameUpdate();

            Assert.AreEqual(0, Shown<TutorialHighlightLabelView>().Count);
            Assert.AreEqual(0, _view.Query(className: "tutorial-outline__clip").Where(e => IsShown(e)).ToList().Count);
        }

        [Test]
        public void ラベルの面はlabel_face_paddingの4px_10pxを受ける()
        {
            MountAnchor(rootVisualElement, "pause.save", 100, 100, 200, 40);
            _model.Set(new TutorialOverlayProjection().Project(Data(1, Outline("o1", "pause.save", LabelGuid.ToString()))));
            simulate.FrameUpdate();
            var label = Shown<TutorialHighlightLabelView>().Single();
            Assert.AreEqual((4f, 10f, 4f, 10f), (label.resolvedStyle.paddingTop, label.resolvedStyle.paddingRight, label.resolvedStyle.paddingBottom, label.resolvedStyle.paddingLeft));
        }

        [Test]
        public void ラベルを隠した後に同じ位置で出し直すとラベルが見える()
        {
            var anchor = MountAnchor(rootVisualElement, "pause.save", 100, 100, 200, 40);
            _model.Set(new TutorialOverlayProjection().Project(Data(1, Outline("o1", "pause.save", LabelGuid.ToString()))));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.AreEqual(1, Shown<TutorialHighlightLabelView>().Count);

            anchor.style.display = DisplayStyle.None;
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.AreEqual(0, Shown<TutorialHighlightLabelView>().Count);

            anchor.style.display = DisplayStyle.Flex;
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.AreEqual(1, Shown<TutorialHighlightLabelView>().Count);
        }

        private List<T> Shown<T>() where T : VisualElement
        {
            return rootVisualElement.Query<T>().Where(e => IsShown(e)).ToList();
        }

        private static float Gap()
        {
            return float.Parse(ThemeTokenFile.Resolve("--tutorial-highlight-label-gap").Replace("px", ""), CultureInfo.InvariantCulture);
        }
    }
}
