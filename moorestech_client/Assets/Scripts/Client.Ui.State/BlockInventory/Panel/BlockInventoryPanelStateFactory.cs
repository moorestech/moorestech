using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Train;
using Client.Ui.State.Items;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Panel
{
    /// <summary>
    /// 源の値からパネルの状態を組む（Web ホストの BlockInventoryDtoFactory・TrainInventoryDtoFactory の規則の写し）
    /// Builds panel states from source values (port of the Web host BlockInventoryDtoFactory and TrainInventoryDtoFactory rules)
    /// </summary>
    public static class BlockInventoryPanelStateFactory
    {
        public static BlockInventoryPanelState FromBlock(bool fetchSucceeded, IReadOnlyList<SlotItem> itemSlots, BlockMasterElement master, BlockInstanceId blockInstanceId, Vector3Int position)
        {
            var error = fetchSucceeded ? BlockInventoryPanelError.None : BlockInventoryPanelError.OpenFailed;
            var blockId = MasterHolder.BlockMaster.GetBlockId(master.BlockGuid);
            return new OpenBlockInventoryPanel(master.BlockType, master.BlockGuid, blockId, blockInstanceId, position, itemSlots, error);
        }

        public static BlockInventoryPanelState FromTrain(long trainCarInstanceId, IReadOnlyList<SlotItem> itemSlots, TrainInventoryMessageType? lastOpenMessage)
        {
            // 3 つの理由を明示の腕で写す。未知の値は enum に値を足して写しを足し忘れた開発時の誤りなので止める（D-44 と揃える。D-61）
            // Map the three reasons with explicit arms; an unknown value is a dev-time miss after extending the enum, so stop (aligned with D-44; D-61)
            var error = lastOpenMessage switch
            {
                null => BlockInventoryPanelError.None,
                TrainInventoryMessageType.ContainerMissing => BlockInventoryPanelError.ContainerMissing,
                TrainInventoryMessageType.TrainCarMissing => BlockInventoryPanelError.TrainCarMissing,
                TrainInventoryMessageType.OpenFailed => BlockInventoryPanelError.OpenFailed,
                _ => throw new ArgumentOutOfRangeException(nameof(lastOpenMessage), lastOpenMessage, null),
            };
            return new OpenTrainInventoryPanel(trainCarInstanceId, itemSlots, error);
        }

        public static IReadOnlyList<SlotItem> ToSlotItems(IReadOnlyList<IItemStack> stacks)
        {
            var slots = new SlotItem[stacks.Count];
            for (var i = 0; i < slots.Length; i++) slots[i] = new SlotItem(stacks[i].Id, stacks[i].Count);
            return slots;
        }
    }
}
