using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Slots.Fluid
{
    /// <summary>
    /// FluidSlot の見た目のトークン適用を確かめる（L1）
    /// Verifies FluidSlot frame, amount type with dark outline, baseline and ghost fade follow the tokens (L1)
    /// </summary>
    public class FluidSlotLookTest : UiTestHost
    {
        [TearDown]
        public void NoUnexpectedLogs()
        {
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 枠は48pxでdark4の1pxの線とdark8の面とsmの角を持つ()
        {
            var slot = Mount(FluidSlotContent.Empty);

            Assert.AreEqual(48f, slot.resolvedStyle.width, 0.01f);
            Assert.AreEqual(48f, slot.resolvedStyle.height, 0.01f);
            Assert.AreEqual(1f, slot.resolvedStyle.borderTopWidth, 0.01f);
            Assert.AreEqual(1f, slot.resolvedStyle.borderLeftWidth, 0.01f);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--mantine-color-dark-4"), slot.resolvedStyle.borderTopColor, "border");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--mantine-color-dark-8"), slot.resolvedStyle.backgroundColor, "face");
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--mantine-radius-sm"), slot.resolvedStyle.borderTopLeftRadius, 0.01f);
            // はみ出しの切り抜き（overflow: hidden）は IResolvedStyle に無い（設計書 A-65）ので、Task 7 Step 3 のフィルの角の画素で見る
            // Overflow clipping is not on IResolvedStyle (design A-65), so Task 7 Step 3 checks it on the fill-corner pixels
        }

        /// <web-test file="src/app/iconTextOutlineDesign.test.ts">白文字2系統が黒縁の共有クラスを持ち、擬似縁を残さない</web-test>
        /// <web-test file="src/app/iconTextOutlineDesign.test.ts">文字色は変更しない</web-test>
        [Test]
        public void 量は12pxの太字の白で後ろの層だけが黒い縁を持つ()
        {
            var slot = Mount(FluidSlotContent.Filled(SlotTestIcons.WithTexture("10", "Water"), 500, 1000, Color.blue));

            var glyph = Layer(slot, "outlined-count-label--glyph");
            var stroke = Layer(slot, "outlined-count-label--stroke");
            Assert.AreEqual(12f, glyph.resolvedStyle.fontSize, 0.01f);
            Assert.IsTrue(glyph.ClassListContains(UiTypographyClass.TextBold));
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--mantine-color-white"), glyph.resolvedStyle.color, "glyph");
            Assert.AreEqual(0f, glyph.resolvedStyle.unityTextOutlineWidth, 0.01f);
            Assert.Less(slot.IndexOf(stroke), slot.IndexOf(glyph));
            Assert.AreEqual("500", stroke.text);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--icon-text-stroke-width"), stroke.resolvedStyle.unityTextOutlineWidth, 0.01f);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--icon-text-stroke-dark"), stroke.resolvedStyle.unityTextOutlineColor, "stroke outline");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--icon-text-stroke-dark"), stroke.resolvedStyle.color, "stroke fill");
        }

        [Test]
        public void 量は右2pxに置き基線をWebの本文の行高にそろえる()
        {
            var slot = Mount(FluidSlotContent.Filled(SlotTestIcons.WithTexture("10", "Water"), 500, 1000, Color.blue));

            // translate は描く位置だけを下げるので worldBound で見る。枠線 1px の内側が Web の bottom: 0・right: 2px の基準
            // translate only moves the drawn position, so check worldBound; the inside of the 1px border is the Web bottom: 0 / right: 2px origin
            var glyph = Layer(slot, "outlined-count-label--glyph");
            var bottom = ThemeTokenFile.ResolvePixels("--fluid-slot-amount-bottom");
            Assert.AreEqual(slot.worldBound.xMax - 1f - 2f, glyph.worldBound.xMax, 0.5f);
            Assert.AreEqual(slot.worldBound.yMax - 1f - bottom + 12f * 0.181f, glyph.worldBound.yMax, 0.5f);
            Assert.AreEqual(glyph.worldBound, Layer(slot, "outlined-count-label--stroke").worldBound);
        }

        [Test]
        public void ゴーストは枠と面を残し絵と量だけを透かす()
        {
            var slot = Mount(FluidSlotContent.Ghost(SlotTestIcons.WithTexture("10", "Water"), 2));

            var opacity = float.Parse(ThemeTokenFile.Resolve("--fluid-slot-ghost-opacity"), System.Globalization.CultureInfo.InvariantCulture);
            Assert.AreEqual(1f, slot.resolvedStyle.opacity, 0.001f);
            Assert.AreEqual(opacity, slot.Q(className: "fluid-slot__icon").resolvedStyle.opacity, 0.001f);
            Assert.AreEqual(opacity, Layer(slot, "outlined-count-label--glyph").resolvedStyle.opacity, 0.001f);
            Assert.AreEqual(opacity, Layer(slot, "outlined-count-label--stroke").resolvedStyle.opacity, 0.001f);
        }

        private FluidSlot Mount(FluidSlotContent content)
        {
            var slot = new FluidSlot("fluid-slot");
            rootVisualElement.Add(slot);
            slot.SetFluid(content);
            simulate.FrameUpdate();
            return slot;
        }

        private static Label Layer(VisualElement slot, string layerClass)
        {
            return slot.Q<Label>(null, "fluid-slot__amount", layerClass);
        }
    }
}
