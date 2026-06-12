using System;
using System.Linq;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Other
{
    /// <summary>
    ///     アイテムマスタのmoduleParam設定のロードを検証するテスト
    ///     Tests verifying the moduleParam settings load on the item master
    /// </summary>
    public class ItemModuleParamTest
    {
        private static readonly Guid SpeedModuleItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private static readonly Guid NonModuleItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        // items.jsonのmoduleParamがロードされ、アイテムから設定を引けることを検証
        // Verify moduleParam in items.json loads and is accessible from the item element
        [Test]
        public void LoadAndGetModuleParamTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // moduleParamを持つアイテムがフィクスチャどおり5種であること
            // Exactly five items carry moduleParam, matching the fixture
            Assert.AreEqual(5, MasterHolder.ItemMaster.Items.Data.Count(item => item.ModuleParam != null));

            // TestSpeedModule（Test1アイテム）の具体的なフィクスチャ内容を検証
            // Verify the concrete fixture content of the speed module item (Test1)
            var speedModuleParam = MasterHolder.ItemMaster.GetItemMaster(SpeedModuleItemGuid).ModuleParam;
            Assert.NotNull(speedModuleParam);
            Assert.AreEqual("Speed", speedModuleParam.EffectAxis);
            Assert.AreEqual(1, speedModuleParam.Tier);
            Assert.AreEqual(0.5f, speedModuleParam.EffectValue);
            Assert.AreEqual(0.5f, speedModuleParam.TradeoffValue);

            // モジュールでないアイテムはmoduleParamがnullであること
            // A non-module item has a null moduleParam
            Assert.IsNull(MasterHolder.ItemMaster.GetItemMaster(NonModuleItemGuid).ModuleParam);
        }
    }
}
