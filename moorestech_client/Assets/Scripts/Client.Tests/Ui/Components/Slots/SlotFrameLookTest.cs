using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Slots;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Slots
{
    /// <summary>
    /// SlotFrame の面・ベベル・選択・減光を既定と祖先の上書きで確かめる（L1）
    /// Verifies SlotFrame face, bevel, selection and dimming with defaults and ancestor overrides (L1)
    /// </summary>
    public class SlotFrameLookTest : UiTestHost
    {
        private const string KnobPath = "Assets/Scripts/Client.Tests/Ui/Components/Slots/SlotKnobOverrides.uss";

        [TearDown]
        public void NoUnexpectedLogs()
        {
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 既定は半透明の暗い面でベベルは箱の外へ育ちc1が最前()
        {
            var frame = Mount(null, new SlotFrameState(false, false, false, false, false));

            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--slot-size"), frame.layout.width, 0.01f);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--slot-frame-face"), frame.resolvedStyle.backgroundColor, "face");
            var ring = frame.Q(className: "slot-frame__ring");
            var band1 = ring.Q(className: "slot-frame__band--1");
            var band2 = ring.Q(className: "slot-frame__band--2");
            var band3 = ring.Q(className: "slot-frame__band--3");
            Assert.Less(ring.IndexOf(band3), ring.IndexOf(band2));
            Assert.Less(ring.IndexOf(band2), ring.IndexOf(band1));
            Assert.Less(ring.IndexOf(band1), ring.IndexOf(ring.Q(className: "slot-frame__face-inset")));
            // 帯は物理画素の整数倍に丸めて外へ積む。後の層は前の層に隠れない輪だけを描く（c3 は c2 と同じ幅なので見える輪が無い）
            // Bands snap to whole physical pixels and stack outward; later layers draw only the ring earlier ones leave visible (c3 matches c2, so none)
            var visible1 = SnappedVisible(frame, "--bevel-1", null);
            var visible2 = SnappedVisible(frame, "--bevel-2", "--bevel-1");
            Assert.AreEqual(-visible1, band1.style.left.value.value, 0.001f);
            Assert.AreEqual(visible1, band1.style.borderTopWidth.value, 0.001f);
            Assert.AreEqual(-(visible1 + visible2), band2.style.bottom.value.value, 0.001f);
            Assert.AreEqual(visible2, band2.style.borderTopWidth.value, 0.001f);
            Assert.AreEqual(0f, band3.style.borderTopWidth.value, 0.001f);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--bevel-c1"), band1.resolvedStyle.borderTopColor, "c1");
            Assert.AreEqual(PickingMode.Ignore, ring.pickingMode);
        }

        [Test]
        public void 祖先でbevel_insetをinsetにすると帯は箱の内側に収まる()
        {
            // 設計書 A-42 の実測: キーワードのつまみを CustomStyleProperty<string> で読めるか
            // Measurement for design A-42: whether a keyword knob reads through CustomStyleProperty<string>
            var frame = Mount("slot-knob-hotbar", new SlotFrameState(false, false, false, false, false));

            // レイアウトは倍率 1 で画素へ丸まる（51.68 が 52）ので 0.5px まで許す
            // Layout rounds to pixels at scale 1 (51.68 becomes 52), so allow 0.5px
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--hotbar-slot-size"), frame.layout.width, 0.5f);
            var band1 = frame.Q(className: "slot-frame__band--1");
            var band2 = frame.Q(className: "slot-frame__band--2");
            Assert.AreEqual(0f, band1.style.left.value.value, 0.001f);
            Assert.AreEqual(SnappedVisible(frame, "--bevel-1", null), band2.style.left.value.value, 0.001f);
            Assert.AreEqual(SnappedVisible(frame, "--bevel-2", "--bevel-1"), band2.style.borderLeftWidth.value, 0.001f);
        }

        [Test]
        public void 白面と灰面はcatalogとfilledの重なりで白が勝つ()
        {
            var filled = Mount(null, new SlotFrameState(false, true, false, false, false));
            var catalog = Mount(null, new SlotFrameState(false, false, true, false, false));
            var both = Mount(null, new SlotFrameState(false, true, true, false, false));

            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--slot-frame-filled-face"), filled.resolvedStyle.backgroundColor, "filled");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--slot-frame-catalog-face"), catalog.resolvedStyle.backgroundColor, "catalog");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--slot-frame-filled-face"), both.resolvedStyle.backgroundColor, "catalog+filled");
        }

        [Test]
        public void 選択でベベルがシアンの3色になり祖先のsel_cで上書きできる()
        {
            var selected = Mount(null, new SlotFrameState(true, false, false, false, false));
            var hotbar = Mount("slot-knob-hotbar", new SlotFrameState(true, false, false, false, false));

            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--sel-c1"), selected.Q(className: "slot-frame__band--1").resolvedStyle.borderTopColor, "sel-c1");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--sel-c3"), selected.Q(className: "slot-frame__band--3").resolvedStyle.borderTopColor, "sel-c3");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--select-cyan"), hotbar.Q(className: "slot-frame__band--1").resolvedStyle.borderTopColor, "hotbar sel-c1");
        }

        [Test]
        public void 選択の白面だけ下端の帯をつまみの位置と高さで出す()
        {
            var selectedEmpty = Mount("slot-knob-hotbar", new SlotFrameState(true, false, false, false, false));
            var selectedFilled = Mount("slot-knob-hotbar", new SlotFrameState(true, true, false, false, false));

            Assert.AreEqual(DisplayStyle.None, selectedEmpty.Q(className: "slot-frame__selected-trim").resolvedStyle.display);
            var trim = selectedFilled.Q(className: "slot-frame__selected-trim");
            Assert.AreEqual(DisplayStyle.Flex, trim.resolvedStyle.display);
            // 位置と高さは画素へ丸まる（0.78 が 1）。枠の高さも丸まるので丸める前のつまみの寸法と比べ 0.5px まで許す
            // Position and height round to pixels (0.78 becomes 1); the frame height rounds too, so compare to the unrounded knob size within 0.5px
            Assert.AreEqual(0.78f, trim.layout.height, 0.5f);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--hotbar-slot-size") - 1.565749f, trim.layout.yMax, 0.5f);
        }

        [Test]
        public void 不足は枠ごと減光しゴーストは中身だけ透かす()
        {
            var insufficient = Mount(null, new SlotFrameState(false, false, false, true, false));
            var ghost = Mount(null, new SlotFrameState(false, false, false, false, true));

            // 値は Web の 0.4・0.35 そのままのトークン
            // The values are the tokens holding the Web 0.4 and 0.35 as is
            Assert.AreEqual(float.Parse(ThemeTokenFile.Resolve("--slot-frame-insufficient-opacity")), insufficient.resolvedStyle.opacity, 0.001f);
            Assert.AreEqual(1f, ghost.resolvedStyle.opacity, 0.001f);
            Assert.AreEqual(float.Parse(ThemeTokenFile.Resolve("--slot-ghost-opacity")), ghost.Q(className: "slot-frame__content").resolvedStyle.opacity, 0.001f);
        }

        [Test]
        public void filledの面際はつまみで太さと色を変える()
        {
            var frame = Mount("slot-knob-filled-inset", new SlotFrameState(false, true, false, false, false));

            var faceInset = frame.Q(className: "slot-frame__face-inset");
            Assert.AreEqual(2f, faceInset.resolvedStyle.borderTopWidth, 0.01f);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--select-cyan"), faceInset.resolvedStyle.borderTopColor, "face inset");
        }

        [Test]
        public void ベベルの層が1つでも読めなければ帯は前の形のまま残る()
        {
            var frame = Mount(null, new SlotFrameState(false, false, false, false, false));
            var band1 = frame.Q(className: "slot-frame__band--1");
            var leftBefore = band1.style.left.value.value;
            var widthBefore = band1.style.borderTopWidth.value;

            // 1 層目だけ読める値に変わり 3 層目が読めない。読み切る前に書くと 1 層目だけ新しくなる
            // Only layer 1 changes to a readable value while layer 3 is unreadable; writing before reading all would update layer 1 alone
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"--slot-frame-bevel-3 is not a resolved length"));
            frame.parent.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(KnobPath));
            frame.parent.AddToClassList("slot-knob-broken-bevel");
            simulate.FrameUpdate();

            Assert.AreEqual(leftBefore, band1.style.left.value.value, 0.001f);
            Assert.AreEqual(widthBefore, band1.style.borderTopWidth.value, 0.001f);
        }

        private SlotFrame Mount(string knobClass, SlotFrameState state)
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
            frame.SetState(state);
            screen.Add(frame);
            rootVisualElement.Add(screen);
            simulate.FrameUpdate();
            return frame;
        }

        private static float SnappedVisible(VisualElement frame, string bevelToken, string coveredToken)
        {
            // 前の層に隠れない見える幅を物理画素の整数倍（最低 1）に丸めた値
            // The visible width beyond the earlier layer, snapped to whole physical pixels (at least 1)
            var pixelsPerPoint = frame.scaledPixelsPerPoint;
            var covered = coveredToken == null ? 0f : ThemeTokenFile.ResolvePixels(coveredToken);
            return Mathf.Max(1f, Mathf.Round((ThemeTokenFile.ResolvePixels(bevelToken) - covered) * pixelsPerPoint)) / pixelsPerPoint;
        }
    }
}
