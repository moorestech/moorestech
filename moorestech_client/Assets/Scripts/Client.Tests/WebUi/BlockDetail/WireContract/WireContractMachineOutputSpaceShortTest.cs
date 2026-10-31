using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.BeltTransport.Statistics;
using Client.Game.InGame.Block;
using Client.Game.InGame.FluidNetwork;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.WebUi.BlockDetail
{
    // 受信した理由が実Builder経由でwireへ載る
    // The received reason reaches the wire via the real builder
    public class WireContractMachineOutputSpaceShortTest
    {
        [TestCase(true)]
        [TestCase(false)]
        public void ApplyCarriesReceivedOutputSpaceShortToWire(bool isOutputSpaceShort)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId);
            var common = CommonMachineBlockStateDetail.CreateState(0f, 100f, 0f, "Idle", "Idle");
            var machine = MachineBlockStateDetail.CreateState(0f, System.Guid.Empty, System.Guid.Empty, isOutputSpaceShort);
            var state = new BlockState(new Dictionary<string, byte[]> { { common.Key, common.Value }, { machine.Key, machine.Value } });

            // 受信状態を載せ親Builderで合成
            // Load the state and compose via the parent builder
            var gameObject = new GameObject("MachineOutputSpaceShortWireTest");
            var block = gameObject.AddComponent<BlockGameObject>();
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockMasterElement)).SetValue(block, master);
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockPosInfo)).SetValue(block, new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one));
            typeof(BlockGameObject).GetField("_blockStateMessagePack", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(block, new BlockStateMessagePack(state, Vector3Int.zero));
            var dto = new BlockInventoryDto { ItemSlots = new List<BlockItemSlotDto>(), FluidSlots = new List<BlockFluidSlotDto>() };
            BlockDetailDtoBuilder.Apply(dto, block, new BlockNetworkInfoCache(), new BeltTransportClientCache(new BeltSegmentExitStatisticsTable()), new FluidNetworkClientCache());

            Assert.AreEqual(isOutputSpaceShort, dto.Machine.OutputSpaceShort);
            Assert.AreEqual(isOutputSpaceShort, JObject.Parse(WebUiJson.Serialize(dto))["machine"]["outputSpaceShort"].Value<bool>());
            Object.DestroyImmediate(gameObject);
        }
    }
}
