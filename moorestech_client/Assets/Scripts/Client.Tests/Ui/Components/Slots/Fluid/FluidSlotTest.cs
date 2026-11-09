using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Tooltip;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Slots.Fluid
{
    /// <summary>
    /// FluidSlot の種類別の描画を確かめる（L1）
    /// Verifies FluidSlot shows fill, picture, amount and tooltip per content kind (L1)
    /// </summary>
    public class FluidSlotTest : UiTestHost
    {
        private static readonly Color WaterFace = new Color32(0x2A, 0x6F, 0xE0, 0xFF);

        [TearDown]
        public void NoUnexpectedLogs()
        {
            LogAssert.NoUnexpectedReceived();
        }

        /// <web-test file="src/shared/ui/FluidSlot/FluidSlot.icon.test.ts">満タンでない液体スロットはフィルとアイコンの両方を描く</web-test>
        [Test]
        public void 満タンでない液体は量の割合の高さで液体の色のフィルと絵を描く()
        {
            var slot = Mount();
            slot.SetFluid(FluidSlotContent.Filled(SlotTestIcons.WithTexture("10", "Water"), 500, 1000, WaterFace));
            simulate.FrameUpdate();

            // Web の % は枠線の内側（48 - 1 × 2）が基準
            // The Web percentage is relative to the inside of the 1px border (48 - 1 x 2)
            var fill = slot.Q(className: "fluid-slot__fill");
            Assert.AreEqual(DisplayStyle.Flex, fill.resolvedStyle.display);
            Assert.AreEqual((48f - 2f) * 0.5f, fill.layout.height, 0.5f);
            Assert.AreEqual(slot.worldBound.yMax - 1f, fill.worldBound.yMax, 0.5f);
            ThemeTokenFile.AssertSameColor(WaterFace, fill.resolvedStyle.backgroundColor, "fill");
            Assert.AreEqual(DisplayStyle.Flex, slot.Q(className: "game-icon__image").resolvedStyle.display);
        }

        /// <web-test file="src/shared/ui/FluidSlot/FluidSlot.icon.test.ts">空スロット(kind: empty)はフィルもアイコンも描かない</web-test>
        [Test]
        public void 空のタンクはフィルも絵も量も描かない()
        {
            var slot = Mount();
            slot.SetFluid(FluidSlotContent.Filled(SlotTestIcons.WithTexture("10", "Water"), 500, 1000, WaterFace));
            slot.SetFluid(FluidSlotContent.Empty);
            simulate.FrameUpdate();

            Assert.AreEqual(DisplayStyle.None, slot.Q(className: "fluid-slot__fill").resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.None, slot.Q(className: "game-icon__image").resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.None, Glyph(slot).resolvedStyle.display);
        }

        /// <web-test file="src/shared/ui/FluidSlot/FluidSlot.icon.test.ts">アイコン読み込み失敗後はimgもidテキストも出さずフィルだけ残る</web-test>
        [Test]
        public void 絵が欠けても代わりの文字を出さずフィルと量だけが残る()
        {
            var slot = Mount();
            slot.SetFluid(FluidSlotContent.Filled(SlotTestIcons.Missing("10", "Water"), 500, 1000, WaterFace));
            simulate.FrameUpdate();

            Assert.AreEqual(DisplayStyle.None, slot.Q(className: "game-icon__image").resolvedStyle.display);
            Assert.IsNull(slot.Q(className: "game-icon__fallback"));
            Assert.AreEqual(DisplayStyle.Flex, slot.Q(className: "fluid-slot__fill").resolvedStyle.display);
            Assert.AreEqual("500", Glyph(slot).text);
        }

        /// <web-test file="src/shared/ui/FluidSlot/FluidSlot.localization.test.ts">空スロット(kind: empty)はTooltip自体を描かない</web-test>
        [Test]
        public void 空のタンクはツールチップの印を持たず中身が入ると名前の印が付く()
        {
            var slot = Mount();
            slot.SetFluid(FluidSlotContent.Empty);
            Assert.IsFalse(HoverTooltipMark.TryFindMarked(slot.Q(className: "fluid-slot__icon"), out _, out _));

            var icon = SlotTestIcons.WithTexture("10", "Water");
            slot.SetFluid(FluidSlotContent.Filled(icon, 500, 1000, WaterFace));
            Assert.IsTrue(HoverTooltipMark.TryFindMarked(slot.Q(className: "fluid-slot__icon"), out var marked, out var text));
            Assert.AreSame(slot, marked);
            Assert.AreSame(icon.Label, text);

            slot.SetFluid(FluidSlotContent.Empty);
            Assert.IsFalse(HoverTooltipMark.TryFindMarked(slot.Q(className: "fluid-slot__icon"), out _, out _));
        }

        /// <web-test file="src/shared/ui/FluidSlot/FluidSlot.localization.test.ts">長い小数の残量は表示桁で止めて描く</web-test>
        [Test]
        public void 長い小数の残量は表示桁で止めて描く()
        {
            var slot = Mount();
            slot.SetFluid(FluidSlotContent.Filled(SlotTestIcons.WithTexture("10", "Water"), 823.3333333333334, 1000, WaterFace));
            simulate.FrameUpdate();

            Assert.AreEqual("823.33", Glyph(slot).text);
        }

        [Test]
        public void ゴーストはフィル無しで絵と必要量を出し名前の印を付ける()
        {
            var slot = Mount();
            var icon = SlotTestIcons.WithTexture("10", "Water");
            slot.SetFluid(FluidSlotContent.Ghost(icon, 2));
            simulate.FrameUpdate();

            Assert.IsTrue(slot.ClassListContains("fluid-slot--ghost"));
            Assert.AreEqual(DisplayStyle.None, slot.Q(className: "fluid-slot__fill").resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.Flex, slot.Q(className: "game-icon__image").resolvedStyle.display);
            Assert.AreEqual("2", Glyph(slot).text);
            Assert.IsTrue(HoverTooltipMark.TryFindMarked(slot.Q(className: "fluid-slot__icon"), out _, out var text));
            Assert.AreSame(icon.Label, text);
        }

        [Test]
        public void 同じスロットで中身からゴーストへ変わるとフィルが消える()
        {
            var slot = Mount();
            var icon = SlotTestIcons.WithTexture("10", "Water");
            slot.SetFluid(FluidSlotContent.Filled(icon, 500, 1000, WaterFace));
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.Flex, slot.Q(className: "fluid-slot__fill").resolvedStyle.display);

            slot.SetFluid(FluidSlotContent.Ghost(icon, 2));
            simulate.FrameUpdate();

            Assert.IsTrue(slot.ClassListContains("fluid-slot--ghost"));
            Assert.AreEqual(DisplayStyle.None, slot.Q(className: "fluid-slot__fill").resolvedStyle.display);
            Assert.AreEqual("2", Glyph(slot).text);
        }

        [Test]
        public void 同じスロットでゴーストから中身へ変わると透けが外れ絵の不透明度が1に戻る()
        {
            var slot = Mount();
            var icon = SlotTestIcons.WithTexture("10", "Water");
            slot.SetFluid(FluidSlotContent.Ghost(icon, 2));
            simulate.FrameUpdate();
            Assert.Less(slot.Q(className: "fluid-slot__icon").resolvedStyle.opacity, 1f);

            slot.SetFluid(FluidSlotContent.Filled(icon, 500, 1000, WaterFace));
            simulate.FrameUpdate();

            Assert.IsFalse(slot.ClassListContains("fluid-slot--ghost"));
            Assert.AreEqual(1f, slot.Q(className: "fluid-slot__icon").resolvedStyle.opacity, 0.001f);
            Assert.AreEqual(DisplayStyle.Flex, slot.Q(className: "fluid-slot__fill").resolvedStyle.display);
        }

        [Test]
        public void 子は全部ポインタを受けずルートだけが受ける()
        {
            var slot = Mount();
            slot.SetFluid(FluidSlotContent.Filled(SlotTestIcons.WithTexture("10", "Water"), 500, 1000, WaterFace));

            Assert.AreEqual("fluid-slot", slot.name);
            Assert.AreEqual(PickingMode.Position, slot.pickingMode);
            foreach (var child in slot.Children()) Assert.AreEqual(PickingMode.Ignore, child.pickingMode, string.Join(" ", child.GetClasses()));
        }

        private FluidSlot Mount()
        {
            var slot = new FluidSlot("fluid-slot");
            rootVisualElement.Add(slot);
            simulate.FrameUpdate();
            return slot;
        }

        private static Label Glyph(VisualElement slot)
        {
            return slot.Q<Label>(null, "fluid-slot__amount", "outlined-count-label--glyph");
        }
    }
}
