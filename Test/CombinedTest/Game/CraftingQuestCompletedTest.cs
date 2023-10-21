#if NET6_0
using System.Reflection;
using Game.PlayerInventory.Interface.Event;
using Game.Quest.Interface;
using Game.Quest.QuestEntity;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PlayerInventory.Event;
using Server.Boot;
using Test.Module.TestMod;

namespace Test.CombinedTest.Game
{
    /// <summary>
    ///     
    /// </summary>
    public class CraftingQuestCompletedTest
    {
        private const int PlayerId = 1;
        private int _eventInvokeCount;


        ///     

        [Test]
        public void CraftToQuestCompleteTest()
        {
            _eventInvokeCount = 0;

            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var questDatastore = serviceProvider.GetService<IQuestDataStore>();
            var craftingEvent = (CraftingEvent)serviceProvider.GetService<ICraftingEvent>();

            
            var quest = (ItemCraftQuest)questDatastore.GetPlayerQuestProgress(PlayerId)[0];
            
            var questItemId = (int)quest.GetType().GetField("_questItemId", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(quest);
            
            quest.OnQuestCompleted += OnQuestCompleted;


            
            Assert.IsFalse(quest.IsCompleted);


            
            var method = typeof(CraftingEvent).GetMethod("InvokeEvent", BindingFlags.NonPublic | BindingFlags.Instance);
            
            method.Invoke(craftingEvent, new object?[] { questItemId, 1 });


            
            Assert.IsTrue(quest.IsCompleted);
            //１
            Assert.AreEqual(1, _eventInvokeCount);


            //２
            method.Invoke(craftingEvent, new object?[] { questItemId, 1 });


            
            Assert.AreEqual(1, _eventInvokeCount);
        }

        private void OnQuestCompleted(QuestConfigData obj)
        {
            _eventInvokeCount++;
        }
    }
}
#endif