using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.CraftChainer.CraftChain;
using Game.CraftChainer.CraftNetwork;
using Newtonsoft.Json;

namespace Game.CraftChainer.BlockComponent.Computer
{
    public class ChainerMainComputerComponent : ICraftChainerNode
    {
        public readonly ChainerNetworkContext ChainerNetworkContext;
        
        public CraftChainerNodeId NodeId { get; } = CraftChainerNodeId.Create();
        
        public ChainerMainComputerComponent(BlockConnectorComponent<IBlockInventory> mainComputerConnector)
        {
            ChainerNetworkContext = new ChainerNetworkContext(mainComputerConnector);
        }
        
        public ChainerMainComputerComponent(Dictionary<string, string> componentStates, BlockConnectorComponent<IBlockInventory> mainComputerConnector) : this(mainComputerConnector)
        {
            var state = componentStates[SaveKey];
            var jsonObject = JsonConvert.DeserializeObject<ChainerMainComputerComponentJsonObject>(state);
            NodeId = new CraftChainerNodeId(jsonObject.NodeId);
        }
        
        public void StartCreateItem(ItemId itemId, int count)
        {
        }
        
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
        public string SaveKey { get; } = typeof(ChainerMainComputerComponent).FullName;
        public string GetSaveState()
        {
            return JsonConvert.SerializeObject(new ChainerMainComputerComponentJsonObject(this));
        }
    }
    
    public class ChainerMainComputerComponentJsonObject
    {
        [JsonProperty("nodeId")] public int NodeId { get; set; }
        
        public ChainerMainComputerComponentJsonObject(ChainerMainComputerComponent component)
        {
            NodeId = component.NodeId.AsPrimitive();
        }
    }
}