using Newtonsoft.Json;

namespace Game.Construction
{
    // 財布ブロックはGuidで保存する（揮発BlockIdは保存しない）
    // The wallet block is saved as a GUID, never the volatile BlockId
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
