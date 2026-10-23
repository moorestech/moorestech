using System;
using System.Text.RegularExpressions;
using Core.Update;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.Orbit;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Tests.Module.TestMod;
using Tests.Util.PlayerIdentity;
using Tests.Util.Rocket;
using UnityEngine;
using UnityEngine.TestTools;
using static Server.Protocol.PacketResponse.RocketLaunchPadCommandProtocol;

namespace Tests.CombinedTest.Server.PacketTest.Rocket
{
    public class RocketLaunchPadCommandProtocolTest
    {
        private PacketResponseCreator _packet;

        [SetUp]
        public void Setup()
        {
            (_packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 建造の合図は待機中の台を建造へ進める()
        {
            var pad = PlaceAwaitingBuildPad();
            AssertSuccess(Send(RocketLaunchPadCommandOperation.Build));
            Assert.AreEqual(RocketLaunchPadPhase.Building, pad.GetComponent<RocketLaunchPadProcessorComponent>().Phase);
        }

        [Test]
        public void 発射の合図は自身のtickまで打ち上げを始めない()
        {
            var pad = PlaceAwaitingBuildPad();
            AssertSuccess(Send(RocketLaunchPadCommandOperation.Build));
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingLaunchCommand, 40));

            // 応答直後は段階だけが変わり、次のtickで到着する
            // Only the stage changes before the response; arrival occurs on the next tick
            AssertSuccess(Send(RocketLaunchPadCommandOperation.Launch));
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            Assert.AreEqual(RocketLaunchPadPhase.ReadyToLaunch, processor.Phase);
            Assert.IsFalse(ServerContext.GetService<RocketLaunchHistory>().HasLaunchedOnce);
            GameUpdater.RunFrames(1);
            Assert.AreEqual(RocketLaunchPadPhase.Launching, processor.Phase);
            Assert.IsTrue(ServerContext.GetService<RocketLaunchHistory>().HasLaunchedOnce);
        }

        [Test]
        public void 空き座標はBlockNotFoundを返す()
        {
            ExpectProtocolRejection(RocketLaunchPadCommandFailureReason.BlockNotFound);
            AssertFailure(Send(RocketLaunchPadCommandOperation.Build), RocketLaunchPadCommandFailureReason.BlockNotFound);
        }

        [Test]
        public void チェストはNotRocketLaunchPadを返す()
        {
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));
            ExpectProtocolRejection(RocketLaunchPadCommandFailureReason.NotRocketLaunchPad);
            AssertFailure(Send(RocketLaunchPadCommandOperation.Build), RocketLaunchPadCommandFailureReason.NotRocketLaunchPad);
        }

        [TestCase(RocketLaunchPadCommandOperation.Build, RocketLaunchPadCommandFailureReason.NotAwaitingBuildCommand)]
        [TestCase(RocketLaunchPadCommandOperation.Launch, RocketLaunchPadCommandFailureReason.NotAwaitingLaunchCommand)]
        public void 空の台は合図待ちでない理由を返す(RocketLaunchPadCommandOperation operation, RocketLaunchPadCommandFailureReason reason)
        {
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            ExpectComponentRejection(reason);
            AssertFailure(Send(operation), reason);
            Assert.AreEqual(RocketLaunchPadPhase.Idle, pad.GetComponent<RocketLaunchPadProcessorComponent>().Phase);
        }

        [Test]
        public void 未知の操作は段階を変えずUnknownOperationを返す()
        {
            var pad = PlaceAwaitingBuildPad();
            ExpectProtocolRejection(RocketLaunchPadCommandFailureReason.UnknownOperation);
            AssertFailure(Send((RocketLaunchPadCommandOperation)99), RocketLaunchPadCommandFailureReason.UnknownOperation);
            Assert.AreEqual(RocketLaunchPadPhase.AwaitingBuildCommand, pad.GetComponent<RocketLaunchPadProcessorComponent>().Phase);
        }

        [Test]
        public void 座標が欠けた要求はInvalidRequestを返す()
        {
            var request = new RocketLaunchPadCommandRequest(Vector3Int.zero, RocketLaunchPadCommandOperation.Build, new BlockInstanceId(1)) { Position = null };
            ExpectProtocolRejection(RocketLaunchPadCommandFailureReason.InvalidRequest);
            AssertFailure(Send(request), RocketLaunchPadCommandFailureReason.InvalidRequest);
        }

        [Test]
        public void 合図待ちの出力口が埋まるとOutputFullを返す()
        {
            var pad = PlaceAwaitingBuildPad();
            RocketLaunchPadTestUtil.FillOutputSlots(pad, 100);
            ExpectComponentRejection(RocketLaunchPadCommandFailureReason.OutputFull);
            AssertFailure(Send(RocketLaunchPadCommandOperation.Build), RocketLaunchPadCommandFailureReason.OutputFull);
            Assert.AreEqual(RocketLaunchPadPhase.AwaitingBuildCommand, pad.GetComponent<RocketLaunchPadProcessorComponent>().Phase);
            Assert.AreEqual(2, pad.GetComponent<IOpenableBlockInventoryComponent>().GetItem(0).Count);
        }

        [Test]
        public void 合図待ちの資材が搬出されるとMissingMaterialsを返す()
        {
            var pad = PlaceAwaitingBuildPad();
            pad.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
            ExpectComponentRejection(RocketLaunchPadCommandFailureReason.MissingMaterials);
            AssertFailure(Send(RocketLaunchPadCommandOperation.Build), RocketLaunchPadCommandFailureReason.MissingMaterials);
            Assert.AreEqual(RocketLaunchPadPhase.AwaitingBuildCommand, pad.GetComponent<RocketLaunchPadProcessorComponent>().Phase);
        }

        [Test]
        public void 古い個体への合図は置換後の台に作用しない()
        {
            var oldPad = PlaceAwaitingBuildPad();
            var request = new RocketLaunchPadCommandRequest(Vector3Int.zero, RocketLaunchPadCommandOperation.Build, oldPad.BlockInstanceId);
            ServerContext.WorldBlockDatastore.RemoveBlock(Vector3Int.zero, BlockRemoveReason.ManualRemove);
            var replacement = PlaceAwaitingBuildPad();
            ExpectProtocolRejection(RocketLaunchPadCommandFailureReason.InstanceMismatch);
            AssertFailure(Send(request), RocketLaunchPadCommandFailureReason.InstanceMismatch);
            Assert.AreEqual(2, replacement.GetComponent<IOpenableBlockInventoryComponent>().GetItem(0).Count);
        }

        [Test]
        public void 操作キーが無い実配列を無消費で拒否する()
        {
            var pad = PlaceAwaitingBuildPad();
            // 操作・個体IDなしの実配列を送信
            // Send a real MessagePack array without trailing operation and instance ID
            var bytes = MessagePackSerializer.Serialize(new object[] { ProtocolTag, 42, new global::Server.Util.MessagePack.Vector3IntMessagePack(Vector3Int.zero) });
            ExpectProtocolRejection(RocketLaunchPadCommandFailureReason.InvalidRequest);
            var responses = _packet.GetPacketResponse(bytes, BoundPacketContext.Bind(1));
            AssertFailure(MessagePackSerializer.Deserialize<RocketLaunchPadCommandResponse>(responses[0]), RocketLaunchPadCommandFailureReason.InvalidRequest);
            Assert.AreEqual(2, pad.GetComponent<IOpenableBlockInventoryComponent>().GetItem(0).Count);
        }

        private static IBlock PlaceAwaitingBuildPad()
        {
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingBuildCommand, 5));
            return pad;
        }

        private RocketLaunchPadCommandResponse Send(RocketLaunchPadCommandOperation operation)
        {
            return Send(new RocketLaunchPadCommandRequest(Vector3Int.zero, operation, ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero)?.BlockInstanceId ?? new BlockInstanceId(1)));
        }

        private RocketLaunchPadCommandResponse Send(RocketLaunchPadCommandRequest request)
        {
            // 実通信のタグ・接続ID・連番を検証
            // Verify tags, connection identity and sequence round-trip through real dispatch
            request.SequenceId = 42;
            var responses = _packet.GetPacketResponse(MessagePackSerializer.Serialize(request), BoundPacketContext.Bind(1));
            Assert.AreEqual(1, responses.Count);
            var response = MessagePackSerializer.Deserialize<RocketLaunchPadCommandResponse>(responses[0]);
            Assert.AreEqual(ProtocolTag, response.Tag);
            Assert.AreEqual(request.SequenceId, response.SequenceId);
            return response;
        }

        private static void AssertSuccess(RocketLaunchPadCommandResponse response)
        {
            Assert.IsTrue(response.Success);
            Assert.AreEqual(RocketLaunchPadCommandFailureReason.None, response.FailureReason);
        }

        private static void AssertFailure(RocketLaunchPadCommandResponse response, RocketLaunchPadCommandFailureReason reason)
        {
            Assert.IsFalse(response.Success);
            Assert.AreEqual(reason, response.FailureReason);
        }

        private static void ExpectProtocolRejection(RocketLaunchPadCommandFailureReason reason)
        {
            LogAssert.Expect(LogType.Warning, new Regex($"ロケット発射台への合図を拒否しました 理由:{reason} 座標:"));
        }

        private static void ExpectComponentRejection(RocketLaunchPadCommandFailureReason reason)
        {
            LogAssert.Expect(LogType.Warning, new Regex($"ロケット発射台への操作を拒否しました 理由:{reason} 段階:"));
        }
    }
}
