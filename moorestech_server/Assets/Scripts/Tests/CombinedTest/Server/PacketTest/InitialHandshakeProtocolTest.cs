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
            var response = packet.GetPacketResponse(GetHandshakePacket(PlayerId), new PacketResponseContext())[0];
            var handShakeResponse =
                MessagePackSerializer.Deserialize<ResponseInitialHandshakeMessagePack>(response);
            
            // スポーンポイントの座標のチェック
            var pos = new Vector3(186, 15.7f, -37.401f);;
            Assert.AreEqual(pos.x, handShakeResponse.PlayerPos.X);
            Assert.AreEqual(pos.y, handShakeResponse.PlayerPos.Y);
            Assert.AreEqual(pos.z, handShakeResponse.PlayerPos.Z);
            
            
            //プレイヤーの座標を変更
            packet.GetPacketResponse(GetPlayerPositionPacket(PlayerId, new Vector3(100, 0, -100)), new PacketResponseContext());
            
            
            //再度ハンドシェイクを実行して座標が変更されていることを確認
            response = packet.GetPacketResponse(GetHandshakePacket(PlayerId), new PacketResponseContext())[0];
            handShakeResponse =
                MessagePackSerializer.Deserialize<ResponseInitialHandshakeMessagePack>(response);
            Assert.AreEqual(100, handShakeResponse.PlayerPos.X);
            Assert.AreEqual(0, handShakeResponse.PlayerPos.Y);
            Assert.AreEqual(-100, handShakeResponse.PlayerPos.Z);
        }

        [Test]
        public void Handshake_RegistersPlayerConnection()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            serviceProvider.GetService<IWorldSettingsDatastore>().Initialize(serviceProvider.GetService<MapInfoJson>());
            var connectionChecker = serviceProvider.GetService<IPlayerConnectionChecker>();

            packet.GetPacketResponse(GetHandshakePacket(PlayerId), new PacketResponseContext());

            // ハンドシェイクプロトコルが接続登録を担当する。
            // The handshake protocol owns connection registration.
            Assert.IsTrue(connectionChecker.IsConnected(PlayerId));
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
                GetHandshakePacket(PlayerId),
                new PacketResponseContext())[0];
            var handshakeResponse = MessagePackSerializer.Deserialize<ResponseInitialHandshakeMessagePack>(response);

            Assert.AreEqual(InitialHandshakeRidingStateType.Restored, handshakeResponse.RidingStateType);
            Assert.IsTrue(handshakeResponse.HasRidingState);
            Assert.IsNotNull(handshakeResponse.RidingTarget);
            Assert.AreEqual(RidableType.TrainCar, handshakeResponse.RidingTarget.RidableType);
            Assert.AreEqual(car.TrainCarInstanceId.AsPrimitive(), handshakeResponse.RidingTarget.TrainCarInstanceId);
            Assert.AreEqual(0, handshakeResponse.RidingSeatIndex);
        }

        [Test]
        public void Handshake_RegistersEventQueue_ForRidingStateBroadcast()
        {
            // handshake 後は登録済みsinkでbroadcastを受け取れる。
            // After handshake, broadcasts reach the registered sink.
            var environment = TrainTestHelper.CreateEnvironment();
            environment.ServiceProvider.GetService<IWorldSettingsDatastore>().Initialize(environment.ServiceProvider.GetService<MapInfoJson>());
            var car = Tests.UnitTest.PlayerRiding.RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
            var datastore = environment.ServiceProvider.GetService<IPlayerRidingDatastore>();
            var id = new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive());
            var sink = EventTestUtil.RegisterCaptureSink(environment.ServiceProvider, PlayerId);
            var context = new PacketResponseContext();
            context.SetEventSink(sink);
            environment.PacketResponseCreator.GetPacketResponse(GetHandshakePacket(PlayerId), context);

            datastore.TryRide(PlayerId, id, out _);

            var events = sink.TakeAll();
            Assert.IsTrue(events.Exists(e => e.Tag == RidingStateEventPacket.EventTag));
        }
        
        private byte[] GetHandshakePacket(int playerId)
        {
            return MessagePackSerializer.Serialize(
                new RequestInitialHandshakeMessagePack(playerId, "test player name"));
        }
        
        
        private byte[] GetPlayerPositionPacket(int playerId, Vector3 pos)
        {
            return MessagePackSerializer.Serialize(
                new SetPlayerCoordinateProtocol.PlayerCoordinateSendProtocolMessagePack(playerId, pos));
        }
    }
}
