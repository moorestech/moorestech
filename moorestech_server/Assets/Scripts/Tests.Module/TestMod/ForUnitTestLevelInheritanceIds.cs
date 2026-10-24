using System;
using Core.Master;

namespace Tests.Module.TestMod
{
    /// <summary>
    ///     Part C のテストマスタ追加分（継承レシピ・研究・機械）の GUID と ID
    ///     GUIDs and IDs of Part C's test-master additions (inheritance recipes, research, machine)
    /// </summary>
    public static class ForUnitTestLevelInheritanceIds
    {
        public static readonly Guid SourceBaseGuid = Guid.Parse("00000000-0000-0000-c0de-000000000011");
        public static readonly Guid TargetBaseGuid = Guid.Parse("00000000-0000-0000-c0de-000000000021");
        public static readonly Guid CappedTargetBaseGuid = Guid.Parse("00000000-0000-0000-c0de-000000000031");

        public static readonly Guid InheritRecipeGuid = Guid.Parse("00000000-0000-4000-8000-0000c0de0001");
        public static readonly Guid CappedInheritRecipeGuid = Guid.Parse("00000000-0000-4000-8000-0000c0de0002");

        public static readonly Guid CraftCapResearchGuid = Guid.Parse("c0de0000-0000-4000-8000-000000000001");
        public static readonly Guid ModuleSlotStackResearchGuid = Guid.Parse("c0de0000-0000-4000-8000-000000000002");

        public static BlockId MachineId => MasterHolder.BlockMaster.GetBlockId(Guid.Parse("ffffffff-0000-4000-8000-00000000c0de"));

        public static ItemId Source(int level) => Variant(SourceBaseGuid, level);
        public static ItemId Target(int level) => Variant(TargetBaseGuid, level);
        public static ItemId CappedTarget(int level) => Variant(CappedTargetBaseGuid, level);

        private static ItemId Variant(Guid baseGuid, int level)
        {
            return MasterHolder.ItemMaster.GetLevelVariantItemId(MasterHolder.ItemMaster.GetItemId(baseGuid), level);
        }
    }
}
