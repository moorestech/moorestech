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
        
    }
}