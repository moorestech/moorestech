using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.CleanRoom.SaveLoad
{
    // 1部屋（または孤立状態）1レコード。V/S は再検出から再導出するため保存しない。
    // One record per room (or orphan); V/S re-derived from detection, so not saved.
    public class CleanRoomSaveData
    {
        [JsonProperty("impurityCount")] public double ImpurityCount;
        [JsonProperty("thresholdIndex")] public int ThresholdIndex;
        [JsonProperty("status")] public int Status;
        [JsonProperty("graceRemainingSeconds")] public float GraceRemainingSeconds;

        // 同一性照合用の全セル署名（x,y,z の配列の配列）。
        // Full-cell signature for identity matching.
        [JsonProperty("cells")] public List<int[]> Cells;
    }
}
