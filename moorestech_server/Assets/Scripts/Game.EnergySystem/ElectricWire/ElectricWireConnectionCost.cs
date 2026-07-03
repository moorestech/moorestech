using Core.Master;

namespace Game.EnergySystem
{
    /// <summary>
    /// ワイヤー1本の接続に消費した電線アイテムの情報。切断・撤去時の返却に使う
    /// Wire item consumption info per wire, used for refund on disconnect or removal
    /// </summary>
    public readonly struct ElectricWireConnectionCost
    {
        public readonly ItemId ItemId;
        public readonly int Count;

        public ElectricWireConnectionCost(ItemId itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }
}
