using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Tooltip;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Slots.Fluid
{
    /// <summary>
    /// FluidAmountSlot の白面・絵・量の帯・ツールチップと、画面のつまみに負けない量の字を確かめる（L1）
    /// Verifies FluidAmountSlot white face, picture, amount band, tooltip and an amount face screens cannot override (L1)
    /// </summary>
    public class FluidAmountSlotTest : UiTestHost
    {
        private const string KnobPath = "Assets/Scripts/Client.Tests/Ui/Components/Slots/Fluid/FluidKnobOverrides.uss";

        [TearDown]
        public void NoUnexpectedLogs()
        {
            LogAssert.NoUnexpectedReceived();
        }

        /// <web-test file="src/shared/ui/FluidAmountSlot/index.test.ts">白面のSlotFrameに寸法クラス付きの液体アイコンを描く</web-test>
        [Test]
        public void 白面の枠に余白付きの液体の絵を描く()
        {
            var (slot, frame) = Mount(null, SlotTestIcons.WithTexture("10", "Oil"), SlotAmountBadge.Show(10));

            Assert.AreEqual("fluid-amount", frame.name);
            Assert.IsTrue(frame.ClassListContains("slot-frame--filled"));
            Assert.IsNotNull(slot.Q(className: "slot-content__icon"));
            Assert.AreEqual(DisplayStyle.Flex, slot.Q(className: "game-icon__image").resolvedStyle.display);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--icon-pad"), slot.Q(className: "slot-content__icon").resolvedStyle.paddingLeft, 0.01f);
        }

        /// <web-test file="src/shared/ui/FluidAmountSlot/index.test.ts">量はN0形式のバッジで右下に出し、アイコン文字の白縁クラスを合成する</web-test>
        /// <web-test file="src/app/iconTextOutlineDesign.test.ts">黒文字が白縁の共有クラスを持ち、擬似縁を残さない</web-test>
        [Test]
        public void 量は桁区切りで枠幅の右寄せの帯に黒字と白縁で出す()
        {
            var (slot, frame) = Mount(null, SlotTestIcons.WithTexture("10", "Oil"), SlotAmountBadge.Show(1000));

            var band = slot.Q("fluid-amount-amount");
            var glyph = Layer(band, "outlined-count-label--glyph");
            var stroke = Layer(band, "outlined-count-label--stroke");
            Assert.AreEqual("1,000", glyph.text);
            Assert.AreEqual(frame.worldBound.xMax - 2f, band.worldBound.xMax, 0.5f);
            Assert.AreEqual(frame.worldBound.xMin, band.worldBound.xMin, 0.5f);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--fluid-amount-font-size"), glyph.resolvedStyle.fontSize, 0.01f);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--count-text"), glyph.resolvedStyle.color, "glyph");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--icon-text-stroke-light"), stroke.resolvedStyle.unityTextOutlineColor, "stroke");
            Assert.Less(band.IndexOf(stroke), band.IndexOf(glyph));
        }

        /// <web-test file="src/shared/ui/FluidAmountSlot/index.test.ts">量0はバッジを出して0と描く</web-test>
        [Test]
        public void 量0も帯を出して0と描く()
        {
            var (slot, _) = Mount(null, SlotTestIcons.WithTexture("10", "Oil"), SlotAmountBadge.Show(0));

            Assert.AreEqual("0", Layer(slot.Q("fluid-amount-amount"), "outlined-count-label--glyph").text);
        }

        /// <web-test file="src/shared/ui/FluidAmountSlot/index.test.ts">バッジ非表示なら量があってもバッジを出さない</web-test>
        [Test]
        public void 量を出さない指定なら帯を木から外し出していた帯も消す()
        {
            var (slot, _) = Mount(null, SlotTestIcons.WithTexture("10", "Oil"), SlotAmountBadge.Show(10));
            slot.SetFluid(SlotTestIcons.WithTexture("10", "Oil"), SlotAmountBadge.Hidden);

            Assert.IsNull(slot.Q("fluid-amount-amount"));
            slot.SetFluid(SlotTestIcons.WithTexture("10", "Oil"), SlotAmountBadge.Show(5));
            Assert.AreEqual("5", Layer(slot.Q("fluid-amount-amount"), "outlined-count-label--glyph").text);
        }

        /// <web-test file="src/shared/ui/FluidAmountSlot/index.test.ts">ホバーツールチップに辞書の液体名を出す</web-test>
        [Test]
        public void ツールチップの印は枠に付き液体名を持つ()
        {
            var icon = SlotTestIcons.WithTexture("10", "Oil");
            var (slot, frame) = Mount(null, icon, SlotAmountBadge.Show(10));

            Assert.IsTrue(HoverTooltipMark.TryFindMarked(slot.Q(className: "game-icon__image"), out var marked, out var text));
            Assert.AreSame(frame, marked);
            Assert.AreEqual("Oil", text.Resolve());
        }

        [Test]
        public void 絵が欠けたら液体名の文字を出す()
        {
            var (slot, _) = Mount(null, SlotTestIcons.Missing("10", "Oil"), SlotAmountBadge.Show(10));

            Assert.AreEqual("Oil", slot.Q<Label>(className: "game-icon__fallback").text);
        }

        [Test]
        public void 画面が個数のつまみを上書きしても量の字の大きさと字間0は変わらず下端だけ追う()
        {
            var (slot, frame) = Mount("fluid-knob-recipe-row", SlotTestIcons.WithTexture("10", "Oil"), SlotAmountBadge.Show(10));

            var band = slot.Q("fluid-amount-amount");
            var glyph = Layer(band, "outlined-count-label--glyph");
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--fluid-amount-font-size"), glyph.resolvedStyle.fontSize, 0.01f);
            Assert.AreEqual(0f, glyph.resolvedStyle.letterSpacing, 0.01f);
            Assert.AreEqual(1f, slot.Q(className: "slot-content__icon").resolvedStyle.paddingLeft, 0.01f);
            Assert.AreEqual(frame.worldBound.yMax + 1f, band.worldBound.yMax, 0.5f);
        }

        private (FluidAmountSlot slot, VisualElement frame) Mount(string knobClass, Client.Ui.Foundation.Icons.UiIcon icon, SlotAmountBadge badge)
        {
            var screen = new VisualElement();
            if (knobClass != null)
            {
                var knobs = AssetDatabase.LoadAssetAtPath<StyleSheet>(KnobPath);
                Assert.IsNotNull(knobs, KnobPath);
                screen.styleSheets.Add(knobs);
                screen.AddToClassList(knobClass);
            }
            var slot = new FluidAmountSlot("fluid-amount");
            slot.SetFluid(icon, badge);
            screen.Add(slot);
            rootVisualElement.Add(screen);
            simulate.FrameUpdate();
            return (slot, slot.Q("fluid-amount"));
        }

        private static Label Layer(VisualElement band, string layerClass)
        {
            return band.Q<Label>(null, "fluid-amount-slot__amount-layer", layerClass);
        }
    }
}
