using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Inventory.Input;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Blocks.Machine.State;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.MachineRecipesModule;
using UnityEngine;

namespace Game.Block.Factory.BlockTemplate.Machine
{
    internal static class MachineSaveRestorer
    {
        public static VanillaMachineProcessorComponent MachineLoadState(Dictionary<string, object> componentStates,
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            VanillaMachineModuleInventory vanillaMachineModuleInventory,
            MachineModuleEffectComponent machineModuleEffectComponent,
            float requestPower, float idlePowerRate, BlockMasterElement blockMasterElement)
        {
            var jsonObject = BlockComponentStateReader.Read<VanillaMachineJsonObject>(componentStates, VanillaMachineSaveComponent.SaveKeyStatic);

            // セーブデータからのロード時はイベントを発火しない（ブロックがまだWorldBlockDatastoreに登録されていないため）
            // Do not invoke events when loading from save data (block is not yet registered in WorldBlockDatastore)
            var inputItems = jsonObject.InputSlot.Select(item => item.ToItemStack()).ToList();
            for (var i = 0; i < inputItems.Count; i++)
            {
                if (vanillaMachineInputInventory.InputSlot.Count <= i)
                {
                    Debug.LogError($"ロードするデータのインベントリサイズが超過しています。一部のアイテムは消失します。ブロック名:{blockMasterElement.Name} Guid:{blockMasterElement.BlockGuid}");
                    break;
                }
                vanillaMachineInputInventory.SetItemWithoutEvent(i, inputItems[i]);
            }

            var outputItems = jsonObject.OutputSlot.Select(item => item.ToItemStack()).ToList();
            for (var i = 0; i < outputItems.Count; i++)
            {
                if (vanillaMachineOutputInventory.OutputSlot.Count <= i)
                {
                    Debug.LogError($"ロードするデータのインベントリサイズが超過しています。一部のアイテムは消失します。ブロック名:{blockMasterElement.Name} Guid:{blockMasterElement.BlockGuid}");
                    break;
                }
                vanillaMachineOutputInventory.SetItemWithoutEvent(i, outputItems[i]);
            }

            // モジュールスロットを復元（旧セーブはキー無し）
            // Restore module slots (older saves lack the key)
            if (jsonObject.ModuleSlot != null)
            {
                var moduleItems = jsonObject.ModuleSlot.Select(item => item.ToItemStack()).ToList();
                for (var i = 0; i < moduleItems.Count; i++)
                {
                    if (vanillaMachineModuleInventory.ModuleSlot.Count <= i)
                    {
                        Debug.LogError($"ロードするデータのインベントリサイズが超過しています。一部のアイテムは消失します。ブロック名:{blockMasterElement.Name} Guid:{blockMasterElement.BlockGuid}");
                        break;
                    }
                    vanillaMachineModuleInventory.SetItemWithoutEvent(i, moduleItems[i]);
                }
            }

            // 液体復元は清浄室と同じ口へ委譲する
            // Restore fluids through the same entry point as clean-room machines
            BlockTemplateUtil.RestoreMachineFluids(jsonObject.InputFluidSlot, jsonObject.OutputFluidSlot, vanillaMachineInputInventory, vanillaMachineOutputInventory, false);

            // processor だけ null の外部セーブ（版7→8移行が通す形）は、インベントリを残して加工状態の復元だけ見送る
            // An external save whose processor alone is null (a shape the V7->V8 migration lets through) keeps its inventories and skips only the processing state
            var processorJson = jsonObject.Processor;
            var raw = Newtonsoft.Json.Linq.JObject.FromObject(componentStates[VanillaMachineSaveComponent.SaveKeyStatic]);
            Game.Block.Blocks.Machine.State.Save.MachineRealizationSaveJsonObject.WarnIfLegacy(raw["processor"] as Newtonsoft.Json.Linq.JObject);
            if (processorJson == null)
            {
                Debug.LogWarning($"通常機械のセーブ状態の processor が null のため、加工状態の復元を見送り待機・未選択で開始します。ブロック名:{blockMasterElement.Name} Guid:{blockMasterElement.BlockGuid}");
                return new VanillaMachineProcessorComponent(vanillaMachineInputInventory, vanillaMachineOutputInventory, requestPower, idlePowerRate, machineModuleEffectComponent);
            }

            // 加工中ジョブは Processor のサブオブジェクトの平らなキーから組む（レシピがマスタから消えていれば null で、加工中の状態は Idle へ戻る）
            // Build the running job from the Processor sub-object's flat keys (null when its recipe left the master; a mid-job state then falls back to Idle)
            var processingJob = MachineProcessingJob.RestoreFromFlatSave(processorJson.RecipeGuidStr, processorJson.RemainingSeconds, processorJson.PendingOutputs, processorJson.InheritedLevel, processorJson.TotalTicks, processorJson.ConsumedInputItemGuids, processorJson.PowerMultiplier);

            // 選択レシピを復元。GUIDがマスタから消えていれば未選択に戻す（機械は停止するが壊れない）
            // Restore the selected recipe; a GUID missing from the master falls back to unselected
            MachineRecipeMasterElement selectedRecipe = null;
            if (!string.IsNullOrEmpty(processorJson.SelectedRecipeGuidStr) && Guid.TryParse(processorJson.SelectedRecipeGuidStr, out var selectedGuid))
            {
                selectedRecipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(selectedGuid);
            }

            var processor = new VanillaMachineProcessorComponent(
                vanillaMachineInputInventory,
                vanillaMachineOutputInventory,
                (ProcessState)processorJson.State,
                processingJob,
                requestPower,
                idlePowerRate,
                machineModuleEffectComponent,
                selectedRecipe);

            processor.RestorePendingRealization(processorJson.PendingRealization);
            return processor;
        }
    }
}
