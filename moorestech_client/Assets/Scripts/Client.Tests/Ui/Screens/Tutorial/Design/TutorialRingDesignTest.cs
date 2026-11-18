using Client.Tests.Ui.Components;
using Client.Tests.Ui.Screens.Tutorial.Overlay;
using Client.Ui.Screens.Tutorial.Overlay;
using Client.Ui.State.Tutorial.Overlay;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Tutorial.TutorialOverlayProjectionTest;

namespace Client.Tests.Ui.Screens.Tutorial.Design
{
    public class TutorialRingDesignTest : TutorialOverlayTestHost
    {
        private static string Uss => TutorialDesignSource.ReadScreenFile("Overlay/TutorialOverlay.uss");

        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">枠線とグローの両方が原色赤トークンを指し、旧来の黄が残らない</web-test>
        [Test]
        public void 枠線とグローは赤のトークンを指し黄が残らない()
        {
            StringAssert.Contains("var(--tutorial-attention-red)", TutorialDesignSource.Rule(Uss, ".tutorial-outline__ring"));
            StringAssert.Contains("var(--tutorial-attention-glow)", TutorialDesignSource.Rule(Uss, ".tutorial-outline__glow"));
            StringAssert.DoesNotContain("yellow", Uss);
            StringAssert.DoesNotContain("--tutorial-highlight-color", Uss);
        }

        // Task 10 のレビューの穴: 解決後の枠線の赤とグローの色を実画面で見る（Web の expectAttentionGlow 相当）
        // Gap from the Task 10 review: check the resolved ring red and glow color on the real panel (Web expectAttentionGlow)
        [Test]
        public void 描かれた枠線は赤でグローはWebと同じ色になる()
        {
            var (view, ring, glow) = MountOutline(rootVisualElement);

            var red = ThemeTokenFile.ResolveColor("--tutorial-attention-red");
            Assert.AreEqual(UnityEngine.Color.red, red);
            ThemeTokenFile.AssertSameColor(red, ring.borderTopColor, "ring");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--tutorial-attention-glow"), glow.borderTopColor, "glow");
            Assert.Greater(glow.borderTopWidth, 0f);
            Assert.IsNotNull(view);
        }

        private (TutorialOverlayView view, IResolvedStyle ring, IResolvedStyle glow) MountOutline(VisualElement anchorParent)
        {
            var model = new FakeTutorialOverlayModel();
            var view = TutorialOverlayPresenter.Mount(rootVisualElement, rootVisualElement, model.Current);
            MountAnchor(anchorParent, "research.node-a", 100, 100, 120, 60);
            model.Set(new TutorialOverlayProjection().Project(Data(1, Outline("o1", "research.node-a", null))));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            return (view, view.Q(className: "tutorial-outline__ring").resolvedStyle, view.Q(className: "tutorial-outline__glow").resolvedStyle);
        }

        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">拡縮は1.03で、内側ノードを足さず既存の.highlight自身に付ける</web-test>
        [Test]
        public void 枠の拡縮は枠の容器自身に弱い振幅で付ける()
        {
            var view = TutorialDesignSource.ReadScreenFile("Overlay/TutorialOutlineView.cs");
            StringAssert.Contains("pulse.Attach(_clip, TutorialAttentionPulse.SubtleScale)", view);
        }

        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">ラベル面は脈動せず、既存のstage同率スケールを保つ</web-test>
        [Test]
        public void ラベルは脈動の結び付けを持たない()
        {
            StringAssert.DoesNotContain("TutorialPulseBinding", TutorialDesignSource.ReadScreenFile("Overlay/TutorialHighlightLabelView.cs"));
        }

        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">機能側CSSに色リテラルと秒数リテラルを直書きしない</web-test>
        [Test]
        public void 枠のUSSに色と秒の直書きが無い()
        {
            Assert.IsFalse(TutorialDesignSource.ColorLiteral.IsMatch(Uss), Uss);
            Assert.IsFalse(TutorialDesignSource.MillisecondLiteral.IsMatch(Uss), Uss);
        }

        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">ドラッグガイド矢印は対象外で、移動ループのまま据え置く</web-test>
        [Test]
        public void ドラッグガイドは脈動せず移動のステップを使う()
        {
            foreach (var path in TutorialDesignSource.ListScreenFiles("DragGuide", "*.cs"))
            {
                var text = TutorialDesignSource.ReadScreenFile(path);
                StringAssert.DoesNotContain("TutorialPulseBinding", text, path);
                StringAssert.DoesNotContain("TutorialAttentionPulse", text, path);
            }
            StringAssert.Contains("TutorialDragGuideStep", TutorialDesignSource.ReadScreenFile("DragGuide/TutorialDragGuideView.cs"));
        }
    }
}
