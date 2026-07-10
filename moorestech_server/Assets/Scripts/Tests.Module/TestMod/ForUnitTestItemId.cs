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

        public static ItemId TestSemiconductorChipLv1 => MasterHolder.ItemMaster.GetItemId(Guid.Parse("fa7bf6df-efc2-421d-b7bc-87be7b229345"));
        public static ItemId TestSemiconductorChipLv2 => MasterHolder.ItemMaster.GetItemId(Guid.Parse("d08f4a3b-50d5-4228-8ef2-9b479b79375b"));
        public static ItemId TestSemiconductorChipLv3 => MasterHolder.ItemMaster.GetItemId(Guid.Parse("868fc884-c0d1-48d7-b6e6-83b0517d381e"));
        public static ItemId TestSemiconductorChipLv4 => MasterHolder.ItemMaster.GetItemId(Guid.Parse("3d2b7bda-0f4d-4887-979f-9f37506fda25"));
        public static ItemId TestCleanRoomFilter => MasterHolder.ItemMaster.GetItemId(Guid.Parse("6e355e23-8e9e-46d2-9405-b0e6bb612890"));
        public static ItemId TestChipRawWafer => MasterHolder.ItemMaster.GetItemId(Guid.Parse("78274378-9a98-4826-8dea-80f7eb52a5ed"));
    }
}