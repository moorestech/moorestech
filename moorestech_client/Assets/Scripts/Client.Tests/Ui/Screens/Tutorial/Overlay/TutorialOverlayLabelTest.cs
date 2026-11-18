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
            var anchor = MountAnchor(rootVisualElement, "pause.save", 1220, 100, 40, 40);
            var shortText = new TutorialOutlineCue.Labeled("s1", "o1", "pause.save", 8, UiText.FromVerbatim("右端のラベル"));
            _model.Set(new TutorialOverlayViewModel(new[] { shortText }, System.Array.Empty<TutorialDragGuideCue>()));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.LessOrEqual(Shown<TutorialHighlightLabelView>().Single().worldBound.xMax, 1280.5f);

            // 器より広い文は、枠の左端が器の左端にあるとき器の幅で折り返して左端に揃う（右寄りの枠は置き場の残りで折り返す。下の別テスト）
            // A label wider than the container wraps at the container width at the left edge when the ring starts there (a ring near the right wraps at the holder's remainder; see the next test)
            anchor.style.left = 8f;
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

        [Test]
        public void 右寄りの枠のラベルは置き場の残りの幅で折り返し枠の左端から出る()
        {
            // Web の面は position: fixed なので、置き場の幅 1280 − 枠の左端 1034 = 246 で縮んで 2 行になる（金槌のセルの吹き出しの実測）
            // The Web face is position: fixed, so it shrinks to holder 1280 - ring left 1034 = 246 and wraps to two lines (the measured hammer-cell callout)
            MountAnchor(rootVisualElement, "pause.save", 1042, 600, 40, 40);
            var text = new TutorialOutlineCue.Labeled("s1", "o1", "pause.save", 8, UiText.FromVerbatim("ハンマーをクラフトしてホットバーの空いている枠へ入れよう"));
            _model.Set(new TutorialOverlayViewModel(new[] { text }, System.Array.Empty<TutorialDragGuideCue>()));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            var label = Shown<TutorialHighlightLabelView>().Single();
            Assert.AreEqual(1034f, label.worldBound.xMin, 0.5f, $"label rect {label.worldBound}");
            Assert.AreEqual(1280f - 1034f, label.worldBound.width, 0.5f, $"label rect {label.worldBound}");
            Assert.Greater(label.worldBound.height, 40f, $"2 行に折り返す {label.worldBound}");
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
