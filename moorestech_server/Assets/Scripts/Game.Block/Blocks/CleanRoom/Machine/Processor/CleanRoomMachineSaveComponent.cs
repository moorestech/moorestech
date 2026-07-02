using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Fluid;
using Newtonsoft.Json;

namespace Game.Block.Blocks.CleanRoom
{
    // VanillaMachineSaveComponent をコピーし、出力を専用型に差し替え＋_processedCycleCount をプロセッサ経由で永続化。
    // Copied from VanillaMachineSaveComponent; output swapped to the dedicated type and _processedCycleCount persisted via the processor.
    public class CleanRoomMachineSaveComponent : IBlockSaveState
    {
        private readonly VanillaMachineInputInventory _inputInventory;
        private readonly CleanRoomMachineOutputInventory _outputInventory;
        private readonly VanillaMachineModuleInventory _moduleInventory;
        private readonly CleanRoomMachineProcessorComponent _processor;

        public CleanRoomMachineSaveComponent(
            VanillaMachineInputInventory inputInventory,
            CleanRoomMachineOutputInventory outputInventory,
            VanillaMachineModuleInventory moduleInventory,
            CleanRoomMachineProcessorComponent processor)
        {
            _inputInventory = inputInventory;
            _outputInventory = outputInventory;
            _moduleInventory = moduleInventory;
            _processor = processor;
        }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
        }

        public static string SaveKeyStatic => typeof(CleanRoomMachineSaveComponent).FullName;
        public string SaveKey { get; } = typeof(CleanRoomMachineSaveComponent).FullName;

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);

            var jsonObject = new CleanRoomMachineJsonObject
            {
                InputSlot = _inputInventory.InputSlot.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                OutputSlot = _outputInventory.OutputSlot.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                ModuleSlot = _moduleInventory.ModuleSlot.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                InputFluidSlot = _inputInventory.FluidInputSlot.Select(fluid => new FluidContainerSaveJsonObject(fluid)).ToList(),
                OutputFluidSlot = _outputInventory.FluidOutputSlot.Select(fluid => new FluidContainerSaveJsonObject(fluid)).ToList(),
                Processor = _processor.GetSaveJsonObject(),
            };

            return JsonConvert.SerializeObject(jsonObject);
        }
    }

    public class CleanRoomMachineJsonObject
    {
        [JsonProperty("inputSlot")]
        public List<ItemStackSaveJsonObject> InputSlot;
        [JsonProperty("outputSlot")]
        public List<ItemStackSaveJsonObject> OutputSlot;
        [JsonProperty("moduleSlot")]
        public List<ItemStackSaveJsonObject> ModuleSlot;
        [JsonProperty("inputFluidSlot")]
        public List<FluidContainerSaveJsonObject> InputFluidSlot;
        [JsonProperty("outputFluidSlot")]
        public List<FluidContainerSaveJsonObject> OutputFluidSlot;

        [JsonProperty("processor")]
        public CleanRoomMachineProcessorSaveJsonObject Processor;
    }
}
