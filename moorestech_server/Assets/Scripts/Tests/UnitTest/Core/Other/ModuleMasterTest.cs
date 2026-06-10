using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Other
{
    public class ModuleMasterTest
    {
        // モジュールマスタが modules.json をロードし、GUIDから定義を引けることを検証
        // Verify ModuleMaster loads modules.json and resolves a definition by GUID
        [Test]
        public void LoadAndGetModuleTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            Assert.NotNull(MasterHolder.ModuleMaster);
            Assert.Greater(MasterHolder.ModuleMaster.Modules.Data.Length, 0);

            var element = MasterHolder.ModuleMaster.Modules.Data[0];
            var found = MasterHolder.ModuleMaster.GetModuleElement(element.ModuleGuid);
            Assert.AreEqual(element.ModuleGuid, found.ModuleGuid);
        }
    }
}
