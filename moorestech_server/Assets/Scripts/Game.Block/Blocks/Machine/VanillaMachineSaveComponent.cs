using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Fluid;
using Newtonsoft.Json;

namespace Game.Block.Blocks.Machine
{
    public class VanillaMachineSaveComponent : IBlockSaveState
    {
        private readonly VanillaMachineInputInventory _vanillaMachineInputInventory;
        private readonly VanillaMachineOutputInventory _vanillaMachineOutputInventory;
        private readonly VanillaMachineModuleInventory _vanillaMachineModuleInventory;
        private readonly VanillaMachineProcessorComponent _vanillaMachineProcessorComponent;

        public VanillaMachineSaveComponent(
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            VanillaMachineModuleInventory vanillaMachineModuleInventory,
            VanillaMachineProcessorComponent vanillaMachineProcessorComponent)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
            _vanillaMachineModuleInventory = vanillaMachineModuleInventory;
            _vanillaMachineProcessorComponent = vanillaMachineProcessorComponent;
        }
        public bool IsDestroy { get; private set; }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        [Obsolete("機械のセーブ周りのリファクタをしたい")] // TODO 機械のセーブ、保存周りのリファクタ
        public static string SaveKeyStatic => typeof(VanillaMachineSaveComponent).FullName;
        public string SaveKey { get; } = typeof(VanillaMachineSaveComponent).FullName;
        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            
            // tickを秒数に変換して保存（tick数の変動に対応）
            // Convert ticks to seconds for storage (to handle tick rate changes)
            var jsonObject = new VanillaMachineJsonObject
            {
                InputSlot = _vanillaMachineInputInventory.InputSlot.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                OutputSlot = _vanillaMachineOutputInventory.OutputSlot.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                ModuleSlot = _vanillaMachineModuleInventory.ModuleSlot.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                InputFluidSlot = _vanillaMachineInputInventory.FluidInputSlot.Select(fluid => new FluidContainerSaveJsonObject(fluid)).ToList(),
                OutputFluidSlot = _vanillaMachineOutputInventory.FluidOutputSlot.Select(fluid => new FluidContainerSaveJsonObject(fluid)).ToList(),
                State = (int)_vanillaMachineProcessorComponent.CurrentState,
                RemainingSeconds = GameUpdater.TicksToSeconds(_vanillaMachineProcessorComponent.RemainingTicks),
                RecipeGuidStr = _vanillaMachineProcessorComponent.RecipeGuid.ToString(),
                // モジュール効果適用済みの加工時間と倍率もセーブし、ロード時にレシピ定義へ巻き戻らないようにする
                // Also save the effect-scaled processing time and multipliers so loads do not revert to the recipe definition
                ProcessingTotalSeconds = GameUpdater.TicksToSeconds(_vanillaMachineProcessorComponent.ProcessingRecipeTicks),
                EffectPowerMultiplier = _vanillaMachineProcessorComponent.CurrentPowerMultiplier,
                EffectExtraOutputChance = _vanillaMachineProcessorComponent.CurrentExtraOutputChance,
                ProcessedCycleCount = _vanillaMachineProcessorComponent.ProcessedCycleCount,
            };
            
            return JsonConvert.SerializeObject(jsonObject);
        }
    }
    
    public class VanillaMachineJsonObject
    {
        [JsonProperty("inputSlot")]
        public List<ItemStackSaveJsonObject> InputSlot;
        [JsonProperty("outputSlot")]
        public List<ItemStackSaveJsonObject> OutputSlot;
        // 過去セーブにはキーが無いことがあるためnull許容で扱う
        // Older saves may lack this key, so it is treated as nullable
        [JsonProperty("moduleSlot")]
        public List<ItemStackSaveJsonObject> ModuleSlot;
        [JsonProperty("inputFluidSlot")]
        public List<FluidContainerSaveJsonObject> InputFluidSlot;
        [JsonProperty("outputFluidSlot")]
        public List<FluidContainerSaveJsonObject> OutputFluidSlot;
        [JsonProperty("recipeGuid")]
        public string RecipeGuidStr;
        [JsonIgnore]
        public Guid RecipeGuid => Guid.Parse(RecipeGuidStr);

        // 秒数として保存（tick数の変動に対応）
        // Save as seconds (to handle tick rate changes)
        [JsonProperty("remainingSeconds")]
        public double RemainingSeconds;

        [JsonProperty("state")]
        public int State;

        // モジュール効果適用済みの加工時間（秒）と効果倍率。旧セーブにはキーが無く0となり、ロード側で中立として扱う
        // Effect-scaled processing time in seconds plus effect multipliers. Old saves lack these keys (0) and load as neutral
        [JsonProperty("processingTotalSeconds")]
        public double ProcessingTotalSeconds;

        [JsonProperty("effectPowerMultiplier")]
        public float EffectPowerMultiplier;

        [JsonProperty("effectExtraOutputChance")]
        public float EffectExtraOutputChance;

        [JsonProperty("processedCycleCount")]
        public int ProcessedCycleCount;
    }
    
    public class FluidContainerSaveJsonObject
    {
        [JsonProperty("fluidId")]
        public int FluidIdValue;
        
        [JsonIgnore]
        public FluidId FluidId => new FluidId(FluidIdValue);
        
        [JsonProperty("amount")]
        public double Amount;
        
        public FluidContainerSaveJsonObject()
        {
        }
        
        public FluidContainerSaveJsonObject(FluidContainer container)
        {
            FluidIdValue = container.FluidId.AsPrimitive();
            Amount = container.Amount;
        }
    }
}