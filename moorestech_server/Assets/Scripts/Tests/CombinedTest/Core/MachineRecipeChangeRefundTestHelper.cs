using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid;
using Mooresmaster.Model.MachineRecipesModule;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    // 加工中レシピ変更返却テストの共通セットアップ
    // Shared setup for mid-processing recipe-change refund tests
    internal static class MachineRecipeChangeRefundTestHelper
    {
        public static void InitDi()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        public static OpenableInventoryItemDataStoreService CreateOverflow(int slotCount)
        {
            return new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, slotCount);
        }

        public static (IBlock block, IMachineRecipeSelectorComponent selector, VanillaMachineProcessorComponent processor, VanillaMachineBlockInventoryComponent inventory) PlaceMachine(MachineRecipeMasterElement recipe)
        {
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector);
            return (block, selector, block.GetComponent<VanillaMachineProcessorComponent>(), block.GetComponent<VanillaMachineBlockInventoryComponent>());
        }

        public static void InsertRecipeInputs(VanillaMachineBlockInventoryComponent inventory, MachineRecipeMasterElement recipe)
        {
            foreach (var inputItem in recipe.InputItems)
            {
                inventory.InsertItem(ServerContext.ItemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }
        }

        public static void StartProcessing(IMachineRecipeSelectorComponent selector, VanillaMachineProcessorComponent processor, VanillaMachineBlockInventoryComponent inventory, MachineRecipeMasterElement recipe, OpenableInventoryItemDataStoreService overflow)
        {
            selector.SetSelectedRecipe(recipe, overflow);
            InsertRecipeInputs(inventory, recipe);
            GameUpdater.UpdateOneTick();
        }

        public static (List<IItemStack> input, List<IItemStack> output) GetNonEmptySlots(VanillaMachineBlockInventoryComponent inventory)
        {
            var inputInv = (VanillaMachineInputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineInputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(inventory);
            var outputInv = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(inventory);
            var input = inputInv.InputSlot.Where(i => i.Count != 0).ToList();
            var output = outputInv.OutputSlot.Where(i => i.Count != 0).ToList();
            return (input, output);
        }

        public static VanillaMachineInputInventory GetInputInventory(VanillaMachineBlockInventoryComponent inventory)
        {
            return (VanillaMachineInputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineInputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(inventory);
        }

        public static int CountItem(IEnumerable<IItemStack> slots, ItemId itemId)
        {
            return slots.Where(s => s.Id == itemId).Sum(s => s.Count);
        }

        public static int CountOverflowItem(OpenableInventoryItemDataStoreService overflow, ItemId itemId)
        {
            var total = 0;
            for (var i = 0; i < overflow.GetSlotSize(); i++)
            {
                var item = overflow.GetItem(i);
                if (item.Id == itemId) total += item.Count;
            }
            return total;
        }

        public static void FillInputSlotsWithFiller(VanillaMachineBlockInventoryComponent inventory, Guid fillerItemGuid)
        {
            var inputInv = GetInputInventory(inventory);
            for (var i = 0; i < inputInv.InputSlot.Count; i++)
            {
                inventory.SetItem(i, ServerContext.ItemStackFactory.Create(fillerItemGuid, 1));
            }
        }

        public static MachineRecipeMasterElement FindAlternateRecipe(MachineRecipeMasterElement current)
        {
            foreach (var recipe in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            {
                if (recipe.BlockGuid != current.BlockGuid) continue;
                if (recipe.MachineRecipeGuid == current.MachineRecipeGuid) continue;
                if (!recipe.InitialUnlocked) continue;
                return recipe;
            }
            return null;
        }

        public static void InsertRecipeFluids(IReadOnlyList<FluidContainer> tanks, MachineRecipeMasterElement recipe)
        {
            for (var i = 0; i < recipe.InputFluids.Length; i++)
            {
                var fluidId = MasterHolder.FluidMaster.GetFluidId(recipe.InputFluids[i].FluidGuid);
                tanks[i].AddLiquid(new FluidStack(recipe.InputFluids[i].Amount, fluidId), FluidContainer.Empty);
            }
        }

        // 返却量より小さい空きを残し、余剰吸収を防ぐため第3タンクを別流体で満杯にする
        // Leave free space smaller than refunds; fill the 3rd tank with another fluid to block overflow absorption
        public static (double before0, double before1, double before2, double expected0, double expected1) PreparePartialFluidTanksForOverflowRefund(IReadOnlyList<FluidContainer> tanks, MachineRecipeMasterElement recipe, FluidId blockerFluidId)
        {
            var fluidId0 = MasterHolder.FluidMaster.GetFluidId(recipe.InputFluids[0].FluidGuid);
            var fluidId1 = MasterHolder.FluidMaster.GetFluidId(recipe.InputFluids[1].FluidGuid);
            const double freeSpace0 = 0.3;
            const double freeSpace1 = 0.5;
            tanks[0].FluidId = fluidId0;
            tanks[0].Amount = tanks[0].Capacity - freeSpace0;
            tanks[1].FluidId = fluidId1;
            tanks[1].Amount = tanks[1].Capacity - freeSpace1;
            tanks[2].FluidId = blockerFluidId;
            tanks[2].Amount = tanks[2].Capacity;
            var before0 = tanks[0].Amount;
            var before1 = tanks[1].Amount;
            var before2 = tanks[2].Amount;
            var expected0 = Math.Min(before0 + recipe.InputFluids[0].Amount, tanks[0].Capacity);
            var expected1 = Math.Min(before1 + recipe.InputFluids[1].Amount, tanks[1].Capacity);
            return (before0, before1, before2, expected0, expected1);
        }
    }
}
