using Game.Block.Interface.Component;
using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.Fluid;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UniRx;
using UnityEngine;
using static Tests.CombinedTest.Core.Machine.MachineFluidInventoryTestHelper;
using static Tests.CombinedTest.Core.Machine.MachineStateDetailsTestHelper;

namespace Tests.CombinedTest.Core.Machine
{
    public class MachineStateObservableTest
    {
        [Test]
        public void MachineBlockStateObservableTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;
            // ブロック情報を取得
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId);
            // 機械を設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machineBlock);
            // コンポーネントを取得
            var machineComponent = machineBlock.GetComponent<VanillaMachineProcessorComponent>();
            var inventoryComponent = machineBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
            var stateObservable = machineBlock.GetComponent<IBlockStateObservable>();
            // OnChangeBlockStateの発火を記録
            var counter = new MachineStateNotificationCounter();
            var disposable = stateObservable.OnChangeBlockState.Subscribe(_ =>
            {
                counter.Count++;
                Debug.Log($"Machine state changed! Count: {counter.Count}");
            });
            // 初期状態のBlockStateDetailsを確認
            var initialDetails = stateObservable.GetBlockStateDetails();
            // マスタの型が変わったら欠損補完せず即座に落とす（フォールバック値でテストが空回りするのを防ぐ）
            // Fail immediately if the master type changes instead of papering over it with fallback values
            var machineParam = blockMaster.BlockParam as ElectricMachineBlockParam;
            Assert.IsNotNull(machineParam, "機械ブロックのマスタがElectricMachineBlockParamではありません");
            var requiredPower = machineParam.RequiredPower;
            var idlePowerRate = machineParam.IdlePowerRate;
            // idle中の要求電力は実効値（基礎×idlePowerRate）になる（ADR 0010）
            // While idle the requested power is the effective value (base × idlePowerRate) per ADR 0010
            ValidateMachineBlockStateDetails(initialDetails, "idle", 0f, requiredPower * idlePowerRate, 0f);
            // 電力を供給（アイドル状態でも通知が発生するはず）
            Debug.Log("Supplying power in idle state...");
            var previousCount = counter.Count;
            for (int i = 0; i < 5; i++)
            {
                machineComponent.SupplyExternalPower(100);
                GameUpdater.UpdateOneTick();
            }
            // アイドル状態でも電力供給で状態変化があることを確認
            Assert.Greater(counter.Count, previousCount, "OnChangeBlockStateがアイドル状態で発火していません");
            previousCount = counter.Count;
            // レシピの材料を投入してクラフトを開始
            var recipes = MasterHolder.MachineRecipesMaster.MachineRecipes.Data
                .Where(r => r.BlockGuid == blockMaster.BlockGuid).ToList();
            if (recipes.Count == 0)
            {
                Debug.Log($"No recipes found for block {blockMaster.BlockGuid}. Looking for any machine recipe...");
                // テスト用マシンレシピを使用
                disposable.Dispose(); // 古いサブスクリプションを解除
                worldBlockDatastore.RemoveBlock(Vector3Int.zero, BlockRemoveReason.ManualRemove);
                worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineRecipeTest1, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out machineBlock);
                blockMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineRecipeTest1);
                machineComponent = machineBlock.GetComponent<VanillaMachineProcessorComponent>();
                inventoryComponent = machineBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
                stateObservable = machineBlock.GetComponent<IBlockStateObservable>();
                // 新しいサブスクリプションを設定
                counter.Count = 0;
                disposable = stateObservable.OnChangeBlockState.Subscribe(_ =>
                {
                    counter.Count++;
                    Debug.Log($"Machine state changed! Count: {counter.Count}");
                });
                recipes = MasterHolder.MachineRecipesMaster.MachineRecipes.Data
                    .Where(r => r.BlockGuid == blockMaster.BlockGuid).ToList();
                // 初期状態の確認を再度実行（要求電力とアイドル率は差し替えたブロックのマスタから取り直す）
                // Re-read both the required power and the idle rate from the replacement block's master
                machineParam = blockMaster.BlockParam as ElectricMachineBlockParam;
                Assert.IsNotNull(machineParam, "差し替えた機械ブロックのマスタがElectricMachineBlockParamではありません");
                requiredPower = machineParam.RequiredPower;
                idlePowerRate = machineParam.IdlePowerRate;
            }
            var recipe = recipes.First();
            Debug.Log($"Using recipe: Block={blockMaster.BlockGuid}, Input={recipe.InputItems[0].ItemGuid}, Output={recipe.OutputItems[0].ItemGuid}");
            MachineRecipeSelectTestUtil.SelectRecipe(machineBlock, recipe);
            // 材料を投入（レシピに必要なアイテムをすべて投入）
            foreach (var inputItemInfo in recipe.InputItems)
            {
                var inputItem = itemStackFactory.Create(inputItemInfo.ItemGuid, inputItemInfo.Count);
                inventoryComponent.InsertItem(inputItem);
                Debug.Log($"Inserted item: {inputItemInfo.ItemGuid} x{inputItemInfo.Count}");
            }
            // 流体が必要な場合は流体も投入
            if (recipe.InputFluids != null && 0 < recipe.InputFluids.Length)
            {
                var inputFluidContainers = GetInputFluidContainers(inventoryComponent);
                for (var i = 0; i < recipe.InputFluids.Length; i++)
                {
                    var inputFluid = recipe.InputFluids[i];
                    var fluidId = MasterHolder.FluidMaster.GetFluidId(inputFluid.FluidGuid);
                    var fluidStack = new FluidStack(inputFluid.Amount, fluidId);
                    inputFluidContainers[i].AddLiquid(fluidStack);
                    Debug.Log($"Added fluid: {inputFluid.FluidGuid} x{inputFluid.Amount}");
                }
            }
            MachineStateProcessingTestHelper.Verify(machineComponent, inventoryComponent, stateObservable, recipe, counter, previousCount);
            MachineStatePowerTestHelper.Verify(machineComponent, inventoryComponent, stateObservable, recipe, requiredPower);
        }
    }
}
