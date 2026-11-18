using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Icons;
using Client.Ui.Foundation.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Slots
{
    /// <summary>
    /// SlotContent のアイコンの余白と個数の位置・書体を既定とつまみの上書きで確かめる（L1）
    /// Verifies SlotContent icon padding and count placement and type with defaults and knob overrides (L1)
    /// </summary>
    public class SlotContentTest : UiTestHost
    {
        private const string KnobPath = "Assets/Scripts/Client.Tests/Ui/Components/Slots/SlotKnobOverrides.uss";

        [TearDown]
        public void NoUnexpectedLogs()
        {
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void アイコンは枠いっぱいでicon_padの余白を取る()
        {
            var (frame, content) = Mount(null);
            content.SetIcon(SlotTestIcons.WithTexture("1", "Iron"), GameIconFallback.IdText);
            simulate.FrameUpdate();

            var icon = content.Q(className: "slot-content__icon");
            Assert.AreEqual(frame.layout.width, icon.layout.width, 0.5f);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--icon-pad"), icon.resolvedStyle.paddingLeft, 0.01f);
            Assert.AreEqual(PickingMode.Ignore, content.pickingMode);
        }

        [Test]
        public void 祖先でicon_padを上書きすると余白が変わる()
        {
            var (_, content) = Mount("slot-knob-recipe-box");
            content.SetIcon(SlotTestIcons.WithTexture("1", "Iron"), GameIconFallback.IdText);
            simulate.FrameUpdate();

            Assert.AreEqual(1f, content.Q(className: "slot-content__icon").resolvedStyle.paddingBottom, 0.01f);
        }

        [Test]
        public void 個数は千区切りでMediumの右下に白い縁で出る()
        {
            var (frame, content) = Mount(null);
            content.SetCount(1500);
            simulate.FrameUpdate();

            var count = content.Q<Label>(null, "slot-content__count", "outlined-count-label--glyph");
            Assert.AreEqual("1,500", count.text);
            Assert.IsTrue(count.ClassListContains(UiTypographyClass.TextMedium));
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--count-font-size"), count.resolvedStyle.fontSize, 0.01f);
            Assert.AreEqual(frame.layout.height - ThemeTokenFile.ResolvePixels("--count-bottom"), count.layout.yMax, 0.5f);
            Assert.AreEqual(frame.layout.width - 2f, count.layout.xMax, 0.5f);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--count-text"), count.resolvedStyle.color, "count");
            Assert.AreEqual(0f, count.resolvedStyle.unityTextOutlineWidth, 0.01f);
        }

        [Test]
        public void 個数はWebのline_height_1と同じ基線になるよう字の大きさの18_1パーセント下げて描く()
        {
            var (frame, content) = Mount(null);
            content.SetCount(7);
            simulate.FrameUpdate();

            // translate はレイアウトを動かさず描く位置だけを下げるので worldBound で見る
            // translate only moves the drawn position, not the layout, so check worldBound
            var count = content.Q<Label>(null, "slot-content__count", "outlined-count-label--glyph");
            var fontSize = ThemeTokenFile.ResolvePixels("--count-font-size");
            Assert.AreEqual(frame.worldBound.yMax - ThemeTokenFile.ResolvePixels("--count-bottom") + fontSize * 0.181f, count.worldBound.yMax, 0.5f);
            Assert.AreEqual(count.worldBound, content.Q<Label>(null, "slot-content__count", "outlined-count-label--stroke").worldBound);
        }

        [Test]
        public void 個数の白い縁は字の後ろの縁だけの層が同じ位置で描く()
        {
            var (_, content) = Mount(null);
            content.SetCount(1500);
            simulate.FrameUpdate();

            // 縁の層は字より前の子（先に描かれる）で、字と同じ文字・位置・大きさを持つ
            // The stroke layer is an earlier child (drawn first) with the same text, place and size as the glyph
            var stroke = content.Q<Label>(null, "slot-content__count", "outlined-count-label--stroke");
            var count = content.Q<Label>(null, "slot-content__count", "outlined-count-label--glyph");
            Assert.Less(content.IndexOf(stroke), content.IndexOf(count));
            Assert.AreEqual("1,500", stroke.text);
            Assert.AreEqual(count.layout, stroke.layout);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--icon-text-stroke-width"), stroke.resolvedStyle.unityTextOutlineWidth, 0.01f);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--icon-text-stroke-light"), stroke.resolvedStyle.unityTextOutlineColor, "stroke outline");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--icon-text-stroke-light"), stroke.resolvedStyle.color, "stroke fill");
        }

        [Test]
        public void 祖先のつまみで個数の位置と大きさと字間が変わる()
        {
            var (frame, content) = Mount("slot-knob-item-list");
            content.SetCount(12);
            simulate.FrameUpdate();

            var count = content.Q<Label>(null, "slot-content__count", "outlined-count-label--glyph");
            Assert.AreEqual(46.144f, frame.layout.width, 0.5f);
            Assert.AreEqual(16f, count.resolvedStyle.fontSize, 0.01f);
            Assert.AreEqual(frame.layout.height + 1f, count.layout.yMax, 0.5f);
            Assert.AreEqual(12f, count.resolvedStyle.letterSpacing, 0.01f);
        }

        [Test]
        public void 個数の字は置き場が受け直した末尾の字間のぶんだけ内へ寄る()
        {
            // 既定は 0。Chrome が最後の字の後ろに足す字間（0.12em × 16px）を置き場が受け直すと、右寄せの字の右端がそのぶん左へ動く
            // Default 0; when the placement re-declares the gap Chrome adds after the last glyph (0.12em x 16px), the right-aligned glyphs' right edge moves left by it
            var (_, plain) = Mount(null);
            plain.SetCount(7);
            var (_, placed) = Mount("slot-knob-item-list");
            placed.SetCount(7);
            simulate.FrameUpdate();

            var plainCount = plain.Q<Label>(null, "slot-content__count", "outlined-count-label--glyph");
            var placedCount = placed.Q<Label>(null, "slot-content__count", "outlined-count-label--glyph");
            Assert.AreEqual(0f, plainCount.resolvedStyle.paddingRight, 0.01f);
            Assert.AreEqual(1.92f, placedCount.resolvedStyle.paddingRight, 0.1f);
            Assert.AreEqual(placedCount.layout.width - placedCount.resolvedStyle.paddingRight, placedCount.contentRect.xMax, 0.01f);
            Assert.AreEqual(placedCount.resolvedStyle.paddingRight, placed.Q<Label>(null, "slot-content__count", "outlined-count-label--stroke").resolvedStyle.paddingRight, 0.01f);
        }

        [Test]
        public void 個数を消すと隠れる()
        {
            var (_, content) = Mount(null);
            content.SetCount(3);
            content.ClearCount();
            simulate.FrameUpdate();

            Assert.AreEqual(DisplayStyle.None, content.Q(null, "slot-content__count", "outlined-count-label--glyph").resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.None, content.Q(null, "slot-content__count", "outlined-count-label--stroke").resolvedStyle.display);
        }

        private (SlotFrame frame, SlotContent content) Mount(string knobClass)
        {
            var screen = new VisualElement();
            if (knobClass != null)
            {
                var knobs = AssetDatabase.LoadAssetAtPath<StyleSheet>(KnobPath);
                Assert.IsNotNull(knobs, KnobPath);
                screen.styleSheets.Add(knobs);
                screen.AddToClassList(knobClass);
            }
            var frame = new SlotFrame("slot");
            var content = new SlotContent();
            frame.Add(content);
            screen.Add(frame);
            rootVisualElement.Add(screen);
            simulate.FrameUpdate();
            return (frame, content);
        }
    }
}
