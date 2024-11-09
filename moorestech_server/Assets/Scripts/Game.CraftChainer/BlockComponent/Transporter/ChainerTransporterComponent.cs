using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.CraftChainer.CraftNetwork;
using Newtonsoft.Json;

namespace Game.CraftChainer.BlockComponent
{
    public class ChainerTransporterComponent : ICraftChainerNode
    {
        public CraftChainerNodeId NodeId { get; } = CraftChainerNodeId.Create();
        
        public ChainerTransporterComponent() { }
        public ChainerTransporterComponent(Dictionary<string, string> componentStates) : this()
        {
            var state = componentStates[SaveKey];
            var jsonObject = JsonConvert.DeserializeObject<ChainerTransporterComponentJsonObject>(state);
            NodeId = new CraftChainerNodeId(jsonObject.NodeId);
        }
        
        
        public string SaveKey { get; } = typeof(ChainerTransporterComponent).FullName;
        public string GetSaveState()
        {
            return JsonConvert.SerializeObject(new ChainerTransporterComponentJsonObject(this));
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
    
    public class ChainerTransporterComponentJsonObject 
    {
        [JsonProperty("nodeId")] public int NodeId { get; set; }
        
        public ChainerTransporterComponentJsonObject(ChainerTransporterComponent component)
        {
            NodeId = component.NodeId.AsPrimitive();
        }
    }
}