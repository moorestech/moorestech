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
    public class GearConnectedBlockChallengeTaskTest
    {
        private static readonly Guid ChallengeGuid = Guid.Parse("00000000-0000-0000-4567-000000000104");

        [Test]
        public void 発電機に繋がってシャフトが回ったら完了する()
        {
            var challengeDatastore = CreateAndStart();
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            GameUpdater.UpdateOneTick();

            Assert.IsTrue(IsCompleted(challengeDatastore));
        }

        [Test]
        public void シャフト単体では完了しない()
        {
            var challengeDatastore = CreateAndStart();
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            GameUpdater.UpdateOneTick();
            GameUpdater.UpdateOneTick();

            Assert.IsFalse(IsCompleted(challengeDatastore));
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
