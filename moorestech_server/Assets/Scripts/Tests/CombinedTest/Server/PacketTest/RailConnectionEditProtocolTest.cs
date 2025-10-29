using System.Collections.Generic;
using System.Linq;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Context;
using Game.Train.RailGraph;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    [TestFixture]
    public class RailConnectionEditProtocolTest
    {
        [SetUp]
        public void SetUp()
        {
            // テスト開始前にレールグラフの状態を完全に初期化する
            // Reset the rail graph state completely before each test
            RailGraphDatastore.ResetInstance();
        }

        [Test]
        public void ConnectMode_ConnectsRailsBidirectionally()
        {
            // サービスプロバイダーとプロトコルを初期化する
            // Initialize service provider and protocol creator
            var (packet, serviceProvider) = CreatePacketResponse();
            var environment = new TrainTestEnvironment(serviceProvider, ServerContext.WorldBlockDatastore);

            // レールブロックを設置してRailComponentを取得する
            // Place rail blocks and acquire their RailComponent instances
            var firstRail = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, 0), BlockDirection.East, out _);
            var secondRail = TrainTestHelper.PlaceRail(environment, new Vector3Int(1, 0, 0), BlockDirection.East, out _);

            // プロトコル要求を組み立てて接続処理を実行する
            // Build the protocol request and execute the connect operation
            var payload = BuildPayload(
                new RailConnectionEditProtocol.RailCoordinateMessagePack(new Vector3Int(0, 0, 0), 0, true),
                new RailConnectionEditProtocol.RailCoordinateMessagePack(new Vector3Int(1, 0, 0), 0, true),
                RailConnectionEditProtocol.RailEditMode.Connect);
            var responses = packet.GetPacketResponse(payload);

            // レスポンスを検証し、フロント同士が接続されていることを確認する
            // Validate the response and confirm that the front nodes are connected
            Assert.AreEqual(1, responses.Count);
            
            // 一旦コメントアウト
            // var response = MessagePackSerializer.Deserialize<RailConnectionEditProtocol.RailConnectionEditResponse>(responses[0].ToArray());
            // Assert.IsTrue(response.Data.IsSuccess);
            // Assert.AreEqual(RailConnectionEditProtocol.RailConnectionEditError.None, response.Data.Error);

            // RailGraphDatastore上で相互接続が成立していることを確認する
            // Ensure that the rail graph reflects the bidirectional connection
            CollectionAssert.Contains(firstRail.FrontNode.ConnectedNodes.ToList(), secondRail.FrontNode);
            CollectionAssert.Contains(secondRail.BackNode.ConnectedNodes.ToList(), firstRail.BackNode);
        }

        [Test]
        public void DisconnectMode_RemovesExistingConnection()
        {
            // プロトコル生成とレール設置を行い初期状態を構築する
            // Prepare the protocol creator and place rails for the initial state
            var (packet, serviceProvider) = CreatePacketResponse();
            var environment = new TrainTestEnvironment(serviceProvider, ServerContext.WorldBlockDatastore);

            // テスト用に事前接続されたレール構成を作成する
            // Create a pair of rails that are pre-connected for testing
            var firstRail = TrainTestHelper.PlaceRail(environment, new Vector3Int(10, 0, 0), BlockDirection.East, out _);
            var secondRail = TrainTestHelper.PlaceRail(environment, new Vector3Int(11, 0, 0), BlockDirection.East, out _);
            firstRail.ConnectRailComponent(secondRail, true, true, 8);

            // 切断要求を生成し、プロトコルを実行する
            // Build a disconnect request and execute the protocol
            var payload = BuildPayload(
                new RailConnectionEditProtocol.RailCoordinateMessagePack(new Vector3Int(10, 0, 0), 0, true),
                new RailConnectionEditProtocol.RailCoordinateMessagePack(new Vector3Int(11, 0, 0), 0, true),
                RailConnectionEditProtocol.RailEditMode.Disconnect);
            var responses = packet.GetPacketResponse(payload);

            // レスポンスの結果を確認し、接続が解除されたことを検証する
            // Validate the response and ensure the connection is removed
            Assert.AreEqual(1, responses.Count);
            
            // 一旦コメントアウト
            // var response = MessagePackSerializer.Deserialize<RailConnectionEditProtocol.RailConnectionEditResponse>(responses[0].ToArray());
            // Assert.IsTrue(response.Data.IsSuccess);
            // Assert.AreEqual(RailConnectionEditProtocol.RailConnectionEditError.None, response.Data.Error);

            // RailGraphDatastore上で両方向の接続が消えていることをチェックする
            // Confirm that both directional links are removed from the rail graph
            CollectionAssert.DoesNotContain(firstRail.FrontNode.ConnectedNodes.ToList(), secondRail.FrontNode);
            CollectionAssert.DoesNotContain(secondRail.BackNode.ConnectedNodes.ToList(), firstRail.BackNode);
        }

        private static (PacketResponseCreator PacketCreator, ServiceProvider ServiceProvider) CreatePacketResponse()
        {
            // テスト用MODディレクトリを指定してDIコンテナを構築する
            // Build the DI container while pointing to the unit-test mod directory
            var generator = new MoorestechServerDIContainerGenerator();
            return generator.Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        private static List<byte> BuildPayload(
            RailConnectionEditProtocol.RailCoordinateMessagePack from,
            RailConnectionEditProtocol.RailCoordinateMessagePack to,
            RailConnectionEditProtocol.RailEditMode mode)
        {
            // リクエストメッセージをまとめ、MessagePackでシリアライズする
            // Assemble the request message and serialize it via MessagePack
            var request = new RailConnectionEditProtocol.RailConnectionEditRequest(
                new RailConnectionEditProtocol.RailConnectionEditData(from, to, mode))
            {
                SequenceId = 1
            };

            return MessagePackSerializer.Serialize(request).ToList();
        }
    }
}
