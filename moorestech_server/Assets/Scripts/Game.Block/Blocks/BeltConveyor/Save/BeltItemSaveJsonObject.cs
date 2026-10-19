using Core.BeltTransport;
using Core.Master;
using Newtonsoft.Json;

namespace Game.Block.Blocks.BeltConveyor.Save
{
    // ベルコン上のアイテム1個の保存形。種類はGUID、進入方向は12方向の値、距離は先頭から出口までの0〜255
    // Save form of one belt item: kind as GUID, entry direction as the 12-way value, distance from the head to the exit in 0..255
    // 個体IDは保存せず、ロード時に振り直す
    // The instance id is not saved and is reissued on load
    public class BeltItemSaveJsonObject
    {
        [JsonProperty("itemGuid")] public string ItemGuidStr;
        [JsonProperty("entryDirection")] public int EntryDirection;
        [JsonProperty("distanceToExit")] public int DistanceToExit;

        public BeltItemSaveJsonObject()
        {
        }

        public BeltItemSaveJsonObject(in BeltItem item, int distanceToExit)
        {
            ItemGuidStr = MasterHolder.ItemMaster.GetItemGuid(item.ItemId).ToString();
            EntryDirection = (int)item.EntryDirection;
            DistanceToExit = distanceToExit;
        }
    }
}
