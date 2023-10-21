#if NET6_0
using System.Reflection;
using Game.PlayerInventory.Interface.Event;
using Game.Quest.Interface;
using Game.Quest.Interface.Extension;
using Game.Quest.QuestEntity;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PlayerInventory.Event;
using Server.Boot;
using Test.Module.TestMod;

namespace Test.UnitTest.Game.Quest
{
    public class CraftingQuestTest
    {
        private const int PlayerId = 1;


        ///     

        [Test]
        public void OnePreRequestQuestTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            var questDataStore = serviceProvider.GetService<IQuestDataStore>();
            var craftEvent = serviceProvider.GetService<ICraftingEvent>();

            var test1Quest = (ItemCraftQuest)questDataStore.GetQuestData(PlayerId, "QuestAuthor:forQuestTest:Test1");
            var test2Quest = (ItemCraftQuest)questDataStore.GetQuestData(PlayerId, "QuestAuthor:forQuestTest:Test2");


            
            Assert.False(test1Quest.IsCompleted);
            Assert.False(test2Quest.IsCompleted);


            //１

            //2Invoke
            InvokeCraftEventWithReflection(craftEvent, GetQuestIdWithReflection(test2Quest));

            //2
            Assert.False(test2Quest.IsRewardEarnable());
            Assert.True(test2Quest.IsCompleted);

            //1
            InvokeCraftEventWithReflection(craftEvent, GetQuestIdWithReflection(test1Quest));
            //1
            Assert.True(test1Quest.IsCompleted);
            Assert.True(test1Quest.IsRewardEarnable());
            //2
            Assert.True(test2Quest.IsRewardEarnable());
        }



        ///     And

        [Test]
        public void AndPreRequestQuest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            var questDataStore = serviceProvider.GetService<IQuestDataStore>();
            var craftEvent = serviceProvider.GetService<ICraftingEvent>();

            var test1Quest = (ItemCraftQuest)questDataStore.GetQuestData(PlayerId, "QuestAuthor:forQuestTest:Test1");
            var test2Quest = (ItemCraftQuest)questDataStore.GetQuestData(PlayerId, "QuestAuthor:forQuestTest:Test2");
            var testAndPreRequestQuest = (ItemCraftQuest)questDataStore.GetQuestData(PlayerId, "QuestAuthor:forQuestTest:Test3");

            
            Assert.False(test1Quest.IsCompleted);
            Assert.False(test2Quest.IsCompleted);
            Assert.False(testAndPreRequestQuest.IsCompleted);


            //And
            InvokeCraftEventWithReflection(craftEvent, GetQuestIdWithReflection(testAndPreRequestQuest));

            
            Assert.True(testAndPreRequestQuest.IsCompleted);
            Assert.False(testAndPreRequestQuest.IsRewardEarnable());

            //1
            InvokeCraftEventWithReflection(craftEvent, GetQuestIdWithReflection(test1Quest));

            //AND
            Assert.False(testAndPreRequestQuest.IsRewardEarnable());

            //2
            InvokeCraftEventWithReflection(craftEvent, GetQuestIdWithReflection(test2Quest));

            //AND
            Assert.True(testAndPreRequestQuest.IsRewardEarnable());
        }


        ///     Or

        [Test]
        public void OrPreRequestQuest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            var questDataStore = serviceProvider.GetService<IQuestDataStore>();
            var craftEvent = serviceProvider.GetService<ICraftingEvent>();

            var test1Quest = (ItemCraftQuest)questDataStore.GetQuestData(PlayerId, "QuestAuthor:forQuestTest:Test1");
            var testOrPreRequestQuest = (ItemCraftQuest)questDataStore.GetQuestData(PlayerId, "QuestAuthor:forQuestTest:Test4");

            
            Assert.False(test1Quest.IsCompleted);
            Assert.False(testOrPreRequestQuest.IsCompleted);


            //Or
            InvokeCraftEventWithReflection(craftEvent, GetQuestIdWithReflection(testOrPreRequestQuest));

            
            Assert.True(testOrPreRequestQuest.IsCompleted);
            Assert.False(testOrPreRequestQuest.IsRewardEarnable());

            //1
            InvokeCraftEventWithReflection(craftEvent, GetQuestIdWithReflection(test1Quest));

            //Or
            Assert.True(testOrPreRequestQuest.IsRewardEarnable());
        }


        ///     

        private void InvokeCraftEventWithReflection(ICraftingEvent craftingEvent, int itemId)
        {
            
            var method = typeof(CraftingEvent).GetMethod("InvokeEvent", BindingFlags.NonPublic | BindingFlags.Instance);
            
            method.Invoke(craftingEvent, new object?[] { itemId, 1 });
        }

        private int GetQuestIdWithReflection(ItemCraftQuest itemCraftQuest)
        {
            return (int)itemCraftQuest.GetType().GetField("_questItemId", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(itemCraftQuest);
        }
    }
}
#endif