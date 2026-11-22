using System.Linq;
using System.Text.RegularExpressions;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Tests.Module.TestMod;
using Tests.Util.OrbitalCargo;
using Tests.Util.PlayerIdentity;
using Tests.Util.Rocket;
using UnityEngine;
using UnityEngine.TestTools;
using static Server.Protocol.PacketResponse.OrbitalCargoSelectionProtocol;

namespace Tests.CombinedTest.Server.PacketTest.OrbitalCargo
{
    // 積荷選択の通信（ADR 0106 5）。実際の MessagePack を通して送る
    // Cargo selection over the wire (ADR 0106 5), sent through real MessagePack
    public class OrbitalCargoSelectionProtocolTest
    {
        private const int PlayerId = 1;
        private PacketResponseCreator _packet;
        private Microsoft.Extensions.DependencyInjection.ServiceProvider _provider;

        [SetUp]
        public void Setup()
        {
            (_packet, _provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 選ぶと発射台の選択が変わり前の種類は要求者の手持ちへ返る()
        {
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            OrbitalCargoTestUtil.SelectCargo(pad, ForUnitTestItemId.SatellitePayloadId);
            Assert.AreEqual(0, pad.GetComponent<IOpenableBlockInventoryComponent>().InsertItem(ForUnitTestItemId.SatellitePayloadId, 1).Count);

            AssertSuccess(Send(Vector3Int.zero, pad.BlockInstanceId, ForUnitTestItemId.CablePayloadId));
            var selection = (SelectedOrbitalCargo)pad.GetComponent<IOrbitalCargoSelectorComponent>().Selection;
            Assert.AreEqual(ForUnitTestItemId.CablePayloadId, selection.PayloadItemId);
            var main = (IPlayerInventoryDataStore)_provider.GetService(typeof(IPlayerInventoryDataStore));
            Assert.AreEqual(1, main.GetInventoryData(PlayerId).MainOpenableInventory.InventoryItems.Where(item => item.Id == ForUnitTestItemId.SatellitePayloadId).Sum(item => item.Count));
        }

        [Test]
        public void 拠点も同じ通信で選べる()
        {
            Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil.PlaceUnselectedBase(Vector3Int.zero);
            var block = ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero);
            AssertSuccess(Send(Vector3Int.zero, block.BlockInstanceId, ForUnitTestItemId.TestElevatorSatellite));
            Assert.IsInstanceOf<SelectedOrbitalCargo>(block.GetComponent<IOrbitalCargoSelectorComponent>().Selection);
        }

        [Test]
        public void 部品の拒否は理由付きで返る()
        {
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            // この台が運ばない品
            // An item this pad does not carry
            LogAssert.Expect(LogType.Warning, new Regex("積荷の選択を拒否しました 理由:NotCarriedHere"));
            AssertFailure(Send(Vector3Int.zero, pad.BlockInstanceId, ForUnitTestItemId.ItemId1), OrbitalCargoSelectionFailureReason.NotCarriedHere);
            // まだ解放していない積荷（ADR 0106 3）
            // A payload not unlocked yet (ADR 0106 3)
            OrbitalCargoTestUtil.LockItem(ForUnitTestItemId.CablePayloadId);
            LogAssert.Expect(LogType.Warning, new Regex("積荷の選択を拒否しました 理由:Locked"));
            AssertFailure(Send(Vector3Int.zero, pad.BlockInstanceId, ForUnitTestItemId.CablePayloadId), OrbitalCargoSelectionFailureReason.Locked);
            Assert.IsInstanceOf<UnselectedOrbitalCargo>(pad.GetComponent<IOrbitalCargoSelectorComponent>().Selection);
        }

        [Test]
        public void 通信側の拒否は理由付きで返る()
        {
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            ExpectProtocolRejection(OrbitalCargoSelectionFailureReason.BlockNotFound);
            AssertFailure(Send(new Vector3Int(50, 0, 0), pad.BlockInstanceId, ForUnitTestItemId.SatellitePayloadId), OrbitalCargoSelectionFailureReason.BlockNotFound);
            ExpectProtocolRejection(OrbitalCargoSelectionFailureReason.InstanceMismatch);
            AssertFailure(Send(Vector3Int.zero, new BlockInstanceId(pad.BlockInstanceId.AsPrimitive() + 1), ForUnitTestItemId.SatellitePayloadId), OrbitalCargoSelectionFailureReason.InstanceMismatch);

            // 選択部品の無いブロック
            // A block without a cargo selector
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(10, 0, 0), BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out var chest));
            ExpectProtocolRejection(OrbitalCargoSelectionFailureReason.NotCargoSelector);
            AssertFailure(Send(new Vector3Int(10, 0, 0), chest.BlockInstanceId, ForUnitTestItemId.SatellitePayloadId), OrbitalCargoSelectionFailureReason.NotCargoSelector);

            // 積荷の欠けた要求（実 MessagePack の配列で末尾を落とす）
            // A request missing the payload, as a real MessagePack array with the tail dropped
            var bytes = MessagePackSerializer.Serialize(new object[] { ProtocolTag, 42, new global::Server.Util.MessagePack.Vector3IntMessagePack(Vector3Int.zero), pad.BlockInstanceId.AsPrimitive() });
            ExpectProtocolRejection(OrbitalCargoSelectionFailureReason.InvalidRequest);
            AssertFailure(MessagePackSerializer.Deserialize<OrbitalCargoSelectionResponse>(_packet.GetPacketResponse(bytes, BoundPacketContext.Bind(PlayerId))[0]), OrbitalCargoSelectionFailureReason.InvalidRequest);
        }

        private OrbitalCargoSelectionResponse Send(Vector3Int position, BlockInstanceId instanceId, ItemId payloadItemId)
        {
            // 実通信のタグ・接続ID・連番を検証
            // Verify tags, connection identity and sequence round-trip through real dispatch
            var request = new OrbitalCargoSelectionRequest(position, instanceId, payloadItemId) { SequenceId = 42 };
            var responses = _packet.GetPacketResponse(MessagePackSerializer.Serialize(request), BoundPacketContext.Bind(PlayerId));
            Assert.AreEqual(1, responses.Count);
            var response = MessagePackSerializer.Deserialize<OrbitalCargoSelectionResponse>(responses[0]);
            Assert.AreEqual(ProtocolTag, response.Tag);
            Assert.AreEqual(42, response.SequenceId);
            return response;
        }

        private static void AssertSuccess(OrbitalCargoSelectionResponse response)
        {
            Assert.IsTrue(response.Success);
            Assert.AreEqual(OrbitalCargoSelectionFailureReason.None, response.FailureReason);
        }

        private static void AssertFailure(OrbitalCargoSelectionResponse response, OrbitalCargoSelectionFailureReason reason)
        {
            Assert.IsFalse(response.Success);
            Assert.AreEqual(reason, response.FailureReason);
        }

        private static void ExpectProtocolRejection(OrbitalCargoSelectionFailureReason reason)
        {
            LogAssert.Expect(LogType.Warning, new Regex($"積荷の選択要求を拒否しました 理由:{reason} 座標:"));
        }
    }
}
