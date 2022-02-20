using Newtonsoft.Json;

namespace Game.World.Interface.DataStore
{
    public class SaveBlockData
    {
        public SaveBlockData(int x, int y, int blockId, int entityId, string state, int direction)
        {
            X = x;
            Y = y;
            BlockId = blockId;
            EntityId = entityId;
            State = state;
            Direction = direction;
        }

        [JsonProperty("X")] public int X { get; }
        [JsonProperty("Y")] public int Y { get; }
        [JsonProperty("id")] public int BlockId { get; }
        [JsonProperty("entityId")] public int EntityId { get; }
        [JsonProperty("state")] public string State { get; }
        [JsonProperty("direction")] public int Direction { get; }
    }
}