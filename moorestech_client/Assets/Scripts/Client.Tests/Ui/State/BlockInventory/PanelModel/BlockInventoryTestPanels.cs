using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Panel;
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
        public static BlockInventoryPanelState OfType(string blockType, string blockScreen, params SlotItem[] slots)
        {
            return OpenBlock(blockType, blockScreen, Guid.Empty, default, new BlockInstanceId(1), new Vector3Int(1, 0, 0), slots, BlockInventoryPanelError.None);
        }

        internal static OpenBlockInventoryPanel OpenBlock(string blockType, string blockScreen, Guid blockGuid, BlockId blockId, BlockInstanceId instanceId, Vector3Int position, IReadOnlyList<SlotItem> slots, BlockInventoryPanelError error)
        {
            return new OpenBlockInventoryPanel(blockType, blockScreen, blockGuid, blockId, instanceId, position, slots, error, BlockComponentRegistry.Resolve(blockScreen));
        }

        // 共有マスタを変更せず、画面だけが異なる同一ブロックを用意する
        // Keep the shared master intact and vary only the screen of the same block
        internal static BlockMasterElement WithScreen(BlockMasterElement master, string screen)
        {
            return new BlockMasterElement(master.Index, master.BlockGuid, master.Name, master.BlockType, master.RequiredItems,
                master.PlacementsPerCost, master.ImagePath, master.Category, master.SubCategory, master.SortPriority,
                master.InitialUnlocked, master.BlockSize, master.BlockPrefabAddressablesPath, screen, master.BlockParam);
        }

        private static BlockMasterElement FindChest()
        {
            return Array.Find(MasterHolder.BlockMaster.Blocks.Data, b => b.BlockType == BlockMasterElement.BlockTypeConst.Chest);
        }
    }
}
