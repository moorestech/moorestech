using System;
using System.Linq;
using System.Reflection;
using Game.Block.Blocks.Pump;
using Game.Block.Interface.Extension;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Interface;
using Game.Context;
using Game.Fluid;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class GearPumpTest
    {
        // テストで使用する流体（Water）のFluidId
        private static readonly Guid DefaultFluidGuid = new("00000000-0000-0000-1234-000000000001");
        
        [Test]
        public void GenerateFluid_ScalesWithGearPower()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var world = ServerContext.WorldBlockDatastore;

            // 出力を受けるための周囲パイプを設置（+Xはジェネレーター用に空ける）
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(-1, 0, 0), BlockDirection.North, out var pipeNegX);
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, 1), BlockDirection.North, out var pipePosZ);
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, -1), BlockDirection.North, out var pipeNegZ);

            // GearPumpを中心に設置
            world.TryAddBlock(ForUnitTestModBlockId.GearPump, Vector3Int.zero, BlockDirection.North, out var pumpBlock);

            // 期待生成レート（full power時の1秒あたり）
            var pumpParam = (GearPumpBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearPump).BlockParam;
            var fullRatePerSec = pumpParam.GenerateFluid.items.Sum(g => g.Amount / Math.Max(0.0001f, g.GenerateTime));

            // テストウィンドウ
            const float testSeconds = 4f;

            // 1) フルパワー（RequiredRpm / RequireTorque を満たす）
            // +XにSimpleGearGeneratorを設置し、ExtensionでRPM/Torqueを設定
            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(1, 0, 0), BlockDirection.East, out var generatorBlock);
            var simpleGenerator = generatorBlock.GetComponent<global::Game.Block.Blocks.Gear.SimpleGearGeneratorComponent>();
            simpleGenerator.SetGenerateRpm(pumpParam.RequiredRpm);
            simpleGenerator.SetGenerateTorque(pumpParam.RequireTorque);

            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalSeconds < testSeconds)
            {
                GameUpdater.UpdateWithWait();
            }

            var fullAmount = GetPipeAmount(pipeNegX) + GetPipeAmount(pipePosZ) + GetPipeAmount(pipeNegZ);
            var expectedFull = fullRatePerSec * testSeconds;
            Assert.AreEqual(expectedFull, fullAmount, expectedFull * 0.25f, "Full power generated amount mismatched");

            // 2) 供給不足（RPMを50%に低下）→ 生成量も50%になる
            // パイプ内を初期化（検証をわかりやすくするために新しい配置に切り替え）
            world.RemoveBlock(new Vector3Int(-1, 0, 0));
            world.RemoveBlock(new Vector3Int(0, 0, 1));
            world.RemoveBlock(new Vector3Int(0, 0, -1));

            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(-1, 0, 0), BlockDirection.North, out pipeNegX);
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, 1), BlockDirection.North, out pipePosZ);
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, -1), BlockDirection.North, out pipeNegZ);

            // ジェネレーターのRPMだけ半減（トルクは維持）
            var halfRpm = new RPM(Math.Max(0.0f, pumpParam.RequiredRpm / 2f));
            simpleGenerator.SetGenerateRpm(halfRpm.AsPrimitive());
            simpleGenerator.SetGenerateTorque(pumpParam.RequireTorque);
            start = DateTime.Now;
            while ((DateTime.Now - start).TotalSeconds < testSeconds)
            {
                GameUpdater.UpdateWithWait();
            }

            var halfAmount = GetPipeAmount(pipeNegX) + GetPipeAmount(pipePosZ) + GetPipeAmount(pipeNegZ);
            var expectedHalf = expectedFull * 0.5f;
            Assert.AreEqual(expectedHalf, halfAmount, expectedHalf * 0.3f, "Half power should generate ~50% amount");
        }

        [Test]
        public void SaveLoad_PreservesInnerTankState()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // ブロックの作成
            // Create block
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearPump, Vector3Int.zero, BlockDirection.North, out var originalPump);
            var outputComponent = originalPump.GetComponent<PumpFluidOutputComponent>();
            
            // ブロックに液体を追加
            // Add fluid to the block
            var pumpParam = (GearPumpBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearPump).BlockParam;
            var fluidGuid = pumpParam.GenerateFluid.items[0].FluidGuid;
            var fluidId = MasterHolder.FluidMaster.GetFluidId(fluidGuid);
            var targetAmount = Math.Min(pumpParam.InnerTankCapacity * 0.5f, pumpParam.InnerTankCapacity);
            outputComponent.EnqueueGeneratedFluid(new FluidStack(targetAmount, fluidId));

            // ブロック内の液体量を保存
            // Save the amount of fluid in the block
            var originalTank = GetPumpTankState(outputComponent);
            Assert.Greater(originalTank.Amount, 0d, "Original pump should hold fluid before save.");

            // ブロックをロードする
            // Load the block
            var saveState = originalPump.GetSaveState();
            var guid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearPump).BlockGuid;
            var loadedPump = ServerContext.BlockFactory.Load(guid, new BlockInstanceId(1), saveState, originalPump.BlockPositionInfo);
            var loadedTank = GetPumpTankState(loadedPump.GetComponent<PumpFluidOutputComponent>());

            // ロード後も液体量が維持されていることを確認
            // Verify that the amount of fluid is maintained after loading
            Assert.AreEqual(originalTank.FluidId, loadedTank.FluidId, "Loaded pump should retain fluid type.");
            Assert.AreEqual(originalTank.Amount, loadedTank.Amount, "Loaded pump should retain fluid amount.");
        }

        [Test]
        public void GenerateFluid_WithAdjacentInfinityTorqueGenerator()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var world = ServerContext.WorldBlockDatastore;

            // GearPumpを原点に設置。ポンプの周囲3方向にパイプを設置（+Xはジェネレーター用に空ける）
            world.TryAddBlock(ForUnitTestModBlockId.GearPump, Vector3Int.zero, BlockDirection.North, out var pumpBlock);

            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(-1, 0, 0), BlockDirection.North, out var pipeNegX);
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, 1), BlockDirection.North, out var pipePosZ);
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, -1), BlockDirection.North, out var pipeNegZ);

            // InfinityTorqueSimpleGearGeneratorを+Xに隣接設置（常時トルク供給）
            world.TryAddBlock(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, new Vector3Int(1, 0, 0), BlockDirection.East, out var generatorBlock);

            // 起動・接続安定化のため少しアップデート
            var testDurationSec = 2.5f;
            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalSeconds < testDurationSec)
            {
                GameUpdater.UpdateWithWait();
            }

            // ポンプの出力先パイプに液体が生成されていること（> 0）を確認
            var totalOut = GetPipeAmount(pipeNegX) + GetPipeAmount(pipePosZ) + GetPipeAmount(pipeNegZ);
            Assert.Greater(totalOut, 0, "Adjacent InfinityTorque generator should power pump to output fluid.");
            
        }

        private static double GetPipeAmount(IBlock pipeBlock)
        {
            var comp = pipeBlock.GetComponent<FluidPipeComponent>();
            var field = typeof(FluidPipeComponent).GetField("_fluidContainer", BindingFlags.NonPublic | BindingFlags.Instance);
            var container = (FluidContainer)field.GetValue(comp);
            return container.Amount;
        }

        private static (double Amount, FluidId FluidId) GetPumpTankState(PumpFluidOutputComponent outputComponent)
        {
            var tankField = typeof(PumpFluidOutputComponent).GetField("_tank", BindingFlags.NonPublic | BindingFlags.Instance);
            var container = (FluidContainer)tankField.GetValue(outputComponent);
            return (container.Amount, container.FluidId);
        }
    }
}
