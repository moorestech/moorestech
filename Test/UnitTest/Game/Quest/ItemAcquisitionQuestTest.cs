#if NET6_0
using Game.Quest.Factory;
using Game.Quest.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Test.Module.TestMod;

namespace Test.UnitTest.Game.Quest
{
    [TestFixture]
    public class ItemAcquisitionQuestTest
    {
        [Test]
        public void ItemAcquisitionCompleteTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var questFactory = serviceProvider.GetService<QuestFactory>();

            var quests = questFactory.CreateQuests();
            var itemAcquisitionQuest = quests.Find(q => q.QuestConfig.QuestType == VanillaQuestTypes.ItemAcquisitionQuestType);

            
        }
    }
}
#endif