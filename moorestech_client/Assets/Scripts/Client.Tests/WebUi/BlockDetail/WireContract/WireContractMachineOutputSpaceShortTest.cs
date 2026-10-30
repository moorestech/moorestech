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
    // 受信した機械の状態詳細から、出力空き不足の理由が実Builderを通ってwireへ載る
    // The output-space-short reason in a received machine state detail reaches the wire through the real builder
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

            // 実オブジェクトへ受信状態を載せ、親Builderで合成する
            // Put the received state on a real object and compose through the parent builder
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
