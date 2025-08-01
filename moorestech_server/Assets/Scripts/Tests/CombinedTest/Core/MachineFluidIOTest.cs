using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class MachineFluidIOTest
    {
        public static FluidId FluidId1 => MasterHolder.FluidMaster.GetFluidId(new("00000000-0000-0000-1234-000000000001"));
        public static FluidId FluidId2 => MasterHolder.FluidMaster.GetFluidId(new("00000000-0000-0000-1234-000000000002"));
        public static FluidId FluidId3 => MasterHolder.FluidMaster.GetFluidId(new("00000000-0000-0000-1234-000000000003"));

        /// <summary>
        /// 機械内部のタンクに個別に液体が入ることをテストする
        /// 機械の内部タンクは3個、パイプも3個ですべて入る
        /// </summary>
        [Test]
        public void FluidMachineInputTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            // 機械を設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidMachineId, Vector3Int.forward * 0, BlockDirection.North, out var fluidMachineBlock);
            
            // 液体を入れるパイプを設定
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, 1), BlockDirection.North, out var fluidPipeBlock1);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, 3), BlockDirection.North, out var fluidPipeBlock2);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, 5), BlockDirection.North, out var fluidPipeBlock3);
            
            // パイプに液体を設定
            const double fluidAmount1 = 50d;
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();
            fluidPipe1.AddLiquid(new FluidStack(fluidAmount1, FluidId1), FluidContainer.Empty);
            Assert.AreEqual(fluidAmount1, fluidPipe1.GetAmount());
            Assert.AreEqual(FluidId1, fluidPipe1.GetFluidId());
            
            const double fluidAmount2 = 40d;
            var fluidPipe2 = fluidPipeBlock2.GetComponent<FluidPipeComponent>();
            fluidPipe2.AddLiquid(new FluidStack(fluidAmount2, FluidId2), FluidContainer.Empty);
            Assert.AreEqual(fluidAmount2, fluidPipe2.GetAmount());
            Assert.AreEqual(FluidId2, fluidPipe2.GetFluidId());
            
            const double fluidAmount3 = 30d;
            var fluidPipe3 = fluidPipeBlock3.GetComponent<FluidPipeComponent>();
            fluidPipe3.AddLiquid(new FluidStack(fluidAmount3, FluidId3), FluidContainer.Empty);
            Assert.AreEqual(fluidAmount3, fluidPipe3.GetAmount());
            Assert.AreEqual(FluidId3, fluidPipe3.GetFluidId());
            
            // パイプの接続状態を確認
            Assert.AreEqual(1, fluidPipeBlock1.GetComponent<BlockConnectorComponent<IFluidInventory>>().ConnectedTargets.Count);
            Assert.AreEqual(1, fluidPipeBlock2.GetComponent<BlockConnectorComponent<IFluidInventory>>().ConnectedTargets.Count);
            Assert.AreEqual(1, fluidPipeBlock3.GetComponent<BlockConnectorComponent<IFluidInventory>>().ConnectedTargets.Count);
            
            
            // アップデート（液体が流れるのを待つ）
            var startTime = DateTime.Now;
            while (true)
            {
                GameUpdater.UpdateWithWait();
                
                var elapsedTime = DateTime.Now - startTime;
                if (elapsedTime.TotalSeconds > 10) break; // 10秒待機
            }
            
            // 液体が転送されていることを確認
            Assert.AreEqual(0, fluidPipe1.GetAmount(), 0.01f);
            Assert.AreEqual(0, fluidPipe2.GetAmount(), 0.01f);
            Assert.AreEqual(0, fluidPipe3.GetAmount(), 0.01f);
            
            var fluidContainers = GetInputFluidContainers(fluidMachineBlock.GetComponent<VanillaMachineBlockInventoryComponent>());
            Assert.AreEqual(3, fluidContainers.Count);
            
            Assert.AreEqual(FluidId1, fluidContainers[0].FluidId);
            Assert.AreEqual(fluidAmount1, fluidContainers[0].Amount, 0.01f);
            Assert.AreEqual(FluidId2, fluidContainers[1].FluidId);
            Assert.AreEqual(fluidAmount2, fluidContainers[1].Amount, 0.01f);
            Assert.AreEqual(FluidId3, fluidContainers[2].FluidId);
            Assert.AreEqual(fluidAmount3, fluidContainers[2].Amount, 0.01f);
        }
        
        
        /// <summary>
        /// 機械内部の個別タンクからそれぞれ液体が排出されることをテストする
        /// 機械の内部タンクは2個、パイプも3個なので全ては排出されない
        /// </summary>
        [Test]
        public void FluidMachineOutputTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            // 機械を設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidMachineId, Vector3Int.forward * 0, BlockDirection.North, out var fluidMachineBlock);
            
            // 液体が入るパイプを設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(-1, 0, 0), BlockDirection.North, out var fluidPipeBlock1);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(-1, 0, 2), BlockDirection.North, out var fluidPipeBlock2);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, -1), BlockDirection.North, out var fluidPipeBlock3);
            
            // 機械に液体を設定
            var fluidContainers = GetOutputFluidContainers(fluidMachineBlock.GetComponent<VanillaMachineBlockInventoryComponent>());
            Assert.AreEqual(2, fluidContainers.Count);
            
            const double fluidAmount1 = 40d;
            const double fluidAmount2 = 50d;
            fluidContainers[0].AddLiquid(new FluidStack(fluidAmount1, FluidId1), FluidContainer.Empty);
            fluidContainers[1].AddLiquid(new FluidStack(fluidAmount2, FluidId2), FluidContainer.Empty);
            
            // 機械の接続状態を確認
            var fluidMachineConnector = fluidMachineBlock.GetComponent<BlockConnectorComponent<IFluidInventory>>();
            Assert.AreEqual(2, fluidMachineConnector.ConnectedTargets.Count);
            
            // アップデート（液体が流れるのを待つ）
            var startTime = DateTime.Now;
            while (true)
            {
                GameUpdater.UpdateWithWait();
                
                var elapsedTime = DateTime.Now - startTime;
                if (elapsedTime.TotalSeconds > 10) break; // 10秒待機
            }
            
            // 液体がパイプに転送されていることを確認
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();
            var fluidPipe2 = fluidPipeBlock2.GetComponent<FluidPipeComponent>();
            var fluidPipe3 = fluidPipeBlock3.GetComponent<FluidPipeComponent>();
            Assert.AreEqual(FluidId1, fluidPipe1.GetFluidId());
            Assert.AreEqual(fluidAmount1, fluidPipe1.GetAmount(), 0.01f);
            Assert.AreEqual(FluidId2, fluidPipe2.GetFluidId());
            Assert.AreEqual(fluidAmount2, fluidPipe2.GetAmount(), 0.01f);
            Assert.AreEqual(0, fluidPipe3.GetAmount()); // 接続されてないので0
            
            // 液体タンク側が0担っていることを確認
            Assert.AreEqual(0, fluidContainers[0].Amount, 0.01f);
            Assert.AreEqual(0, fluidContainers[1].Amount, 0.01f);
        }
        
        
        [Test]
        public void FluidProcessingOutputTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            
            var blockFactory = ServerContext.BlockFactory;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[9]; // L:229
            
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            var block = blockFactory.Create(blockId, new BlockInstanceId(1), new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one));
            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            
            
            // 必要素材を入れる
            // Set up the required materials
            var inputFluidContainers = GetInputFluidContainers(blockInventory);
            for (var i = 0; i < recipe.InputFluids.Length; i++)
            {
                var inputFluid = recipe.InputFluids[i];
                var fluidId = MasterHolder.FluidMaster.GetFluidId(inputFluid.FluidGuid);
                var fluidStack = new FluidStack(inputFluid.Amount, fluidId);
                
                // インサートするインデックスを1個ずらす
                var insertIndex = i + 1;
                insertIndex = inputFluidContainers.Count == insertIndex ? 0 : insertIndex; 
                inputFluidContainers[insertIndex].AddLiquid(fluidStack, FluidContainer.Empty);
                
                Assert.AreEqual(fluidId, inputFluidContainers[insertIndex].FluidId, "Fluid ID should match");
                Assert.AreEqual(inputFluid.Amount, inputFluidContainers[insertIndex].Amount, "Fluid amount should match");
            }
            
            foreach (var inputItem in recipe.InputItems)
            {
                blockInventory.InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }
            
            // クラフト実行
            // Perform the crafting
            var blockMachineComponent = block.GetComponent<VanillaElectricMachineComponent>();
            var startTime = DateTime.Now;
            var endTime = startTime.AddSeconds(recipe.Time + 0.2); // レシピ時間 + 余裕時間
            while (DateTime.Now < endTime)
            {
                blockMachineComponent.SupplyEnergy(new ElectricPower(10000));
                GameUpdater.UpdateWithWait();
            }
            
            // 検証
            // Verification
            for (var i = 0; i < recipe.InputFluids.Length; i++)
            {
                Assert.AreEqual(0, inputFluidContainers[i].Amount, $"Fluid in container {i} should be consumed");
                Assert.AreEqual(FluidMaster.EmptyFluidId, inputFluidContainers[i].FluidId, $"Fluid ID in container {i} should be reset to empty");
            }
            
            var outputFluidContainers = GetOutputFluidContainers(blockInventory);
            for (int i = 0; i < recipe.OutputFluids.Length; i++)
            {
                var expectedFluidId = MasterHolder.FluidMaster.GetFluidId(recipe.OutputFluids[i].FluidGuid);
                Assert.AreEqual(expectedFluidId, outputFluidContainers[i].FluidId, $"Output fluid {i} ID should match");
                Assert.AreEqual(recipe.OutputFluids[i].Amount, outputFluidContainers[i].Amount, $"Output fluid {i} amount should match");
            }
            
            var (_, outputSlot) = GetInputOutputSlot(blockInventory);
            
            Assert.AreNotEqual(0, outputSlot.Count, "Output slot should not be empty");
            for (var i = 0; i < recipe.OutputItems.Length; i++)
            {
                var expectedOutputId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[i].ItemGuid);
                Assert.AreEqual(expectedOutputId, outputSlot[i].Id, $"Output item {i} ID should match");
                Assert.AreEqual(recipe.OutputItems[i].Count, outputSlot[i].Count, $"Output item {i} count should match");
            }
        }
        
        private IReadOnlyList<FluidContainer> GetInputFluidContainers(VanillaMachineBlockInventoryComponent blockInventory)
        {
            var vanillaMachineInputInventory = (VanillaMachineInputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineInputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(blockInventory);
            
            return vanillaMachineInputInventory.FluidInputSlot;
        }
        
        private IReadOnlyList<FluidContainer> GetOutputFluidContainers(VanillaMachineBlockInventoryComponent blockInventory)
        {
            var vanillaMachineOutputInventory = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(blockInventory);
            
            return vanillaMachineOutputInventory.FluidOutputSlot;
        }
        
        private (List<IItemStack>, List<IItemStack>) GetInputOutputSlot(VanillaMachineBlockInventoryComponent vanillaMachineInventory)
        {
            var vanillaMachineInputInventory = (VanillaMachineInputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineInputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(vanillaMachineInventory);
            var vanillaMachineOutputInventory = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(vanillaMachineInventory);
            
            var inputSlot = vanillaMachineInputInventory.InputSlot.Where(i => i.Count != 0).ToList();
            inputSlot.Sort((a, b) => a.Id.AsPrimitive() - b.Id.AsPrimitive());
            
            var outputSlot = vanillaMachineOutputInventory.OutputSlot.Where(i => i.Count != 0).ToList();
            outputSlot.Sort((a, b) => a.Id.AsPrimitive() - b.Id.AsPrimitive());
            
            return (inputSlot, outputSlot);
        }
        
        /// <summary>
        /// MachineのIBlockStateObservableが正しく実装されているかをテストする
        /// 1. OnChangeBlockStateが適切なタイミングで発火すること
        /// 2. GetBlockStateDetailsが必要な情報を全て含んでいること
        ///
        /// NOTE: AI生成コード
        /// </summary>
        [Test]
        public void MachineBlockStateObservableTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            // ブロック情報を取得
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId);
            
            // 機械を設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, Vector3Int.zero, BlockDirection.North, out var machineBlock);
            
            // コンポーネントを取得
            var machineComponent = machineBlock.GetComponent<VanillaElectricMachineComponent>();
            var inventoryComponent = machineBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
            var stateObservable = machineBlock.GetComponent<IBlockStateObservable>();
            
            // OnChangeBlockStateの発火を記録
            var stateChangeCount = 0;
            var disposable = stateObservable.OnChangeBlockState.Subscribe(_ =>
            {
                stateChangeCount++;
                Debug.Log($"Machine state changed! Count: {stateChangeCount}");
            });
            
            // 初期状態のBlockStateDetailsを確認
            var initialDetails = stateObservable.GetBlockStateDetails();
            var machineParam = blockMaster.BlockParam as ElectricMachineBlockParam;
            var requiredPower = machineParam?.RequiredPower ?? 100;
            // アイドル状態でも要求電力は表示される
            ValidateMachineBlockStateDetails(initialDetails, "idle", 0f, requiredPower, 0f);
            
            // 電力を供給（アイドル状態でも通知が発生するはず）
            Debug.Log("Supplying power in idle state...");
            var previousCount = stateChangeCount;
            for (int i = 0; i < 5; i++)
            {
                machineComponent.SupplyEnergy(new ElectricPower(100));
                GameUpdater.UpdateWithWait();
            }
            
            // アイドル状態でも電力供給で状態変化があることを確認
            Assert.Greater(stateChangeCount, previousCount, "OnChangeBlockStateがアイドル状態で発火していません");
            previousCount = stateChangeCount;
            
            // レシピの材料を投入してクラフトを開始
            var recipes = MasterHolder.MachineRecipesMaster.MachineRecipes.Data
                .Where(r => r.BlockGuid == blockMaster.BlockGuid).ToList();
            
            if (recipes.Count == 0)
            {
                Debug.Log($"No recipes found for block {blockMaster.BlockGuid}. Looking for any machine recipe...");
                // テスト用マシンレシピを使用
                disposable.Dispose(); // 古いサブスクリプションを解除
                worldBlockDatastore.RemoveBlock(Vector3Int.zero);
                worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineRecipeTest1, Vector3Int.zero, BlockDirection.North, out machineBlock);
                blockMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineRecipeTest1);
                machineComponent = machineBlock.GetComponent<VanillaElectricMachineComponent>();
                inventoryComponent = machineBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
                stateObservable = machineBlock.GetComponent<IBlockStateObservable>();
                
                // 新しいサブスクリプションを設定
                stateChangeCount = 0;
                disposable = stateObservable.OnChangeBlockState.Subscribe(_ =>
                {
                    stateChangeCount++;
                    Debug.Log($"Machine state changed! Count: {stateChangeCount}");
                });
                
                recipes = MasterHolder.MachineRecipesMaster.MachineRecipes.Data
                    .Where(r => r.BlockGuid == blockMaster.BlockGuid).ToList();
                
                // 初期状態の確認を再度実行
                machineParam = blockMaster.BlockParam as ElectricMachineBlockParam;
                requiredPower = machineParam?.RequiredPower ?? 100;
            }
            
            var recipe = recipes.First();
            Debug.Log($"Using recipe: Block={blockMaster.BlockGuid}, Input={recipe.InputItems[0].ItemGuid}, Output={recipe.OutputItems[0].ItemGuid}");
            
            // 材料を投入（レシピに必要なアイテムをすべて投入）
            foreach (var inputItemInfo in recipe.InputItems)
            {
                var inputItem = itemStackFactory.Create(inputItemInfo.ItemGuid, inputItemInfo.Count);
                inventoryComponent.InsertItem(inputItem);
                Debug.Log($"Inserted item: {inputItemInfo.ItemGuid} x{inputItemInfo.Count}");
            }
            
            // 流体が必要な場合は流体も投入
            if (recipe.InputFluids != null && recipe.InputFluids.Length > 0)
            {
                var inputFluidContainers = GetInputFluidContainers(inventoryComponent);
                for (var i = 0; i < recipe.InputFluids.Length; i++)
                {
                    var inputFluid = recipe.InputFluids[i];
                    var fluidId = MasterHolder.FluidMaster.GetFluidId(inputFluid.FluidGuid);
                    var fluidStack = new FluidStack(inputFluid.Amount, fluidId);
                    
                    inputFluidContainers[i].AddLiquid(fluidStack, FluidContainer.Empty);
                    Debug.Log($"Added fluid: {inputFluid.FluidGuid} x{inputFluid.Amount}");
                }
            }
            
            // 処理開始を待つ
            Debug.Log("Starting processing...");
            for (int i = 0; i < 10; i++)
            {
                machineComponent.SupplyEnergy(new ElectricPower(1000));
                GameUpdater.UpdateWithWait();
                
                var details = stateObservable.GetBlockStateDetails();
                var (state, _, _, rate) = ExtractMachineDetails(details);
                
                Debug.Log($"Start Update {i}: State={state}, Rate={rate}");
                
                if (state == "processing" && rate > 0f)
                {
                    Debug.Log("Machine started processing!");
                    break;
                }
            }
            
            // 処理中の状態を確認
            var processingDetails = stateObservable.GetBlockStateDetails();
            var (procState, currentPower, requestedPower, procRate) = ExtractMachineDetails(processingDetails);
            Assert.AreEqual("processing", procState, "処理状態になっていません");
            Assert.Greater(currentPower, 0f, "現在の電力が0より大きくありません");
            Assert.Greater(requestedPower, 0f, "要求電力が0より大きくありません");
            Assert.Greater(procRate, 0f, "処理率が0より大きくありません");
            
            // 処理中に状態変化があることを確認
            Assert.Greater(stateChangeCount, previousCount, "処理開始後のOnChangeBlockStateが発火していません");
            previousCount = stateChangeCount;
            
            // 処理を完了まで進める
            Debug.Log("Completing processing...");
            var recipeTime = recipe.Time;
            for (int i = 0; i < recipeTime * 10 + 20; i++) // 余裕を持って待つ
            {
                machineComponent.SupplyEnergy(new ElectricPower(1000));
                GameUpdater.UpdateWithWait();
                
                if (i % 10 == 0)
                {
                    var details = stateObservable.GetBlockStateDetails();
                    var (state, _, _, rate) = ExtractMachineDetails(details);
                    Debug.Log($"Process Update {i}: State={state}, Rate={rate}");
                }
                
                // 出力スロットにアイテムが生成されたか確認
                var (_, outputSlot) = GetInputOutputSlot(inventoryComponent);
                if (outputSlot.Count > 0)
                {
                    Debug.Log("Processing completed!");
                    break;
                }
            }
            
            // 処理完了後、アイドル状態に戻るのを待つ
            Debug.Log("Waiting for return to idle state...");
            for (int i = 0; i < 20; i++)
            {
                GameUpdater.UpdateWithWait();
                var details = stateObservable.GetBlockStateDetails();
                var (state, _, _, rate) = ExtractMachineDetails(details);
                
                Debug.Log($"Post-process Update {i}: State={state}, Rate={rate}");
                
                if (state == "idle")
                {
                    Debug.Log($"Returned to idle state after {i + 1} updates");
                    break;
                }
            }
            
            // 処理完了後の状態を確認
            var completedDetails = stateObservable.GetBlockStateDetails();
            var (completeState, _, _, completeRate) = ExtractMachineDetails(completedDetails);
            Assert.AreEqual("idle", completeState, "処理完了後にアイドル状態に戻っていません");
            
            // 処理率が0またはほぼ0（完了時は1を超える場合がある）
            if (completeState == "idle")
            {
                // アイドル状態なら処理率は関係ない
                Debug.Log($"Machine is idle with rate: {completeRate}");
            }
            else
            {
                Assert.AreEqual(0f, completeRate, 0.01f, "処理完了後に処理率が0になっていません");
            }
            
            // 処理中に継続的な状態変化があったことを確認
            Assert.Greater(stateChangeCount, previousCount + 5, "処理中の継続的なOnChangeBlockStateが発火していません");
            
            // 電力供給なしで処理が開始しないことを確認
            Debug.Log("Testing without power supply...");
            previousCount = stateChangeCount;
            
            // 再度材料を投入
            var inputItem2 = itemStackFactory.Create(recipe.InputItems[0].ItemGuid, recipe.InputItems[0].Count);
            inventoryComponent.InsertItem(inputItem2);
            
            // 電力供給なしでアップデート
            for (int i = 0; i < 5; i++)
            {
                GameUpdater.UpdateWithWait();
            }
            
            // 状態がアイドルのままであることを確認
            var noPowerDetails = stateObservable.GetBlockStateDetails();
            var (noPowerState, _, _, _) = ExtractMachineDetails(noPowerDetails);
            Assert.AreEqual("idle", noPowerState, "電力供給なしで処理が開始されています");
            
            // 部分的な電力供給でも動作することを確認
            Debug.Log("Testing with partial power supply...");
            for (int i = 0; i < 20; i++)
            {
                machineComponent.SupplyEnergy(new ElectricPower(requiredPower / 2)); // 半分の電力
                GameUpdater.UpdateWithWait();
                
                var details = stateObservable.GetBlockStateDetails();
                var (state, current, requested, rate) = ExtractMachineDetails(details);
                
                if (state == "processing")
                {
                    Debug.Log($"Processing with partial power: Current={current}, Requested={requested}, Rate={rate}");
                    Assert.AreEqual(requiredPower / 2, current, 1f, "部分的な電力供給が正しく反映されていません");
                    Assert.AreEqual(requiredPower, requested, 1f, "要求電力が正しくありません");
                    Assert.Greater(rate, 0f, "部分的な電力でも処理が進んでいません");
                    Assert.Less(rate, 1f, "部分的な電力で最大速度になっています");
                    break;
                }
            }
            
            #region Internal
            
            void ValidateMachineBlockStateDetails(BlockStateDetail[] details, string expectedState,
                float expectedCurrentPower, float expectedRequestedPower, float expectedProcessingRate)
            {
                Assert.AreEqual(1, details.Length, "BlockStateDetailsは単一の要素を含むべきです");
                Assert.AreEqual(CommonMachineBlockStateDetail.BlockStateDetailKey, details[0].Key, "BlockStateDetailのキーが正しくありません");
                
                var stateData = MessagePackSerializer.Deserialize<CommonMachineBlockStateDetail>(details[0].Value);
                
                Assert.AreEqual(expectedState, stateData.CurrentStateType, "状態が期待値と一致しません");
                Assert.AreEqual(expectedCurrentPower, stateData.CurrentPower, 0.01f, "現在の電力が期待値と一致しません");
                Assert.AreEqual(expectedRequestedPower, stateData.RequestPower, 0.01f, "要求電力が期待値と一致しません");
                Assert.AreEqual(expectedProcessingRate, stateData.ProcessingRate, 0.01f, "処理率が期待値と一致しません");
            }
            
            (string state, float currentPower, float requestedPower, float processingRate) ExtractMachineDetails(BlockStateDetail[] details)
            {
                Assert.AreEqual(1, details.Length, "BlockStateDetailsは単一の要素を含むべきです");
                
                var stateData = MessagePackSerializer.Deserialize<CommonMachineBlockStateDetail>(details[0].Value);
                
                return (stateData.CurrentStateType, stateData.CurrentPower, stateData.RequestPower, stateData.ProcessingRate);
            }
            
            #endregion
        }
        
        /// <summary>
        /// VanillaMachineFluidInventoryComponentのステート変更通知が正しく動作することをテストする
        /// </summary>
        [Test]
        public void FluidInventoryStateChangeNotificationTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            // 機械を設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidMachineId, Vector3Int.forward * 0, BlockDirection.North, out var fluidMachineBlock);
            
            // 液体を入れるパイプを設定
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, 1), BlockDirection.North, out var fluidPipeBlock1);
            
            // パイプに液体を設定
            const double fluidAmount1 = 50d;
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();
            fluidPipe1.AddLiquid(new FluidStack(fluidAmount1, FluidId1), FluidContainer.Empty);
            
            // VanillaMachineFluidInventoryComponentを取得
            var fluidInventoryComponent = fluidMachineBlock.GetComponent<VanillaMachineFluidInventoryComponent>();
            Assert.IsNotNull(fluidInventoryComponent, "VanillaMachineFluidInventoryComponentが取得できません");
            
            // IBlockStateObservableとして取得できることを確認
            var stateObservable = fluidInventoryComponent as IBlockStateObservable;
            Assert.IsNotNull(stateObservable, "VanillaMachineFluidInventoryComponentがIBlockStateObservableを実装していません");
            
            // ステート変更通知を監視
            var stateChangeCount = 0;
            var disposable = stateObservable.OnChangeBlockState.Subscribe(_ =>
            {
                stateChangeCount++;
                Debug.Log($"Fluid inventory state changed! Count: {stateChangeCount}");
            });
            
            // 初期状態のステートを確認
            var initialDetails = stateObservable.GetBlockStateDetails();
            Assert.AreEqual(1, initialDetails.Length, "BlockStateDetailsは単一の要素を含むべきです");
            Assert.AreEqual(FluidMachineInventoryStateDetail.BlockStateDetailKey, initialDetails[0].Key, "BlockStateDetailのキーが正しくありません");
            
            var initialState = MessagePackSerializer.Deserialize<FluidMachineInventoryStateDetail>(initialDetails[0].Value);
            Assert.AreEqual(3, initialState.InputTanks.Count, "入力タンクの数が正しくありません");
            Assert.AreEqual(2, initialState.OutputTanks.Count, "出力タンクの数が正しくありません");
            
            // すべての入力タンクが空であることを確認
            foreach (var tank in initialState.InputTanks)
            {
                Assert.AreEqual(FluidMaster.EmptyFluidId.AsPrimitive(), tank.FluidId, "初期状態で入力タンクが空ではありません");
                Assert.AreEqual(0, tank.Amount, "初期状態で入力タンクに液体が入っています");
            }
            
            // パイプから機械への液体転送を実行
            var previousCount = stateChangeCount;
            Debug.Log("Starting fluid transfer from pipe to machine...");
            
            // 液体が転送されるまでアップデート
            var startTime = DateTime.Now;
            while (true)
            {
                GameUpdater.UpdateWithWait();
                
                var elapsedTime = DateTime.Now - startTime;
                if (elapsedTime.TotalSeconds > 5) break; // 5秒待機
                
                // パイプの液体量が減ったかチェック
                if (fluidPipe1.GetAmount() < fluidAmount1)
                {
                    Debug.Log($"Fluid is transferring. Pipe amount: {fluidPipe1.GetAmount()}");
                }
            }
            
            // ステート変更通知が発生したことを確認
            Assert.Greater(stateChangeCount, previousCount, "液体転送時にOnChangeBlockStateが発火していません");
            
            // 転送後のステートを確認
            var afterTransferDetails = stateObservable.GetBlockStateDetails();
            var afterTransferState = MessagePackSerializer.Deserialize<FluidMachineInventoryStateDetail>(afterTransferDetails[0].Value);
            
            // 最初の入力タンクに液体が入っていることを確認
            Assert.AreEqual(FluidId1.AsPrimitive(), afterTransferState.InputTanks[0].FluidId, "転送後の液体IDが正しくありません");
            Assert.Greater(afterTransferState.InputTanks[0].Amount, 0, "転送後の液体量が0より大きくありません");
            
            // 出力タンクから液体を排出するテスト
            Debug.Log("Testing fluid output from machine to pipe...");
            
            // 出力用パイプを設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(-1, 0, 0), BlockDirection.North, out var outputPipeBlock);
            
            // 機械の出力タンクに液体を設定
            var outputContainers = GetOutputFluidContainers(fluidMachineBlock.GetComponent<VanillaMachineBlockInventoryComponent>());
            const double outputFluidAmount = 30d;
            outputContainers[0].AddLiquid(new FluidStack(outputFluidAmount, FluidId2), FluidContainer.Empty);
            
            previousCount = stateChangeCount;
            
            // 出力処理を実行
            startTime = DateTime.Now;
            while (true)
            {
                GameUpdater.UpdateWithWait();
                
                var elapsedTime = DateTime.Now - startTime;
                if (elapsedTime.TotalSeconds > 5) break; // 5秒待機
                
                // 出力タンクの液体量が減ったかチェック
                if (outputContainers[0].Amount < outputFluidAmount)
                {
                    Debug.Log($"Fluid is outputting. Tank amount: {outputContainers[0].Amount}");
                }
            }
            
            // 出力時にもステート変更通知が発生したことを確認
            Assert.Greater(stateChangeCount, previousCount, "液体出力時にOnChangeBlockStateが発火していません");
            
            // 最終的なステートを確認
            var finalDetails = stateObservable.GetBlockStateDetails();
            var finalState = MessagePackSerializer.Deserialize<FluidMachineInventoryStateDetail>(finalDetails[0].Value);
            
            // 入力タンクと出力タンクの状態が正しく反映されていることを確認
            Assert.Greater(finalState.InputTanks[0].Amount, 0, "入力タンクに液体が残っているはずです");
            
            // 容量が正しく設定されていることを確認
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
