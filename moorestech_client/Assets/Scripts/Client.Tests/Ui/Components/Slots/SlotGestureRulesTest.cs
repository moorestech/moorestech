using System.Linq;
using Client.Ui.Components.Slots;
using NUnit.Framework;

namespace Client.Tests.Ui.Components.Slots
{
    /// <summary>
    /// SlotGestureRules がボタンを Web の useSlotMouse と同じ身振りへ訳すことを確かめる（L0）
    /// Verifies SlotGestureRules maps buttons to the same gestures as the Web useSlotMouse (L0)
    /// </summary>
    public class SlotGestureRulesTest
    {
        /// <web-test file="src/shared/ui/useSlotMouse.test.ts">左右押下とコンテキストメニューを共通契約どおり処理する</web-test>
        [Test]
        public void 左押下はShiftを運び右押下はShiftを捨て中ボタンは身振りにしない()
        {
            Assert.IsTrue(SlotGestureRules.TryFromPointerDown(0, true, out var left));
            Assert.AreEqual(SlotGestureKind.LeftDown, left.Kind);
            Assert.IsTrue(left.Shift);

            Assert.IsTrue(SlotGestureRules.TryFromPointerDown(1, true, out var right));
            Assert.AreEqual(SlotGestureKind.RightDown, right.Kind);
            Assert.IsFalse(right.Shift);

            Assert.IsFalse(SlotGestureRules.TryFromPointerDown(2, false, out _));
        }

        /// <web-test file="src/shared/ui/useSlotMouse.test.ts">右ボタンを押したまま進入した時だけ右ドラッグ処理を呼ぶ</web-test>
        [Test]
        public void 右ボタンを押したまま進入した時だけ右の進入を出す()
        {
            CollectionAssert.AreEqual(new[] { SlotGestureKind.RightEnter }, SlotGestureRules.FromPointerEnter(2).Select(g => g.Kind));
            CollectionAssert.IsEmpty(SlotGestureRules.FromPointerEnter(0));
        }

        [Test]
        public void 左右を押したまま進入すると左右の順に出す()
        {
            CollectionAssert.AreEqual(new[] { SlotGestureKind.LeftEnter }, SlotGestureRules.FromPointerEnter(1).Select(g => g.Kind));
            CollectionAssert.AreEqual(new[] { SlotGestureKind.LeftEnter, SlotGestureKind.RightEnter }, SlotGestureRules.FromPointerEnter(3).Select(g => g.Kind));
        }

        [Test]
        public void 左の2回目のクリックだけをダブルクリックにする()
        {
            Assert.IsTrue(SlotGestureRules.IsDoubleClick(0, 2));
            Assert.IsFalse(SlotGestureRules.IsDoubleClick(0, 1));
            Assert.IsFalse(SlotGestureRules.IsDoubleClick(0, 3));
            Assert.IsFalse(SlotGestureRules.IsDoubleClick(1, 2));
        }
    }
}
