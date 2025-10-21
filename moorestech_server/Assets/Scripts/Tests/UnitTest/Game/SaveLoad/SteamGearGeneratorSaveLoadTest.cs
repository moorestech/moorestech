using System.Collections.Generic;
using System.Reflection;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid;
using Game.Gear.Common;
using Game.PlayerInventory;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.CombinedTest.Core;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class SteamGearGeneratorSaveLoadTest
    {
        [Test]
        public void SteamGearGeneratorSaveLoadTest_AllStates()
        {
            // DIコンテナの初期化
            var (blockFactory, worldBlockDatastore, _, assembleSaveJsonText, _) = CreateBlockTestModule();
            
            // SteamGearGeneratorを設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SteamGearGeneratorId, Vector3Int.zero, BlockDirection.North, out var steamGeneratorBlock);
            
            // 複数のパイプを設置して十分な蒸気を供給
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, -1), BlockDirection.North, out var fluidPipeBlock1);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(1, 0, 0), BlockDirection.North, out var fluidPipeBlock2);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, 1), BlockDirection.North, out var fluidPipeBlock3);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(-1, 0, 0), BlockDirection.North, out var fluidPipeBlock4);
            var pipes = new[] { fluidPipeBlock1, fluidPipeBlock2, fluidPipeBlock3, fluidPipeBlock4 };
            
            // コンポーネントを取得
            var steamGeneratorComponent = steamGeneratorBlock.GetComponent<SteamGearGeneratorComponent>();
            var fluidComponent = steamGeneratorBlock.GetComponent<SteamGearGeneratorFluidComponent>();
            
            // タンクに十分な蒸気を溜める
            for (int i = 0; i < 20; i++)
            {
                SetSteam();
                GameUpdater.UpdateWithWait();
                
                var tank = fluidComponent.SteamTank;
                if (tank.Amount >= 10.0)
                {
                    break;
                }
            }
            
            // 加速が始まるまで待つ
            for (int i = 0; i < 30; i++)
            {
                SetSteam();
                GameUpdater.UpdateWithWait();
                
                if (steamGeneratorComponent.GenerateRpm.AsPrimitive() > 0)
                {
                    break;
                }
            }
            
            // 加速中の値を記録
            var acceleratingRpm = steamGeneratorComponent.GenerateRpm.AsPrimitive();
            var acceleratingTorque = steamGeneratorComponent.GenerateTorque.AsPrimitive();
            
            // 0より大きく、最大値未満であることを確認
            var param = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.SteamGearGeneratorId).BlockParam as SteamGearGeneratorBlockParam;
            Assert.Greater(acceleratingRpm, 0, "加速中のRPMは0より大きいはず");
            Assert.Less(acceleratingRpm, param.GenerateMaxRpm, "加速中のRPMは最大値未満のはず");
            
            // 現在の状態を取得
            var runtimeSave = JsonUtility.FromJson<SteamGearGeneratorSaveData>(steamGeneratorComponent.GetSaveState());
            
            // 流体コンポーネントの状態を取得
            var steamTank = fluidComponent.SteamTank;
            var tankFluidId = steamTank.FluidId;
            var tankAmount = steamTank.Amount;
            
            // JSONでセーブ
            var json = assembleSaveJsonText.AssembleSaveJson();
            Debug.Log(json);
            
            // ブロックを削除
            worldBlockDatastore.RemoveBlock(Vector3Int.zero);
            worldBlockDatastore.RemoveBlock(new Vector3Int(0, 0, -1));
            worldBlockDatastore.RemoveBlock(new Vector3Int(1, 0, 0));
            worldBlockDatastore.RemoveBlock(new Vector3Int(0, 0, 1));
            worldBlockDatastore.RemoveBlock(new Vector3Int(-1, 0, 0));
            
            // ロード
            var (_, loadWorldBlockDatastore, _, _, loadJsonFile) = CreateBlockTestModule();
            loadJsonFile.Load(json);
            
            var loadedSteamGeneratorBlock = loadWorldBlockDatastore.GetBlock(Vector3Int.zero);
            
            // ブロックID、intIDが同じであることを確認
            Assert.AreEqual(steamGeneratorBlock.BlockId, loadedSteamGeneratorBlock.BlockId);
            Assert.AreEqual(steamGeneratorBlock.BlockInstanceId, loadedSteamGeneratorBlock.BlockInstanceId);
            
            // コンポーネントの状態を確認
            var loadedSteamGeneratorComponent = loadedSteamGeneratorBlock.GetComponent<SteamGearGeneratorComponent>();
            var loadedFluidComponent = loadedSteamGeneratorBlock.GetComponent<SteamGearGeneratorFluidComponent>();
            var loadedSave = JsonUtility.FromJson<SteamGearGeneratorSaveData>(loadedSteamGeneratorComponent.GetSaveState());
            
            // 出力値が同じであることを確認
            Assert.AreEqual(acceleratingRpm, loadedSteamGeneratorComponent.GenerateRpm.AsPrimitive(), 0.01f, "ロード後のRPMが一致しません");
            Assert.AreEqual(acceleratingTorque, loadedSteamGeneratorComponent.GenerateTorque.AsPrimitive(), 0.01f, "ロード後のトルクが一致しません");
            
            // 内部状態が同じであることを確認
            Assert.AreEqual(runtimeSave.CurrentState, loadedSave.CurrentState, "状態が一致しません");
            Assert.AreEqual(runtimeSave.StateElapsedTime, loadedSave.StateElapsedTime, 0.01f, "経過時間が一致しません");
            Assert.AreEqual(runtimeSave.SteamConsumptionRate, loadedSave.SteamConsumptionRate, 0.01f, "消費率が一致しません");
            
            // 流体タンクの状態を確認
            var loadedSteamTank = loadedFluidComponent.SteamTank;
            Assert.AreEqual(tankFluidId, loadedSteamTank.FluidId, "流体IDが一致しません");
            Assert.AreEqual(tankAmount, loadedSteamTank.Amount, 0.01d, "流体量が一致しません");
            
            #region Internal
            
            void SetSteam()
            {
                foreach (var pipeBlock in pipes)
                {
                    var pipe = pipeBlock.GetComponent<FluidPipeComponent>();
                    var steamStack = new FluidStack(10000d, SteamGearGeneratorTest.SteamFluidId);
                    pipe.AddLiquid(steamStack, FluidContainer.Empty);
                }
            }
            
            #endregion
        }
        
        [Test]
        public void SteamGearGeneratorSaveLoadTest_DeceleratingState()
        {
            // DIコンテナの初期化
            var (blockFactory, worldBlockDatastore, _, assembleSaveJsonText, _) = CreateBlockTestModule();
            
            // SteamGearGeneratorを設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SteamGearGeneratorId, Vector3Int.zero, BlockDirection.North, out var steamGeneratorBlock);
            
            // 複数のパイプを設置して十分な蒸気を供給
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, -1), BlockDirection.North, out var fluidPipeBlock1);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(1, 0, 0), BlockDirection.North, out var fluidPipeBlock2);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, 1), BlockDirection.North, out var fluidPipeBlock3);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(-1, 0, 0), BlockDirection.North, out var fluidPipeBlock4);
            var pipes = new[] { fluidPipeBlock1, fluidPipeBlock2, fluidPipeBlock3, fluidPipeBlock4 };
            
            // コンポーネントを取得
            var steamGeneratorComponent = steamGeneratorBlock.GetComponent<SteamGearGeneratorComponent>();
            var param = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.SteamGearGeneratorId).BlockParam as SteamGearGeneratorBlockParam;
            
            // 初期化フェーズ
            for (int i = 0; i < 4; i++)
            {
                SetSteam();
                GameUpdater.UpdateWithWait();
            }
            
            // 現在の状態を取得するためのフィールド
            var currentStateField = typeof(SteamGearGeneratorComponent).GetField("_currentState", BindingFlags.NonPublic | BindingFlags.Instance);
            
            // 最大出力まで加速
            var startTime = System.DateTime.Now;
            while (System.DateTime.Now < startTime.AddSeconds(param.TimeToMax + 1))
            {
                SetSteam();
                GameUpdater.UpdateWithWait();
                
                if (steamGeneratorComponent.GenerateRpm.AsPrimitive() >= param.GenerateMaxRpm * 0.99f)
                {
                    break;
                }
            }
            
            // パイプを削除して減速を開始
            worldBlockDatastore.RemoveBlock(new Vector3Int(0, 0, -1));
            worldBlockDatastore.RemoveBlock(new Vector3Int(1, 0, 0));
            worldBlockDatastore.RemoveBlock(new Vector3Int(0, 0, 1));
            worldBlockDatastore.RemoveBlock(new Vector3Int(-1, 0, 0));
            
            // パイプ切断を検知するまで少し待つ（1更新で検知されるはず）
            GameUpdater.UpdateWithWait();
            
            // 減速が始まるまで待つ
            bool foundDecelerating = false;
            for (int i = 0; i < 10; i++)
            {
                GameUpdater.UpdateWithWait();
                
                // 現在の状態を確認
                var state = currentStateField.GetValue(steamGeneratorComponent).ToString();
                var rpm = steamGeneratorComponent.GenerateRpm.AsPrimitive();
                Debug.Log($"Update {i}: State={state}, RPM={rpm}");
                
                if (state == "Decelerating")
                {
                    foundDecelerating = true;
                    // 減速が始まったら、さらに数回更新して値が下がるのを待つ
                    for (int j = 0; j < 10; j++)
                    {
                        GameUpdater.UpdateWithWait();
                        rpm = steamGeneratorComponent.GenerateRpm.AsPrimitive();
                        Debug.Log($"Deceleration update {j}: RPM={rpm}");
                        
                        if (rpm < param.GenerateMaxRpm * 0.99f)
                        {
                            break;
                        }
                    }
                    break;
                }
            }
            
            Assert.IsTrue(foundDecelerating, "減速状態に移行しませんでした");
            
            // 減速中の値を記録
            var deceleratingRpm = steamGeneratorComponent.GenerateRpm.AsPrimitive();
            var deceleratingTorque = steamGeneratorComponent.GenerateTorque.AsPrimitive();
            
            // 0より大きく、最大値未満であることを確認
            Assert.Greater(deceleratingRpm, 0, "減速中のRPMは0より大きいはず");
            Assert.Less(deceleratingRpm, param.GenerateMaxRpm, "減速中のRPMは最大値未満のはず");
            
            // 現在の状態を取得（リフレクションを使用）
            var currentState = currentStateField.GetValue(steamGeneratorComponent).ToString();
            Assert.AreEqual("Decelerating", currentState, "減速状態のはず");
            
            // JSONでセーブ
            var json = assembleSaveJsonText.AssembleSaveJson();
            Debug.Log(json);
            
            // ブロックを削除
            worldBlockDatastore.RemoveBlock(Vector3Int.zero);
            
            // ロード
            var (_, loadWorldBlockDatastore, _, _, loadJsonFile) = CreateBlockTestModule();
            loadJsonFile.Load(json);
            
            var loadedSteamGeneratorBlock = loadWorldBlockDatastore.GetBlock(Vector3Int.zero);
            var loadedSteamGeneratorComponent = loadedSteamGeneratorBlock.GetComponent<SteamGearGeneratorComponent>();
            
            // 出力値が同じであることを確認
            Assert.AreEqual(deceleratingRpm, loadedSteamGeneratorComponent.GenerateRpm.AsPrimitive(), 0.01f, "ロード後のRPMが一致しません");
            Assert.AreEqual(deceleratingTorque, loadedSteamGeneratorComponent.GenerateTorque.AsPrimitive(), 0.01f, "ロード後のトルクが一致しません");
            
            // 状態が減速中であることを確認
            var loadedCurrentState = currentStateField.GetValue(loadedSteamGeneratorComponent).ToString();
            Assert.AreEqual("Decelerating", loadedCurrentState, "ロード後も減速状態のはず");
            
            #region Internal
            
            void SetSteam()
            {
                foreach (var pipeBlock in pipes)
                {
                    var pipe = pipeBlock.GetComponent<FluidPipeComponent>();
                    var steamStack = new FluidStack(10000d, SteamGearGeneratorTest.SteamFluidId);
                    pipe.AddLiquid(steamStack, FluidContainer.Empty);
                }
            }
            
            #endregion
        }
        
        private (IBlockFactory, IWorldBlockDatastore, PlayerInventoryDataStore, AssembleSaveJsonText, WorldLoaderFromJson)
            CreateBlockTestModule()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var blockFactory = ServerContext.BlockFactory;
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var playerInventoryDataStore = serviceProvider.GetService<PlayerInventoryDataStore>();
            var loadJsonFile = serviceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;
            
            return (blockFactory, worldBlockDatastore, playerInventoryDataStore, assembleSaveJsonText, loadJsonFile);
        }
    }
}
