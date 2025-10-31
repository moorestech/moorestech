using System;
using System.Linq;
using System.Reflection;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Pump;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class ElectricPumpTest
    {
        [Test]
        public void GenerateFluid_ScalesWithElectricPower()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var world = ServerContext.WorldBlockDatastore;

            // ポンプとパイプを設置
            // Arrange pump and pipes
            PlacePumpWithPipes(out var pumpComponent, out var pipeNegX, out var pipePosZ, out var pipeNegZ);

            // パラメーターの取得
            // Get parameters
            var pumpParam = (ElectricPumpBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricPump).BlockParam;
            var requiredPower = new ElectricPower(pumpParam.RequiredPower);
            var fullRatePerSec = pumpParam.GenerateFluid.items.Sum(g => g.Amount / Math.Max(0.0001f, g.GenerateTime));

            const float testSeconds = 4f;

            // フルパワー供給で一定時間稼働させ、生成された液体量を計測
            // Run at full power for a set time and measure the amount of fluid generated
            RunForSeconds(pumpComponent, requiredPower, testSeconds);
            
            // フルパワー供給時に期待される生成量と実際の生成量が概ね一致することを確認
            // Verify that the expected and actual output amounts are approximately equal at full power
            var expectedAmount = fullRatePerSec * testSeconds;
            Assert.AreEqual(expectedAmount, GetTotalPipeAmount(), expectedAmount * 0.1f, "Full power should produce expected amount");

            
            
            
            // ブロックをリセットして再度テスト
            // Reset blocks and test again
            ResetBlocks(out pumpComponent, out pipeNegX, out pipePosZ, out pipeNegZ);
            
            // 供給電力を半分にした場合、生成量も比例して半減することを確認
            // Verify that halving the supplied power roughly halves the output
            var halfPower = new ElectricPower(requiredPower.AsPrimitive() / 2f);
            RunForSeconds(pumpComponent, halfPower, testSeconds);

            var expectedHalf = expectedAmount * 0.5f;
            Assert.AreEqual(expectedHalf,  GetTotalPipeAmount(), expectedHalf * 0.1f, "Half power should scale output");

            #region Internal

            // ElectricPump本体と周囲3方向のパイプを配置
            // Place the ElectricPump itself and pipes in 3 surrounding directions
            void PlacePumpWithPipes(out ElectricPumpComponent pumpComponentRef, out IBlock pipeNegXRef, out IBlock pipePosZRef, out IBlock pipeNegZRef)
            {
                world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(-1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out pipeNegXRef);
                world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out pipePosZRef);
                world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, -1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out pipeNegZRef);
                world.TryAddBlock(ForUnitTestModBlockId.ElectricPump, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pump);
                
                pumpComponentRef = pump.GetComponent<ElectricPumpComponent>();
            }
            
            // パイプを再設置
            // Re-place pipes
            void ResetBlocks(out ElectricPumpComponent pumpComponentRef, out IBlock pipeNegXRef, out IBlock pipePosZRef, out IBlock pipeNegZRef)
            {
                world.RemoveBlock(Vector3Int.zero);
                world.RemoveBlock(new Vector3Int(-1, 0, 0));
                world.RemoveBlock(new Vector3Int(0, 0, 1));
                world.RemoveBlock(new Vector3Int(0, 0, -1));
                
                PlacePumpWithPipes(out pumpComponentRef, out pipeNegXRef, out pipePosZRef, out pipeNegZRef);
            }

            // 指定秒数分、毎フレーム電力を供給しながらゲームを進めるヘルパー
            // Helper to run the game for a specified number of seconds while supplying power each frame
            void RunForSeconds(ElectricPumpComponent component, ElectricPower supply, float seconds)
            {
                var start = DateTime.Now;
                while ((DateTime.Now - start).TotalSeconds < seconds)
                {
                    component.SupplyEnergy(supply);
                    GameUpdater.UpdateWithWait();
                }
            }
            
            // すべてのパイプの液体量を合計するヘルパー
            // Helper to sum the fluid amounts of all pipes
            double GetTotalPipeAmount()
            {
                return GetPipeAmount(pipeNegX) + GetPipeAmount(pipePosZ) + GetPipeAmount(pipeNegZ);
            }
            
            // パイプ内の液体量を取得するヘルパー
            // Helper to get the amount of fluid inside a pipe
            double GetPipeAmount(IBlock pipeBlock)
            {
                var comp = pipeBlock.GetComponent<FluidPipeComponent>();
                var field = typeof(FluidPipeComponent)
                    .GetField("_fluidContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var container = (FluidContainer)field.GetValue(comp);
                return container.Amount;
            }

            #endregion
        }

        [Test]
        public void SaveLoad_PreservesInnerTankState()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // ElectricPumpを設置し、内部タンクに液体を注入
            // Place an ElectricPump and inject fluid into the internal tank
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricPump);
            var pumpParam = (ElectricPumpBlockParam)blockMaster.BlockParam;
            var positionInfo = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one);

            var originalPump = ServerContext.BlockFactory.Create(ForUnitTestModBlockId.ElectricPump, new BlockInstanceId(1), positionInfo);
            var outputComponent = originalPump.GetComponent<PumpFluidOutputComponent>();

            // 内部タンクに液体を注入
            // Inject fluid into the internal tank
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
            var loadedPump = ServerContext.BlockFactory.Load(blockMaster.BlockGuid, new BlockInstanceId(1), saveState, positionInfo);
            var loadedTank = GetPumpTankState(loadedPump.GetComponent<PumpFluidOutputComponent>());

            // ロード後も液体量が維持されていることを確認
            // Verify that the amount of fluid is maintained after loading
            Assert.AreEqual(originalTank.FluidId, loadedTank.FluidId, "Loaded pump should retain fluid type.");
            Assert.AreEqual(originalTank.Amount, loadedTank.Amount, "Loaded pump should retain fluid amount.");
        }

        private static (double Amount, FluidId FluidId) GetPumpTankState(PumpFluidOutputComponent outputComponent)
        {
            var tankField = typeof(PumpFluidOutputComponent).GetField("_tank", BindingFlags.NonPublic | BindingFlags.Instance);
            var container = (FluidContainer)tankField.GetValue(outputComponent);
            return (container.Amount, container.FluidId);
        }
    }
}
