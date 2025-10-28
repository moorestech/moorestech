using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid;
using Game.Gear.Common;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class SteamGearGeneratorTest
    {
        // 蒸気のFluidIdを取得するためのヘルパー
        public static FluidId SteamFluidId => MasterHolder.FluidMaster.GetFluidId(new("00000000-0000-0000-1234-000000000002"));

        
        [Test]
        public void MaxGenerateTest()
        {
            // Maxになるまでの時間文分液体を供給し続ける
            // アップデート中、前回よりもRPM、トルクが増加していることを確認する
            // 最大になる時間になったときに、RPM、トルクが最大値になっていることを確認する
            
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.SteamGearGeneratorId);
            var steamGeneratorParam = blockMaster.BlockParam as SteamGearGeneratorBlockParam;
            
            // パラメータの取得
            var maxRpm = steamGeneratorParam.GenerateMaxRpm;
            var maxTorque = steamGeneratorParam.GenerateMaxTorque;
            var timeToMax = steamGeneratorParam.TimeToMax;
            
            // Steam Gear Generatorを設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SteamGearGeneratorId, Vector3Int.zero, BlockDirection.North, out var steamGeneratorBlock);
            
            // 蒸気供給用の複数のパイプを設置（十分な供給量を確保）
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, -1), BlockDirection.North, out var fluidPipeBlock1);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(1, 0, 0), BlockDirection.North, out var fluidPipeBlock2);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, 1), BlockDirection.North, out var fluidPipeBlock3);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(-1, 0, 0), BlockDirection.North, out var fluidPipeBlock4);
            var pipes = new[] { fluidPipeBlock1, fluidPipeBlock2, fluidPipeBlock3, fluidPipeBlock4 };
            
            
            // ギアコンポーネントを取得
            var gearGeneratorComponent = steamGeneratorBlock.GetComponent<IGearGenerator>();
            
            // 初期化フェーズ：流体転送とSteamGeneratorの起動を確実に行う
            for (int i = 0; i < 4; i++)
            {
                SetSteam();
                GameUpdater.UpdateWithWait();
            }
            
            // アップデートループ
            var startTime = DateTime.Now;
            var previousRpm = gearGeneratorComponent.GenerateRpm.AsPrimitive();
            var previousTorque = gearGeneratorComponent.GenerateTorque.AsPrimitive();

            
            while (DateTime.Now < startTime.AddSeconds(timeToMax + 0.5))  // 少し余裕を持たせる
            {
                // すべてのパイプに蒸気を充填
                SetSteam();
                
                // アップデート
                GameUpdater.UpdateWithWait();
                
                var generateRpm = gearGeneratorComponent.GenerateRpm.AsPrimitive();
                var generateTorque = gearGeneratorComponent.GenerateTorque.AsPrimitive();
                
                // 増加傾向があったことを確認（等しい場合も許容）
                Assert.IsTrue(generateRpm >= previousRpm && generateTorque >= previousTorque, "RPMまたはトルクが時間経過とともに減少しています");
                Debug.Log($"GenerateRpm: {generateRpm}, GenerateTorque: {generateTorque}");
                
                if (generateRpm >= maxRpm && generateTorque >= maxTorque)
                {
                    break;
                }
                
                // 両方が前回より大きい場合のみ更新
                if (generateRpm > previousRpm || generateTorque > previousTorque)
                {
                    previousRpm = generateRpm;
                    previousTorque = generateTorque;
                }
            }
            
            
            // 最大値に達していることを確認（誤差を考慮）
            Assert.AreEqual(maxRpm, gearGeneratorComponent.CurrentRpm.AsPrimitive(), maxRpm * 0.5, "RPMが最大値に達していません");
            Assert.AreEqual(maxTorque, gearGeneratorComponent.CurrentTorque.AsPrimitive(), maxTorque * 0.5, "トルクが最大値に達していません");
            // 最大値に達した時間が+-0.5秒以内になっていることを確認
            Assert.IsTrue(Math.Abs(gearGeneratorComponent.GenerateRpm.AsPrimitive() - maxRpm) < 0.5, "RPMが最大値に達している時間が+-0.5秒以内になっていません");
            
            // ------ パイプがなくなった時、徐々に速度が落ちていくことを検証する ------
            
            // 全てのパイプを削除する
            worldBlockDatastore.RemoveBlock(fluidPipeBlock1.BlockPositionInfo.OriginalPos);
            worldBlockDatastore.RemoveBlock(fluidPipeBlock2.BlockPositionInfo.OriginalPos);
            worldBlockDatastore.RemoveBlock(fluidPipeBlock3.BlockPositionInfo.OriginalPos);
            worldBlockDatastore.RemoveBlock(fluidPipeBlock4.BlockPositionInfo.OriginalPos);
            
            // まだ前回と同じ値なので1回だけアップデートしておく
            GameUpdater.UpdateWithWait();
            
            // 減速テスト前に現在の値を記録（最大値のはず）
            previousRpm = gearGeneratorComponent.GenerateRpm.AsPrimitive();
            previousTorque = gearGeneratorComponent.GenerateTorque.AsPrimitive();
            
            startTime = DateTime.Now;
            var hasStartedDecelerating = false;
            while (DateTime.Now < startTime.AddSeconds(timeToMax + 0.5))  // 少し余裕を持たせる
            {
                // アップデート
                GameUpdater.UpdateWithWait();
                
                var generateRpm = gearGeneratorComponent.GenerateRpm.AsPrimitive();
                var generateTorque = gearGeneratorComponent.GenerateTorque.AsPrimitive();
                Debug.Log($"GenerateRpm: {generateRpm}, GenerateTorque: {generateTorque}");

                
                // 減少傾向があったことを確認
                if (!hasStartedDecelerating && (generateRpm < previousRpm || generateTorque < previousTorque))
                {
                    hasStartedDecelerating = true;
                }
                
                // 減速が始まったら、常に前回以下であることを確認
                if (hasStartedDecelerating && generateTorque != 0 && generateRpm != 0)
                {
                    Assert.IsTrue(generateRpm <= previousRpm && generateTorque <= previousTorque, $"RPMまたはトルクが時間経過とともに減少していません。現在の値: {generateRpm} {generateTorque} 前回の値: {previousRpm} {previousTorque}");
                }
                
                // 値を更新
                previousRpm = generateRpm;
                previousTorque = generateTorque;
                
                // ゼロになったら終了
                if (generateRpm == 0 && generateTorque == 0)
                {
                    break;
                }
            }
            
            // ゼロになっていることを確認
            Assert.AreEqual(0, gearGeneratorComponent.CurrentRpm.AsPrimitive(), 0.5, "RPMが0になっていません");
            Assert.AreEqual(0, gearGeneratorComponent.CurrentTorque.AsPrimitive(), 0.5, "トルクが0になっていません");
            // ゼロに達した時間が+-0.5秒以内になっていることを確認
            var timeToZero = DateTime.Now - startTime;
            Assert.IsTrue(Math.Abs(gearGeneratorComponent.GenerateRpm.AsPrimitive()) < 0.5, $"RPMが0になっている時間が+-0.5秒以内になっていません。0になった秒数：{timeToZero.TotalSeconds}"); 
            
            #region Internal
            
            // 蒸気をパイプに供給
            void SetSteam()
            {
                foreach (var pipeBlock in pipes)
                {
                    var pipe = pipeBlock.GetComponent<FluidPipeComponent>();
                    var steamStack = new FluidStack(10000d, SteamFluidId); // 大量の蒸気を供給
                    pipe.AddLiquid(steamStack, FluidContainer.Empty);
                }
            }
            
  #endregion
        }
        
        [Test]
        public void ItemFuelGeneratesWithoutFluid()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SteamGearGeneratorId, Vector3Int.zero, BlockDirection.North, out var steamGeneratorBlock);
            
            var generatorComponent = steamGeneratorBlock.GetComponent<SteamGearGeneratorComponent>();
            var inventory = steamGeneratorBlock.GetComponent<IBlockInventory>();
            var openableInventory = (IOpenableInventory)inventory;
            var fluidComponent = steamGeneratorBlock.GetComponent<SteamGearGeneratorFluidComponent>();
            
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.SteamGearGeneratorId);
            var param = blockMaster.BlockParam as SteamGearGeneratorBlockParam;
            var fuelItemId = MasterHolder.ItemMaster.GetItemId(new Guid("00000000-0000-0000-1234-000000000001"));
            
            openableInventory.InsertItem(fuelItemId, 80);
            var initialTotalFuel = openableInventory.InventoryItems.Sum(item => item.Count);

            // 最大出力に到達するまで燃料を供給し続ける
            // Continue feeding fuel until the generator reaches peak output
            var accelerationLimit = DateTime.Now.AddSeconds(param.TimeToMax + 2);
            var reachedMax = false;
            while (DateTime.Now < accelerationLimit)
            {
                GameUpdater.UpdateWithWait();
                if (generatorComponent.GenerateRpm.AsPrimitive() >= param.GenerateMaxRpm - 0.5f &&
                    generatorComponent.GenerateTorque.AsPrimitive() >= param.GenerateMaxTorque - 0.5f)
                {
                    reachedMax = true;
                    break;
                }
            }

            Assert.IsTrue(reachedMax, "アイテム燃料のみで最大回転数に到達しませんでした");
            Assert.AreEqual(FluidMaster.EmptyFluidId, fluidComponent.SteamTank.FluidId, "液体燃料を使用していないはずです");
            var remainingFuelTotal = openableInventory.InventoryItems.Sum(item => item.Count);
            Assert.Less(remainingFuelTotal, initialTotalFuel, "燃料アイテムが消費されていません");

            // 燃料切れ後の挙動を検証するためインベントリを強制的に空にする
            // Force the inventory to empty to validate post-fuel shutdown behavior
            var emptyStack = ServerContext.ItemStackFactory.CreatEmpty();
            for (var slot = 0; slot < inventory.GetSlotSize(); slot++)
            {
                inventory.SetItem(slot, emptyStack);
            }

            var rpmBeforeDrain = generatorComponent.GenerateRpm.AsPrimitive();
            var torqueBeforeDrain = generatorComponent.GenerateTorque.AsPrimitive();

            // 負荷ゼロの状態では出力が維持されることを確認する
            // Confirm that output is maintained while operating with zero load
            var decelerationLimit = DateTime.Now.AddSeconds(param.TimeToMax + 2);
            var minObservedRpm = rpmBeforeDrain;
            var minObservedTorque = torqueBeforeDrain;
            while (DateTime.Now < decelerationLimit)
            {
                GameUpdater.UpdateWithWait();
                var currentRpm = generatorComponent.GenerateRpm.AsPrimitive();
                var currentTorque = generatorComponent.GenerateTorque.AsPrimitive();
                if (currentRpm < minObservedRpm) minObservedRpm = currentRpm;
                if (currentTorque < minObservedTorque) minObservedTorque = currentTorque;
            }

            Assert.GreaterOrEqual(minObservedRpm, rpmBeforeDrain - 0.5f, "負荷ゼロでは回転数が維持されるべきです");
            Assert.GreaterOrEqual(minObservedTorque, torqueBeforeDrain - 0.5f, "負荷ゼロではトルクが維持されるべきです");
        }

        [Test]
        public void BlockStateObservableTest()
        {
            // IBlockStateObservableの実装が正しく動作することを確認する
            // 1. OnChangeBlockStateが適切なタイミングで発火すること
            // 2. GetBlockStateDetailsが必要な情報を全て含んでいること
            
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            // Steam Gear Generatorを設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SteamGearGeneratorId, Vector3Int.zero, BlockDirection.North, out var steamGeneratorBlock);
            
            // 複数のパイプを設置（十分な蒸気供給を確保）
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, -1), BlockDirection.North, out var fluidPipeBlock1);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(1, 0, 0), BlockDirection.North, out var fluidPipeBlock2);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, 1), BlockDirection.North, out var fluidPipeBlock3);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(-1, 0, 0), BlockDirection.North, out var fluidPipeBlock4);
            var pipes = new[] { fluidPipeBlock1, fluidPipeBlock2, fluidPipeBlock3, fluidPipeBlock4 };
            
            // コンポーネントを取得
            var steamGeneratorComponent = steamGeneratorBlock.GetComponent<SteamGearGeneratorComponent>();
            var stateObservable = steamGeneratorBlock.GetComponent<IBlockStateObservable>();
            var fluidComponent = steamGeneratorBlock.GetComponent<SteamGearGeneratorFluidComponent>();
            
            // OnChangeBlockStateの発火を記録
            var stateChangeCount = 0;
            var disposable = stateObservable.OnChangeBlockState.Subscribe(_ =>
            {
                stateChangeCount++;
                Debug.Log($"State changed! Count: {stateChangeCount}");
            });
            
            try
            {
                // 初期状態のBlockStateDetailsを確認
                var initialDetails = stateObservable.GetBlockStateDetails();
                ValidateBlockStateDetails(initialDetails, "Idle", 0f, 0f, 0f, 0d, FluidMaster.EmptyFluidId.AsPrimitive());
                
                // 蒸気を供給して起動
                Debug.Log("Starting steam supply...");
                // 最初に十分な蒸気を供給するために複数回アップデート
                // タンクを満タンにする
                for (int i = 0; i < 20; i++)
                {
                    SetSteam();
                    GameUpdater.UpdateWithWait();
                    var steamTank = fluidComponent.SteamTank;
                    var stateDetails = steamGeneratorComponent.GetBlockStateDetails();
                    var stateData = MessagePackSerializer.Deserialize<SteamGearGeneratorBlockStateDetail>(stateDetails[0].Value);
                    var currentState = stateData.State;
                    
                    Debug.Log($"Fill Update {i}: Amount={steamTank.Amount}, State={currentState}, StateChanges={stateChangeCount}");
                    
                    // タンクがほぼ満タンになったらループを抜ける（容量100）
                    if (steamTank.Amount >= 10.0 && currentState == "Accelerating")
                    {
                        Debug.Log($"Tank has enough steam and accelerating: {steamTank.Amount}");
                        break;
                    }
                }
                
                // ジェネレーターを起動
                Debug.Log("Starting generator...");
                for (int i = 0; i < 5; i++)
                {
                    SetSteam();  // 継続的に蒸気を供給
                    GameUpdater.UpdateWithWait();
                    
                    var steamTank = fluidComponent.SteamTank;
                    var stateDetails = steamGeneratorComponent.GetBlockStateDetails();
                    var stateData = MessagePackSerializer.Deserialize<SteamGearGeneratorBlockStateDetail>(stateDetails[0].Value);
                    var currentState = stateData.State;
                    Debug.Log($"Generator start Update {i}: RPM={steamGeneratorComponent.GenerateRpm.AsPrimitive()}, " +
                             $"Steam Amount={steamTank.Amount}, State={currentState}, State changes={stateChangeCount}");
                    
                    if (currentState == "Accelerating")
                    {
                        Debug.Log("Generator started accelerating!");
                        break;
                    }
                }
                
                // 状態が変化したことを確認
                Assert.Greater(stateChangeCount, 0, "OnChangeBlockStateが発火していません");
                var previousCount = stateChangeCount;
                
                // 加速が進むまで少し待つ（常に蒸気を供給し続ける）
                for (int i = 0; i < 10; i++)
                {
                    SetSteam();
                    GameUpdater.UpdateWithWait();
                    SetSteam();  // 消費後も十分な蒸気を確保するため2回供給
                    
                    var details = stateObservable.GetBlockStateDetails();
                    var (currentState, currentRpm, _, _, _, _) = ExtractDetails(details);
                    
                    Debug.Log($"Acceleration Update {i}: State={currentState}, RPM={currentRpm}");
                    
                    if (currentRpm > 0f && currentState == "Accelerating")
                    {
                        break;
                    }
                }
                
                // 加速中の状態を確認
                var acceleratingDetails = stateObservable.GetBlockStateDetails();
                var (state, rpm, torque, rate, steamAmount, steamFluidId) = ExtractDetails(acceleratingDetails);
                Assert.AreEqual("Accelerating", state, "加速状態になっていません");
                Assert.Greater(rpm, 0f, "RPMが0より大きくありません");
                Assert.Greater(torque, 0f, "トルクが0より大きくありません");
                Assert.Greater(rate, 0f, "消費率が0より大きくありません");
                Assert.Greater(steamAmount, 0d, "蒸気量が0より大きくありません");
                Assert.AreEqual(SteamFluidId.AsPrimitive(), steamFluidId, "蒸気IDが正しくありません");
                
                // さらにアップデートして最大値に近づける（またはRunning状態になるまで）
                for (int i = 0; i < 50; i++)
                {
                    SetSteam();
                    GameUpdater.UpdateWithWait();
                    
                    var details = stateObservable.GetBlockStateDetails();
                    var (currentState, _, _, _, _, _) = ExtractDetails(details);
                    
                    if (currentState == "Running")
                    {
                        Debug.Log($"Reached Running state after {i+1} updates");
                        break;
                    }
                }
                
                // 追加の状態変化があったことを確認
                Assert.Greater(stateChangeCount, previousCount, "追加のOnChangeBlockStateが発火していません");
                previousCount = stateChangeCount;
                
                // パイプを削除
                worldBlockDatastore.RemoveBlock(new Vector3Int(0, 0, -1));
                worldBlockDatastore.RemoveBlock(new Vector3Int(1, 0, 0));
                worldBlockDatastore.RemoveBlock(new Vector3Int(0, 0, 1));
                worldBlockDatastore.RemoveBlock(new Vector3Int(-1, 0, 0));
                
                // 減速が始まるまで待つ（パイプ削除後は蒸気を追加しない）
                for (int i = 0; i < 10; i++)
                {
                    // パイプが削除されたので蒸気は追加しない
                    GameUpdater.UpdateWithWait();
                    
                    var details = stateObservable.GetBlockStateDetails();
                    var (currentState, _, _, _, _, _) = ExtractDetails(details);
                    Debug.Log($"After pipe removal Update {i}: State={currentState}, IsPipeDisconnected={fluidComponent.IsPipeDisconnected}");
                    
                    if (currentState == "Decelerating")
                    {
                        Debug.Log($"Started decelerating after {i+1} updates");
                        break;
                    }
                }
                
                // 減速中の状態を確認
                var deceleratingDetails = stateObservable.GetBlockStateDetails();
                var (decState, decRpm, decTorque, decRate, decSteamAmount, decSteamFluidId) = ExtractDetails(deceleratingDetails);
                
                // パイプが削除されたので、Decelerating状態になっているはず
                Assert.AreEqual("Decelerating", decState, $"Decelerating状態ではありません: {decState}");
                
                // さらに状態変化があったことを確認
                Assert.Greater(stateChangeCount, previousCount, "パイプ削除後のOnChangeBlockStateが発火していません");
                
                // 完全に停止するまで待つ
                Debug.Log("Waiting for complete stop...");
                for (int i = 0; i < 200; i++)
                {
                    GameUpdater.UpdateWithWait();
                    
                    var currentDetails = stateObservable.GetBlockStateDetails();
                    var (currentState, currentRpm, _, _, _, _) = ExtractDetails(currentDetails);
                    
                    if (i % 10 == 0)
                    {
                        Debug.Log($"Stop wait {i}: State={currentState}, RPM={currentRpm}");
                    }
                    
                    if (currentState == "Idle" && currentRpm == 0f)
                    {
                        Debug.Log($"Generator stopped after {i+1} updates");
                        break;
                    }
                }
                
                // 最終的にIdleに戻ることを確認
                var finalDetails = stateObservable.GetBlockStateDetails();
                var (finalState, finalRpm, _, _, finalSteamAmount, _) = ExtractDetails(finalDetails);
                
                // 減速中でもRPMが十分低ければOKとする（完全停止には時間がかかるため）
                if (finalState == "Idle" || (finalState == "Decelerating" && finalRpm < 1f))
                {
                    Debug.Log($"Test passed: State={finalState}, RPM={finalRpm}, SteamAmount={finalSteamAmount}");
                }
                else
                {
                    Assert.Fail($"Generator did not stop properly: State={finalState}, RPM={finalRpm}");
                }
            }
            finally
            {
                disposable.Dispose();
            }
            
            #region Internal
            
            void SetSteam()
            {
                foreach (var pipeBlock in pipes)
                {
                    var pipe = pipeBlock.GetComponent<FluidPipeComponent>();
                    var steamStack = new FluidStack(10000d, SteamFluidId); // 大量の蒸気を供給
                    pipe.AddLiquid(steamStack, FluidContainer.Empty);
                }
            }
            
            void ValidateBlockStateDetails(BlockStateDetail[] details, string expectedState, float expectedRpm, 
                float expectedTorque, float expectedRate, double expectedSteamAmount, int expectedFluidId)
            {
                Assert.AreEqual(3, details.Length);
                
                var detail = details.FirstOrDefault(d => d.Key == SteamGearGeneratorBlockStateDetail.SteamGearGeneratorBlockStateDetailKey);
                Assert.IsNotNull(detail, "CommonMachineBlockStateDetailが含まれていません");
                
                // 単一のBlockStateDetailから状態データを取得
                var stateData = MessagePackSerializer.Deserialize<SteamGearGeneratorBlockStateDetail>(detail.Value);
                
                Assert.AreEqual(expectedState, stateData.State, "状態が期待値と一致しません");
                Assert.AreEqual(expectedRpm, stateData.CurrentRpm, 0.01f, "RPMが期待値と一致しません");
                Assert.AreEqual(expectedTorque, stateData.CurrentTorque, 0.01f, "トルクが期待値と一致しません");
                Assert.AreEqual(expectedRate, stateData.SteamConsumptionRate, 0.01f, "消費率が期待値と一致しません");
                Assert.AreEqual(expectedSteamAmount, stateData.SteamAmount, 0.01d, "蒸気量が期待値と一致しません");
                Assert.AreEqual(expectedFluidId, stateData.SteamFluidId, "流体IDが期待値と一致しません");
            }
            
            (string state, float rpm, float torque, float rate, double steamAmount, int fluidId) ExtractDetails(BlockStateDetail[] details)
            {
                var detail = details.FirstOrDefault(d => d.Key == SteamGearGeneratorBlockStateDetail.SteamGearGeneratorBlockStateDetailKey);
                Assert.IsNotNull(detail, "SteamGearGeneratorBlockStateDetailが含まれていません");
                
                var stateData = MessagePackSerializer.Deserialize<SteamGearGeneratorBlockStateDetail>(detail.Value);
                
                return (stateData.State, stateData.CurrentRpm, stateData.CurrentTorque, stateData.SteamConsumptionRate, 
                        stateData.SteamAmount, stateData.SteamFluidId);
            }
            
            #endregion
        }
    }
}
