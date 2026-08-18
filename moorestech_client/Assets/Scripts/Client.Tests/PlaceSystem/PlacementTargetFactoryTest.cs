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
        public void CreateはKindごとに対応する型とIdを持つターゲットを生成する()
        {
            var blockGuid = MasterHolder.BlockMaster.Blocks.Data.First().BlockGuid;
            AssertCreated(PlacementTargetKind.Block, blockGuid, typeof(BlockPlacementTarget));
            AssertCreated(PlacementTargetKind.TrainCar, Guid.NewGuid(), typeof(TrainCarPlacementTarget));
            AssertCreated(PlacementTargetKind.ConnectTool, Guid.NewGuid(), typeof(ConnectToolPlacementTarget));
            AssertCreated(PlacementTargetKind.BlueprintCopy, Guid.NewGuid(), typeof(BlueprintCopyPlacementTarget));
            AssertCreated(PlacementTargetKind.Blueprint, Guid.NewGuid(), typeof(BlueprintPlacementTarget));

            #region Internal

            void AssertCreated(PlacementTargetKind kind, Guid id, Type expectedTargetType)
            {
                var entry = new PlacementTargetEntry(id, kind, "placement-target-factory-test");
                var target = PlacementTargetFactory.Create(entry);
                Assert.IsInstanceOf(expectedTargetType, target, $"{kind} should resolve to {expectedTargetType.Name}");
                Assert.AreEqual(id, target.Id, $"{kind} target id should round-trip from entry.Id");
                Assert.AreEqual(kind, target.Kind, $"{kind} target should carry its catalog kind");
            }

            #endregion
        }
    }
}
