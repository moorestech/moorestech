using Newtonsoft.Json;

namespace Game.CleanRoom.Save
{
    public class CleanRoomCellSaveJsonObject
    {
        [JsonProperty("x")] public int X;
        [JsonProperty("y")] public int Y;
        [JsonProperty("z")] public int Z;
    }
}
