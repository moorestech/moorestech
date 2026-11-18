using Core.Master;

namespace Client.Ui.State.Items
{
    /// <summary>
    /// アイテムと個数の組。レシピの必要素材・生産物に使う
    /// An item paired with a count; used for recipe requirements and outputs
    /// </summary>
    public readonly struct ItemAmount
    {
        public readonly ItemId ItemId;
        public readonly int Count;

        public ItemAmount(ItemId itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }
}
