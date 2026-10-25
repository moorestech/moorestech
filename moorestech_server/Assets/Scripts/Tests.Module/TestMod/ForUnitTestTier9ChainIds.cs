using System;
using Core.Master;

namespace Tests.Module.TestMod
{
    /// <summary>
    ///     G4 結合テストの連鎖の GUID と ID
    ///     GUIDs and IDs of the G4 chain test
    /// </summary>
    public static class ForUnitTestTier9ChainIds
    {
        public static readonly Guid IcChipBaseGuid = Guid.Parse("00000000-0000-0000-c0de-000000000041");
        public static readonly Guid GpuBaseGuid = Guid.Parse("00000000-0000-0000-c0de-000000000051");
        public static readonly Guid GpuSatelliteLv1Guid = Guid.Parse("00000000-0000-0000-c0de-000000000061");
        public static readonly Guid GpuSatelliteLv2Guid = Guid.Parse("00000000-0000-0000-c0de-000000000062");

        public static readonly Guid GpuRecipeGuid = Guid.Parse("00000000-0000-4000-8000-0000c0de0011");
        public static readonly Guid GpuSatelliteLv1RecipeGuid = Guid.Parse("00000000-0000-4000-8000-0000c0de0012");
        public static readonly Guid GpuSatelliteLv2RecipeGuid = Guid.Parse("00000000-0000-4000-8000-0000c0de0013");

        public static readonly Guid RocketTechnologyResearchGuid = Guid.Parse("c0de0000-0000-4000-8000-000000000011");
        public static readonly Guid GpuUpgradeLv2ResearchGuid = Guid.Parse("c0de0000-0000-4000-8000-000000000012");
        public static readonly Guid OrbitalElevatorConstructionResearchGuid = Guid.Parse("c0de0000-0000-4000-8000-000000000013");

        public static readonly Guid RocketLaunchPadBlockGuid = Guid.Parse("5c8e0000-0000-4000-8000-000000000100");
        public static readonly Guid SpaceElevatorBaseBlockGuid = Guid.Parse("5e1ea7a0-0001-4000-8000-000000000001");

        public static ItemId IcChip(int level) => Variant(IcChipBaseGuid, level);
        public static ItemId Gpu(int level) => Variant(GpuBaseGuid, level);

        // 衛星はレベル別アイテムなので GUID で引く
        // Satellites are per-level items, so resolve them by GUID
        public static ItemId GpuSatellite(int level)
        {
            var guid = level switch
            {
                1 => GpuSatelliteLv1Guid,
                2 => GpuSatelliteLv2Guid,
                _ => throw new ArgumentOutOfRangeException(nameof(level), level, "テスト mod の GPU衛星は Lv1 と Lv2 だけです"),
            };
            return MasterHolder.ItemMaster.GetItemId(guid);
        }

        private static ItemId Variant(Guid baseGuid, int level)
        {
            return MasterHolder.ItemMaster.GetLevelVariantItemId(MasterHolder.ItemMaster.GetItemId(baseGuid), level);
        }
    }
}
