using System.Linq;
using Client.Ui.Components.Slots;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Machine;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 機械の枠の押下が型付きコマンドへ渡る
    /// Machine slot presses reach the typed block commands
    /// </summary>
    public class MachineGesturesTest : MachineScreenTestHost
    {
        // 入力 2・出力 1 の機械。出力は統合 index 2
        // A 2-input, 1-output machine; the output is combined index 2
        private VisualElement MountTwoInputs()
        {
            var root = MountSection(MachineScreenViewModels.Filled(2, 0, 0, FirstItem, 5, 1), false).Root;
            simulate.MouseMove(new Vector2(600, 400), Slot(root, "machine-input-slot-0").worldBound.center);
            return root;
        }

        /// <web-test file="e2e/tests/block/machineGestures.spec.ts">機械入力スロットの空手右クリックで block_inventory.split を送る</web-test>
        [Test]
        public void 入力の枠の右押下は入力の番地でRightDownへ渡る()
        {
            var root = MountTwoInputs();
            simulate.Click(Slot(root, "machine-input-slot-0"), MouseButton.RightMouse);
            CollectionAssert.AreEqual(new[] { "RightDown:0" }, Commands.Issued);
        }

        /// <web-test file="e2e/tests/block/machineGestures.spec.ts">機械出力スロットのダブルクリックで collect を送る</web-test>
        [Test]
        public void 出力の枠のダブルクリックは統合の番地でDoubleClickへ渡る()
        {
            var root = MountTwoInputs();
            simulate.DoubleClick(Slot(root, "machine-output-slot-0"));
            Assert.AreEqual(1, Commands.Issued.Count(c => c == "DoubleClick:2"));
        }

        /// <web-test file="e2e/tests/block/machineGestures.spec.ts">機械入力スロットの Shift+クリックで main へ配分移動する</web-test>
        [Test]
        public void 入力の枠のShift押下はShiftつきでLeftDownへ渡る()
        {
            var root = MountTwoInputs();
            simulate.Click(Slot(root, "machine-input-slot-0"), MouseButton.LeftMouse, EventModifiers.Shift);
            CollectionAssert.AreEqual(new[] { "LeftDown:0:True" }, Commands.Issued);
        }

        [Test]
        public void 加工中の状態更新では枠の要素が同一のままダブルクリックが届く()
        {
            // 入力 1・出力 1・モジュール 1
            // 1 input, 1 output, 1 module; keep the drawn slots
            var root = MountSection(MachineScreenViewModels.Filled(1, 1, 0.1f, FirstItem, 3, 1), false).Root;
            var output = Slot(root, "machine-output-slot-0");
            var input = Slot(root, "machine-input-slot-0");
            var module = Slot(root, "machine-module-slot-0");
            Assert.IsNotNull(output);
            Assert.IsNotNull(input);
            Assert.IsNotNull(module);

            // 進捗と個数だけの更新は個数だけ変わる
            // A progress/count-only update changes counts only
            SetMachine(MachineScreenViewModels.Filled(1, 1, 0.6f, FirstItem, 7, 1));
            Assert.AreSame(output, Slot(root, "machine-output-slot-0"));
            Assert.AreSame(input, Slot(root, "machine-input-slot-0"));
            Assert.AreSame(module, Slot(root, "machine-module-slot-0"));
            Assert.AreEqual("7", output.Q<Label>(null, "slot-content__count", "outlined-count-label--glyph").text);

            // 間に状態更新が来てもダブルクリック
            // A state update between clicks still double-clicks
            simulate.MouseMove(new Vector2(600, 400), output.worldBound.center);
            // 更新は Model の差し替えで同期に描かせる（FrameUpdate を挟むと模擬の時計がダブルクリックの間隔を越えると実測した）
            // The update renders synchronously via the model swap (measured: a FrameUpdate in between pushes the simulated clock past the double-click window)
            simulate.Click(output);
            MachineModel.Set(SectionContent<MachineSectionViewModel>.Present(MachineScreenViewModels.Filled(1, 1, 0.7f, FirstItem, 7, 1)));
            simulate.Click(output);
            Assert.AreEqual(1, Commands.Issued.Count(c => c == "DoubleClick:1"));

            // 入力の枠の数が変わると作り直される
            // A changed input slot count rebuilds the slots
            SetMachine(MachineScreenViewModels.Filled(2, 1, 0.7f, FirstItem, 7, 1));
            Assert.AreNotSame(input, Slot(root, "machine-input-slot-0"));
        }

        private static SlotFrame Slot(VisualElement root, string testid)
        {
            return root.Q<SlotFrame>(testid);
        }
    }
}
