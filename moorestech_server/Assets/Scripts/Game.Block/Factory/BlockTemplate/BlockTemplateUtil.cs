using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.Machine.State;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.BeltConveyor.Connection;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Inventory.Input;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Component;
using Game.Fluid;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.MachineUpgrade;
using Game.Context;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.InventoryConnectsModule;
using Mooresmaster.Model.MachineRecipesModule;
using UnityEngine;
using Game.Block.Interface.Component.ConnectJudge;

namespace Game.Block.Factory.BlockTemplate
{
    public class BlockTemplateUtil
    {
        public static BlockConnectorComponent<IBlockInventory, BeltInventoryConnectionContext> CreateInventoryConnector(InventoryConnects inventoryConnects, BlockPositionInfo blockPositionInfo)
        {
            return BeltInventoryConnectionContext.CreateMachine(inventoryConnects, blockPositionInfo);
        }
        
        // TODO 保存ステートを誰でも持てるようになったので、このあたりも各自でセーブ、ロードできるように簡略化したい
        public static (VanillaMachineInputInventory, VanillaMachineOutputInventory, VanillaMachineModuleInventory) GetMachineIOInventory(
            BlockId blockId, BlockInstanceId blockInstanceId,
            IMachineParam machineParam,
            BlockConnectorComponent<IBlockInventory, BeltInventoryConnectionContext> blockConnectorComponent,
            BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            var inputSlotCount = machineParam.InputSlotCount;
            var outputSlotCount = machineParam.OutputSlotCount;
            
            // タンク数と容量は機械パラメータ共通の口から読む（電気機械・歯車機械・清浄室機械で同じ）
            // Tank counts and capacity come from the shared machine-param interface (electric, gear and clean-room machines alike)
            var innerTankCapacity = machineParam.InnerTankCapacity;
            
            var input = new VanillaMachineInputInventory(
                blockId,
                inputSlotCount,
                machineParam.InputTankCount,
                innerTankCapacity,
                blockInventoryUpdateEvent,
                blockInstanceId,
                ServerContext.GetService<IGameUnlockStateDataController>(),
                ServerContext.GetService<ILevelInheritanceCraftCapLookup>()
            );
            
            var output = new VanillaMachineOutputInventory(
                outputSlotCount, machineParam.OutputTankCount, innerTankCapacity, ServerContext.ItemStackFactory, blockInventoryUpdateEvent, blockInstanceId,
                inputSlotCount, blockConnectorComponent);

            // モジュールスロットは第3レンジとして生成
            // Create module slots as the third range
            var module = new VanillaMachineModuleInventory(
                machineParam.ModuleSlotCount, machineParam.AllowQualityModule, ServerContext.GetService<IModuleSlotStackLimitLookup>(),
                blockInventoryUpdateEvent, blockInstanceId, inputSlotCount, outputSlotCount);

            return (input, output, module);
        }
        
        // 必須キーの扱いだけを分け、両機械の超過ログと復元規則を揃える
        // Vary only missing-key handling; share overflow logging and restoration for both machine kinds
        internal static void RestoreMachineFluids(List<FluidContainerSaveJsonObject> inputs, List<FluidContainerSaveJsonObject> outputs,
            VanillaMachineInputInventory input, VanillaMachineOutputInventory output, bool requireKeys)
        {
            if (requireKeys && (inputs == null || outputs == null))
                throw new InvalidOperationException($"清浄室機械のセーブに液体タンクのキーがありません。 inputFluidSlot={(inputs == null ? "missing" : "present")} outputFluidSlot={(outputs == null ? "missing" : "present")}");
            Restore(inputs, input.FluidInputSlot, "input");
            Restore(outputs, output.FluidOutputSlot, "output");

            #region Internal
            void Restore(List<FluidContainerSaveJsonObject> saved, IReadOnlyList<FluidContainer> tanks, string side)
            {
                if (saved == null) return; // silent-ok: 通常機械の旧セーブは液体節なし / 液体を保存していない旧形式のため復元対象なし
                if (tanks.Count < saved.Count) Debug.LogError($"機械のタンク数がセーブより少ないため、超過分の液体は消失します。 {side}={saved.Count}->{tanks.Count}");
                for (var i = 0; i < saved.Count && i < tanks.Count; i++)
                {
                    tanks[i].FluidId = saved[i].FluidId;
                    tanks[i].Amount = saved[i].Amount;
                }
            }
            #endregion
        }
    }
}
