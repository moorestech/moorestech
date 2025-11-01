using System;
using System.Linq;
using Game.Block.Interface;
using MessagePack;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Util;
using UnityEngine;
using static Server.Protocol.PacketResponse.RailConnectionEditProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class GetRailConnectionsProtocolTest
    {
        [Test]
        public void GetRailConnectionsProtocol_RailConnectionEditProtocolで接続と切断を検証()
        {
            // Arrange
            // テスト環境を作成
            // Create test environment
            var env = TrainTestHelper.CreateEnvironment();
            var packet = env.PacketResponseCreator;
            
            // レールを2つ設置（まだ接続していない）
            // Place two rails (not connected yet)
            var fromPos = new Vector3Int(0, 0, 0);
            var toPos = new Vector3Int(1, 0, 0);
            TrainTestHelper.PlaceRail(env, fromPos, BlockDirection.North);
            TrainTestHelper.PlaceRail(env, toPos, BlockDirection.North);
            
            // Act & Assert 1: 接続前は接続情報が空であることを確認
            // Act & Assert 1: Verify that connection information is empty before connection
            var responseBeforeConnect = SendGetRailConnectionsRequest(packet);
            Assert.IsNotNull(responseBeforeConnect);
            Assert.IsNotNull(responseBeforeConnect.Connections);
            Assert.AreEqual(0, responseBeforeConnect.Connections.Length);
            
            // Act & Assert 2: RailConnectionEditProtocolで接続を実行
            // Act & Assert 2: Execute connection using RailConnectionEditProtocol
            SendRailConnectionEditRequest(packet, fromPos, toPos, RailEditMode.Connect, true, true);
            var responseAfterConnect = SendGetRailConnectionsRequest(packet);
            Assert.IsNotNull(responseAfterConnect);
            Assert.IsNotNull(responseAfterConnect.Connections);
            Assert.AreEqual(1, responseAfterConnect.Connections.Length);

            // Act & Assert 3: RailConnectionEditProtocolで切断を実行
            // Act & Assert 3: Execute disconnect using RailConnectionEditProtocol
            SendRailConnectionEditRequest(packet, fromPos, toPos, RailEditMode.Disconnect, true, true);
            var responseAfterDisconnect = SendGetRailConnectionsRequest(packet);
            Assert.IsNotNull(responseAfterDisconnect);
            Assert.IsNotNull(responseAfterDisconnect.Connections);
            Assert.AreEqual(0, responseAfterDisconnect.Connections.Length);
        }
        
        // GetRailConnectionsプロトコルを送信するヘルパーメソッド
        // Helper method to send GetRailConnections protocol
        private GetRailConnectionsProtocol.GetRailConnectionsResponse SendGetRailConnectionsRequest(PacketResponseCreator packet)
        {
            var request = new GetRailConnectionsProtocol.GetRailConnectionsRequest();
            var requestData = MessagePackSerializer.Serialize(request).ToList();
            var response = packet.GetPacketResponse(requestData);
            return MessagePackSerializer.Deserialize<GetRailConnectionsProtocol.GetRailConnectionsResponse>(response[0].ToArray());
        }
        
        // RailConnectionEditプロトコルを送信するヘルパーメソッド
        // Helper method to send RailConnectionEdit protocol
        private void SendRailConnectionEditRequest(PacketResponseCreator packet, Vector3Int from, Vector3Int to, RailEditMode mode, bool connectFromIsFront, bool connectToIsFront)
        {
            var fromSpecifier = RailComponentSpecifier.CreateRailSpecifier(from);
            var toSpecifier = RailComponentSpecifier.CreateRailSpecifier(to);

            var request = mode switch
            {
                RailEditMode.Connect => RailConnectionEditRequest.CreateConnectRequest(fromSpecifier, toSpecifier, connectFromIsFront, connectToIsFront),
                RailEditMode.Disconnect => RailConnectionEditRequest.CreateDisconnectRequest(fromSpecifier, toSpecifier),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };
            var requestData = MessagePackSerializer.Serialize(request).ToList();
            packet.GetPacketResponse(requestData);
        }
    }
}

