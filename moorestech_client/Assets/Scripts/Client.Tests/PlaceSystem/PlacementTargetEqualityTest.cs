using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.ConnectTool;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem
{
    public class PlacementTargetEqualityTest
    {
        // 同一性の比較だけが目的なのでマスタに実在しない固定Guidで足りる
        // Fixed guids suffice since only identity comparison is exercised here
        private static readonly Guid BlockGuidA = Guid.Parse("00000000-0000-4000-8000-0000000000a1");
        private static readonly Guid BlockGuidB = Guid.Parse("00000000-0000-4000-8000-0000000000b2");

        [Test]
        public void BlockTargetIsEqualOnlyWhenIdAndDirectionMatch()
        {
            var a = new BlockPlacementTarget(BlockGuidA, BlockDirection.North);
            var b = new BlockPlacementTarget(BlockGuidA, BlockDirection.North);
            var differentDirection = new BlockPlacementTarget(BlockGuidA, null);
            var differentId = new BlockPlacementTarget(BlockGuidB, BlockDirection.North);

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
            var connectToolGuid = Guid.NewGuid();
            Assert.IsTrue(new ConnectToolPlacementTarget(connectToolGuid).Equals(new ConnectToolPlacementTarget(connectToolGuid)));
            Assert.IsFalse(new ConnectToolPlacementTarget(connectToolGuid).Equals(new ConnectToolPlacementTarget(Guid.NewGuid())));

            // ブループリントはGuidで区別する（同名でも別Guidなら別物、同Guidなら名前が違っても同一）
            // Blueprints are distinguished by GUID (same name with a different GUID is distinct; same GUID is identical even with a different name)
            var blueprintGuid = Guid.NewGuid();
            Assert.IsTrue(new BlueprintPlacementTarget(blueprintGuid, "bp1").Equals(new BlueprintPlacementTarget(blueprintGuid, "bp1")));
            Assert.IsFalse(new BlueprintPlacementTarget(blueprintGuid, "bp1").Equals(new BlueprintPlacementTarget(Guid.NewGuid(), "bp1")));
        }

        [Test]
        public void BuildToolTargetsAreEqualByGuidAndCrossTypeNeverEqual()
        {
            var buildToolGuid = Guid.NewGuid();
            Assert.IsTrue(new BlueprintCopyPlacementTarget(buildToolGuid).Equals(new BlueprintCopyPlacementTarget(buildToolGuid)));
            Assert.IsFalse(new BlueprintCopyPlacementTarget(buildToolGuid).Equals(new BlueprintCopyPlacementTarget(Guid.NewGuid())));
            var guid = Guid.NewGuid();
            Assert.IsFalse(new BlockPlacementTarget(BlockGuidA, null).Equals(new TrainCarPlacementTarget(guid)));
            Assert.IsFalse(new BlueprintPlacementTarget(Guid.NewGuid(), "x").Equals(new ConnectToolPlacementTarget(Guid.NewGuid())));
        }
    }
}
