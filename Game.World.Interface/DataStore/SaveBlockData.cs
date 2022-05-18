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

        [JsonProperty("X")] public int X { get; }
        [JsonProperty("Y")] public int Y { get; }
        [JsonProperty("blockHash")] public ulong BlockHash { get; }
        [JsonProperty("entityId")] public int EntityId { get; }
        [JsonProperty("state")] public string State { get; }
        [JsonProperty("direction")] public int Direction { get; }
    }
}