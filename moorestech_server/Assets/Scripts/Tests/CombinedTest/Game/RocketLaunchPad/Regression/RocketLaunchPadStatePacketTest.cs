using System.Linq;
using Core.Update;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.Orbit;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;
using Tests.Util.PlayerIdentity;
using Tests.Util.Rocket;
using UnityEngine;
using static Server.Protocol.PacketResponse.RequestBlockStateProtocol;

namespace Tests.CombinedTest.Game.RocketLaunchPad.Regression
{
    public class RocketLaunchPadStatePacketTest
    {
        [Test]
        public void 初期取得と三つの通知口が実パケットへ届く()
        {
            var (packet, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            var sink = EventTestUtil.RegisterCaptureSink(services, 1);
            var tag = ChangeBlockStateEventPacket.CreateSpecifiedBlockEventTag(pad.BlockPositionInfo);

            // pull・tickの待因変更を通信検証
            // Verify the initial pull and tick-driven wait-reason change over the wire
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new RequestBlockStateProtocolMessagePack(Vector3Int.zero)), BoundPacketContext.Bind(1));
            Assert.AreEqual(RocketLaunchPadPhase.Idle, LastState().Phase);
            GameUpdater.RunFrames(1);
            Assert.AreEqual(RocketLaunchPadWaitReason.MissingMaterials, LastState().WaitReason);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingBuildCommand, 5));
            Assert.AreEqual(RocketLaunchPadPhase.AwaitingBuildCommand, LastState().Phase);

            // 合図直後と進捗tickの通知は直接状態参照で代用しない
            // Do not replace command and progress notifications with direct state reads
            processor.RequestBuild();
            Assert.AreEqual(RocketLaunchPadPhase.Building, LastState().Phase);
            GameUpdater.RunFrames(5);
            Assert.That(LastState().Progress, Is.GreaterThan(0));
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingLaunchCommand, 40));
            Assert.AreEqual(RocketLaunchPadPhase.AwaitingLaunchCommand, LastState().Phase);

            // 他台の初発射履歴を受けた購読がtick前に配信する
            // The subscription publishes another pad's first launch before this pad ticks
            ServerContext.GetService<RocketLaunchHistory>().RecordLaunch();
            Assert.AreEqual(RocketLaunchPadPhase.ReadyToLaunch, LastState().Phase);

            #region Internal
            RocketLaunchPadStateDetail LastState()
            {
                var events = sink.TakeAll().Where(item => item.Tag == tag).ToList();
                Assert.IsNotEmpty(events, "発射台の状態パケットが届いていない");
                return MessagePackSerializer.Deserialize<BlockStateMessagePack>(events.Last().Payload)
                    .GetStateDetail<RocketLaunchPadStateDetail>(RocketLaunchPadStateDetail.BlockStateDetailKey);
            }
            #endregion
        }
    }
}
