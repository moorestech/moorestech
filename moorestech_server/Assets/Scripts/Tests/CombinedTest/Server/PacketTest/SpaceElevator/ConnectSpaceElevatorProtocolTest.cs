using System;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Context;
using Game.SpaceElevator;
using MessagePack;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorConnectionTest;

namespace Tests.CombinedTest.Server.PacketTest.SpaceElevator
{
    public class ConnectSpaceElevatorProtocolTest
    {
        [Test]
        public void 足りなければ理由を返し足りれば接続する()
        {
            var (packet, _) = CreateServer();
            var position = new Vector3Int(10, 0, 5);
            var elevatorBase = PlaceBase(position);
            var pool = ServerContext.GetService<SpaceElevatorCablePool>();
            LaunchCables(50);

            // 不足時は接続と残量の両方を維持する
            // Preserve both connection state and cable amount on shortage
            ExpectRefusal(SpaceElevatorConnectResult.InsufficientCable, position);
            Assert.AreEqual(SpaceElevatorConnectResult.InsufficientCable, Send(packet, position).Result);
            Assert.IsFalse(ReadDetail(elevatorBase).IsConnected);
            Assert.AreEqual(50, pool.Amount);

            // ちょうど1基分なら消費して接続状態を通知する
            // Consume exactly one base's worth and expose the connected state
            LaunchCables(50);
            Assert.AreEqual(SpaceElevatorConnectResult.Connected, Send(packet, position).Result);
            Assert.IsTrue(ReadDetail(elevatorBase).IsConnected);
            Assert.AreEqual(0, pool.Amount);
        }

        [Test]
        public void 二重接続は拒否して残量を維持する()
        {
            var (packet, _) = CreateServer();
            PlaceBase(Vector3Int.zero);
            LaunchCables(150);
            Assert.AreEqual(SpaceElevatorConnectResult.Connected, Send(packet, Vector3Int.zero).Result);

            // 再送で二重にケーブルを消費しない
            // A repeated request must not consume cables twice
            ExpectRefusal(SpaceElevatorConnectResult.AlreadyConnected, Vector3Int.zero);
            Assert.AreEqual(SpaceElevatorConnectResult.AlreadyConnected, Send(packet, Vector3Int.zero).Result);
            Assert.AreEqual(50, ServerContext.GetService<SpaceElevatorCablePool>().Amount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 拠点でない座標は拒否する(bool placeOtherBlock)
        {
            var (packet, _) = CreateServer();
            var position = new Vector3Int(5, 0, 5);
            LaunchCables(100);

            // 空座標と別種ブロックの両方で拒否経路を通す
            // Exercise refusal for both an empty position and a different block
            if (placeOtherBlock)
            {
                Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, position,
                    BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));
            }
            var expected = placeOtherBlock ? SpaceElevatorConnectResult.NotSpaceElevatorBase : SpaceElevatorConnectResult.BlockNotFound;
            ExpectRefusal(expected, position);
            Assert.AreEqual(expected, Send(packet, position).Result);
            Assert.AreEqual(100, ServerContext.GetService<SpaceElevatorCablePool>().Amount);
        }

        [Test]
        public void 送信後に置換された別個体へは接続しない()
        {
            var (packet, _) = CreateServer();
            var old = PlaceBase(Vector3Int.zero);
            var request = new ConnectSpaceElevatorRequest(Vector3Int.zero, old.BlockInstanceId);
            ServerContext.WorldBlockDatastore.RemoveBlock(Vector3Int.zero, BlockRemoveReason.ManualRemove);
            var replacement = PlaceBase(Vector3Int.zero);
            LaunchCables(100);

            // 古い要求は新しい拠点とプールを変えない
            // A stale request must change neither the replacement nor the pool
            ExpectRefusal(SpaceElevatorConnectResult.InstanceMismatch, Vector3Int.zero);
            Assert.AreEqual(SpaceElevatorConnectResult.InstanceMismatch, Send(packet, request).Result);
            Assert.IsFalse(ReadDetail(replacement).IsConnected);
            Assert.AreEqual(100, ServerContext.GetService<SpaceElevatorCablePool>().Amount);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void 欠損フィールドにも明示的な拒否応答を返す(bool missingPosition)
        {
            var (packet, _) = CreateServer();
            var block = PlaceBase(Vector3Int.zero);
            var request = new ConnectSpaceElevatorRequest(Vector3Int.zero, block.BlockInstanceId);
            if (missingPosition) request.Position = null;
            else request.ExpectedInstanceId = null;
            LaunchCables(100);
            LogAssert.Expect(LogType.Log, $"[ConnectSpaceElevator] connection refused: InvalidRequest pos:{request.Position?.Vector3Int} player:1");
            Assert.AreEqual(SpaceElevatorConnectResult.InvalidRequest, Send(packet, request).Result);
            Assert.IsFalse(ReadDetail(block).IsConnected);
            Assert.AreEqual(100, ServerContext.GetService<SpaceElevatorCablePool>().Amount);
        }

        [Test]
        public void Nil入力をプロトコル境界で拒否する()
        {
            var (_, provider) = CreateServer();
            LogAssert.Expect(LogType.Log, "[ConnectSpaceElevator] connection refused: InvalidRequest pos: player:1");
            var response = (ConnectSpaceElevatorResponse)new ConnectSpaceElevatorProtocol(provider).GetResponse(
                MessagePackSerializer.Serialize<ConnectSpaceElevatorRequest>(null), 1);
            Assert.AreEqual(SpaceElevatorConnectResult.InvalidRequest, response.Result);
        }

        private static ConnectSpaceElevatorResponse Send(PacketResponseCreator packet, Vector3Int position)
        {
            var block = ServerContext.WorldBlockDatastore.GetBlock(position);
            return Send(packet, new ConnectSpaceElevatorRequest(position, block == null ? BlockInstanceId.Create() : block.BlockInstanceId));
        }

        private static ConnectSpaceElevatorResponse Send(PacketResponseCreator packet, ConnectSpaceElevatorRequest request)
        {
            request.SequenceId = 42;
            var payload = MessagePackSerializer.Serialize(request);
            var responses = packet.GetPacketResponse(payload, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(1));

            // 実通信で登録タグと応答の対応を検証
            // Verify registered tags and response correlation over the real wire format
            Assert.AreEqual(1, responses.Count);
            var response = MessagePackSerializer.Deserialize<ConnectSpaceElevatorResponse>(responses[0]);
            Assert.AreEqual(ConnectSpaceElevatorProtocol.ProtocolTag, response.Tag);
            Assert.AreEqual(request.SequenceId, response.SequenceId);
            return response;
        }

        private static void ExpectRefusal(SpaceElevatorConnectResult result, Vector3Int position)
        {
            LogAssert.Expect(LogType.Log, $"[ConnectSpaceElevator] connection refused: {result} pos:{position} player:1");
        }
    }
}
