using System.Collections.Generic;
using System.Linq;
using Game.Block.Blocks.SpaceElevator;
using System.IO;
using System.Reflection;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.BeltTransport.Statistics;
using Client.Game.InGame.FluidNetwork;
using Client.Game.InGame.Block;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.State;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using Server.Boot;
using Server.Event.EventReceive;
using Tests.Module.TestMod;
using UnityEngine.TestTools;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi.BlockDetail
{
    /// <summary>
    /// 宇宙エレベーター拠点のwire契約を検証する
    /// Verifies the SpaceElevator capability DTO against the shared wire fixture
    /// </summary>
    public class WireContractSpaceElevatorTest
    {
        [Test]
        public void SpaceElevatorFixtureMatchesDto()
        {
            var dto = new BlockInventoryDto
            {
                Open = true,
                Source = "block",
                BlockType = "SpaceElevatorBase",
                Identifier = "(20, 0, 4)",
                BlockGuid = "99999999-9999-4999-8999-999999999999",
                ItemSlots = new List<BlockItemSlotDto>(),
                FluidSlots = new List<BlockFluidSlotDto>(),
                SpaceElevator = new SpaceElevatorDetailDto { SatelliteInputSlots = new List<int> { 0 }, OutputSlots = new List<int> { 1, 2 }, Connected = true, CablePoolAmount = 250, CablesPerElevator = 1000, ClimbProgress = 0.5f, Status = "climbing",
                    SlotBindings = new List<InputSlotBindingDto> { new() { Slot = 0, AcceptedItemIds = new List<int> { 5 } } } },
            };

            // 共有fixtureと厳密照合する
            // Match the production serializer output exactly against the fixture shared with TypeScript
            var actual = JToken.Parse(WebUiJson.Serialize(dto));
            var path = Path.Combine(
                Application.dataPath,
                "Scripts/Client.Tests/WebUi/WireFixtures/block_inventory_space_elevator.json");
            var expected = JToken.Parse(File.ReadAllText(path));
            Assert.IsTrue(JToken.DeepEquals(expected, actual), $"fixture mismatch\nexpected: {expected}\nactual: {actual}");
        }

        [TestCase(SpaceElevatorBaseStatus.ReadyToConnect, "readyToConnect")]
        [TestCase(SpaceElevatorBaseStatus.InsufficientCable, "insufficientCable")]
        [TestCase(SpaceElevatorBaseStatus.WaitingSatellite, "waitingSatellite")]
        [TestCase(SpaceElevatorBaseStatus.Climbing, "climbing")]
        [TestCase(SpaceElevatorBaseStatus.LowPower, "lowPower")]
        [TestCase(SpaceElevatorBaseStatus.OutputBlocked, "outputBlocked")]
        [TestCase(SpaceElevatorBaseStatus.InletBlocked, "inletBlocked")]
        public void ParentApplyCopiesMasterAndEveryStatus(SpaceElevatorBaseStatus status, string wireStatus)
        {
            var block = CreateBlock(new SpaceElevatorBaseStateDetail(true, 250, 0.5f, status));
            var dto = new BlockInventoryDto();
            BlockDetailDtoBuilder.Apply(dto, block, new BlockNetworkInfoCache(), new BeltTransportClientCache(new BeltSegmentExitStatisticsTable()), new FluidNetworkClientCache());
            var required = ((SpaceElevatorBaseBlockParam)block.BlockMasterElement.BlockParam).CablesPerElevator;
            var outputCount = ((SpaceElevatorBaseBlockParam)block.BlockMasterElement.BlockParam).OutputSlotCount;
            var carriedIds = ((SpaceElevatorBaseBlockParam)block.BlockMasterElement.BlockParam).CarriedPayloadItemGuids.Select(guid => MasterHolder.ItemMaster.GetItemId(guid).AsPrimitive()).ToList();
            Object.DestroyImmediate(block.gameObject);

            Assert.IsNotNull(dto.SpaceElevator);
            CollectionAssert.AreEqual(new[] { SpaceElevatorBaseInventoryComponent.SatelliteSlot }, dto.SpaceElevator.SatelliteInputSlots);
            CollectionAssert.AreEqual(Enumerable.Range(SpaceElevatorBaseInventoryComponent.FirstOutputSlot, outputCount), dto.SpaceElevator.OutputSlots);
            Assert.AreEqual(true, dto.SpaceElevator.Connected);
            Assert.AreEqual(250, dto.SpaceElevator.CablePoolAmount);
            Assert.AreEqual(required, dto.SpaceElevator.CablesPerElevator);
            Assert.AreEqual(0.5f, dto.SpaceElevator.ClimbProgress);
            Assert.AreEqual(wireStatus, dto.SpaceElevator.Status);
            // 入口だけがマスタの運ぶ積荷を受け付ける
            // Only the inlet carries accepted items, listing the master's carried payloads as-is (outlets are never insertion targets)
            Assert.AreEqual(1, dto.SpaceElevator.SlotBindings.Count);
            Assert.AreEqual(SpaceElevatorBaseInventoryComponent.SatelliteSlot, dto.SpaceElevator.SlotBindings[0].Slot);
            CollectionAssert.IsNotEmpty(carriedIds, "テスト前提: 運ぶ積荷がマスタにあること");
            CollectionAssert.AreEqual(carriedIds, dto.SpaceElevator.SlotBindings[0].AcceptedItemIds);
        }

        [Test]
        public void PendingDetailIsOmittedUntilStateArrives()
        {
            var block = CreateBlock(null);
            var dto = new BlockInventoryDto();
            SpaceElevatorDetailDtoBuilder.Apply(dto, block, block.BlockMasterElement.BlockParam);
            Object.DestroyImmediate(block.gameObject);
            Assert.IsNull(dto.SpaceElevator);
            Assert.IsNull(JObject.Parse(WebUiJson.Serialize(dto))["spaceElevator"]);
        }

        [Test]
        public void UnsupportedStatusThrowsInsteadOfPublishingUnknownWireValue()
        {
            var block = CreateBlock(new SpaceElevatorBaseStateDetail(true, 0, 0, (SpaceElevatorBaseStatus)999));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                SpaceElevatorDetailDtoBuilder.Apply(new BlockInventoryDto(), block, block.BlockMasterElement.BlockParam));
            Object.DestroyImmediate(block.gameObject);
        }

        private static BlockGameObject CreateBlock(SpaceElevatorBaseStateDetail detail)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestSpaceElevatorBase);
            var details = new Dictionary<string, byte[]>();
            if (detail != null)
                details.Add(SpaceElevatorBaseStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(detail));

            // 受信状態を実クライアントへ注入する
            // Inject received state into a real client block object
            var block = new GameObject("SpaceElevatorWireContractTest").AddComponent<BlockGameObject>();
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockMasterElement)).SetValue(block, master);
            // 本番のBlockGameObjectは必ず位置を持つ。流体複製の照会が座標で引くので同じ前提を満たす
            // A production BlockGameObject always has a position; the fluid replica lookup keys by it, so meet the same premise
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockPosInfo)).SetValue(block, new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one));
            typeof(BlockGameObject).GetField("_blockStateMessagePack", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(block, new BlockStateMessagePack(new BlockState(details), Vector3Int.zero));
            return block;
        }
    }
}
