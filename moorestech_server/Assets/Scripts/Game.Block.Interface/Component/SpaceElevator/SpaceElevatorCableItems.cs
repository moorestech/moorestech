using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Interface.Component
{
    // ケーブルプールへ足す積荷か（全拠点のケーブル品）。マスタだけから決まり、サーバーのプールとクライアントの表示が同じ口を読む
    // Whether a payload feeds the cable pool (any base's cable item); derived from the master alone and read by both the server pool and the client display
    public static class SpaceElevatorCableItems
    {
        public static bool Contains(ItemId itemId)
        {
            // 拠点の無いマスタでは何も数えない。種類の整合はマスタ読込時の検証が持つ
            // A master without bases counts nothing; consistency across bases is checked at master load
            foreach (var block in MasterHolder.BlockMaster.Blocks.Data)
            {
                if (block.BlockParam is SpaceElevatorBaseBlockParam param && MasterHolder.ItemMaster.GetItemId(param.CableItemGuid) == itemId) return true;
            }
            return false;
        }
    }
}
