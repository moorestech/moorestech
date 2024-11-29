using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Blocks.Chest;
using Game.CraftChainer.CraftNetwork;
using Newtonsoft.Json;
using UniRx;

namespace Game.CraftChainer.BlockComponent.ProviderChest
{
    public class CraftChainerProviderChestComponent : ICraftChainerNode
    {
        public CraftChainerNodeId NodeId { get; }
        public IReadOnlyList<IItemStack> Inventory => _vanillaChestComponent.InventoryItems;
        private VanillaChestComponent _vanillaChestComponent;
        
        public CraftChainerProviderChestComponent()
        {
            NodeId = CraftChainerNodeId.Create();
        }
        public CraftChainerProviderChestComponent(Dictionary<string, string> componentStates) : this()
        {
            var state = componentStates[SaveKey];
            var jsonObject = JsonConvert.DeserializeObject<ChainerProviderChestComponentJsonObject>(state);
            NodeId = new CraftChainerNodeId(jsonObject.NodeId);
        }
        
        public void SetInitialVanillaChestComponent(VanillaChestComponent vanillaChestComponent)
        {
            _vanillaChestComponent = vanillaChestComponent;
        }
        
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        public string SaveKey { get; } = typeof(CraftChainerProviderChestComponent).FullName;
        public string GetSaveState()
        {
            return JsonConvert.SerializeObject(new ChainerProviderChestComponentJsonObject(this));
        }
    }
    
    public class ChainerProviderChestComponentJsonObject
    {
        [JsonProperty("nodeId")] public int NodeId { get; set; }
        
        public ChainerProviderChestComponentJsonObject(){}
        public ChainerProviderChestComponentJsonObject(CraftChainerProviderChestComponent component)
        {
            NodeId = component.NodeId.AsPrimitive();
        }
    }
}