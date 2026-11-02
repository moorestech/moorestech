using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom;
using Game.Context;
using Game.Fluid;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomMachineTestHelpers;

namespace Tests.CombinedTest.Core.CleanRoom.Machine
{
    // 工程用テスト機械（入力1・出力4・モジュール3・入力タンク1）の共通操作
    // Shared operations for the process test machine (1 input, 4 outputs, 3 module slots, 1 input tank)
    internal static class CleanRoomProcessMachineTestHelpers
    {
        internal const int InputSlot = 0;
        internal const int FirstOutputSlot = 1;
        internal const int OutputSlotCount = 4;
        internal const int FirstModuleSlot = 5;
        internal static readonly Vector3Int MachinePosition = new(2, 1, 1);

        internal static FluidId TestIpaFluidId => MasterHolder.FluidMaster.GetFluidId(Guid.Parse("00000000-0000-0000-1234-000000000003"));

        internal static IBlock PlaceProcessMachine()
        {
            return CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomProcessMachineId, MachinePosition);
        }

        internal static void SelectRecipeAndLoad(IBlock machine, Guid recipeGuid, ItemId inputItemId, int count)
        {
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(recipeGuid);
            MachineRecipeSelectTestUtil.SelectRecipe(machine, recipe);
            machine.GetComponent<IOpenableBlockInventoryComponent>().SetItem(InputSlot, inputItemId, count);
        }

        // 入力タンクへIPAを足す。容量を超えた分は戻り値として捨てる
        // Add IPA to the input tank; anything over capacity comes back as the discarded remainder
        internal static void TopUpIpa(IBlock machine, double amount)
        {
            machine.GetComponent<VanillaMachineFluidInventoryComponent>().AddLiquid(new FluidStack(amount, TestIpaFluidId), default);
        }

        // 部屋を回しながら毎tickIPAを補充し、出力を全部回収する。回収数が目標に届くか上限tickで止める
        // Run the room, top up IPA and drain every output each tick; stop at the target count or the tick cap
        internal static Dictionary<ItemId, int> RunAndCollect(IBlock filter, IBlock machine, int targetOutputCount, int maxTicks)
        {
            var collected = new Dictionary<ItemId, int>();
            var total = 0;
            IOpenableInventory inventory = machine.GetComponent<IOpenableBlockInventoryComponent>();
            for (var tick = 0; tick < maxTicks && total < targetOutputCount; tick++)
            {
                TopUpIpa(machine, 20);
                TickRoom(filter, machine);
                total += DrainOutputs();
            }
            return collected;

            #region Internal

            int DrainOutputs()
            {
                // 別グレードで出力スロットが詰まらないよう、毎tick空にする
                // Empty the output slots every tick so a different grade never blocks them
                var drained = 0;
                for (var slot = FirstOutputSlot; slot < FirstOutputSlot + OutputSlotCount; slot++)
                {
                    var stack = inventory.GetItem(slot);
                    if (stack.Count == 0) continue;
                    collected[stack.Id] = CountOf(collected, stack.Id) + stack.Count;
                    drained += stack.Count;
                    inventory.SetItem(slot, ServerContext.ItemStackFactory.CreatEmpty());
                }
                return drained;
            }

            #endregion
        }

        internal static int CountOf(Dictionary<ItemId, int> collected, ItemId itemId)
        {
            return collected.TryGetValue(itemId, out var count) ? count : 0;
        }

        // 機械が加工を始める前に部屋をクラスA（閾値行0）まで清浄化する
        // Purify the room to class A (threshold row 0) before the machine starts work
        internal static bool WaitForClassA(CleanRoomDatastore datastore, IBlock filter, IBlock machine, int maxTicks)
        {
            for (var tick = 0; tick < maxTicks; tick++)
            {
                if (datastore.TryGetCleanRoomAt(MachinePosition, out var room) && room.ThresholdIndex == 0) return true;
                TickRoom(filter, machine);
            }
            return false;
        }
    }
}
