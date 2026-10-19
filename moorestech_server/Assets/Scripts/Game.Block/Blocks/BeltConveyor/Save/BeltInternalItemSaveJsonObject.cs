using Core.BeltTransport;
using Newtonsoft.Json;

namespace Game.Block.Blocks.BeltConveyor.Save
{
    // 合流blockが入力方向ごとに持つ内部segment上のアイテム1個の保存形
    // Save form of one item on an internal segment, kept by the merge block per input direction
    public class BeltInternalItemSaveJsonObject
    {
        [JsonProperty("inputDirection")] public int InputDirection;
        [JsonProperty("item")] public BeltItemSaveJsonObject Item;

        public BeltInternalItemSaveJsonObject()
        {
        }

        public BeltInternalItemSaveJsonObject(BeltDirection inputDirection, in BeltItem item, int distanceToExit)
        {
            InputDirection = (int)inputDirection;
            Item = new BeltItemSaveJsonObject(item, distanceToExit);
        }
    }
}
