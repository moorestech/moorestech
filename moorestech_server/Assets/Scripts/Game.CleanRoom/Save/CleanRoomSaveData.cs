using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.CleanRoom.Save
{
    public class CleanRoomSaveData
    {
        [JsonProperty("impurityCount")] public double ImpurityCount;
        [JsonProperty("className")] public string ClassName;

        // セル座標は配列順序に依存させず、JSONキーで永続化する
        // Persist cell coordinates by JSON keys instead of positional array order
        [JsonProperty("cells")] public List<CleanRoomCellSaveJsonObject> Cells;
    }
}
