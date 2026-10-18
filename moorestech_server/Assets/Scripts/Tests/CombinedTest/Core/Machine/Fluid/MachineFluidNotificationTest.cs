using Tests.CombinedTest.Core.Fluid;
using Game.Block.Interface.Component;
using System;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.Fluid;
using Game.UnlockState;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UniRx;
using UnityEngine;
using static Tests.CombinedTest.Core.Machine.MachineFluidIOTest;

namespace Tests.CombinedTest.Core.Machine
{
    public class MachineFluidNotificationTest
    {
        // 流体の速度モデルが落ち着くのに十分な tick 数(ゲーム内 60 秒)
        // Enough ticks for the fluid velocity model to settle (60 in-game seconds)
        private static readonly uint FluidSettleTicks = GameUpdater.SecondsToTicks(60);

        [Test]
        public void FluidInventoryStateChangeNotificationTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            // 機械を設置し、入力液体を持つレシピを選択する（束縛前提。タンク0がfluid1の受け口になる）
            // Place the machine and select a recipe with input fluids (binding requires this; tank 0 becomes fluid1's inlet)
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidMachineId, Vector3Int.forward * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidMachineBlock);
            ServerContext.GetService<IGameUnlockStateDataController>().UnlockMachineRecipe(ForUnitTestMachineRecipeId.LockedMachineRecipe);
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestMachineRecipeId.LockedMachineRecipe);
            MachineRecipeSelectTestUtil.SelectRecipe(fluidMachineBlock, recipe);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, -1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);
            const double fluidAmount1 = 50d;
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();
            fluidPipe1.AddLiquid(new FluidStack(fluidAmount1, FluidId1), default);
            var fluidInventoryComponent = fluidMachineBlock.GetComponent<VanillaMachineFluidInventoryComponent>();
            Assert.IsNotNull(fluidInventoryComponent, "VanillaMachineFluidInventoryComponentが取得できません");
            var stateObservable = fluidInventoryComponent as IBlockStateObservable;
            Assert.IsNotNull(stateObservable, "VanillaMachineFluidInventoryComponentがIBlockStateObservableを実装していません");
            var stateChangeCount = 0;
            var disposable = stateObservable.OnChangeBlockState.Subscribe(_ =>
            {
                stateChangeCount++;
                Debug.Log($"Fluid inventory state changed! Count: {stateChangeCount}");
            });
            var initialDetails = stateObservable.GetBlockStateDetails();
            Assert.AreEqual(1, initialDetails.Length, "BlockStateDetailsは単一の要素を含むべきです");
            Assert.AreEqual(FluidMachineInventoryStateDetail.BlockStateDetailKey, initialDetails[0].Key, "BlockStateDetailのキーが正しくありません");
            var initialState = MessagePackSerializer.Deserialize<FluidMachineInventoryStateDetail>(initialDetails[0].Value);
            Assert.AreEqual(3, initialState.InputTanks.Count, "入力タンクの数が正しくありません");
            Assert.AreEqual(2, initialState.OutputTanks.Count, "出力タンクの数が正しくありません");
            foreach (var tank in initialState.InputTanks)
            {
                Assert.AreEqual(FluidMaster.EmptyFluidId.AsPrimitive(), tank.FluidId, "初期状態で入力タンクが空ではありません");
                Assert.AreEqual(0, tank.Amount, "初期状態で入力タンクに液体が入っています");
            }
            // パイプから機械への液体転送を実行
            var previousCount = stateChangeCount;
            Debug.Log("Starting fluid transfer from pipe to machine...");
            // 液体が転送されるまでアップデート
            // 液体が流れ切るまで tick を進める。壁時計の待ちは速い PC でも秒数ぶん必ず払うので tick 数に置き換えた
            // Advance ticks until the fluid has flowed through; the wall-clock wait cost its full seconds even on a fast PC, so it is counted in ticks
            GameUpdater.RunFrames(FluidSettleTicks);
            Assert.Greater(stateChangeCount, previousCount, "液体転送時にOnChangeBlockStateが発火していません");
            var afterTransferDetails = stateObservable.GetBlockStateDetails();
            var afterTransferState = MessagePackSerializer.Deserialize<FluidMachineInventoryStateDetail>(afterTransferDetails[0].Value);
            Assert.AreEqual(FluidId1.AsPrimitive(), afterTransferState.InputTanks[0].FluidId, "転送後の液体IDが正しくありません");
            Assert.Greater(afterTransferState.InputTanks[0].Amount, 0, "転送後の液体量が0より大きくありません");
            // 出力タンクから液体を排出するテスト
            Debug.Log("Testing fluid output from machine to pipe...");
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(-1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var outputPipeBlock);
            var outputContainers = MachineFluidTestUtil.GetOutputFluidContainers(fluidMachineBlock.GetComponent<VanillaMachineBlockInventoryComponent>());
            const double outputFluidAmount = 30d;
            outputContainers[0].AddLiquid(new FluidStack(outputFluidAmount, FluidId2));
            previousCount = stateChangeCount;
            // 出力処理を実行
            // 液体が流れ切るまで tick を進める。壁時計の待ちは速い PC でも秒数ぶん必ず払うので tick 数に置き換えた
            // Advance ticks until the fluid has flowed through; the wall-clock wait cost its full seconds even on a fast PC, so it is counted in ticks
            GameUpdater.RunFrames(FluidSettleTicks);
            Assert.Greater(stateChangeCount, previousCount, "液体出力時にOnChangeBlockStateが発火していません");
            var finalDetails = stateObservable.GetBlockStateDetails();
            var finalState = MessagePackSerializer.Deserialize<FluidMachineInventoryStateDetail>(finalDetails[0].Value);
            Assert.Greater(finalState.InputTanks[0].Amount, 0, "入力タンクに液体が残っているはずです");
            foreach (var tank in finalState.InputTanks)
            {
                Assert.Greater(tank.MaxCapacity, 0, "入力タンクの最大容量が設定されていません");
            }
            foreach (var tank in finalState.OutputTanks)
            {
                Assert.Greater(tank.MaxCapacity, 0, "出力タンクの最大容量が設定されていません");
            }
            disposable.Dispose();
            Debug.Log($"Test completed. Total state changes: {stateChangeCount}");
        }
    }
}
