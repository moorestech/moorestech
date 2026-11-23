using Core.Master;
using Game.Block.Interface.State;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.OrbitalCargo
{
    // 状態に載せた積荷（ItemId?）が MessagePack を往復する。値あり・null の両方
    // The cargo fields (ItemId?) in block states round-trip through MessagePack, both with a value and null
    public class OrbitalCargoStateWireTest
    {
        [Test]
        public void 発射台の積荷2欄が往復する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var withValue = RoundTrip(new RocketLaunchPadStateDetail(RocketLaunchPadPhase.Building, RocketLaunchPadWaitReason.None, 0.5f, ForUnitTestItemId.CablePayloadId, ForUnitTestItemId.SatellitePayloadId));
            Assert.AreEqual(ForUnitTestItemId.CablePayloadId, withValue.SelectedCargoItemId);
            Assert.AreEqual(ForUnitTestItemId.SatellitePayloadId, withValue.LoadedCargoItemId);
            var empty = RoundTrip(new RocketLaunchPadStateDetail(RocketLaunchPadPhase.Idle, RocketLaunchPadWaitReason.CargoNotSelected, 0f, null, null));
            Assert.IsNull(empty.SelectedCargoItemId);
            Assert.IsNull(empty.LoadedCargoItemId);
        }

        [Test]
        public void 拠点の積荷2欄が往復する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var withValue = RoundTrip(new SpaceElevatorBaseStateDetail(true, 0, 0.5f, SpaceElevatorBaseStatus.Climbing, ForUnitTestItemId.TestElevatorBulkPayload, ForUnitTestItemId.TestElevatorSatellite));
            Assert.AreEqual(ForUnitTestItemId.TestElevatorBulkPayload, withValue.SelectedCargoItemId);
            Assert.AreEqual(ForUnitTestItemId.TestElevatorSatellite, withValue.ClimbingCargoItemId);
            var empty = RoundTrip(new SpaceElevatorBaseStateDetail(false, 0, 0f, SpaceElevatorBaseStatus.CargoNotSelected, null, null));
            Assert.IsNull(empty.SelectedCargoItemId);
            Assert.IsNull(empty.ClimbingCargoItemId);
        }

        private static T RoundTrip<T>(T detail)
        {
            return MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(detail));
        }
    }
}
