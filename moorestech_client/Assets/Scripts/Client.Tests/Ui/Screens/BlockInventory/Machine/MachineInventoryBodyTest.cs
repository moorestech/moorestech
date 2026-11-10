using Client.Ui.Components.Slots;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 機械の素材の枠の所持/必要と、数字の上の押下（View＋Presenter を偽の Model で組む）
    /// Held/required on machine input slots and presses over the numbers (view and presenter on a fake model)
    /// </summary>
    public class MachineInventoryBodyTest : MachineScreenTestHost
    {
        /// <web-test file="e2e/tests/block/machineRequiredCount.spec.ts">必要数を満たした素材スロットは入っている数/必要数を黒字で出し、個数バッジを重ねない</web-test>
        [Test]
        public void 必要数を満たした素材は所持と必要を黒字で個数バッジなし()
        {
            // 素材 1 に必要 2 の束縛、枠 0 に 3 個
            // Input binding needs 2 of the item; slot 0 holds 3
            var root = MountSection(MachineScreenViewModels.WithInput(FirstItem, 2, 3), false).Root;
            var slot = Find<SlotFrame>(root, "machine-input-slot-0");

            Assert.AreEqual("3/2", ShortageText(slot));
            Assert.IsFalse(IsShortageLacking(slot));
            Assert.IsNull(CountBadge(slot));
        }

        /// <web-test file="e2e/tests/block/machineRequiredCount.spec.ts">不足している素材スロットは数字だけ赤くし、入れたアイテムは減光しない</web-test>
        [Test]
        public void 不足の素材は数字だけ赤くしアイコンは減光しない()
        {
            var root = MountSection(MachineScreenViewModels.WithInput(FirstItem, 3, 1), false).Root;
            var slot = Find<SlotFrame>(root, "machine-input-slot-0");

            Assert.AreEqual("1/3", ShortageText(slot));
            Assert.IsTrue(IsShortageLacking(slot));
            Assert.IsFalse(slot.ClassListContains("slot-frame--insufficient"));
            Assert.IsTrue(slot.ClassListContains("slot-frame--filled"));
            Assert.AreEqual(1f, slot.resolvedStyle.opacity);

            // 数字は枠内に収まる
            // The numbers stay inside the slot frame
            var text = ShortageLabel(slot).worldBound;
            Assert.GreaterOrEqual(text.xMin, slot.worldBound.xMin - 0.5f);
            Assert.LessOrEqual(text.xMax, slot.worldBound.xMax + 0.5f);
        }

        /// <web-test file="e2e/tests/block/machineRequiredCount.spec.ts">出力スロットには実物が入っていても必要数を出さない</web-test>
        [Test]
        public void 出力の枠は実物があっても必要数を出さない()
        {
            var root = MountSection(MachineScreenViewModels.WithFilledOutput(FirstItem, 5), false).Root;
            var slot = Find<SlotFrame>(root, "machine-output-slot-0");

            Assert.AreEqual("5", CountBadge(slot));
            Assert.IsNull(ShortageText(slot));
        }

        /// <web-test file="e2e/tests/block/machineRequiredCount.spec.ts">必要数の数字の上で右クリックしても split を送る</web-test>
        [Test]
        public void 必要数の数字の上で右押下しても枠の右押下になる()
        {
            var root = MountSection(MachineScreenViewModels.WithInput(FirstItem, 2, 3), false).Root;
            var slot = Find<SlotFrame>(root, "machine-input-slot-0");
            var center = ShortageLabel(slot).worldBound.center;

            // 実ゲームと同じく押す前にポインタを舞台へ入れる
            // Move the pointer into the stage before pressing, as in the game
            simulate.MouseMove(new Vector2(600, 400), center);
            simulate.MouseDown(center, MouseButton.RightMouse);
            simulate.MouseUp(center, MouseButton.RightMouse);

            CollectionAssert.AreEqual(new[] { "RightDown:0" }, Commands.Issued);
        }

        // 所持/必要と個数バッジは ItemSlot（枠の親）が持つ。C3-a ItemSlotShortageTest と同じ引き方
        // Held/required and the count badge live on ItemSlot (the frame's parent); same lookup as C3-a ItemSlotShortageTest
        private static Label ShortageLabel(SlotFrame frame)
        {
            return frame.parent.Q<Label>(null, "item-slot__shortage", "outlined-count-label--glyph");
        }

        private static string ShortageText(SlotFrame frame)
        {
            var label = ShortageLabel(frame);
            return label.resolvedStyle.display == DisplayStyle.None || !frame.parent.ClassListContains("item-slot--shortage") ? null : label.text;
        }

        private static bool IsShortageLacking(SlotFrame frame)
        {
            return frame.parent.ClassListContains("item-slot--lack");
        }

        private static string CountBadge(SlotFrame frame)
        {
            var badge = frame.Q<Label>(null, "slot-content__count", "outlined-count-label--glyph");
            return badge == null || badge.resolvedStyle.display == DisplayStyle.None ? null : badge.text;
        }
    }
}
