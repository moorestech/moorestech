using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Newtonsoft.Json;

namespace Game.Block.Blocks.Machine
{
    public class VanillaMachineSaveComponent : IBlockSaveState
    {
        public bool IsDestroy { get; private set; }
        
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
        
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            
            // JsonObjectにリファクタ
            var jsonObject = new VanillaMachineJsonObject
            {
                InputSlot = _vanillaMachineInputInventory.InputSlot.Select(item => new ItemStackJsonObject(item)).ToList(),
                OutputSlot = _vanillaMachineOutputInventory.OutputSlot.Select(item => new ItemStackJsonObject(item)).ToList(),
                State = (int)_vanillaMachineProcessorComponent.CurrentState,
                RemainingTime = _vanillaMachineProcessorComponent.RemainingMillSecond,
                RecipeId = _vanillaMachineProcessorComponent.RecipeDataId,
            };
            
            return JsonConvert.SerializeObject(jsonObject);
        }
    }
    
    public class VanillaMachineJsonObject
    {
        [JsonProperty("inputSlot")]
        public List<ItemStackJsonObject> InputSlot;
        [JsonProperty("outputSlot")]
        public List<ItemStackJsonObject> OutputSlot;
        
        [JsonProperty("state")]
        public int State;
        [JsonProperty("remainingTime")]
        public double RemainingTime;
        [JsonProperty("recipeId")]
        public int RecipeId;
    }
}