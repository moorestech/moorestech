using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem
{
    public class PlacementTargetEqualityTest
    {
        [Test]
        public void BlockTargetIsEqualOnlyWhenIdAndDirectionMatch()
        {
            var a = new BlockPlacementTarget(new BlockId(1), BlockDirection.North);
            var b = new BlockPlacementTarget(new BlockId(1), BlockDirection.North);
            var differentDirection = new BlockPlacementTarget(new BlockId(1), null);
            var differentId = new BlockPlacementTarget(new BlockId(2), BlockDirection.North);

            Assert.IsTrue(a.Equals(b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.IsFalse(a.Equals(differentDirection));
            Assert.IsFalse(a.Equals(differentId));
        }

        [Test]
        public void ValueTargetsAreEqualByTheirIdentityField()
        {
            var guid = Guid.NewGuid();
            Assert.IsTrue(new TrainCarPlacementTarget(guid).Equals(new TrainCarPlacementTarget(guid)));
            Assert.IsFalse(new TrainCarPlacementTarget(guid).Equals(new TrainCarPlacementTarget(Guid.NewGuid())));
            Assert.IsTrue(new ConnectToolPlacementTarget("wire").Equals(new ConnectToolPlacementTarget("wire")));
            Assert.IsFalse(new ConnectToolPlacementTarget("wire").Equals(new ConnectToolPlacementTarget("rail")));
            Assert.IsTrue(new BlueprintPlacementTarget("bp1").Equals(new BlueprintPlacementTarget("bp1")));
            Assert.IsFalse(new BlueprintPlacementTarget("bp1").Equals(new BlueprintPlacementTarget("bp2")));
        }

        [Test]
        public void CopyToolTargetsAreAlwaysEqualAndCrossTypeNeverEqual()
        {
            Assert.IsTrue(new BlueprintCopyToolPlacementTarget().Equals(new BlueprintCopyToolPlacementTarget()));
            var guid = Guid.NewGuid();
            Assert.IsFalse(new BlockPlacementTarget(new BlockId(1), null).Equals(new TrainCarPlacementTarget(guid)));
            Assert.IsFalse(new BlueprintPlacementTarget("x").Equals(new ConnectToolPlacementTarget("x")));
        }
    }
}
