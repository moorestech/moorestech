using System.Text.RegularExpressions;
using Core.Update;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util.PlayerIdentity;
using Tests.Util.Rocket;
using UnityEngine;
using UnityEngine.TestTools;
using static Server.Protocol.PacketResponse.RocketLaunchPadCommandProtocol;

namespace Tests.CombinedTest.Server.PacketTest.Rocket
{
    public class RocketLaunchPadMissingCommandFieldTest
    {
        [TestCase(true)]
        [TestCase(false)]
        public void 操作と個体IDの欠損を個別に拒否する(bool missingOperation)
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingBuildCommand, 5));
            var request = new RocketLaunchPadCommandRequest(Vector3Int.zero, RocketLaunchPadCommandOperation.Build, pad.BlockInstanceId);
            if (missingOperation) request.Operation = null;
            else request.ExpectedInstanceId = null;
            LogAssert.Expect(LogType.Warning, new Regex("合図を拒否しました 理由:InvalidRequest"));
            var responses = packet.GetPacketResponse(MessagePackSerializer.Serialize(request), BoundPacketContext.Bind(1));
            Assert.AreEqual(RocketLaunchPadCommandFailureReason.InvalidRequest, MessagePackSerializer.Deserialize<RocketLaunchPadCommandResponse>(responses[0]).FailureReason);
            Assert.AreEqual(2, pad.GetComponent<IOpenableBlockInventoryComponent>().GetItem(0).Count);
        }
    }
}
