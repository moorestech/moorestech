using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.BeltTransport.Statistics;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Game.PlayerInventory.Interface.Subscription;
using Server.Protocol.PacketResponse;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.WebUi.BlockDetail
{
    public class RocketLaunchPadWireContractTest
    {
        // 実直列化結果をTS共有値と照合
        // Compare real serialization with the TypeScript fixture
        [Test]
        public void LaunchPadFixtureMatchesDto()
        {
            var dto = new BlockInventoryDto
            {
                Open = true, Source = "block", BlockType = "RocketLaunchPad", Identifier = "(8, 0, 4)",
                BlockGuid = "88888888-8888-4888-8888-888888888888",
                ItemSlots = new List<BlockItemSlotDto>
                {
                    new() { ItemId = 3, Count = 20 }, new() { ItemId = 0, Count = 0 }, new() { ItemId = 0, Count = 0 },
                },
                FluidSlots = new List<BlockFluidSlotDto>(),
                RocketLaunchPad = new RocketLaunchPadDetailDto
                {
                    Phase = "awaitingBuildCommand", WaitReason = "none", Progress = 0f,
                    MaterialSlots = new List<int> { 0 }, PayloadSlots = new List<int> { 1 }, OutputSlots = new List<int> { 2 },
                },
            };
            var actual = JToken.Parse(WebUiJson.Serialize(dto));
            var path = Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures/block_inventory_rocket_launch_pad.json");
            var expected = JToken.Parse(File.ReadAllText(path));
            Assert.IsTrue(JToken.DeepEquals(expected, actual), $"fixture mismatch\nexpected: {expected}\nactual: {actual}");
        }

        // 親builderで全段階と待ち理由を配線し、共有並びの絶対添字を保つ
        // Exercise parent wiring for every phase and wait reason and preserve the shared layout indices
        [TestCase(RocketLaunchPadPhase.Idle, RocketLaunchPadWaitReason.MissingMaterials, "idle", "missingMaterials")]
        [TestCase(RocketLaunchPadPhase.AwaitingBuildCommand, RocketLaunchPadWaitReason.None, "awaitingBuildCommand", "none")]
        [TestCase(RocketLaunchPadPhase.Building, RocketLaunchPadWaitReason.NoPower, "building", "noPower")]
        [TestCase(RocketLaunchPadPhase.AwaitingLaunchCommand, RocketLaunchPadWaitReason.None, "awaitingLaunchCommand", "none")]
        [TestCase(RocketLaunchPadPhase.ReadyToLaunch, RocketLaunchPadWaitReason.OutputFull, "readyToLaunch", "outputFull")]
        [TestCase(RocketLaunchPadPhase.Launching, RocketLaunchPadWaitReason.MissingPayload, "launching", "missingPayload")]
        public void ParentApplyCarriesReceivedStateAndSharedLayout(RocketLaunchPadPhase phase, RocketLaunchPadWaitReason reason, string wirePhase, string wireReason)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.RocketLaunchPadId);
            var layout = new RocketLaunchPadSlotLayout((RocketLaunchPadBlockParam)master.BlockParam);
            var state = new BlockState(new Dictionary<string, byte[]>
            {
                { RocketLaunchPadStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(new RocketLaunchPadStateDetail(phase, reason, 0.375f)) },
            });

            // 実オブジェクトの受信状態で合成を検証
            // Test public composition with received state in a real Unity object
            var gameObject = new GameObject("RocketLaunchPadWireTest");
            var block = gameObject.AddComponent<BlockGameObject>();
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockMasterElement)).SetValue(block, master);
            typeof(BlockGameObject).GetField("_blockStateMessagePack", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(block, new BlockStateMessagePack(state, Vector3Int.zero));
            var dto = new BlockInventoryDto();
            BlockDetailDtoBuilder.Apply(dto, block, new BlockNetworkInfoCache(), new BeltTransportClientCache(new BeltSegmentExitStatisticsTable()), layout);
            // 状態受信済みでも取得失敗の在庫には発射台の添字を載せない
            // Omit launch pad indices for failed inventory even when state has arrived
            var source = new BlockSubInventorySource(null, Vector3Int.zero);
            var failedModel = new SubInventoryModel(new BlockInventorySubInventoryIdentifier(Vector3Int.zero));
            failedModel.SetResult(InventoryRequestResult.Timeout);
            var failed = BlockInventoryDtoFactory.Create(source, failedModel, block,
                new BlockNetworkInfoCache(), new BeltTransportClientCache(new BeltSegmentExitStatisticsTable()), layout);
            Assert.AreEqual("openFailed", failed.Error);
            Assert.IsEmpty(failed.ItemSlots);
            Assert.IsNull(failed.RocketLaunchPad);
            Assert.IsNull(JObject.Parse(WebUiJson.Serialize(failed))["rocketLaunchPad"]);
            Object.DestroyImmediate(gameObject);

            Assert.AreEqual(wirePhase, dto.RocketLaunchPad.Phase);
            Assert.AreEqual(wireReason, dto.RocketLaunchPad.WaitReason);
            Assert.AreEqual(0.375f, dto.RocketLaunchPad.Progress);
            CollectionAssert.AreEqual(layout.MaterialSlots, dto.RocketLaunchPad.MaterialSlots);
            CollectionAssert.AreEqual(layout.PayloadSlots, dto.RocketLaunchPad.PayloadSlots);
            CollectionAssert.AreEqual(layout.OutputSlots, dto.RocketLaunchPad.OutputSlots);
        }
    }
}
