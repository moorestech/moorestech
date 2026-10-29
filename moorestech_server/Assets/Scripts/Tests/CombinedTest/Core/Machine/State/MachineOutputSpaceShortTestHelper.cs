using System;
using System.Linq;
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
using UnityEngine;
using static Tests.CombinedTest.Core.Machine.MachineInventoryTestHelper;

namespace Tests.CombinedTest.Core.Machine
{
    // 出力空き不足の理由テスト用に、出口を満杯にした機械を組み立てて状態詳細を読む
    // Builds a machine whose output is full and reads its state details for the output-space-short tests
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

        // 出力スロット0を産出物の上限まで埋め、次のジョブが入りきらない状態にする
        // Fill output slot 0 to the product's max stack so the next job cannot fit
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

        public static string ReadPublishedState(IBlockStateObservable processor)
        {
            var detail = processor.GetBlockStateDetails().First(d => d.Key == CommonMachineBlockStateDetail.BlockStateDetailKey);
            return MessagePackSerializer.Deserialize<CommonMachineBlockStateDetail>(detail.Value).CurrentStateType;
        }
    }
}
