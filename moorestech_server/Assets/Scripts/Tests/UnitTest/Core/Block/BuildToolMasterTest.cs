using System.Linq;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Block
{
    public class BuildToolMasterTest
    {
        [Test]
        public void BuildToolsをマスタからロードできる()
        {
            // DIコンテナ生成でMasterHolderがロードされる
            // Building the DI container loads MasterHolder
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            Assert.AreEqual(1, MasterHolder.BuildToolMaster.All.Count);
            var tool = MasterHolder.BuildToolMaster.All[0];
            Assert.AreEqual("blueprintCopy", tool.ToolType);
            Assert.AreEqual(tool.BuildToolGuid, MasterHolder.BuildToolMaster.GetBuildTool(tool.BuildToolGuid).BuildToolGuid);
        }
    }
}
