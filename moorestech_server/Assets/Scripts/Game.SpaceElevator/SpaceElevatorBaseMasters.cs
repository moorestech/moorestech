using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Game.SpaceElevator
{
    // - マスタ内の拠点を列挙
    // - 参照ケーブルを導出
    // - Enumerate bases in the master
    // - Derive their referenced cable items
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
