using System.Collections.Generic;
using Game.CraftChainer.CraftNetwork;
using Newtonsoft.Json;

namespace Game.CraftChainer.BlockComponent.Computer
{
    public class ChainerMainComputerComponent : ICraftChainerNode
    {
        public readonly ChainerNetworkContext ChainerNetworkContext;
        
        public CraftChainerNodeId NodeId { get; } = CraftChainerNodeId.Create();
        
        public ChainerMainComputerComponent()
        {
            ChainerNetworkContext = new ChainerNetworkContext();
        }
        
        public ChainerMainComputerComponent(Dictionary<string, string> componentStates) : this()
        {
            var state = componentStates[SaveKey];
            var jsonObject = JsonConvert.DeserializeObject<ChainerMainComputerComponentJsonObject>(state);
            NodeId = new CraftChainerNodeId(jsonObject.NodeId);
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