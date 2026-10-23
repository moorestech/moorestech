using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Game.SpaceElevator
{
    // マスタにある宇宙エレベーター拠点の列挙と、拠点が数えるケーブルの導出
    // Enumerates the space elevator bases in the master and derives the cable items they count
    internal static class SpaceElevatorBaseMasters
    {
        internal static IEnumerable<(string BlockName, SpaceElevatorBaseBlockParam Param)> All()
        {
            foreach (var block in MasterHolder.BlockMaster.Blocks.Data)
            {
                if (block.BlockParam is SpaceElevatorBaseBlockParam param) yield return (block.Name, param);
            }
        }

        internal static HashSet<ItemId> CableItemIds()
        {
            var ids = new HashSet<ItemId>();
            foreach (var (_, param) in All()) ids.Add(MasterHolder.ItemMaster.GetItemId(param.CableItemGuid));
            return ids;
        }
    }
}
