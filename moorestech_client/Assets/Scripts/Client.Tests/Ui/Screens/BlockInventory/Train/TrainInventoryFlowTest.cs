using System.Collections.Generic;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Train.Inventory;
using Core.Master;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Train
{
    public class TrainInventoryFlowTest : BlockInventoryScreenTestHost
    {
        /// <web-test file="e2e/tests/train.spec.ts">貨車スロットとコンテナ不在エラーを表示する</web-test>
        [Test]
        public void 貨車スロットとコンテナ不在エラーを出す()
        {
            // Web のモック trainCargo: 24・8 と空 7 枠
            // Web mock trainCargo: 24, 8 and seven empty slots
            var bodies = new TrainTestBodies(Toaster, BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            var shell = BlockInventoryTestShell.Create(this, bodies.Panel, bodies.TrainBodyFactory(BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content()));
            bodies.Sources.SetBody(new TrainInventoryBodyViewModel(TrainCargoSlots(), new List<BlockFluidSlotState>()));
            simulate.FrameUpdate();
            StringAssert.Contains("24", TrainTestText.TextsUnder(shell.View.Q("train-inventory-slots-slot-0")));
            StringAssert.Contains("8", TrainTestText.TextsUnder(shell.View.Q("train-inventory-slots-slot-1")));
            Assert.IsNotNull(shell.View.Q("train-inventory-slots-slot-8"));

            // コンテナ不在では alpha のエラー行が出て、本文は作られない
            // With the container missing, alpha's error row shows and no body is built
            bodies.Panel.Value = BlockInventoryTestPanels.Train(BlockInventoryPanelError.ContainerMissing);
            simulate.FrameUpdate();
            Assert.IsTrue(IsShown(shell.View.Q("train-inventory-error")));
            Assert.IsNull(shell.View.Q("train-inventory-slots"));
        }

        private static SlotItem[] TrainCargoSlots()
        {
            var slots = new SlotItem[9];
            slots[0] = new SlotItem(new ItemId(1), 24);
            slots[1] = new SlotItem(new ItemId(2), 8);
            for (var i = 2; i < slots.Length; i++) slots[i] = new SlotItem(ItemMaster.EmptyItemId, 0);
            return slots;
        }
    }
}
