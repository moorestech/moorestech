using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.BeltTransport.Statistics;
using Client.Game.InGame.Block;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.State;
using Game.Fluid;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.WebUi.BlockDetail
{
    public class FluidBlockDetailDtoBuilderTest
    {
        private static readonly System.Guid WaterGuid = System.Guid.Parse("00000000-0000-0000-1234-000000000001");

        [Test]
        public void PipeStateFillsOneFluidSlot()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var waterId = MasterHolder.FluidMaster.GetFluidId(WaterGuid);
            var detail = new FluidPipeStateDetail(waterId, 40f, 100f);
            var dto = ApplyWithState(ForUnitTestModBlockId.FluidPipe, FluidPipeStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(detail));

            Assert.AreEqual(1, dto.FluidSlots.Count);
            Assert.AreEqual(waterId.AsPrimitive(), dto.FluidSlots[0].FluidId);
            Assert.AreEqual(40.0, dto.FluidSlots[0].Amount, 0.001);
            Assert.AreEqual(100.0, dto.FluidSlots[0].Capacity, 0.001);
            Assert.AreEqual(WaterGuid.ToString("D"), dto.FluidSlots[0].FluidGuid);
        }

        // 空のパイプは空Guidのスロットになる
        // An empty pipe becomes a slot with an empty GUID
        [Test]
        public void EmptyPipeFillsEmptyFluidSlot()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var detail = new FluidPipeStateDetail(FluidMaster.EmptyFluidId, 0f, 100f);
            var dto = ApplyWithState(ForUnitTestModBlockId.FluidPipe, FluidPipeStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(detail));

            Assert.AreEqual(1, dto.FluidSlots.Count);
            Assert.AreEqual("", dto.FluidSlots[0].FluidGuid);
        }

        // PF出力タンクを1スロット化
        // Map the platform output tank to one slot
        [Test]
        public void FluidPlatformStateFillsOneFluidSlot()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var waterId = MasterHolder.FluidMaster.GetFluidId(WaterGuid);
            var detail = new FluidMachineInventoryStateDetail(new List<FluidMessagePack>(), new List<FluidMessagePack> { new(waterId, 250.0, 1000.0) });
            var dto = ApplyWithState(ForUnitTestModBlockId.TestTrainFluidPlatform, FluidMachineInventoryStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(detail));

            Assert.AreEqual(1, dto.FluidSlots.Count);
            Assert.AreEqual(250.0, dto.FluidSlots[0].Amount, 0.001);
            Assert.AreEqual(1000.0, dto.FluidSlots[0].Capacity, 0.001);
        }

        private static BlockInventoryDto ApplyWithState(BlockId blockId, string key, byte[] value)
        {
            // 受信状態を実クライアントへ注入して親の合成入口を通す
            // Inject a received state into a real client object and run the parent composition entry
            var master = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var state = new BlockState(new Dictionary<string, byte[]> { { key, value } });
            var gameObject = new GameObject("FluidBlockDetailDtoBuilderTest");
            var block = gameObject.AddComponent<BlockGameObject>();
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockMasterElement)).SetValue(block, master);
            typeof(BlockGameObject).GetField("_blockStateMessagePack", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(block, new BlockStateMessagePack(state, Vector3Int.zero));
            var dto = new BlockInventoryDto { FluidSlots = new List<BlockFluidSlotDto>() };
            BlockDetailDtoBuilder.Apply(dto, block, new BlockNetworkInfoCache(), new BeltTransportClientCache(new BeltSegmentExitStatisticsTable()), null);
            Object.DestroyImmediate(gameObject);
            return dto;
        }
    }
}
