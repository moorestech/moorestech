using Newtonsoft.Json;

namespace Game.World.Interface
{
    public class SaveBlockData
    {
        public SaveBlockData(int x, int y, int blockId, int intId,string state)
        {
            X = x;
            Y = y;
            BlockId = blockId;
            IntId = intId;
            State = state;
        }

        [JsonProperty("X")]
        public int X { get; }
        [JsonProperty("Y")]
        public int Y { get; }
        [JsonProperty("id")]
        public int BlockId { get; }
        [JsonProperty("intId")]
        public int IntId { get; }
        [JsonProperty("state")]
        public string State { get;}
        
    }
}