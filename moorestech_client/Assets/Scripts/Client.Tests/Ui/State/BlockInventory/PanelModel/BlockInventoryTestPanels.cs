using System;
using Client.Game.InGame.UI.Inventory.Train;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.PanelModel
{
    /// <summary>
    /// テスト用のパネルの状態の組み立て（マスタ読み込み済みが前提。Screens の L1 でも使う）
    /// Builds panel states for tests (requires the master to be loaded; also used by the Screens L1 tests)
    /// </summary>
    public static class BlockInventoryTestPanels
    {
        // テスト用 mod の最初のチェストの名前（題に出る）
        // Name of the test mod's first chest (shown as the title)
        public const string ChestName = "TestChest";

        public static BlockInventoryPanelState Chest()
        {
            return ChestWithSlots(Array.Empty<SlotItem>());
        }

        public static BlockInventoryPanelState ChestWithSlots(params SlotItem[] slots)
        {
            return BlockInventoryPanelStateFactory.FromBlock(true, slots, FindChest(), new BlockInstanceId(1), new Vector3Int(1, 0, 0));
        }

        public static BlockInventoryPanelState ChestAtPosition(Vector3Int position, params SlotItem[] slots)
        {
            return BlockInventoryPanelStateFactory.FromBlock(true, slots, FindChest(), new BlockInstanceId(2), position);
        }

        public static BlockInventoryPanelState OpenFailedChest()
        {
            return BlockInventoryPanelStateFactory.FromBlock(false, Array.Empty<SlotItem>(), FindChest(), new BlockInstanceId(1), new Vector3Int(1, 0, 0));
        }

        public static BlockInventoryPanelState Train(BlockInventoryPanelError error)
        {
            TrainInventoryMessageType? message = error switch
            {
                BlockInventoryPanelError.None => null,
                BlockInventoryPanelError.OpenFailed => TrainInventoryMessageType.OpenFailed,
                BlockInventoryPanelError.ContainerMissing => TrainInventoryMessageType.ContainerMissing,
                BlockInventoryPanelError.TrainCarMissing => TrainInventoryMessageType.TrainCarMissing,
                _ => throw new ArgumentOutOfRangeException(nameof(error), error, null),
            };
            return BlockInventoryPanelStateFactory.FromTrain(1, Array.Empty<SlotItem>(), message);
        }

        // マスタに無い blockType（"unknown"）も組めるよう internal ctor を直に呼ぶ（Client.Ui.State の InternalsVisibleTo）
        // Calls the internal ctor directly so blockTypes absent from the master (e.g. "unknown") can be built (Client.Ui.State InternalsVisibleTo)
        public static BlockInventoryPanelState OfType(string blockType, params SlotItem[] slots)
        {
            return new OpenBlockInventoryPanel(blockType, Guid.Empty, default, new BlockInstanceId(1), new Vector3Int(1, 0, 0), slots, BlockInventoryPanelError.None);
        }

        private static BlockMasterElement FindChest()
        {
            return Array.Find(MasterHolder.BlockMaster.Blocks.Data, b => b.BlockType == BlockMasterElement.BlockTypeConst.Chest);
        }
    }
}
