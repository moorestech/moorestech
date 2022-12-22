using Game.Quest.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Test.Module.TestMod;

namespace Test.UnitTest.Game
{
    [TestFixture]
    public class ItemAcquisitionQuestTest
    {

        [Test]
        public void ItemAcquisitionCompleteTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var questConfig = serviceProvider.GetService<IQuestConfig>();
        }
    }
}