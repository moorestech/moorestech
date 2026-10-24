using System;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.MachineRecipesModule;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core.Machine.Upgrade
{
    /// <summary>
    ///     継承機械（入力2・出力2・モジュール4）を置いて回すための共通処理
    ///     Shared helpers to place and run the inheritance machine (2 inputs, 2 outputs, 4 module slots)
    /// </summary>
    public static class LevelInheritanceTestHelper
    {
        public const int InputSlotCount = 2;
        public const int OutputSlotCount = 2;
        public const int ModuleRangeStart = InputSlotCount + OutputSlotCount;
        public static readonly Guid Test1ItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        public static (IBlock block, VanillaMachineBlockInventoryComponent inventory, VanillaMachineProcessorComponent processor) PlaceMachine(Vector3Int position)
        {
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestLevelInheritanceIds.MachineId, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            return (block, block.GetComponent<VanillaMachineBlockInventoryComponent>(), block.GetComponent<VanillaMachineProcessorComponent>());
        }

        public static MachineRecipeMasterElement Recipe(Guid recipeGuid)
        {
            return MasterHolder.MachineRecipesMaster.GetRecipeElement(recipeGuid);
        }

        public static IItemStack Create(ItemId itemId, int count)
        {
            return ServerContext.ItemStackFactory.Create(itemId, count);
        }

        public static IItemStack CreateTest1(int count)
        {
            return ServerContext.ItemStackFactory.Create(Test1ItemGuid, count);
        }

        public static OpenableInventoryItemDataStoreService CreateOverflow(int slotCount)
        {
            return new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, slotCount);
        }

        // 毎tick有効要求電力ちょうどを供給して進める（電力比1.0で決定論化）
        // Advance supplying exactly the effective request power each tick (ratio 1.0 keeps it deterministic)
        public static void AdvanceTicksWithFullPower(int ticks, VanillaMachineProcessorComponent processor)
        {
            for (var i = 0; i < ticks; i++)
            {
                processor.SupplyExternalPower(processor.EffectiveRequestPower);
                GameUpdater.UpdateOneTick();
            }
        }

        // レシピ1回分を開始から払い出しまで進めるtick数
        // Ticks to run one craft from start to payout
        public static int TicksToComplete(MachineRecipeMasterElement recipe)
        {
            return (int)GameUpdater.SecondsToTicks(recipe.Time) + 5;
        }

        // 指定範囲の指定アイテムの合計数
        // Total count of the item within the slot range
        public static int CountItem(IOpenableInventory inventory, int start, int end, ItemId itemId)
        {
            var total = 0;
            for (var i = start; i < end; i++)
            {
                var stack = inventory.GetItem(i);
                if (stack.Id == itemId) total += stack.Count;
            }
            return total;
        }
    }
}
