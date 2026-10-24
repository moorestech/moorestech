using System;
using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.BeltTransport.Statistics;
using Client.Game.InGame.Block;
using Client.Game.InGame.FluidNetwork;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.State;
using Game.Fluid.Sync.State;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Tests.Module.TestMod;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Tests.WebUi.BlockDetail
{
    public class FluidBlockDetailDtoBuilderTest
    {
        private static readonly Guid WaterGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        // パイプの中身は流体複製のノードから1スロットに写す
        // A pipe's contents map from the fluid replica node to one slot
        [Test]
        public void PipeNodeFillsOneFluidSlot()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var waterId = MasterHolder.FluidMaster.GetFluidId(WaterGuid);
            // 複製の量は 10 億分の 1 単位なので、容量 100・量 40 は 100 × 10^9 / 40 × 10^9
            // Replica amounts are 10^-9 units, so capacity 100 and amount 40 are 100 × 10^9 / 40 × 10^9
            var dto = ApplyWithFluidNode(ForUnitTestModBlockId.FluidPipe, new FluidNodeState(Vector3Int.zero, 100_000_000_000, 40_000_000_000, waterId));

            Assert.AreEqual(1, dto.FluidSlots.Count);
            Assert.AreEqual(waterId.AsPrimitive(), dto.FluidSlots[0].FluidId);
            Assert.AreEqual(40.0, dto.FluidSlots[0].Amount, 0.001);
            Assert.AreEqual(100.0, dto.FluidSlots[0].Capacity, 0.001);
            Assert.AreEqual(WaterGuid.ToString("D"), dto.FluidSlots[0].FluidGuid);
        }

        // 空のパイプは空Guidのスロットになる
        // An empty pipe becomes a slot with an empty GUID
        [Test]
        public void EmptyPipeNodeFillsEmptyFluidSlot()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var dto = ApplyWithFluidNode(ForUnitTestModBlockId.FluidPipe, new FluidNodeState(Vector3Int.zero, 100_000_000_000, 0, FluidMaster.EmptyFluidId));

            Assert.AreEqual(1, dto.FluidSlots.Count);
            Assert.AreEqual("", dto.FluidSlots[0].FluidGuid);
        }

        // 複製にノードが無い座標(パイプ以外・複製未到着)は液体スロットを足さない
        // A position without a replica node (not a pipe, or no replica yet) adds no fluid slot
        [Test]
        public void PositionWithoutNodeAddsNoFluidSlot()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var dto = Apply(ForUnitTestModBlockId.FluidPipe, null, new FluidNetworkClientCache());

            Assert.AreEqual(0, dto.FluidSlots.Count);
        }

        // PF出力タンクを1スロット化
        // Map the platform output tank to one slot
        [Test]
        public void FluidPlatformStateFillsOneFluidSlot()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var waterId = MasterHolder.FluidMaster.GetFluidId(WaterGuid);
            var detail = new FluidMachineInventoryStateDetail(new List<FluidMessagePack>(), new List<FluidMessagePack> { new(waterId, 250.0, 1000.0) });
            var state = new BlockState(new Dictionary<string, byte[]> { { FluidMachineInventoryStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(detail) } });
            var dto = Apply(ForUnitTestModBlockId.TestTrainFluidPlatform, state, new FluidNetworkClientCache());

            Assert.AreEqual(1, dto.FluidSlots.Count);
            Assert.AreEqual(250.0, dto.FluidSlots[0].Amount, 0.001);
            Assert.AreEqual(1000.0, dto.FluidSlots[0].Capacity, 0.001);
        }

        private static BlockInventoryDto ApplyWithFluidNode(BlockId blockId, FluidNodeState node)
        {
            var cache = new FluidNetworkClientCache();
            cache.ApplyFullState(new FluidNetworkFullState(new[] { node }, Array.Empty<FluidFaceState>(), Array.Empty<FluidPortState>()));
            return Apply(blockId, null, cache);
        }

        private static BlockInventoryDto Apply(BlockId blockId, BlockState receivedState, FluidNetworkClientCache fluidCache)
        {
            // 受信状態を実クライアントへ注入して親の合成入口を通す
            // Inject a received state into a real client object and run the parent composition entry
            var master = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var gameObject = new GameObject("FluidBlockDetailDtoBuilderTest");
            var block = gameObject.AddComponent<BlockGameObject>();
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockMasterElement)).SetValue(block, master);
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockPosInfo)).SetValue(block, new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one));
            var state = receivedState ?? new BlockState(new Dictionary<string, byte[]>());
            typeof(BlockGameObject).GetField("_blockStateMessagePack", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(block, new BlockStateMessagePack(state, Vector3Int.zero));
            var dto = new BlockInventoryDto { FluidSlots = new List<BlockFluidSlotDto>() };
            BlockDetailDtoBuilder.Apply(dto, block, new BlockNetworkInfoCache(), new BeltTransportClientCache(new BeltSegmentExitStatisticsTable()), fluidCache);
            Object.DestroyImmediate(gameObject);
            return dto;
        }
    }
}
