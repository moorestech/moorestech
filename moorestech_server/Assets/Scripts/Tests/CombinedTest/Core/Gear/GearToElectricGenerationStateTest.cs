using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.GearToElectric;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

namespace Tests.CombinedTest.Core.Gear
{
    public class GearToElectricGenerationStateTest
    {
        // 孤立網でも需要0のtickが届き、前回供給を消す
        // Even an isolated network gets zero-demand ticks and clears prior supply
        [Test]
        public void UnwiredGeneratorReportsIdleGeneratingAndStopped()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.TestGearToElectricGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(1, 0, 0), BlockDirection.East, Array.Empty<BlockCreateParam>(), out var driveBlock);
            var generator = generatorBlock.GetComponent<GearToElectricGeneratorComponent>();
            var drive = driveBlock.GetComponent<SimpleGearGeneratorComponent>();
            var param = (GearToElectricGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearToElectricGenerator).BlockParam;

            // ブロックシステムと同じインターフェースから購読する
            // Subscribe through the same interface as the block system
            var changeCount = 0;
            generatorBlock.GetComponent<IBlockStateObservable>().OnChangeBlockState.Subscribe(_ => changeCount++);
            drive.SetGenerateRpm(param.GearConsumption.BaseRpm);
            drive.SetGenerateTorque(param.GearConsumption.BaseTorque);
            AdvanceTicks(30);
            Assert.AreEqual(GearToElectricGenerationState.Idle, ReadDetail().GenerationState);
            Assert.AreEqual(0f, ReadDetail().GeneratedElectricPower, 0.001f);

            // 人工需要で放電し、次の実tickでゼロへ戻す
            // Discharge for injected demand and return to zero on the next real tick
            var supply = generator.OutputEnergy().AsPrimitive();
            Assert.Greater(supply, 0f);
            var beforeDischarge = changeCount;
            generator.OnElectricTickPostProcess(new ElectricNetworkStatistics(supply, supply * 0.4f, 1f, 1));
            Assert.AreEqual(GearToElectricGenerationState.Generating, ReadDetail().GenerationState);
            Assert.AreEqual(supply * 0.4f, ReadDetail().GeneratedElectricPower, 0.01f);
            Assert.Greater(changeCount, beforeDischarge);
            var beforeTick = changeCount;
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(GearToElectricGenerationState.Idle, ReadDetail().GenerationState);
            Assert.AreEqual(0f, ReadDetail().GeneratedElectricPower, 0.001f);
            Assert.Greater(changeCount, beforeTick);

            // 回転を止めると状態が停止中へ変わる
            // Stopping rotation changes the state to stopped
            drive.SetGenerateRpm(0f);
            AdvanceTicks(30);
            Assert.AreEqual(GearToElectricGenerationState.Stopped, ReadDetail().GenerationState);

            #region Internal

            GearToElectricGeneratorBlockStateDetail ReadDetail()
            {
                var detail = generator.GetBlockStateDetails().First(d => d.Key == GearToElectricGeneratorBlockStateDetail.GearGeneratorBlockStateDetailKey);
                return MessagePackSerializer.Deserialize<GearToElectricGeneratorBlockStateDetail>(detail.Value);
            }

            #endregion
        }

        private static void AdvanceTicks(int count)
        {
            for (var tick = 0; tick < count; tick++) GameUpdater.UpdateOneTick();
        }
    }
}
