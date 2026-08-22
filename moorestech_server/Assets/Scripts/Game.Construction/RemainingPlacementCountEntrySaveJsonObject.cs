using Newtonsoft.Json;

namespace Game.Construction
{
    // 財布ブロックはGuidで保存（揮発BlockId不可）
    // The wallet block is saved as a GUID (never the volatile BlockId)
    public class RemainingPlacementCountEntrySaveJsonObject
    {
        [JsonProperty("BlockGuid")] public string BlockGuid;
        [JsonProperty("Count")] public int Count;

        public RemainingPlacementCountEntrySaveJsonObject()
        {
        }

        internal RemainingPlacementCountEntrySaveJsonObject(string blockGuid, int count)
        {
            BlockGuid = blockGuid;
            Count = count;
        }
    }
}
