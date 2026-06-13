using System;
using NUnit.Framework;
using Core.Master;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Other
{
    /// <summary>
    ///     アイテムマスタのroot modules定義のロードと装着アイテムからの解決を検証するテスト
    ///     Tests verifying the root modules load on the item master and resolution from the equipped item
    /// </summary>
    public class ItemModuleParamTest
    {
        private static readonly Guid SpeedModuleItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private static readonly Guid NonModuleItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        // items.jsonのroot modulesがロードされ、装着アイテムIdからモジュール定義を引けることを検証
        // Verify root modules in items.json load and are resolvable from the equipped item id
        [Test]
        public void LoadAndGetModuleParamTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // モジュール定義がフィクスチャどおり5種であること
            // Exactly five modules exist, matching the fixture
            Assert.AreEqual(5, MasterHolder.ItemMaster.Items.Modules.Length);

            // TestSpeedModule（Test1アイテム）の具体的なフィクスチャ内容を検証
            // Verify the concrete fixture content of the speed module item (Test1)
            var speedModuleItemId = MasterHolder.ItemMaster.GetItemId(SpeedModuleItemGuid);
            var speedModule = MasterHolder.ItemMaster.GetModuleByItemIdOrNull(speedModuleItemId);
            Assert.NotNull(speedModule);
            Assert.AreEqual("Speed", speedModule.EffectAxis);
            Assert.AreEqual(1, speedModule.Tier);
            Assert.AreEqual(0.5f, speedModule.EffectValue);
            Assert.AreEqual(0.5f, speedModule.TradeoffValue);

            // モジュールでないアイテムは解決結果がnullであること
            // A non-module item resolves to null
            var nonModuleItemId = MasterHolder.ItemMaster.GetItemId(NonModuleItemGuid);
            Assert.IsNull(MasterHolder.ItemMaster.GetModuleByItemIdOrNull(nonModuleItemId));
        }
    }
}
