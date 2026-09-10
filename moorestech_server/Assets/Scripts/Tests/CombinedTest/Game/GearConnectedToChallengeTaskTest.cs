using System;
using System.Linq;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Challenge;
using Game.Context;
using Game.Gear.Common;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class GearConnectedToChallengeTaskTest
    {
        private static readonly Guid ChallengeGuid = Guid.Parse("00000000-0000-0000-4567-000000000105");

        [Test]
        public void シャフトをベルトの横に置くと回転していなくても完了する()
        {
            var challengeDatastore = CreateAndStart();
            var world = ServerContext.WorldBlockDatastore;

            // 接続成立のみで完了しRPMは見ない
            // No generator, so RPM stays 0; completion must come from the connection alone
            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
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

        [Test]
        public void 接続先種別が違う歯車ブロックの横に置いても完了しない()
        {
            var challengeDatastore = CreateAndStart();
            var world = ServerContext.WorldBlockDatastore;

            // ベルト以外へ歯車接続しても完了しない。接続先GUID照合を常時trueへ変異させるとここが落ちる
            // Connecting to a non-belt gear block must not complete; mutating the connected-guid match to always-true fails here
            world.TryAddBlock(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var shaft);

            GameUpdater.UpdateOneTick();
            GameUpdater.UpdateOneTick();

            // 接続が成立していなければ種別違いを見ていないので、前提を明示的に確かめる
            // Without an actual connection the kind mismatch is never exercised, so the premise is asserted
            Assert.IsTrue(shaft.TryGetComponent<IGearEnergyTransformer>(out var transformer));
            Assert.IsNotEmpty(transformer.GetGearConnects().ToList(), "the shaft never gear-connected to the generator");
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
