using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Chest;
using Game.CraftChainer.CraftNetwork;
using Newtonsoft.Json;
using UniRx;

namespace Game.CraftChainer.BlockComponent.ProviderChest
{
    public class ChainerProviderChestComponent : ICraftChainerNode
    {
        public CraftChainerNodeId NodeId { get; } = CraftChainerNodeId.Create();
        public IReadOnlyList<IItemStack> Inventory => _vanillaChestComponent.InventoryItems;
        
        private readonly ProviderChestBlockInventoryInserter _providerChestBlockInventoryInserter;
        private readonly VanillaChestComponent _vanillaChestComponent;
        
        public ChainerProviderChestComponent(ProviderChestBlockInventoryInserter providerChestBlockInventoryInserter, VanillaChestComponent vanillaChestComponent)
        {
            _providerChestBlockInventoryInserter = providerChestBlockInventoryInserter;
            _vanillaChestComponent = vanillaChestComponent;
        }
        public ChainerProviderChestComponent(Dictionary<string, string> componentStates, ProviderChestBlockInventoryInserter providerChestBlockInventoryInserter, VanillaChestComponent vanillaChestComponent) : this(providerChestBlockInventoryInserter, vanillaChestComponent)
        {
            var state = componentStates[SaveKey];
            var jsonObject = JsonConvert.DeserializeObject<ChainerProviderChestComponentJsonObject>(state);
            NodeId = new CraftChainerNodeId(jsonObject.NodeId);
        }
        
        
        public void EnqueueItemDistributedOnNetwork(ItemId itemId, int count)
        {
            _providerChestBlockInventoryInserter.EnqueueItemDistributedOnNetwork(itemId, count);
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
        
        public ChainerProviderChestComponentJsonObject(ChainerProviderChestComponent component)
        {
            NodeId = component.NodeId.AsPrimitive();
        }
    }
}