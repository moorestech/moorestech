using Client.Ui.State.BuildMenu.Logic;
using Core.Master;

namespace Client.Ui.State.BuildMenu.Entries
{
    /// <summary>
    /// 必要素材 1 行。絵と名前を引く ItemId と、不足判定の BuildMenuRequiredItem を対で持つ
    /// One required material: the ItemId for picture and name, paired with the BuildMenuRequiredItem for shortage checks
    /// </summary>
    public sealed class BuildMenuMaterialRow
    {
        public ItemId ItemId { get; }
        public BuildMenuRequiredItem Item { get; }

        public BuildMenuMaterialRow(ItemId itemId, BuildMenuRequiredItem item)
        {
            ItemId = itemId;
            Item = item;
        }
    }

    // VM が持つ素材の行。不足は VM の ctor で 1 回だけ判定し、ツールチップと詳細はこの値を読む
    // A material line held by the VM; shortage is judged once in the VM ctor and the tooltip and detail read this value
    public sealed class BuildMenuMaterialLine
    {
        public ItemId ItemId { get; }
        public BuildMenuRequiredItem Item { get; }
        public bool IsInsufficient { get; }

        public BuildMenuMaterialLine(BuildMenuMaterialRow row, bool isInsufficient)
        {
            ItemId = row.ItemId;
            Item = row.Item;
            IsInsufficient = isInsufficient;
        }
    }
}
