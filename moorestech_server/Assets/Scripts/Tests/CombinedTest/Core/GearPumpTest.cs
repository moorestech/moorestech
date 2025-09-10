using System.Linq;
using Game.Block.Interface.Extension;
using System;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using Game.Fluid;
using Game.Gear.Common;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class GearPumpTest
    {
        // テストで使用する流体（Water）のFluidId
        private static readonly Guid DefaultFluidGuid = new("00000000-0000-0000-1234-000000000001");
        private static FluidId DefaultFluidId => MasterHolder.FluidMaster.GetFluidId(DefaultFluidGuid);

        [Test]
        public void GenerateFluid_ScalesWithGearPower()
        {
            // Arrange: DI起動 + BlockMasterからGearPumpを特定
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var world = ServerContext.WorldBlockDatastore;

            // 出力を受けるための周囲パイプを設置（方角を気にせず受けられるように4方向）
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(2, 0, 0), BlockDirection.North, out var pipePosX);
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(-2, 0, 0), BlockDirection.North, out var pipeNegX);
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, 2), BlockDirection.North, out var pipePosZ);
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, -2), BlockDirection.North, out var pipeNegZ);

            // GearPumpを設置 (x=1の位置)
            world.TryAddBlock(ForUnitTestModBlockId.GearPump, new Vector3Int(1, 0, 0), BlockDirection.North, out var pumpBlock);
            
            // SimpleGearGeneratorを隣に設置してギアネットワークを構築 (x=0の位置)
            world.TryAddBlock(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, Vector3Int.zero, BlockDirection.North, out var generatorBlock);

            // 期待生成レート（full power時の1秒あたり）
            var pumpParam = (GearPumpBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearPump).BlockParam;
            var fullRatePerSec = pumpParam.GenerateFluid.Sum(g => g.Amount / Math.Max(0.0001f, g.GenerateTime));

            // テストウィンドウ
            const float testSeconds = 4f;

            // 1) フルパワー（RequiredRpm / RequireTorque を満たす）
            var generatorTransformer = generatorBlock.GetComponent<IGearEnergyTransformer>();
            Assert.NotNull(generatorTransformer, "Generator should implement IGearEnergyTransformer");
            
            // GearNetworkDatastoreを取得
            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            
            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalSeconds < testSeconds)
            {
                // ジェネレーターに動力を供給（InfinityTorqueなので十分な動力が出る）
                generatorTransformer.SupplyPower(new RPM(pumpParam.RequiredRpm * 2), new Torque(1000), true);
                
                // GearNetworkを手動で更新
                if (gearNetworkDatastore.GearNetworks.Count > 0)
                {
                    var gearNetwork = gearNetworkDatastore.GearNetworks.First().Value;
                    gearNetwork.ManualUpdate();
                }
                
                GameUpdater.UpdateWithWait();
            }

            var fullAmount = GetPipeAmount(pipePosX) + GetPipeAmount(pipeNegX) + GetPipeAmount(pipePosZ) + GetPipeAmount(pipeNegZ);
            var expectedFull = fullRatePerSec * testSeconds;
            Assert.AreEqual(expectedFull, fullAmount, expectedFull * 0.25f, "Full power generated amount mismatched");

            // 2) 供給不足（RPMを50%に低下）→ 生成量も50%になる
            // パイプ内を初期化（検証をわかりやすくするために新しい配置に切り替え）
            world.RemoveBlock(new Vector3Int(2, 0, 0));
            world.RemoveBlock(new Vector3Int(-2, 0, 0));
            world.RemoveBlock(new Vector3Int(0, 0, 2));
            world.RemoveBlock(new Vector3Int(0, 0, -2));

            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(2, 0, 0), BlockDirection.North, out pipePosX);
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(-2, 0, 0), BlockDirection.North, out pipeNegX);
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, 2), BlockDirection.North, out pipePosZ);
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, -2), BlockDirection.North, out pipeNegZ);

            var halfRpm = new RPM(Math.Max(0.0f, pumpParam.RequiredRpm / 2f));
            start = DateTime.Now;
            while ((DateTime.Now - start).TotalSeconds < testSeconds)
            {
                // ジェネレーターに半分のRPMを供給
                generatorTransformer.SupplyPower(halfRpm, new Torque(1000), true);
                
                // GearNetworkを手動で更新
                if (gearNetworkDatastore.GearNetworks.Count > 0)
                {
                    var gearNetwork = gearNetworkDatastore.GearNetworks.First().Value;
                    gearNetwork.ManualUpdate();
                }
                
                GameUpdater.UpdateWithWait();
            }

            var halfAmount = GetPipeAmount(pipePosX) + GetPipeAmount(pipeNegX) + GetPipeAmount(pipePosZ) + GetPipeAmount(pipeNegZ);
            var expectedHalf = expectedFull * 0.5f;
            Assert.AreEqual(expectedHalf, halfAmount, expectedHalf * 0.3f, "Half power should generate ~50% amount");
        }

        #region Internal

        private static double GetPipeAmount(IBlock pipeBlock)
        {
            var comp = pipeBlock.GetComponent<global::Game.Block.Blocks.Fluid.FluidPipeComponent>();
            var field = typeof(global::Game.Block.Blocks.Fluid.FluidPipeComponent)
                .GetField("_fluidContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var container = (FluidContainer)field.GetValue(comp);
            return container.Amount;
        }

        #endregion
    }
}
