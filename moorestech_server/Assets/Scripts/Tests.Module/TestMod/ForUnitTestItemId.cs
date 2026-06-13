using System;
using Core.Master;

namespace Tests.Module.TestMod
{
    public class ForUnitTestItemId
    {
        public static readonly ItemId ItemId1 = new(1);
        public static readonly ItemId ItemId2 = new(2);
        public static readonly ItemId ItemId3 = new(3);
        public static readonly ItemId ItemId4 = new(4);
        public static ItemId TrainCarItem => MasterHolder.ItemMaster.GetItemId(Guid.Parse("20000000-0000-0000-0000-000000000000"));

        // IC チップ Lv1-Lv4 の ItemId アクセサ（決定的 GUID 手書き）。
        // ItemId accessors for IC chip Lv1-Lv4 (deterministic handwritten GUIDs).
        public static ItemId IcChipLv1 => MasterHolder.ItemMaster.GetItemId(Guid.Parse("3a000000-0000-0000-0000-000000000001"));
        public static ItemId IcChipLv2 => MasterHolder.ItemMaster.GetItemId(Guid.Parse("3a000000-0000-0000-0000-000000000002"));
        public static ItemId IcChipLv3 => MasterHolder.ItemMaster.GetItemId(Guid.Parse("3a000000-0000-0000-0000-000000000003"));
        public static ItemId IcChipLv4 => MasterHolder.ItemMaster.GetItemId(Guid.Parse("3a000000-0000-0000-0000-000000000004"));
    }
}