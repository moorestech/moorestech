using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Challenge;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Research;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class CompleteResearchChallengeTaskTest
    {
        private const int PlayerId = 0;
        private static readonly Guid Research1Guid = Guid.Parse("cd05e30d-d599-46d3-a079-769113cbbf17");
        private static readonly Guid UnrelatedResearchGuid = Guid.Parse("a5b6c7d8-0000-4000-8000-000000000001");
        private static readonly Guid ChallengeGuid = Guid.Parse("00000000-0000-0000-4567-000000000101");

        // 研究完了イベントでチャレンジが完了する
        // Completing the research completes the challenge via the event
        [Test]
        public void ResearchCompleteEventCompletesChallenge()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.InitializeCurrentChallenges();

            CompleteResearch(serviceProvider, Research1Guid);

            Assert.IsTrue(challengeDatastore.CurrentChallengeInfo.CompletedChallenges.Any(c => c.ChallengeGuid == ChallengeGuid));
        }

        // チャレンジ開始前に完了済みの研究は初回tickで回収される
        // Research completed before the challenge starts is recovered on the first tick
        [Test]
        public void AlreadyCompletedResearchCompletesChallengeOnFirstTick()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();

            CompleteResearch(serviceProvider, Research1Guid);
            challengeDatastore.InitializeCurrentChallenges();
            GameUpdater.UpdateOneTick();

            Assert.IsTrue(challengeDatastore.CurrentChallengeInfo.CompletedChallenges.Any(c => c.ChallengeGuid == ChallengeGuid));
        }

        // researchNodeGuidフィルタの否定系
        // Negative case for the researchNodeGuid filter
        [Test]
        public void UnrelatedResearchCompleteEventDoesNotCompleteChallenge()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.InitializeCurrentChallenges();

            CompleteResearch(serviceProvider, UnrelatedResearchGuid);

            Assert.IsFalse(challengeDatastore.CurrentChallengeInfo.CompletedChallenges.Any(c => c.ChallengeGuid == ChallengeGuid));
        }

        private static void CompleteResearch(ServiceProvider serviceProvider, Guid researchGuid)
        {
            // 消費アイテムを投入して研究を完了させる
            // Insert the consume items and complete the research
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var researchMaster = MasterHolder.ResearchMaster.GetResearch(researchGuid);
            foreach (var consumeItem in researchMaster.ConsumeItems)
            {
                var item = ServerContext.ItemStackFactory.Create(consumeItem.ItemGuid, consumeItem.ItemCount);
                playerInventory.MainOpenableInventory.InsertItem(item);
            }
            var result = serviceProvider.GetService<IResearchDataStore>().CompleteResearch(researchGuid, PlayerId);
            Assert.IsTrue(result);
        }
    }
}
