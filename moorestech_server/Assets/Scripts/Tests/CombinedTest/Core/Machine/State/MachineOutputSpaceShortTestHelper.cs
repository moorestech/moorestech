using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using MessagePack;
using Mooresmaster.Model.MachineRecipesModule;
using Tests.Util;
using UniRx;
using UnityEngine;
using static Tests.CombinedTest.Core.Machine.MachineInventoryTestHelper;

namespace Tests.CombinedTest.Core.Machine
{
    // - 機械配置とレシピ選択
    // - 出口充填と状態詳細読取
    // - Machine placement and recipe selection
    // - Output filling and state-detail reading
    internal static class MachineOutputSpaceShortTestHelper
    {
        public static MachineRecipeMasterElement ItemRecipe()
        {
            return MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => 0 < r.InputItems.Length && 0 < r.OutputItems.Length);
        }

        public static (IBlock block, VanillaMachineBlockInventoryComponent inventory, VanillaMachineProcessorComponent processor) PlaceSelected(MachineRecipeMasterElement recipe, Vector3Int position)
        {
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            return (block, block.GetComponent<VanillaMachineBlockInventoryComponent>(), block.GetComponent<VanillaMachineProcessorComponent>());
        }

        // 出力スロット0を産出物の上限で埋める
        // Fill output slot 0 to the product maximum stack
        public static void FillOutputSlot(MachineRecipeMasterElement recipe, VanillaMachineBlockInventoryComponent inventory)
        {
            var outputItemId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
            inventory.SetItem(GetInputSlotCount(inventory), ServerContext.ItemStackFactory.Create(outputItemId, ItemStackLevelDataStore.Instance.GetMaxStack(outputItemId)));
        }

        public static void EmptyOutputSlot(VanillaMachineBlockInventoryComponent inventory)
        {
            inventory.SetItem(GetInputSlotCount(inventory), ServerContext.ItemStackFactory.CreatEmpty());
        }

        public static void InsertInputs(MachineRecipeMasterElement recipe, VanillaMachineBlockInventoryComponent inventory)
        {
            foreach (var inputItem in recipe.InputItems) inventory.InsertItem(ServerContext.ItemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
        }

        public static void EmptyInputs(VanillaMachineBlockInventoryComponent inventory)
        {
            for (var slot = 0; slot < GetInputSlotCount(inventory); slot++) inventory.SetItem(slot, ServerContext.ItemStackFactory.CreatEmpty());
        }

        public static bool ReadOutputSpaceShort(IBlockStateObservable processor)
        {
            var detail = processor.GetBlockStateDetails().First(d => d.Key == MachineBlockStateDetail.BlockStateDetailKey);
            return MessagePackSerializer.Deserialize<MachineBlockStateDetail>(detail.Value).IsOutputSpaceShort;
        }

        // 状態イベントごとに、その時点の理由を記録する
        // Record the reason read at each state event
        public static List<bool> RecordReasonNotices(IBlockStateObservable processor)
        {
            var notices = new List<bool>();
            processor.OnChangeBlockState.Subscribe(_ => notices.Add(ReadOutputSpaceShort(processor)));
            return notices;
        }

        public static string ReadPublishedState(IBlockStateObservable processor)
        {
            var detail = processor.GetBlockStateDetails().First(d => d.Key == CommonMachineBlockStateDetail.BlockStateDetailKey);
            return MessagePackSerializer.Deserialize<CommonMachineBlockStateDetail>(detail.Value).CurrentStateType;
        }
    }
}
