using Core.Item.Interface;
using Core.Master;

namespace Game.Block.Blocks.BeltConveyor
{
    /// <summary>
    /// 張替えで退避した搬送品。進行率は「残りtick / 総tick」で0〜1
    /// A transit item held aside during replace; RemainingRate is remainingTicks / totalTicks in 0..1
    /// </summary>
    public readonly struct BeltTransitItem
    {
        public readonly ItemId ItemId;
        public readonly ItemInstanceId ItemInstanceId;
        public readonly double RemainingRate;

        public BeltTransitItem(ItemId itemId, ItemInstanceId itemInstanceId, double remainingRate)
        {
            ItemId = itemId;
            ItemInstanceId = itemInstanceId;
            RemainingRate = remainingRate;
        }
    }
}
