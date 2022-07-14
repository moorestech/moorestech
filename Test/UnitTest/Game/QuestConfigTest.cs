using System.Collections.Generic;
using Core.ConfigJson;
using Core.Item;
using Game.Quest;
using Game.Quest.Config;
using Game.Quest.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Test.Module.TestConfig;
using Test.Module.TestMod;

namespace Test.UnitTest.Game
{
    public class QuestConfigTest
    {

        private const string ModName = "QuestTest";
        
        [Test]
        public void QuestLoadTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            
            var configJson = new ConfigJson(ModName,"","","","","",
                TestModuleConfig.QuestConfigUintTestJson);
            var questOnlyConfig = new ConfigJsonList(new (){{ModName,configJson}});

            IQuestConfig questConfig = new QuestConfig(questOnlyConfig,itemStackFactory);


            var ids = questConfig.GetQuestIds(ModName);
            Assert.AreEqual("Test1",ids[0]);
            Assert.AreEqual("Test2",ids[1]);
            Assert.AreEqual("Test3",ids[2]);
            Assert.AreEqual("Test4",ids[3]);
            
            //Test1のチェック
            var test1Quest = questConfig.GetQuestConfig("Test1");
            Assert.AreEqual("Test1",test1Quest.QuestId); //クエストIDのチェク
            Assert.AreEqual(0,test1Quest.PrerequisiteQuests.Count); //前提クエストのチェック
            Assert.AreEqual(QuestPrerequisiteType.And,test1Quest.QuestPrerequisiteType);
            Assert.AreEqual("Test Category",test1Quest.QuestCategory);
            Assert.AreEqual("ItemCraft",test1Quest.QuestType);
            Assert.AreEqual("Test Quest",test1Quest.QuestName);
            Assert.AreEqual("Test Quest Description",test1Quest.QuestDescription);
            Assert.AreEqual(3,test1Quest.UiPosition.X);
            Assert.AreEqual(5,test1Quest.UiPosition.Y);
            Assert.AreEqual(0,test1Quest.RewardItemStacks.Count);
            Assert.AreEqual("{\"id\":1,\"count\":1}",test1Quest.QuestParameter);
            
            //Test2のチェック
            //これ以降は前提クエストのチェックだけを行う
            var test2Quest = questConfig.GetQuestConfig("Test2");
            Assert.AreEqual(1,test2Quest.PrerequisiteQuests.Count); //前提クエストの数のチェック
            Assert.AreEqual("Test1",test2Quest.PrerequisiteQuests[0].QuestId);
            
            
            //Test3のチェック
            var test3Quest = questConfig.GetQuestConfig("Test3");
            Assert.AreEqual(2,test3Quest.PrerequisiteQuests.Count); //前提クエストの数のチェック
            Assert.AreEqual("Test1",test3Quest.PrerequisiteQuests[0].QuestId);
            Assert.AreEqual("Test2",test3Quest.PrerequisiteQuests[1].QuestId);
            
            
            //Test4のチェック
            var test4Quest = questConfig.GetQuestConfig("Test4");
            Assert.AreEqual(2,test4Quest.PrerequisiteQuests.Count); //前提クエストの数のチェック
            Assert.AreEqual("Test1",test4Quest.PrerequisiteQuests[0].QuestId);
            Assert.AreEqual("Test2",test4Quest.PrerequisiteQuests[1].QuestId);
            Assert.AreEqual(QuestPrerequisiteType.Or,test4Quest.QuestPrerequisiteType);
        }
    }
}