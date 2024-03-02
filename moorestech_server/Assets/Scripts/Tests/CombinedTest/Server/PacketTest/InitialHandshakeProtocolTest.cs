using System.Collections.Generic;
using System.Linq;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class InitialHandshakeProtocolTest
    {
        private const int PlayerId = 1;

        [Test]
        public void SpawnCoordinateTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);

            //ワールド設定情報を初期化
            serviceProvider.GetService<IWorldSettingsDatastore>().Initialize();

            //最初のハンドシェイクを実行
            var response = packet.GetPacketResponse(GetHandshakePacket(PlayerId))[0];
            var handShakeResponse =
                MessagePackSerializer.Deserialize<ResponseInitialHandshakeMessagePack>(response.ToArray());
            //スポーンポイントは0,0近くの鉄鉱石になるので、今のところはマジックナンバーでテストする
            //SEEDが変わると変換するので、その設定が追加されたら変更する
            Assert.AreEqual(2, handShakeResponse.PlayerPos.X);
            Assert.AreEqual(-88, handShakeResponse.PlayerPos.Y);


            //プレイヤーの座標を変更
            packet.GetPacketResponse(GetPlayerPositionPacket(PlayerId, 100, -100));


            //再度ハンドシェイクを実行して座標が変更されていることを確認
            response = packet.GetPacketResponse(GetHandshakePacket(PlayerId))[0];
            handShakeResponse =
                MessagePackSerializer.Deserialize<ResponseInitialHandshakeMessagePack>(response.ToArray());
            Assert.AreEqual(100, handShakeResponse.PlayerPos.X);
            Assert.AreEqual(-100, handShakeResponse.PlayerPos.Y);
        }

        private List<byte> GetHandshakePacket(int playerId)
        {
            return MessagePackSerializer.Serialize(
                new RequestInitialHandshakeMessagePack(playerId, "test player name")).ToList();
        }


        private List<byte> GetPlayerPositionPacket(int playerId, int x, int y)
        {
            return MessagePackSerializer.Serialize(
                new PlayerCoordinateSendProtocolMessagePack(playerId, x, y)).ToList();
        }
    }
}