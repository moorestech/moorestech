using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Context;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Game.PlayerRiding.Interface;
using Game.SaveLoad.Json;
using Game.Train.Unit;
using Game.Train.Event;
using Microsoft.Extensions.DependencyInjection;
using MessagePack;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;
using Tests.UnitTest.PlayerRiding;
using Tests.Util;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    // 実ローダーへ渡す保存worldとマスタをテストごとに隔離する。
    // Isolate the saved world and master data consumed by the real loader per test.
    internal sealed class SavedRidingWorldFixture : IDisposable
    {
        public const int PlayerId = 1;
        public readonly string Root;
        public readonly string ServerDirectory;
        public readonly string WorldDirectory;
        public readonly TrainUnitInstanceId TrainId;
        public readonly TrainCarInstanceId CarId;
        public readonly int SeatIndex;
        public readonly byte[] RailPayload;
        public readonly byte[] TrainPayload;
        public readonly byte[] DeletePayload;
        public readonly byte[] UpsertPayload;
        public readonly InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack Handshake;

        public SavedRidingWorldFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "moorestech_tick_sync_" + Guid.NewGuid());
            ServerDirectory = Path.Combine(Root, "server");
            WorldDirectory = Path.Combine(Root, "world");
            var providerField = typeof(ServerContext).GetField("_serviceProvider", BindingFlags.Static | BindingFlags.NonPublic);
            var previousProvider = providerField.GetValue(null);
            var constructed = false;
            try
            {
                CopyServerData();
                AddTrainMasters();

                // 保存用DIを閉じてから、同じworldをゲーム起動へ渡す。
                // Dispose the save-building DI before booting the game with this world.
                var world = WorldDataDirectory.FromWorldRoot(WorldDirectory);
                WorldProvisioner.EnsureWorld(new WorldProvisionSettings(world, ServerDirectory, WorldMapMode.Template, 0));
                var (packets, services) = new MoorestechServerDIContainerGenerator().Create(
                    new MoorestechServerDIContainerOptions(ServerDirectory) { worldDataDirectory = world });
                using (services)
                {
                    var environment = new TrainTestEnvironment(services, ServerContext.WorldBlockDatastore, packets);
                    var car = RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
                    CarId = car.TrainCarInstanceId;
                    Assert.IsTrue(environment.GetTrainUnitDatastore().TryGetTrainUnitByCar(CarId, out var train));
                    TrainId = train.TrainUnitInstanceId;
                    var riding = services.GetRequiredService<IPlayerRidingDatastore>();
                    Assert.AreEqual(RideActionResult.Success, riding.TryRide(PlayerId,
                        new TrainCarRidableIdentifier(CarId.AsPrimitive()), out var seat));
                    SeatIndex = seat;
                    Assert.AreEqual(0, SeatIndex);
                    File.WriteAllText(world.SaveJsonFilePath, services.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());

                    // 保存乗車情報を再投入し、遅延payload検証も実handshake応答を使う。
                    // Reload saved riding data so the delayed-payload fixture also uses a real handshake.
                    riding.LoadSaveData(riding.GetSaveData());
                    var sink = new CapturedEventSink();
                    var request = MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(PlayerId, "Tick sync"));
                    var response = packets.GetPacketResponse(request, new PacketResponseContext(sink));
                    Handshake = MessagePackSerializer.Deserialize<InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack>(response[0]);
                    Assert.AreEqual(InitialHandshakeRidingStateType.Restored, Handshake.RidingStateType);
                    RailPayload = sink.Events.Single(e => e.Tag == TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventTag).Payload;
                    TrainPayload = sink.Events.Single(e => e.Tag == TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag).Payload;
                    sink.TakeAll();
                    var notifications = services.GetRequiredService<ITrainUnitSnapshotNotifyEvent>();
                    notifications.NotifyDeleted(TrainId);
                    notifications.NotifySnapshot(train);
                    DeletePayload = sink.Events[0].Payload;
                    UpsertPayload = sink.Events[1].Payload;
                }
                constructed = true;
            }
            finally
            {
                // 構築途中の例外でもstaticと一時rootを回収する。
                // Restore the static provider and remove temporary data even if construction fails.
                providerField.SetValue(null, previousProvider);
                if (!constructed) Dispose();
            }

            #region Internal
            void CopyServerData()
            {
                // 共有テストデータを変更せず、一時コピーだけを拡張する。
                // Extend only a temporary copy, leaving shared test data unchanged.
                var source = Path.GetFullPath(EditModeInPlayingTestServerDirectoryPath);
                foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                {
                    if (file.EndsWith(".meta", StringComparison.Ordinal)) continue;
                    var target = Path.Combine(ServerDirectory, Path.GetRelativePath(source, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(file, target);
                }
            }

            void AddTrainMasters()
            {
                var source = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods/forUnitTest/master");
                var target = Path.Combine(ServerDirectory, "mods/EditModeInPlayingTestMod/master");
                var blocks = JObject.Parse(File.ReadAllText(Path.Combine(target, "blocks.json")));
                var sourceBlocks = JObject.Parse(File.ReadAllText(Path.Combine(source, "blocks.json")));
                var rail = (JObject)sourceBlocks["data"].Single(b => (string)b["blockGuid"] == "00000000-0000-0000-0000-000000000024").DeepClone();
                rail["requiredItems"] = new JArray();
                rail["category"] = blocks["data"][0]["category"].DeepClone();
                rail["subCategory"] = blocks["data"][0]["subCategory"].DeepClone();
                rail["blockPrefabAddressablesPath"] = "Vanilla/Block/Train_Rail_Pier";
                ((JArray)blocks["data"]).Add(rail);
                File.WriteAllText(Path.Combine(target, "blocks.json"), blocks.ToString());

                // 実Prefabの座席index0を使い、駆動力0でhash観測中も停止させる。
                // Use seat zero on the real prefab and zero traction to keep hash observation stationary.
                var train = JObject.Parse(File.ReadAllText(Path.Combine(target, "train.json")));
                var sourceTrain = JObject.Parse(File.ReadAllText(Path.Combine(source, "train.json")));
                var car = (JObject)sourceTrain["trainCars"].Single(c => (string)c["trainCarGuid"] == RidingTestHelper.SeatedTrainCarGuid.ToString()).DeepClone();
                car["addressablePath"] = "Vanilla/Train/Locomotive";
                car["ridableSeatCount"] = 1;
                ((JArray)train["trainCars"]).Add(car);
                File.WriteAllText(Path.Combine(target, "train.json"), train.ToString());
            }
            #endregion
        }

        public void Dispose()
        {
            // 自身が作った一時rootだけを削除する。
            // Delete only the temporary root created by this fixture.
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath());
            Assert.IsTrue(Path.GetFullPath(Root).StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase));
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
