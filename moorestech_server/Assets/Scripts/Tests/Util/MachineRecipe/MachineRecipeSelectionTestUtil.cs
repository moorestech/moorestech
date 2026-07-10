using System;
using System.Reflection;
using Core.Inventory;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.State;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;

namespace Tests.Util.MachineRecipe
{
    public static class MachineRecipeSelectionTestUtil
    {
        public static MachineRecipeChangeResult SelectRecipe(IBlock block, Guid? recipeGuid)
        {
            return block.GetComponent<IMachineRecipeSelectable>().TrySetRecipe(recipeGuid, CreatePlayerInventory(10));
        }

        public static IOpenableInventory CreatePlayerInventory(int slotCount)
        {
            return new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, slotCount, new OpenableInventoryItemDataStoreServiceOption());
        }

        public static VanillaMachineInputInventory GetInputInventory(IBlock block)
        {
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            return (VanillaMachineInputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineInputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(inventory);
        }

        public static VanillaMachineOutputInventory GetOutputInventory(IBlock block)
        {
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            return (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(inventory);
        }
    }
}
