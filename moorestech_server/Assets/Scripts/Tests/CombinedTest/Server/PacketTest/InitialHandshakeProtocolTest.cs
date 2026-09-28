using System.Collections.Generic;
using System.Linq;
using Game.Map.Interface.Json;
using Game.PlayerConnection;
using Game.PlayerRiding.Interface;
using Game.World.DataStore.WorldSettings;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;
using UnityEngine;
using static Server.Protocol.PacketResponse.InitialHandshakeProtocol;
using Server.Protocol;
using Tests.Util;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class InitialHandshakeProtocolTest
    {
        private const int PlayerId = 1;
        
        [Test]
        public void SpawnCoordinateTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            //ワールド設定情報を初期化
            serviceProvider.GetService<IWorldSettingsDatastore>().Initialize(serviceProvider.GetService<MapInfoJson>());
            
            //最初のハンドシェイクを実行
            var context = new PacketResponseContext(null);
            var response = packet.GetPacketResponse(GetHandshakePacket(), context)[0];
            var handShakeResponse =
                MessagePackSerializer.Deserialize<ResponseInitialHandshakeMessagePack>(response);
            
            // スポーンポイントの座標のチェック
            var pos = new Vector3(186, 15.7f, -37.401f);;
            Assert.AreEqual(pos.x, handShakeResponse.Accepted.PlayerPos.X);
            Assert.AreEqual(pos.y, handShakeResponse.Accepted.PlayerPos.Y);
            Assert.AreEqual(pos.z, handShakeResponse.Accepted.PlayerPos.Z);
            
            
            //プレイヤーの座標を変更
            packet.GetPacketResponse(GetPlayerPositionPacket(new Vector3(100, 0, -100)), context);
            
            
            // 切断後に同じ身元で入り直し、保存座標を復元する
            // Reconnect with the same identity after disconnecting and restore the position
            var disconnectedPlayerId = context.MarkClosedAndGetPlayerId().Value;
            ((PlayerConnectionRegistry)serviceProvider.GetService<IPlayerConnectionChecker>()).Unregister(disconnectedPlayerId);
            response = packet.GetPacketResponse(GetHandshakePacket(), new PacketResponseContext(null))[0];
            handShakeResponse =
                MessagePackSerializer.Deserialize<ResponseInitialHandshakeMessagePack>(response);
            Assert.AreEqual(100, handShakeResponse.Accepted.PlayerPos.X);
            Assert.AreEqual(0, handShakeResponse.Accepted.PlayerPos.Y);
            Assert.AreEqual(-100, handShakeResponse.Accepted.PlayerPos.Z);
        }

        [Test]
        public void Handshake_RegistersPlayerConnection()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            serviceProvider.GetService<IWorldSettingsDatastore>().Initialize(serviceProvider.GetService<MapInfoJson>());
            var connectionChecker = serviceProvider.GetService<IPlayerConnectionChecker>();

            var response = packet.GetPacketResponse(GetHandshakePacket(), new PacketResponseContext(null))[0];
            var handshakeResponse = MessagePackSerializer.Deserialize<ResponseInitialHandshakeMessagePack>(response);

            // ハンドシェイクプロトコルが接続登録を担当する。
            // The handshake protocol owns connection registration.
            Assert.IsTrue(connectionChecker.IsConnected(handshakeResponse.Accepted.PlayerId));
        }

        [Test]
        public void Handshake_ReturnsRestoredRidingState_WhenLoginRestoreSucceeds()
        {
            // ログイン復帰できる保存済み乗車状態をレスポンスに含める。
            // Includes restorable saved riding state in the handshake response.
            var environment = TrainTestHelper.CreateEnvironment();
            environment.ServiceProvider.GetService<IWorldSettingsDatastore>().Initialize(environment.ServiceProvider.GetService<MapInfoJson>());
            var car = Tests.UnitTest.PlayerRiding.RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
            var datastore = environment.ServiceProvider.GetService<IPlayerRidingDatastore>();
            datastore.LoadSaveData(new List<PlayerRidingSaveData>
            {
                new(PlayerId, RidableType.TrainCar.AsPrimitive(), car.TrainCarInstanceId.AsPrimitive().ToString(), 0),
            });

            var response = environment.PacketResponseCreator.GetPacketResponse(
                GetHandshakePacket(),
                new PacketResponseContext(null))[0];
            var handshakeResponse = MessagePackSerializer.Deserialize<ResponseInitialHandshakeMessagePack>(response);

            Assert.AreEqual(PlayerId, handshakeResponse.Accepted.PlayerId);
            Assert.IsNotNull(handshakeResponse.Accepted.RidingTarget);
            Assert.AreEqual(RidableType.TrainCar, handshakeResponse.Accepted.RidingTarget.RidableType);
            Assert.AreEqual(car.TrainCarInstanceId.AsPrimitive(), handshakeResponse.Accepted.RidingTarget.TrainCarInstanceId);
            Assert.AreEqual(0, handshakeResponse.Accepted.RidingSeatIndex);
        }

        [Test]
        public void Handshake_RegistersEventQueue_ForRidingStateBroadcast()
        {
            // handshake後はsinkでbroadcast受信可
            // After handshake, broadcasts reach the sink.
            var environment = TrainTestHelper.CreateEnvironment();
            environment.ServiceProvider.GetService<IWorldSettingsDatastore>().Initialize(environment.ServiceProvider.GetService<MapInfoJson>());
            var car = Tests.UnitTest.PlayerRiding.RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
            var datastore = environment.ServiceProvider.GetService<IPlayerRidingDatastore>();
            var id = new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive());

            // handshake自身がsinkを配線することを検証するため、事前登録しないsinkを使う
            // Use an unregistered sink so the handshake itself must wire the context's sink
            var sink = new CapturedEventSink();
            var context = new PacketResponseContext(sink);
            var handshakeResponsePacket = environment.PacketResponseCreator.GetPacketResponse(GetHandshakePacket(), context)[0];
            var handshakeResponse = MessagePackSerializer.Deserialize<ResponseInitialHandshakeMessagePack>(handshakeResponsePacket);
            Assert.AreEqual(PlayerId, handshakeResponse.Accepted.PlayerId);
            sink.TakeAll();

            datastore.TryRide(PlayerId, id, out _);

            var events = sink.TakeAll();
            Assert.IsTrue(events.Exists(e => e.Tag == RidingStateEventPacket.EventTag));
        }
        
        private byte[] GetHandshakePacket()
        {
            return MessagePackSerializer.Serialize(
                new RequestInitialHandshakeMessagePack("steam:1"));
        }
        
        
        private byte[] GetPlayerPositionPacket(Vector3 pos)
        {
            return MessagePackSerializer.Serialize(
                new SetPlayerCoordinateProtocol.PlayerCoordinateSendProtocolMessagePack(pos));
        }
    }
}
