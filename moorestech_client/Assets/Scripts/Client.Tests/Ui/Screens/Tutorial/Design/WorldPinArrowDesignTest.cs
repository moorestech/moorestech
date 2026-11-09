using System;
using NUnit.Framework;

namespace Client.Tests.Ui.Screens.Tutorial.Design
{
    public class WorldPinArrowDesignTest
    {
        private static string Uss => TutorialDesignSource.ReadScreenFile("WorldPin/WorldPin.uss");

        private static string Source(string file)
        {
            return TutorialDesignSource.ReadScreenFile("WorldPin/" + file);
        }

        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">塗りは原色赤トークンで、世界分離用の縁取りは残す</web-test>
        [Test]
        public void 矢印の塗りは赤のトークンで縁取りは残る()
        {
            var root = TutorialDesignSource.Rule(Uss, ".world-pin-overlay");
            StringAssert.Contains("--world-pin-view-arrow-fill: var(--tutorial-attention-red)", root);
            StringAssert.Contains("--world-pin-view-face: var(--world-pin-face)", root);
            var glyphs = Source("WorldPinGlyphs.cs");
            var drawArrow = glyphs.Substring(glyphs.IndexOf("void DrawArrow", StringComparison.Ordinal));
            StringAssert.Contains("Stroke()", drawArrow);
        }

        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">脈動はsvg側に付け、1.08で回す</web-test>
        [Test]
        public void 脈動は矢印の絵の要素に強い振幅で付ける()
        {
            StringAssert.Contains("pulse.Attach(_glyph, TutorialAttentionPulse.StrongScale)", Source("WorldPinEdgeArrowView.cs"));
        }

        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">.arrow div側にはanimationもtransformも付けない（インラインtransformを潰さないため）</web-test>
        [Test]
        public void 矢印の外側の要素はUSSで回転も拡縮も持たない()
        {
            var rule = TutorialDesignSource.Rule(Uss, ".world-pin__arrow");
            StringAssert.DoesNotContain("scale", rule);
            StringAssert.DoesNotContain("rotate", rule);
            StringAssert.DoesNotContain("translate", rule);
        }

        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">矢印の位置と回転はTSXのインラインtransformが持ち続ける</web-test>
        [Test]
        public void 矢印の位置と回転はコードのインラインが持つ()
        {
            var view = Source("WorldPinEdgeArrowView.cs");
            StringAssert.Contains("style.rotate =", view);
            StringAssert.Contains("style.translate =", view);
        }

        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">機能側CSSに色リテラルと秒数リテラルを直書きしない</web-test>
        [Test]
        public void ピンのUSSに色と秒の直書きが無い()
        {
            Assert.IsFalse(TutorialDesignSource.ColorLiteral.IsMatch(Uss), Uss);
            Assert.IsFalse(TutorialDesignSource.MillisecondLiteral.IsMatch(Uss), Uss);
        }

        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">ピン本体のラベル・マーカーは据え置く</web-test>
        [Test]
        public void ピン本体のラベルとマーカーは脈動せず面と字の大きさを受ける()
        {
            StringAssert.DoesNotContain("TutorialPulseBinding", Source("WorldPinMarkerView.cs"));
            var label = TutorialDesignSource.Rule(Uss, ".world-pin__label");
            StringAssert.Contains("var(--world-pin-face)", label);
            StringAssert.Contains("var(--label-face-font-size)", label);
        }
    }
}
