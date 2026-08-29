using System;
using System.Linq;
using Core.Update;
using Game.Block.Interface;
using Game.Challenge;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class BlockPlaceOnVeinChallengeTaskTest
    {
        private static readonly Guid ChallengeGuid = Guid.Parse("00000000-0000-0000-4567-000000000103");

        // ForUnitTest map.json の item 鉱脈 11111111-…0001 は (0,5,0) の1セル
        // The ForUnitTest item vein 11111111-…0001 occupies the single cell (0,5,0)
        private static readonly Vector3Int VeinCell = new(0, 5, 0);
        private static readonly Vector3Int OutsideCell = new(3, 3, 3);

        [Test]
        public void 指定鉱脈の上に設置したら次のティックで完了する()
        {
            var challengeDatastore = CreateAndStart();

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, VeinCell, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            Assert.IsFalse(IsCompleted(challengeDatastore), "completed inside the placement event instead of on the tick");

            GameUpdater.UpdateOneTick();

            Assert.IsTrue(IsCompleted(challengeDatastore));
        }

        [Test]
        public void 鉱脈外に設置しても完了しない()
        {
            var challengeDatastore = CreateAndStart();

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, OutsideCell, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            GameUpdater.UpdateOneTick();

            Assert.IsFalse(IsCompleted(challengeDatastore));
        }

        [Test]
        public void チャレンジ開始前に置かれたブロックも初回ティックで回収する()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, VeinCell, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.InitializeCurrentChallenges();
            GameUpdater.UpdateOneTick();

            Assert.IsTrue(IsCompleted(challengeDatastore));
        }

        private static ChallengeDatastore CreateAndStart()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.InitializeCurrentChallenges();
            return challengeDatastore;
        }

        private static bool IsCompleted(ChallengeDatastore challengeDatastore)
        {
            return challengeDatastore.CurrentChallengeInfo.CompletedChallenges.Any(c => c.ChallengeGuid == ChallengeGuid);
        }
    }
}
