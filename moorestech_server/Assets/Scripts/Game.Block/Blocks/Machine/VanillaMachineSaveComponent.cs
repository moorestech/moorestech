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
        private readonly VanillaMachineProcessorComponent _vanillaMachineProcessorComponent;
        
        public VanillaMachineSaveComponent(
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            VanillaMachineProcessorComponent vanillaMachineProcessorComponent)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
            _vanillaMachineProcessorComponent = vanillaMachineProcessorComponent;
        }
        public bool IsDestroy { get; private set; }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        [Obsolete("Want to refactor machine save system")] // TODO Refactor machine save/load system
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
                InputFluidSlot = _vanillaMachineInputInventory.FluidInputSlot.Select(fluid => new FluidContainerSaveJsonObject(fluid)).ToList(),
                OutputFluidSlot = _vanillaMachineOutputInventory.FluidOutputSlot.Select(fluid => new FluidContainerSaveJsonObject(fluid)).ToList(),
                State = (int)_vanillaMachineProcessorComponent.CurrentState,
                RemainingSeconds = GameUpdater.TicksToSeconds(_vanillaMachineProcessorComponent.RemainingTicks),
                RecipeGuidStr = _vanillaMachineProcessorComponent.RecipeGuid.ToString(),
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