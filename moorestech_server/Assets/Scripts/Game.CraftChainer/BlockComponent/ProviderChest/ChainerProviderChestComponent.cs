using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Blocks.Chest;
using Game.CraftChainer.CraftNetwork;
using Newtonsoft.Json;
using UniRx;

namespace Game.CraftChainer.BlockComponent.ProviderChest
{
    public class ChainerProviderChestComponent : ICraftChainerNode
    {
        public CraftChainerNodeId NodeId { get; }
        public IReadOnlyList<IItemStack> Inventory => _vanillaChestComponent.InventoryItems;
        
        private readonly VanillaChestComponent _vanillaChestComponent;
        
        public ChainerProviderChestComponent(CraftChainerNodeId nodeId, VanillaChestComponent vanillaChestComponent)
        {
            NodeId = nodeId;
            _vanillaChestComponent = vanillaChestComponent;
        }
        public ChainerProviderChestComponent(Dictionary<string, string> componentStates, CraftChainerNodeId nodeId, VanillaChestComponent vanillaChestComponent) : 
            this(nodeId, vanillaChestComponent)
        {
            var state = componentStates[SaveKey];
            var jsonObject = JsonConvert.DeserializeObject<ChainerProviderChestComponentJsonObject>(state);
            NodeId = new CraftChainerNodeId(jsonObject.NodeId);
        }
        
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        public string SaveKey { get; } = typeof(ChainerProviderChestComponent).FullName;
        public string GetSaveState()
        {
            return JsonConvert.SerializeObject(new ChainerProviderChestComponentJsonObject(this));
        }
    }
    
    public class ChainerProviderChestComponentJsonObject
    {
        [JsonProperty("nodeId")] public int NodeId { get; set; }
        
        public ChainerProviderChestComponentJsonObject(){}
        public ChainerProviderChestComponentJsonObject(ChainerProviderChestComponent component)
        {
            NodeId = component.NodeId.AsPrimitive();
        }
    }
}