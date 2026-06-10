using System;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Other
{
    /// <summary>
    ///     LevelFamilyMasterの変種解決とテストデータのGUID整合を検証するテスト
    ///     Tests verifying LevelFamilyMaster variant resolution and the GUID sync of the test data
    /// </summary>
    public class LevelFamilyMasterTest
    {
        // テストファミリーの基準アイテム(Test3)とそのLv2変種（items.json/levelFamilies.jsonと一致させる）
        // Base item of the test family (Test3) and its Lv2 variant (matches items.json/levelFamilies.json)
        private static readonly Guid BaseItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        private static readonly Guid Lv2ItemGuid = Guid.Parse("f6d1a313-93a3-f429-3884-0ae12b1f4a05");
        private static readonly Guid NonFamilyItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        // 基準ItemIdとレベル番号から変種ItemIdを解決でき、範囲外はクランプされることを検証
        // Verify variant ItemIds resolve from base ItemId + level and out-of-range levels clamp
        [Test]
        public void ResolveVariantTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            Assert.NotNull(MasterHolder.LevelFamilyMaster);
            var baseItemId = MasterHolder.ItemMaster.GetItemId(BaseItemGuid);
            var lv2ItemId = MasterHolder.ItemMaster.GetItemId(Lv2ItemGuid);
            Assert.IsTrue(MasterHolder.LevelFamilyMaster.HasFamily(baseItemId));

            // レベル1は基準アイテム自身、レベル2は別IDの変種であること
            // Level 1 resolves to the base item itself; level 2 resolves to a distinct variant
            Assert.AreEqual(baseItemId, MasterHolder.LevelFamilyMaster.GetVariantItemId(baseItemId, 1));
            var lv2 = MasterHolder.LevelFamilyMaster.GetVariantItemId(baseItemId, 2);
            Assert.AreNotEqual(baseItemId, lv2);
            Assert.AreEqual(lv2ItemId, lv2);

            // 範囲外レベルは[1, 最大レベル]へクランプされ、最大レベルIDとも一致すること
            // Out-of-range levels clamp into [1, max level] and match the max level ItemId
            Assert.AreEqual(MasterHolder.LevelFamilyMaster.GetMaxLevelItemId(baseItemId), MasterHolder.LevelFamilyMaster.GetVariantItemId(baseItemId, 999));
            Assert.AreEqual(lv2ItemId, MasterHolder.LevelFamilyMaster.GetVariantItemId(baseItemId, 999));
            Assert.AreEqual(baseItemId, MasterHolder.LevelFamilyMaster.GetVariantItemId(baseItemId, 0));

            // ファミリー未登録のアイテムはHasFamilyがfalseを返すこと
            // HasFamily returns false for items without a family
            var nonFamilyItemId = MasterHolder.ItemMaster.GetItemId(NonFamilyItemGuid);
            Assert.IsFalse(MasterHolder.LevelFamilyMaster.HasFamily(nonFamilyItemId));
        }

        // items.jsonのLv2変種GUIDがDeterministicGuidUtilの算出値と同期していることを検証
        // Verify the Lv2 variant GUID in items.json stays in sync with the DeterministicGuidUtil output
        [Test]
        public void Lv2GuidMatchesDeterministicGuidTest()
        {
            var computed = DeterministicGuidUtil.Create("00000000-0000-0000-1234-000000000003:lv2");
            Assert.AreEqual(Lv2ItemGuid, computed);
        }
    }
}
