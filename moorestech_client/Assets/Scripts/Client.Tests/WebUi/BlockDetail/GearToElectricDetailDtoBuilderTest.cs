using Client.WebUiHost.Game.Topics;
using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.BeltTransport.Statistics;
using Client.Game.InGame.Block;
using Game.Block.Interface.State;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Core.Master;
using Game.Block.Blocks.GearToElectric;
using Game.EnergySystem;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.WebUi.BlockDetail
{
    public class GearToElectricDetailDtoBuilderTest
    {
        // 3状態を文字列へ変換
        // Convert all three states to strings
        [TestCase(GearToElectricGenerationState.Generating, "generating")]
        [TestCase(GearToElectricGenerationState.Idle, "idle")]
        [TestCase(GearToElectricGenerationState.Stopped, "stopped")]
        public void BuildDetailCopiesStateAndMasterMaximum(GearToElectricGenerationState state, string expectedState)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var param = (GearToElectricGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearToElectricGenerator).BlockParam;
            var detail = new GearToElectricGeneratorBlockStateDetail(true, new RPM(120f), new Torque(100f), new ElectricPower(72f), state);
            var dto = GearToElectricDetailDtoBuilder.BuildDetail(detail, param);
            Assert.AreEqual(expectedState, dto.GenerationState);
            Assert.AreEqual(72f, dto.SuppliedPower, 0.001f);
            Assert.AreEqual(100f, dto.MaxGeneratedPower, 0.001f);
        }
        // 親の合成入口まで通し、委譲の欠落を検出する
        // Exercise the parent composition entry to detect a missing delegation
        [Test]
        public void ParentApplyIncludesGenerationStateSupplyAndMaximum()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearToElectricGenerator);
            var detail = new GearToElectricGeneratorBlockStateDetail(true, new RPM(120f), new Torque(100f), new ElectricPower(72f), GearToElectricGenerationState.Generating);
            var state = new BlockState(new Dictionary<string, byte[]>
            {
                { GearToElectricGeneratorBlockStateDetail.GearGeneratorBlockStateDetailKey, MessagePackSerializer.Serialize(detail) },
            });

            // 受信状態を実クライアントへ注入
            // Inject received state into a real client object
            var gameObject = new GameObject("ParentApplyGeneratorTest");
            var block = gameObject.AddComponent<BlockGameObject>();
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockMasterElement)).SetValue(block, master);
            typeof(BlockGameObject).GetField("_blockStateMessagePack", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(block, new BlockStateMessagePack(state, Vector3Int.zero));
            var dto = new BlockInventoryDto();
            BlockDetailDtoBuilder.Apply(dto, block, new BlockNetworkInfoCache(), new BeltTransportClientCache(new BeltSegmentExitStatisticsTable()), null);
            Object.DestroyImmediate(gameObject);

            // 公開DTOの3値で親の配線を保証する
            // Assert all three public DTO values to cover the parent wiring
            Assert.IsNotNull(dto.GearToElectric);
            Assert.AreEqual("generating", dto.GearToElectric.GenerationState);
            Assert.AreEqual(72f, dto.GearToElectric.SuppliedPower, 0.001f);
            Assert.AreEqual(100f, dto.GearToElectric.MaxGeneratedPower, 0.001f);
        }
    }
}
