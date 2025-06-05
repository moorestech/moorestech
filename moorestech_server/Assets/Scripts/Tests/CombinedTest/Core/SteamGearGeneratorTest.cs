using System;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
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
            
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
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
                
                // 両方が前回より大きい場合のみ更新
                if (generateRpm > previousRpm || generateTorque > previousTorque)
                {
                    previousRpm = generateRpm;
                    previousTorque = generateTorque;
                }
            }
            
            
            // 最大値に達していることを確認（誤差を考慮）
            Assert.AreEqual(maxRpm, gearGeneratorComponent.CurrentRpm.AsPrimitive(), maxRpm * 0.05, "RPMが最大値に達していません");
            Assert.AreEqual(maxTorque, gearGeneratorComponent.CurrentTorque.AsPrimitive(), maxTorque * 0.05, "トルクが最大値に達していません");
            
            // ------ パイプがなくなった時、徐々に速度が落ちていくことを検証する ------
            
            // 全てのパイプを削除する
            worldBlockDatastore.RemoveBlock(fluidPipeBlock1.BlockPositionInfo.OriginalPos);
            worldBlockDatastore.RemoveBlock(fluidPipeBlock2.BlockPositionInfo.OriginalPos);
            worldBlockDatastore.RemoveBlock(fluidPipeBlock3.BlockPositionInfo.OriginalPos);
            worldBlockDatastore.RemoveBlock(fluidPipeBlock4.BlockPositionInfo.OriginalPos);
            
            // まだ前回と同じ値なので1回だけアップデートしておく
            GameUpdater.UpdateWithWait();
            
            startTime = DateTime.Now;
            while (DateTime.Now < startTime.AddSeconds(timeToMax + 0.5))  // 少し余裕を持たせる
            {
                // アップデート
                GameUpdater.UpdateWithWait();
                
                var generateRpm = gearGeneratorComponent.GenerateRpm.AsPrimitive();
                var generateTorque = gearGeneratorComponent.GenerateTorque.AsPrimitive();
                
                // 減少傾向があったことを確認（等しい場合は非許容）
                if (generateTorque != 0 && generateRpm != 0) // 完全に停止するまで減速をチェックする
                {
                    Assert.IsTrue(generateRpm < previousRpm && generateTorque < previousTorque, $"RPMまたはトルクが時間経過とともに減少していません。{generateRpm} {generateTorque}");
                }
                
                // 両方が前回より小さい場合のみ更新
                if (generateRpm < previousRpm && generateTorque < previousTorque)
                {
                    previousRpm = generateRpm;
                    previousTorque = generateTorque;
                }
            }
            
            // ゼロになっていることを確認
            Assert.AreEqual(0, gearGeneratorComponent.CurrentRpm.AsPrimitive(), "RPMが0になっていません");
            Assert.AreEqual(0, gearGeneratorComponent.CurrentTorque.AsPrimitive(), "トルクが0になっていません");
            
            #region Internal
            
            // 蒸気をパイプに供給
            void SetSteam()
            {
                foreach (var pipeBlock in pipes)
                {
                    var pipe = pipeBlock.GetComponent<FluidPipeComponent>();
                    var steamStack = new FluidStack(1000d, SteamFluidId); // 大量の蒸気を供給
                    pipe.AddLiquid(steamStack, FluidContainer.Empty);
                }
            }
            
  #endregion
        }
    }
}