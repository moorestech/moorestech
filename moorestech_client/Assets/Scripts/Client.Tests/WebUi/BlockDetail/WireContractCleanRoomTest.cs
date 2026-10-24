using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockInfoPolling;
using Client.Game.InGame.CleanRoom;
using Client.Tests.BlockInfoPolling;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.State;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine.TestTools;
using System.IO;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Topics.BlockDetail.CleanRoom;
using Game.Block.Interface.Component;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Tests.WebUi.BlockDetail
{
    /// <summary>
    ///     部屋照会の種別ごとの型→部屋DTO の詰め替えと、WireFixtures との一致を固定する
    ///     Pins the per-kind room query view to room DTO conversion and its equality with WireFixtures
    /// </summary>
    public class WireContractCleanRoomTest
    {
        [Test]
        public void EnclosedMachineWithClassOutMatchesFixtureTest()
        {
            // 密閉済みでOutの部屋にいる停止中の機械は、機械用の口で停止理由を渡す
            // A halted machine in a sealed but Out room passes its halt reason through the machine entry point
            var filter = new CleanRoomAirFilterMessagePack(new Vector3IntMessagePack(1, 33, 5), 1d, 10d, 5, 0.5d);
            var room = CleanRoomSummaryMessagePack.CreateOut(1.5d, 0.25d, 74, new List<CleanRoomAirFilterMessagePack> { filter });
            var view = new CleanRoomEnclosedView(new List<CleanRoomSummaryMessagePack> { room });

            var dto = new BlockInventoryDto
            {
                Open = true,
                Source = "block",
                BlockType = "CleanRoomMachine",
                Identifier = "(3, 33, 3)",
                BlockGuid = "44444444-4444-4444-8444-444444444444",
                ItemSlots = new List<BlockItemSlotDto>(),
                FluidSlots = new List<BlockFluidSlotDto>(),
                CleanRoom = CleanRoomDetailDtoBuilder.BuildForMachine(view, CleanRoomOperability.ClassOut),
            };
            AssertMatchesFixture(dto, "block_inventory_clean_room_machine.json");
        }

        [Test]
        public void LeakingAirFilterMatchesFixtureTest()
        {
            // 清浄機は停止理由の引数が無い口で組むので haltReason は出ない
            // A purifier goes through the entry point without a halt-reason argument, so haltReason never appears
            var cells = new List<Vector3IntMessagePack> { new(0, 33, 0), new(1, 33, 0), new(2, 33, 0) };
            var leak = new CleanRoomOpeningsLeakView(cells, 3);

            var dto = new BlockInventoryDto
            {
                Open = true,
                Source = "block",
                BlockType = "CleanRoomAirFilter",
                Identifier = "(1, 33, 5)",
                BlockGuid = "55555555-5555-4555-8555-555555555555",
                ItemSlots = new List<BlockItemSlotDto> { new() { ItemId = 12, Count = 5 } },
                FluidSlots = new List<BlockFluidSlotDto>(),
                CleanRoom = CleanRoomDetailDtoBuilder.BuildForAirFilter(new CleanRoomNotEnclosedView(leak)),
            };
            AssertMatchesFixture(dto, "block_inventory_clean_room_air_filter_leak.json");
        }

        [Test]
        public void ClassifiedRoomCarriesClassHeadlineTest()
        {
            // 分類済みの部屋は、見出しの種別 class とクラス名を持つ
            // A classified room carries the class headline kind and its class name
            var room = CleanRoomSummaryMessagePack.CreateClassified("A", 0, 0.1d, 2d, 26, new List<CleanRoomAirFilterMessagePack>());
            var dto = CleanRoomDetailDtoBuilder.BuildForAirFilter(new CleanRoomEnclosedView(new List<CleanRoomSummaryMessagePack> { room }));
            Assert.AreEqual("enclosed", dto.Kind);
            Assert.AreEqual("class", dto.Room.Headline);
            Assert.AreEqual("A", dto.Room.ClassName);
            Assert.IsNull(dto.HaltReason);
        }

        [Test]
        public void NoEnclosureLeakMapsToWireKindTest()
        {
            AssertLeakMapsTo(CleanRoomNoEnclosureLeakView.Instance, "noEnclosure", null);
        }

        [Test]
        public void TooLargeLeakCarriesVolumeCapTest()
        {
            AssertLeakMapsTo(new CleanRoomTooLargeLeakView(4096), "tooLarge", 4096);
        }

        [Test]
        public void DetectionPendingLeakMapsToWireKindTest()
        {
            AssertLeakMapsTo(CleanRoomDetectionPendingLeakView.Instance, "detecting", null);
        }

        [UnityTest]
        public IEnumerator InvalidOperabilityWireValueClearsSectionAtApplyTest() => UniTask.ToCoroutine(async () =>
        {
            // 復号済み部屋情報を実Applyへ渡す
            // Pass decoded room information to the real Apply
            var source = new WireResponsePollSource<GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack, CleanRoomInfoView>(new CleanRoomInfoPollSource());
            var poller = new BlockInfoPoller<GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack, CleanRoomInfoView>(source, "CleanRoom", System.TimeSpan.FromSeconds(1));
            poller.Start(new BlockInstanceId(1));
            await source.Respond(GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack.CreateNotEnclosed(CleanRoomLeakMessagePack.CreateNoEnclosure()));
            var cache = new BlockNetworkInfoCache();
            typeof(BlockNetworkInfoCache).GetField("_cleanRoomPoller", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(cache, poller);
            typeof(BlockNetworkInfoCache).GetProperty(nameof(BlockNetworkInfoCache.CleanRoomTarget)).SetValue(cache, CleanRoomDetailTarget.Machine);

            // 既存の状態注入前例に従い、稼働可否も実MessagePackで復号する
            // Follow the existing state-injection fixture and decode operability through real MessagePack
            var gameObject = new GameObject("InvalidOperabilityTest");
            var block = gameObject.AddComponent<BlockGameObject>();
            var state = new BlockState(new Dictionary<string, byte[]>
            {
                { CleanRoomOperabilityStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(new CleanRoomOperabilityStateDetail((CleanRoomOperability)99)) },
            });
            var wire = MessagePackSerializer.Serialize(new BlockStateMessagePack(state, Vector3Int.zero));
            typeof(BlockGameObject).GetField("_blockStateMessagePack", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(block, MessagePackSerializer.Deserialize<BlockStateMessagePack>(wire));
            var dto = new BlockInventoryDto { CleanRoom = CleanRoomDetailDtoBuilder.BuildForAirFilter(new CleanRoomNotEnclosedView(CleanRoomNoEnclosureLeakView.Instance)) };
            LogAssert.Expect(LogType.Warning, "Unreadable clean-room operability: 99");
            Assert.DoesNotThrow(() => CleanRoomDetailDtoBuilder.Apply(dto, block, cache));
            Assert.IsNull(dto.CleanRoom);
            cache.Clear();
            Object.DestroyImmediate(gameObject);
        });

        private static void AssertLeakMapsTo(CleanRoomLeakView leak, string expectedKind, int? expectedMaxRoomVolume)
        {
            // 稼働可の機械は停止理由を出さず、上限体積は tooLarge のときだけ載る
            // An operable machine shows no halt reason, and the volume cap appears only for tooLarge
            var dto = CleanRoomDetailDtoBuilder.BuildForMachine(new CleanRoomNotEnclosedView(leak), CleanRoomOperability.Operable);
            Assert.AreEqual(expectedKind, dto.Kind);
            Assert.IsNull(dto.HaltReason);
            Assert.IsNull(dto.OpeningCount);
            Assert.AreEqual(expectedMaxRoomVolume, dto.MaxRoomVolume);
        }

        private static void AssertMatchesFixture(object dto, string fixtureName)
        {
            var actual = JToken.Parse(WebUiJson.Serialize(dto));
            var path = Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures", fixtureName);
            var expected = JToken.Parse(File.ReadAllText(path));
            Assert.IsTrue(JToken.DeepEquals(expected, actual), $"fixture mismatch: {fixtureName}\nexpected: {expected}\nactual: {actual}");
        }
    }
}
