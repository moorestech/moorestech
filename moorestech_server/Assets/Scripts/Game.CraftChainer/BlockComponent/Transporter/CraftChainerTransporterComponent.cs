using System.Collections.Generic;
using Game.CraftChainer.CraftNetwork;
using Newtonsoft.Json;

namespace Game.CraftChainer.BlockComponent
{
    public class CraftChainerTransporterComponent : ICraftChainerNode
    {
        public CraftChainerNodeId NodeId { get; } = CraftChainerNodeId.Create();
        
        public CraftChainerTransporterComponent() { }
        public CraftChainerTransporterComponent(Dictionary<string, string> componentStates) : this()
        {
            var state = componentStates[SaveKey];
            var jsonObject = JsonConvert.DeserializeObject<ChainerTransporterComponentJsonObject>(state);
            NodeId = new CraftChainerNodeId(jsonObject.NodeId);
        }
        
        
        public string SaveKey { get; } = typeof(CraftChainerTransporterComponent).FullName;
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
        
        public ChainerTransporterComponentJsonObject(){}
        public ChainerTransporterComponentJsonObject(CraftChainerTransporterComponent component)
        {
            NodeId = component.NodeId.AsPrimitive();
        }
    }
}