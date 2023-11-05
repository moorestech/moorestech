using Game.Quest.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.Quest
{
    public class QuestConfigTest
    {
        private const string ModId = "QuestAuthor:forQuestTest";

        [Test]
        public void QuestLoadTest()
        {
            var (packet, serviceProvider) =
                new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var questConfig = serviceProvider.GetService<IQuestConfig>();

            const string previousQuestId = ModId + ":";

            var ids = questConfig.GetQuestIds(ModId);
            Assert.AreEqual(previousQuestId + "Test1", ids[0]);
            Assert.AreEqual(previousQuestId + "Test2", ids[1]);
            Assert.AreEqual(previousQuestId + "Test3", ids[2]);
            Assert.AreEqual(previousQuestId + "Test4", ids[3]);

            //Test1のチェック
            var test1Quest = questConfig.GetQuestConfig(previousQuestId + "Test1");
            Assert.AreEqual(previousQuestId + "Test1", test1Quest.QuestId); //クエストIDのチェク
            Assert.AreEqual(0, test1Quest.PrerequisiteQuests.Count); //前提クエストのチェック
            Assert.AreEqual(QuestPrerequisiteType.And, test1Quest.QuestPrerequisiteType);
            Assert.AreEqual("Test Category", test1Quest.QuestCategory);
            Assert.AreEqual("ItemCraft", test1Quest.QuestType);
            Assert.AreEqual("Test Quest", test1Quest.QuestName);
            Assert.AreEqual("Test Quest Description", test1Quest.QuestDescription);
            Assert.AreEqual(3, test1Quest.UiPosition.X);
            Assert.AreEqual(5, test1Quest.UiPosition.Y);
            Assert.AreEqual(0, test1Quest.RewardItemStacks.Count);
            Assert.AreEqual("{\"modId\":\"Test Author:forUniTest\",\"name\":\"Test1\"}", test1Quest.QuestParameter);

            //Test2のチェック
            //これ以降は前提クエストのチェックだけを行うa
            var test2Quest = questConfig.GetQuestConfig(previousQuestId + "Test2");
            Assert.AreEqual(1, test2Quest.PrerequisiteQuests.Count); //前提クエストの数のチェック
            Assert.AreEqual(previousQuestId + "Test1", test2Quest.PrerequisiteQuests[0].QuestId);


            //Test3のチェック
            var test3Quest = questConfig.GetQuestConfig(previousQuestId + "Test3");
            Assert.AreEqual(2, test3Quest.PrerequisiteQuests.Count); //前提クエストの数のチェック
            Assert.AreEqual(previousQuestId + "Test1", test3Quest.PrerequisiteQuests[0].QuestId);
            Assert.AreEqual(previousQuestId + "Test2", test3Quest.PrerequisiteQuests[1].QuestId);


            //Test4のチェック
            var test4Quest = questConfig.GetQuestConfig(previousQuestId + "Test4");
            Assert.AreEqual(2, test4Quest.PrerequisiteQuests.Count); //前提クエストの数のチェック
            Assert.AreEqual(previousQuestId + "Test1", test4Quest.PrerequisiteQuests[0].QuestId);
            Assert.AreEqual(previousQuestId + "Test2", test4Quest.PrerequisiteQuests[1].QuestId);
            Assert.AreEqual(QuestPrerequisiteType.Or, test4Quest.QuestPrerequisiteType);
        }
    }
}