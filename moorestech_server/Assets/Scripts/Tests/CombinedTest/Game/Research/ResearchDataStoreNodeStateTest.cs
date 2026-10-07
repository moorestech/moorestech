using System;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Research;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using Game.PlayerIdentity;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util.PlayerIdentity;
using static Tests.CombinedTest.Game.ResearchDataStoreTest;

namespace Tests.CombinedTest.Game.Research
{
    public class ResearchDataStoreNodeStateTest
    {
        [Test]
        public void GetResearchNodeStatesReflectRequirements()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerId = PlayerIdentityTestHelper.Register(serviceProvider.GetRequiredService<PlayerIdentityRegistry>(), "steam:1").PlayerId;

            var researchDataStore = serviceProvider.GetService<IResearchDataStore>();
            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);

            var initialStates = researchDataStore.GetResearchNodeStates(playerId);
            Assert.AreEqual(ResearchNodeState.UnresearchableNotEnoughItem, initialStates[Research1Guid]);
            Assert.AreEqual(ResearchNodeState.UnresearchableAllReasons, initialStates[Research2Guid]);
            Assert.AreEqual(ResearchNodeState.UnresearchableAllReasons, initialStates[Research3Guid]);
            Assert.AreEqual(ResearchNodeState.UnresearchableNotEnoughItem, initialStates[Research4Guid]);

            InsertRequiredItems(Research1Guid);
            var readyForFirstResearch = researchDataStore.GetResearchNodeStates(playerId);
            Assert.AreEqual(ResearchNodeState.Researchable, readyForFirstResearch[Research1Guid]);
            Assert.AreEqual(ResearchNodeState.UnresearchableNotEnoughPreNode, readyForFirstResearch[Research2Guid]);
            Assert.AreEqual(ResearchNodeState.UnresearchableAllReasons, readyForFirstResearch[Research3Guid]);

            Assert.IsTrue(researchDataStore.CompleteResearch(Research1Guid, playerId));

            var afterFirstResearch = researchDataStore.GetResearchNodeStates(playerId);
            Assert.AreEqual(ResearchNodeState.Completed, afterFirstResearch[Research1Guid]);
            Assert.AreEqual(ResearchNodeState.UnresearchableNotEnoughItem, afterFirstResearch[Research2Guid]);
            Assert.AreEqual(ResearchNodeState.UnresearchableAllReasons, afterFirstResearch[Research3Guid]);
            Assert.AreEqual(ResearchNodeState.UnresearchableNotEnoughItem, afterFirstResearch[Research4Guid]);

            InsertRequiredItems(Research2Guid);
            var afterSecondItems = researchDataStore.GetResearchNodeStates(playerId);
            Assert.AreEqual(ResearchNodeState.Researchable, afterSecondItems[Research2Guid]);
            Assert.AreEqual(ResearchNodeState.UnresearchableAllReasons, afterSecondItems[Research3Guid]);

            InsertRequiredItems(Research4Guid);
            var afterFourthItems = researchDataStore.GetResearchNodeStates(playerId);
            Assert.AreEqual(ResearchNodeState.Researchable, afterFourthItems[Research4Guid]);

            #region Internal

            void InsertRequiredItems(Guid researchGuid)
            {
                var researchElement = MasterHolder.ResearchMaster.GetResearch(researchGuid);
                foreach (var consumeItem in researchElement.ConsumeItems)
                {
                    var item = ServerContext.ItemStackFactory.Create(consumeItem.ItemGuid, consumeItem.ItemCount);
                    inventory.MainOpenableInventory.InsertItem(item);
                }
            }

            #endregion
        }
    }
}
