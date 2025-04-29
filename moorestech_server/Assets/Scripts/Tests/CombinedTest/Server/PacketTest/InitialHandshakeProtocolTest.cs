using System.Collections.Generic;
using System.Linq;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using static Server.Protocol.PacketResponse.InitialHandshakeProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class InitialHandshakeProtocolTest
    {
        private const int PlayerId = 1;
        
        [Test]
        public void SpawnCoordinateTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            //ワールド設定情報を初期化
            serviceProvider.GetService<IWorldSettingsDatastore>().Initialize();
            
            //最初のハンドシェイクを実行
            var response = packet.GetPacketResponse(GetHandshakePacket(PlayerId))[0];
            var handShakeResponse =
                MessagePackSerializer.Deserialize<ResponseInitialHandshakeMessagePack>(response.ToArray());
            
            //今のところ初期スポーンはゼロ固定
            var pos = InitialHandshakeProtocol.DefaultPlayerPosition;
            Assert.AreEqual(pos.x, handShakeResponse.PlayerPos.X);
            Assert.AreEqual(pos.y, handShakeResponse.PlayerPos.Y);
            Assert.AreEqual(pos.z, handShakeResponse.PlayerPos.Z);
            
            
            //プレイヤーの座標を変更
            packet.GetPacketResponse(GetPlayerPositionPacket(PlayerId, new Vector3(100, 0, -100)));
            
            
            //再度ハンドシェイクを実行して座標が変更されていることを確認
            response = packet.GetPacketResponse(GetHandshakePacket(PlayerId))[0];
            handShakeResponse =
                MessagePackSerializer.Deserialize<ResponseInitialHandshakeMessagePack>(response.ToArray());
            Assert.AreEqual(100, handShakeResponse.PlayerPos.X);
            Assert.AreEqual(0, handShakeResponse.PlayerPos.Y);
            Assert.AreEqual(-100, handShakeResponse.PlayerPos.Z);
        }
        
        private List<byte> GetHandshakePacket(int playerId)
        {
            return MessagePackSerializer.Serialize(
                new RequestInitialHandshakeMessagePack(playerId, "test player name")).ToList();
        }
        
        
        private List<byte> GetPlayerPositionPacket(int playerId, Vector3 pos)
        {
            return MessagePackSerializer.Serialize(
                new SetPlayerCoordinateProtocol.PlayerCoordinateSendProtocolMessagePack(playerId, pos)).ToList();
        }
    }
}