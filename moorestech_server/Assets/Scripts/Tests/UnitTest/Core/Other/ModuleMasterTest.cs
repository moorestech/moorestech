using System;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Other
{
    public class ModuleMasterTest
    {
        private static readonly Guid SpeedModuleGuid = Guid.Parse("00000000-0000-0000-5678-000000000001");
        private static readonly Guid SpeedModuleItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private static readonly Guid NonModuleItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        // モジュールマスタが modules.json をロードし、GUIDから定義を引けることを検証
        // Verify ModuleMaster loads modules.json and resolves a definition by GUID
        [Test]
        public void LoadAndGetModuleTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            Assert.NotNull(MasterHolder.ModuleMaster);
            Assert.AreEqual(5, MasterHolder.ModuleMaster.Modules.Data.Length);

            // TestSpeedModule の具体的なフィクスチャ内容を検証
            // Verify the concrete fixture content of TestSpeedModule
            var speedModule = MasterHolder.ModuleMaster.GetModuleElement(SpeedModuleGuid);
            Assert.AreEqual(SpeedModuleGuid, speedModule.ModuleGuid);
            Assert.AreEqual("TestSpeedModule", speedModule.Name);
            Assert.AreEqual(SpeedModuleItemGuid, speedModule.ItemGuid);
            Assert.AreEqual("Speed", speedModule.EffectAxis);
            Assert.AreEqual(1, speedModule.Tier);
            Assert.AreEqual(0.5f, speedModule.EffectValue);
            Assert.AreEqual(0.5f, speedModule.TradeoffValue);
        }

        // アイテムGUIDからのモジュール解決（ヒットとミスの両方）を検証
        // Verify module resolution by item GUID (both hit and miss)
        [Test]
        public void GetModuleElementByItemGuidOrNullTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // モジュールとして登録されたアイテムGUIDは定義を返す
            // An item GUID registered as a module returns its definition
            var speedModule = MasterHolder.ModuleMaster.GetModuleElementByItemGuidOrNull(SpeedModuleItemGuid);
            Assert.NotNull(speedModule);
            Assert.AreEqual("TestSpeedModule", speedModule.Name);
            Assert.AreEqual(SpeedModuleGuid, speedModule.ModuleGuid);

            // モジュールでないアイテムGUIDは null を返す
            // A non-module item GUID returns null
            var nonModule = MasterHolder.ModuleMaster.GetModuleElementByItemGuidOrNull(NonModuleItemGuid);
            Assert.IsNull(nonModule);
        }
    }
}
