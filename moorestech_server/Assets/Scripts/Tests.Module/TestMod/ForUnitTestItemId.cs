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

        // スループット回帰テスト用。1スロットに1,000,000個積める
        // For the throughput regression suite; one slot holds 1,000,000
        public static ItemId ThroughputTestItem => MasterHolder.ItemMaster.GetItemId(Guid.Parse("7b3e1a10-0002-4000-8000-000000000001"));

        // ロケット発射台のテスト用資材・積荷・成果
        // Test materials, payloads and arrival output for the rocket launch pad
        public static ItemId RocketCaseId => MasterHolder.ItemMaster.GetItemId(Guid.Parse("5c8e0000-0000-4000-8000-000000000001"));
        public static ItemId RocketFuelId => MasterHolder.ItemMaster.GetItemId(Guid.Parse("5c8e0000-0000-4000-8000-000000000002"));
        public static ItemId RocketComputerId => MasterHolder.ItemMaster.GetItemId(Guid.Parse("5c8e0000-0000-4000-8000-000000000003"));
        public static ItemId SatellitePayloadId => MasterHolder.ItemMaster.GetItemId(Guid.Parse("5c8e0000-0000-4000-8000-000000000004"));
        public static ItemId CablePayloadId => MasterHolder.ItemMaster.GetItemId(Guid.Parse("5c8e0000-0000-4000-8000-000000000005"));
        public static ItemId ComputeDataId => MasterHolder.ItemMaster.GetItemId(Guid.Parse("5c8e0000-0000-4000-8000-000000000006"));
    }
}