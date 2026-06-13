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

            // 加工状態はProcessor自身が構築する。Saveはインベントリ分のみ担う
            // Processor builds its own state; Save handles only the inventory parts
            var jsonObject = new VanillaMachineJsonObject
            {
                InputSlot = _vanillaMachineInputInventory.InputSlot.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                OutputSlot = _vanillaMachineOutputInventory.OutputSlot.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                ModuleSlot = _vanillaMachineModuleInventory.ModuleSlot.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                InputFluidSlot = _vanillaMachineInputInventory.FluidInputSlot.Select(fluid => new FluidContainerSaveJsonObject(fluid)).ToList(),
                OutputFluidSlot = _vanillaMachineOutputInventory.FluidOutputSlot.Select(fluid => new FluidContainerSaveJsonObject(fluid)).ToList(),
                Processor = _vanillaMachineProcessorComponent.GetSaveJsonObject(),
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
        // 旧セーブはキー無しのためnull許容
        // Nullable because older saves lack this key
        [JsonProperty("moduleSlot")]
        public List<ItemStackSaveJsonObject> ModuleSlot;
        [JsonProperty("inputFluidSlot")]
        public List<FluidContainerSaveJsonObject> InputFluidSlot;
        [JsonProperty("outputFluidSlot")]
        public List<FluidContainerSaveJsonObject> OutputFluidSlot;

        // 加工状態はProcessorが構築・所有するサブオブジェクト
        // Processing state is a sub-object built and owned by the Processor
        [JsonProperty("processor")]
        public VanillaMachineProcessorSaveJsonObject Processor;
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