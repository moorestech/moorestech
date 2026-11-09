using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Slots;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Slots
{
    /// <summary>
    /// SlotFrame のベベルの帯が倍率ごとに物理画素へ揃い、実行中の倍率変更でも組み直されることを確かめる（L1）
    /// Verifies SlotFrame bevel bands snap to physical pixels per scale and rebuild when the scale changes at runtime (L1)
    /// </summary>
    public class SlotFrameRingScaleTest : UiTestHost
    {
        [TearDown]
        public void RestoreScaleAndCheckLogs()
        {
            pixelsPerPoint = 1f;
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void パネルの倍率だけを変えると帯は新しい倍率の物理画素で組み直される()
        {
            var frame = new SlotFrame("slot");
            frame.SetState(new SlotFrameState(false, false, false, false, false));
            rootVisualElement.Add(frame);
            simulate.FrameUpdate();

            // 倍率 1 と 1.5 は c1・c2 とも 1 物理画素
            // At scales 1 and 1.5 both c1 and c2 are one physical pixel
            AssertBandPhysicalPixels(frame, 1f, 1, 1);
            AssertBandPhysicalPixels(frame, 1.5f, 1, 1);

            // 倍率 2 は同じ幅の c1・c2 が同じ 2 物理画素になる（外縁の丸めでは 2+1 に崩れていた）
            // At scale 2 the equal c1 and c2 both become two physical pixels (outer-edge rounding gave 2+1)
            AssertBandPhysicalPixels(frame, 2f, 2, 2);

            // 倍率を戻すと元の形に戻る
            // Restoring the scale restores the original shape
            AssertBandPhysicalPixels(frame, 1f, 1, 1);
        }

        private void AssertBandPhysicalPixels(SlotFrame frame, float scale, int expectedC1, int expectedC2)
        {
            // 論理寸法は変えず倍率だけを変える
            // Change only the scale, keeping the logical size
            pixelsPerPoint = scale;
            simulate.FrameUpdate();
            Assert.AreEqual(scale, frame.scaledPixelsPerPoint, 0.001f, "panel scale");

            var band1 = frame.Q(className: "slot-frame__band--1");
            var band2 = frame.Q(className: "slot-frame__band--2");
            var band3 = frame.Q(className: "slot-frame__band--3");
            Assert.AreEqual(expectedC1, band1.style.borderTopWidth.value * scale, 0.001f, $"c1 at scale {scale}");
            Assert.AreEqual(expectedC2, band2.style.borderTopWidth.value * scale, 0.001f, $"c2 at scale {scale}");
            Assert.AreEqual(-(expectedC1 + expectedC2), band2.style.left.value.value * scale, 0.001f, $"c2 outer edge at scale {scale}");
            Assert.AreEqual(0f, band3.style.borderTopWidth.value, 0.001f, $"c3 at scale {scale}");
        }
    }
}
