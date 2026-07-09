using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.CleanRoom.Save
{
    public class CleanRoomSaveData
    {
        [JsonProperty("impurityCount")] public double ImpurityCount;
        [JsonProperty("className")] public string ClassName;
        [JsonProperty("cells")] public List<int[]> Cells;
    }
}
