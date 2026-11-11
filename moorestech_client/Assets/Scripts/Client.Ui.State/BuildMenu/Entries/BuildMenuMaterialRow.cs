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

    // VM が持つ素材の行。Row を丸ごと持ち、不足は VM の ctor で 1 回だけ判定した値を足す
    // A material line held by the VM; wraps the Row whole and adds the shortage judged once in the VM ctor
    public sealed class BuildMenuMaterialLine
    {
        public BuildMenuMaterialRow Row { get; }
        public bool IsInsufficient { get; }

        public BuildMenuMaterialLine(BuildMenuMaterialRow row, bool isInsufficient)
        {
            Row = row;
            IsInsufficient = isInsufficient;
        }
    }
}
