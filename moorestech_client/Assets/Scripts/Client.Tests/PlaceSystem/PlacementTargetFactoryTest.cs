using System;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Core.Master;
using Game.PlacementTarget;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem
{
    public class PlacementTargetFactoryTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void TryCreateはKindごとに対応する型とIdを持つターゲットを生成する()
        {
            // Block以外はEntry.Idを直接ターゲットのIdへ引き継ぐ想定の型・値を検証する
            // For non-Block kinds, assert the type produced and that entry.Id passes through unchanged
            var blockGuid = MasterHolder.BlockMaster.Blocks.Data.First().BlockGuid;
            AssertCreated(PlacementTargetKind.Block, blockGuid, typeof(BlockPlacementTarget));
            AssertCreated(PlacementTargetKind.TrainCar, Guid.NewGuid(), typeof(TrainCarPlacementTarget));
            AssertCreated(PlacementTargetKind.ConnectTool, Guid.NewGuid(), typeof(ConnectToolPlacementTarget));
            AssertCreated(PlacementTargetKind.BuildTool, Guid.NewGuid(), typeof(BuildToolPlacementTarget));
            AssertCreated(PlacementTargetKind.Blueprint, Guid.NewGuid(), typeof(BlueprintPlacementTarget));

            #region Internal

            void AssertCreated(PlacementTargetKind kind, Guid id, Type expectedTargetType)
            {
                var entry = new PlacementTargetEntry(id, kind, "placement-target-factory-test");
                Assert.IsTrue(PlacementTargetFactory.TryCreate(entry, out var target), $"{kind} should resolve to a target");
                Assert.IsInstanceOf(expectedTargetType, target, $"{kind} should resolve to {expectedTargetType.Name}");
                Assert.AreEqual(id, target.Id, $"{kind} target id should round-trip from entry.Id");
            }

            #endregion
        }
    }
}
