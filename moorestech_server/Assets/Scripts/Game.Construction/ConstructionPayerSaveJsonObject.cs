using Newtonsoft.Json;

namespace Game.Construction
{
    // 課金元プレイヤーはブロックインスタンスIDで保存する（位置は複数セルに跨るため使えない）
    // The paying player is saved against the block instance id, since a position can span multiple cells
    public class ConstructionPayerSaveJsonObject
    {
        [JsonProperty("BlockInstanceId")] public int BlockInstanceId;
        [JsonProperty("PlayerId")] public int PlayerId;

        public ConstructionPayerSaveJsonObject()
        {
        }

        internal ConstructionPayerSaveJsonObject(int blockInstanceId, int playerId)
        {
            BlockInstanceId = blockInstanceId;
            PlayerId = playerId;
        }
    }
}
