using System.Linq;
using Game.Map.Interface.Json;
using Game.Research;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive.MachineUpgrade;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    /// <summary>
    ///     スロット上限と製作上限の解放がイベントで配信され、初期ハンドシェイクにも同梱されることを検証する
    ///     Verifies slot-limit and craft-cap unlocks are broadcast as events and bundled into the initial handshake
    /// </summary>
    public class MachineUpgradeEventPacketTest
    {
        private const int PlayerId = 1;

        [Test]
        public void ModuleSlotStackLevelIsBroadcastTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            // スロット上限の研究でレベル1が配信される
            // The slot-limit research broadcasts level 1
            serviceProvider.GetService<IResearchDataStore>().CompleteResearch(ForUnitTestLevelInheritanceIds.ModuleSlotStackResearchGuid, PlayerId);
            var events = sink.TakeAll().Where(e => e.Tag == ModuleSlotStackLevelEventPacket.EventTag).ToList();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(1, MessagePackSerializer.Deserialize<ModuleSlotStackLevelEventPacket.ModuleSlotStackLevelMessagePack>(events[0].Payload).Level);
        }

        [Test]
        public void LevelInheritanceCraftCapIsBroadcastTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            // 製作上限の研究で継承先の基準GUIDとレベル2が配信される
            // The craft-cap research broadcasts the target base GUID with level 2
            serviceProvider.GetService<IResearchDataStore>().CompleteResearch(ForUnitTestLevelInheritanceIds.CraftCapResearchGuid, PlayerId);
            var events = sink.TakeAll().Where(e => e.Tag == LevelInheritanceCraftCapUnlockEventPacket.EventTag).ToList();
            Assert.AreEqual(1, events.Count);
            var data = MessagePackSerializer.Deserialize<LevelInheritanceCraftCapUnlockEventPacket.LevelInheritanceCraftCapMessagePack>(events[0].Payload);
            Assert.AreEqual(ForUnitTestLevelInheritanceIds.CappedTargetBaseGuid, data.FamilyBaseItemGuid);
            Assert.AreEqual(2, data.Level);
        }

        [Test]
        public void MachineUpgradeStateIsIncludedInInitialHandshakeTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            serviceProvider.GetService<IWorldSettingsDatastore>().Initialize(serviceProvider.GetService<MapInfoJson>());
            var researchDataStore = serviceProvider.GetService<IResearchDataStore>();
            researchDataStore.CompleteResearch(ForUnitTestLevelInheritanceIds.ModuleSlotStackResearchGuid, PlayerId);
            researchDataStore.CompleteResearch(ForUnitTestLevelInheritanceIds.CraftCapResearchGuid, PlayerId);

            var handshakeRequest = MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack("steam:1"));
            var response = packetResponse.GetPacketResponse(handshakeRequest, new PacketResponseContext(null))[0];
            var accepted = MessagePackSerializer.Deserialize<InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack>(response).Accepted;

            // Key 7 にスロット上限のレベル、Key 8 に解放済みの製作上限だけが乗る
            // Key 7 carries the slot-limit level and Key 8 only the unlocked craft caps
            Assert.AreEqual(1, accepted.ModuleSlotStackLevel);
            Assert.AreEqual(1, accepted.LevelInheritanceCraftCaps.Length);
            Assert.AreEqual(ForUnitTestLevelInheritanceIds.CappedTargetBaseGuid, accepted.LevelInheritanceCraftCaps[0].FamilyBaseItemGuid);
            Assert.AreEqual(2, accepted.LevelInheritanceCraftCaps[0].Level);
        }
    }
}
