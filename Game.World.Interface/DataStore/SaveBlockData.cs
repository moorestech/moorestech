using Newtonsoft.Json;

namespace Game.World.Interface.DataStore
{
    public class SaveBlockData
    {
        public SaveBlockData(int x, int y, ulong blocHash, int entityId, string state, int direction)
        {
            
            X = x;
            Y = y;
            BlockHash = blocHash;
            EntityId = entityId;
            State = state;
            Direction = direction;
        }

        [JsonProperty("X")] public int X;
        [JsonProperty("Y")] public int Y;
        [JsonProperty("blockHash")] public ulong BlockHash;
        [JsonProperty("entityId")] public int EntityId;
        [JsonProperty("state")] public string State;
        [JsonProperty("direction")] public int Direction;
    }
}