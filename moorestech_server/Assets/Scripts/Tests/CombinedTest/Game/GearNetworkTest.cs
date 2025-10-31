using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Block.Blocks.Gear;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlockConnectInfoModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using System;

namespace Tests.CombinedTest.Game
{
    public class GearNetworkTest
    {
        [Test]
        //シンプルジェネレーターを設置し、簡易的な歯車NWを作り、RPM、回転方向があっているかをテスト
        //Install a simple generator, make a simple gear NW, and test if RPM and direction of rotation are correct.
        public void SimpleGeneratorAndGearRpmTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generator);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var shaft);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BigGear, new Vector3Int(-1, -1, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var bigGear);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, new Vector3Int(2, 0, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var smallGear);
            
            //ネットワークをアップデート
            //Update the network
            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDatastore.GearNetworks.First().Value;
            gearNetwork.ManualUpdate();
            
            //ジェネレーターの供給が正しいか
            //Is the generator supply correct?
            var generatorComponent = generator.GetComponent<IGearGenerator>();
            AreEqual(10.0f, generatorComponent.CurrentRpm);
            Assert.AreEqual(true, generatorComponent.GenerateIsClockwise);
            
            //シャフトの回転は正しいか
            //Is the rotation of the shaft correct?
            var shaftComponent = shaft.GetComponent<GearEnergyTransformer>();
            AreEqual(10.0f, shaftComponent.CurrentRpm);
            Assert.AreEqual(true, shaftComponent.IsCurrentClockwise);
            
            //BigGearの回転は正しいか
            //Is the rotation of BigGear correct?
            var bigGearComponent = bigGear.GetComponent<GearComponent>();
            AreEqual(10.0f, bigGearComponent.CurrentRpm);
            Assert.AreEqual(true, bigGearComponent.IsCurrentClockwise);
            
            //SmallGearの回転は正しいか
            //Is the rotation of SmallGear correct?
            var smallGearComponent = smallGear.GetComponent<GearComponent>();
            AreEqual(20.0f, smallGearComponent.CurrentRpm); // ギア比2:1 Gear ratio 2:1
            Assert.AreEqual(false, smallGearComponent.IsCurrentClockwise); // 回転が反転する Rotation is reversed
        }
        
        [Test]
        // ループした歯車NWを作成し、RPM、回転方向があっているかをテスト
        // Create a looped gear NW and test if RPM and direction of rotation are correct.
        public void LoopGearNetworkTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            // C - D
            // |   |
            // A - B
            //
            // A = 0,0,0
            // GeneratorはGearの下に
            
            const float rpm = 10.0f;
            
            var gearPositionA = new Vector3Int(0, 0, 0);
            var gearPositionB = new Vector3Int(1, 0, 0);
            var gearPositionC = new Vector3Int(0, 0, 1);
            var gearPositionD = new Vector3Int(1, 0, 1);
            var generatorPosition = new Vector3Int(0, 0, -1);
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, generatorPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPositionA, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var smallGearABlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPositionB, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var smallGearBBlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPositionC, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var smallGearCBlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPositionD, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var smallGearDBlock);
            
            var generator = generatorBlock.GetComponent<IGearGenerator>();
            var smallGearA = smallGearABlock.GetComponent<GearComponent>();
            var smallGearB = smallGearBBlock.GetComponent<GearComponent>();
            var smallGearC = smallGearCBlock.GetComponent<GearComponent>();
            var smallGearD = smallGearDBlock.GetComponent<GearComponent>();
            
            
            var gearNetworkDataStore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDataStore.GearNetworks.First().Value;
            gearNetwork.ManualUpdate();
            
            // Generatorの回転方向とRPMのテスト
            AreEqual(rpm, generator.CurrentRpm);
            Assert.AreEqual(true, generator.IsCurrentClockwise);
            
            // smallGearAの回転方向とRPMのテスト
            AreEqual(rpm, smallGearA.CurrentRpm);
            Assert.AreEqual(true, smallGearA.IsCurrentClockwise);
            
            // smallGearBの回転方向とRPMのテスト
            AreEqual(rpm, smallGearB.CurrentRpm);
            Assert.AreEqual(false, smallGearB.IsCurrentClockwise);
            
            // smallGearCの回転方向とRPMのテスト
            AreEqual(rpm, smallGearC.CurrentRpm);
            Assert.AreEqual(true, smallGearC.IsCurrentClockwise);
            
            // smallGearDの回転方向とRPMのテスト
            AreEqual(rpm, smallGearD.CurrentRpm);
            Assert.AreEqual(false, smallGearD.IsCurrentClockwise);
        }
        
        [Test]
        // BigGearを使ってRPMを変えたSmallGearと、RPMを変えていないSmallGearを無理やりつなぎ、ロックされることをテストする
        // Using BigGear, forcibly connect SmallGear with a different RPM and SmallGear with an unchanged RPM, and test that it locks.
        public void DifferentRpmGearNetworkToRockTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            var generatorPos = new Vector3Int(1, 1, 0); // 大歯車を使ってRPMを変化させた側の歯車
            var bigGearPos = new Vector3Int(0, 0, 1); // Gears on the side that changed RPM with large gears
            var smallGear1Pos = new Vector3Int(3, 1, 1);
            
            var smallGear2Pos = new Vector3Int(1, 1, 2); // RPMを変化させていない側の歯車（回転方向を変えないために2つの小歯車をつかう）
            
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BigGear, bigGearPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, smallGear2Pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, smallGear1Pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var smallGear1);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, smallGear2Pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var smallGear2);
            
            //RPMが違う歯車同士を強制的に接続
            //Force connection between gears with different RPM
            ForceConnectGear(smallGear1, smallGear2);
            
            // find the network
            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDatastore.GearNetworks.First().Value;
            
            Assert.NotNull(gearNetwork);
            
            //ネットワークをアップデート
            //Update the network
            gearNetwork.ManualUpdate();

            // ネットワークがロックされているかどうかを確認する
            Assert.AreEqual(GearNetworkStopReason.Rocked, gearNetwork.CurrentGearNetworkInfo.StopReason);
        }

        [Test]
        public void DifferentDirectionGearNetworkToRockTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            var generatorPosition = new Vector3Int(0, 0, 0);
            var gearPosition1 = new Vector3Int(0, 0, 1);
            var gearPosition2 = new Vector3Int(1, 0, 0);
            
            var gearPosition3 = new Vector3Int(0, 0, -1);
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPosition1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPosition2, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear2);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPosition3, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear3);
            
            //回転方向が違う歯車同士を強制的に接続
            //Forced connection of gears with different directions of rotation
            ForceConnectGear(gear2, gear3);
            
            var gearNetworkDataStore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDataStore.GearNetworks.First().Value;
            gearNetwork.ManualUpdate();

            // ネットワークがロックされているかどうかを確認する
            Assert.AreEqual(GearNetworkStopReason.Rocked, gearNetwork.CurrentGearNetworkInfo.StopReason);
        }

        [Test]
        public void MultiGeneratorOverrideRpmTest()
        {
            // 複数のジェネレーターのRPMがオーバーライドされるテスト
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            var fastGeneratorPosition = new Vector3Int(0, 0, 0);
            var fastGeneratorGearPosition = new Vector3Int(0, 0, 1);
            var smallGearAPosition = new Vector3Int(1, 0, 1);
            var generatorPosition = new Vector3Int(2, 0, 0);
            var generatorGearPosition = new Vector3Int(2, 0, 1);
            var smallGearBPosition = new Vector3Int(3, 0, 1);
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleFastGearGenerator, fastGeneratorPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fastGeneratorBlock);
            var fastGenerator = fastGeneratorBlock.GetComponent<IGearGenerator>();
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, fastGeneratorGearPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            
            // SmallGearA
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, smallGearAPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var smallGearABlock);
            var smallGearA = smallGearABlock.GetComponent<GearComponent>();
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            var generator = generatorBlock.GetComponent<IGearGenerator>();
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, generatorGearPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, smallGearBPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var smallGearBBlock);
            var smallGearB = smallGearBBlock.GetComponent<GearComponent>();
            
            var gearNetworkDataStore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDataStore.GearNetworks.First().Value;
            
            gearNetwork.ManualUpdate();
            
            Assert.AreEqual(fastGenerator.CurrentRpm, 20f);
            Assert.AreEqual(smallGearA.CurrentRpm, 20f);
            Assert.AreEqual(generator.CurrentRpm, 20f);
            Assert.AreEqual(smallGearB.CurrentRpm, 20f);
        }
        
        [Test]
        public void MultiGeneratorDifferentDirectionToRockTest()
        {
            // 複数のジェネレーターの回転方向が違うことでロックされるテスト
            // Gen1 - Gear1 このような構成になっている
            //        Gear2
            // Gen2 - Gear3
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            var generator1Position = new Vector3Int(0, 0, 0);
            var generator2Position = new Vector3Int(1, 0, 0);
            
            var gearPosition1 = new Vector3Int(0, 0, 1);
            var gearPosition2 = new Vector3Int(1, 0, 1);
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generator1Position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generator1Block);
            var generator1 = generator1Block.GetComponent<IGearEnergyTransformer>();
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generator2Position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generator2Block);
            var generator2 = generator2Block.GetComponent<IGearEnergyTransformer>();
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPosition1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear1Block);
            var gear1 = gear1Block.GetComponent<IGearEnergyTransformer>();
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPosition2, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear2Block);
            var gear2 = gear2Block.GetComponent<IGearEnergyTransformer>();
            
            var gearNetworkDataStore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDataStore.GearNetworks.First().Value;

            gearNetwork.ManualUpdate();

            Assert.AreEqual(GearNetworkStopReason.Rocked, gearNetwork.CurrentGearNetworkInfo.StopReason);
        }
        
        [Test]
        public void ServeTorqueTest()
        {
            // 機械によってトルクが消費されるテスト（正しいトルクが供給されるかのテスト
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            var generatorPosition = new Vector3Int(0, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            
            var gearPosition1 = new Vector3Int(0, 0, 1);
            var gearPosition2 = new Vector3Int(1, 0, 1);
            var gearPosition3 = new Vector3Int(2, 0, 1);
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear1Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition2, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear2Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition3, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear3Block);
            
            var gear1 = gear1Block.GetComponent<IGearEnergyTransformer>();
            var gear2 = gear2Block.GetComponent<IGearEnergyTransformer>();
            var gear3 = gear3Block.GetComponent<IGearEnergyTransformer>();
            
            var gearNetworkDataStore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDataStore.GearNetworks.First().Value;
            
            gearNetwork.ManualUpdate();
            
            AreEqual(10, gear1.CurrentPower);
            AreEqual(10, gear2.CurrentPower);
            AreEqual(10, gear3.CurrentPower);
        }
        
        [Test]
        public void ServeTorqueOverTest()
        {
            // エネルギー不足時にネットワークが完全停止することをテスト
            // ジェネレーターは3のトルクを生成するが、6つの歯車がつながっているため、要求するトルクは6になる
            // 必要GP > 生成GPのため、ネットワークは完全停止する（新仕様）
            // Test that the network halts completely when energy is insufficient
            // The generator generates 3 torque, but since it is connected to 6 gears, the required torque becomes 6
            // Since required GP > generated GP, the network halts completely (new specification)

            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            var generatorPosition = new Vector3Int(0, 0, 0);

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            var generator = generatorBlock.GetComponent<SimpleGearGeneratorComponent>();

            var gearPosition1 = new Vector3Int(0, 0, 1);
            var gearPosition2 = new Vector3Int(0, 0, 2);
            var gearPosition3 = new Vector3Int(1, 0, 2);
            var gearPosition4 = new Vector3Int(2, 0, 2);
            var gearPosition5 = new Vector3Int(2, 0, 3);
            var gearPosition6 = new Vector3Int(3, 0, 3);

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear1Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth20RequireTorqueTestGear, gearPosition2, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear2Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition3, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear3Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition4, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear4Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth20RequireTorqueTestGear, gearPosition5, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear5Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition6, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear6Block);

            var gear1 = gear1Block.GetComponent<IGearEnergyTransformer>();
            var gear2 = gear2Block.GetComponent<IGearEnergyTransformer>();
            var gear3 = gear3Block.GetComponent<IGearEnergyTransformer>();
            var gear4 = gear4Block.GetComponent<IGearEnergyTransformer>();
            var gear5 = gear5Block.GetComponent<IGearEnergyTransformer>();
            var gear6 = gear6Block.GetComponent<IGearEnergyTransformer>();

            var gearNetworkDataStore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDataStore.GearNetworks.First().Value;
            gearNetwork.ManualUpdate();

            // エネルギー不足により、すべてのコンポーネントのRPMが0になる
            AreEqual(0f, gear1.CurrentRpm);
            AreEqual(0f, gear2.CurrentRpm);
            AreEqual(0f, gear3.CurrentRpm);
            AreEqual(0f, gear4.CurrentRpm);
            AreEqual(0f, gear5.CurrentRpm);
            AreEqual(0f, gear6.CurrentRpm);

            // OperatingRateが0になる
            Assert.AreEqual(0f, gearNetwork.CurrentGearNetworkInfo.OperatingRate);

            // 必要GP > 生成GPであることを確認
            Assert.IsTrue(gearNetwork.CurrentGearNetworkInfo.TotalRequiredGearPower > gearNetwork.CurrentGearNetworkInfo.TotalGenerateGearPower);
        }
        
        
        [Test]
        // エネルギー不足時にネットワークが完全停止することをテスト（ギア比ありのケース）
        // Test that the network halts completely when energy is insufficient (case with gear ratio)
        public void TorqueHalfTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            var generatorPosition = new Vector3Int(0, 0, 0);

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            var generator = generatorBlock.GetComponent<SimpleGearGeneratorComponent>();
            // 生成するトルクを1に設定する
            // Set the generated torque to 1
            SetGenerateTorque(generator);

            var gearPosition1 = new Vector3Int(0, 0, 1);
            var gearPosition2 = new Vector3Int(1, 0, 1);

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth20RequireTorqueTestGear, gearPosition1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear1Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition2, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear2Block);

            var gear1 = gear1Block.GetComponent<IGearEnergyTransformer>();
            var gear2 = gear2Block.GetComponent<IGearEnergyTransformer>();

            var gearNetworkDataStore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDataStore.GearNetworks.First().Value;
            gearNetwork.ManualUpdate();

            // ジェネレーターのトルクが1、gear1とgear2の要求トルクの合計が1を超えるため、エネルギー不足で停止
            // Generator torque is 1, total required torque exceeds 1, so the network halts due to energy deficit
            AreEqual(0f, gear1.CurrentRpm);
            AreEqual(0f, gear2.CurrentRpm);
            AreEqual(0f, gear1.CurrentTorque);
            AreEqual(0f, gear2.CurrentTorque);

            // エネルギー収支を確認（必要GP > 生成GP）
            Assert.IsTrue(gearNetwork.CurrentGearNetworkInfo.TotalRequiredGearPower > gearNetwork.CurrentGearNetworkInfo.TotalGenerateGearPower);
            Assert.AreEqual(0f, gearNetwork.CurrentGearNetworkInfo.OperatingRate);
        }
        
        
        private void SetGenerateTorque(SimpleGearGeneratorComponent component)
        {
            var value = new Torque(1);
            
            var type = typeof(SimpleGearGeneratorComponent);
            var property = type.GetProperty("GenerateTorque", BindingFlags.Public | BindingFlags.Instance);
            var backingFieldName = $"<{property.Name}>k__BackingField";
            var backingField = type.GetField(backingFieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            backingField.SetValue(component, value);
        }
        
        [Test]
        public void GearNetworkMergeTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var gearNetworkDataStore = serviceProvider.GetService<GearNetworkDatastore>();
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            var generatorPosition = new Vector3Int(0, 0, 0);
            var gearPosition1 = new Vector3Int(1, 0, 0);
            var gearPosition2 = new Vector3Int(2, 0, 0);
            var gearPosition3 = new Vector3Int(3, 0, 0);
            
            // 2つのネットワークを作成
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPosition2, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPosition3, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            AreEqual(2, gearNetworkDataStore.GearNetworks.Count);
            
            // ネットワークをマージ
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPosition1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            AreEqual(1, gearNetworkDataStore.GearNetworks.Count);
            
            // ネットワークの分離のテスト
            ServerContext.WorldBlockDatastore.RemoveBlock(gearPosition2);
            AreEqual(2, gearNetworkDataStore.GearNetworks.Count);
        }
        
        private static void ForceConnectGear(IBlock gear1, IBlock gear2)
        {
            BlockConnectorComponent<IGearEnergyTransformer> gear1Connector = gear1.GetComponent<BlockConnectorComponent<IGearEnergyTransformer>>();
            var gear1Transform = gear1.GetComponent<IGearEnergyTransformer>();
            
            BlockConnectorComponent<IGearEnergyTransformer> gear2Connector = gear2.GetComponent<BlockConnectorComponent<IGearEnergyTransformer>>();
            var gear2Transform = gear2.GetComponent<IGearEnergyTransformer>();
            
            
            var gear1Info = new ConnectedInfo(new GearConnectOption(true), new GearConnectOption(true), gear1);
            var gear2Info = new ConnectedInfo(new GearConnectOption(true), new GearConnectOption(true), gear2);
            
            ((Dictionary<IGearEnergyTransformer, ConnectedInfo>)gear1Connector.ConnectedTargets).Add(gear2Transform, gear2Info);
            ((Dictionary<IGearEnergyTransformer, ConnectedInfo>)gear2Connector.ConnectedTargets).Add(gear1Transform, gear1Info);
        }
        
        private void AreEqual(float expected, RPM actual)
        {
            AreEqual(expected, actual.AsPrimitive());
        }
        
        private void AreEqual(float expected, Torque actual)
        {
            AreEqual(expected, actual.AsPrimitive());
        }
        
        private void AreEqual(float expected, GearPower actual)
        {
            AreEqual(expected, actual.AsPrimitive());
        }
        
        private void AreEqual(float expected, float actual)
        {
            // 0.01fの誤差を許容する
            // Allow an error of 0.01f
            Assert.IsTrue(Mathf.Abs(expected - actual) < 0.01f, $"Expected: {expected}, Actual: {actual}");
        }

        [Test]
        // エネルギー不足時にネットワーク全体が停止するテスト
        // Test that the entire network stops when energy is insufficient
        public void EnergyDeficitHaltTest()
        {
            // セットアップ: SimpleGearGenerator（トルク3）と高負荷歯車（要求トルク計4）を配置
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            var generatorPosition = new Vector3Int(0, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // 必要トルク計4の歯車を配置（ジェネレーターのトルク3を上回る）
            var gearPosition1 = new Vector3Int(0, 0, 1);
            var gearPosition2 = new Vector3Int(1, 0, 1);
            var gearPosition3 = new Vector3Int(2, 0, 1);
            var gearPosition4 = new Vector3Int(3, 0, 1);

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear1Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition2, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear2Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition3, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear3Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition4, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear4Block);

            var gear1 = gear1Block.GetComponent<IGearEnergyTransformer>();
            var gear2 = gear2Block.GetComponent<IGearEnergyTransformer>();
            var gear3 = gear3Block.GetComponent<IGearEnergyTransformer>();
            var gear4 = gear4Block.GetComponent<IGearEnergyTransformer>();

            // 実行: ネットワーク更新
            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDatastore.GearNetworks.First().Value;
            gearNetwork.ManualUpdate();

            // 検証: すべてのコンポーネントのRPMが0であること
            AreEqual(0f, gear1.CurrentRpm);
            AreEqual(0f, gear2.CurrentRpm);
            AreEqual(0f, gear3.CurrentRpm);
            AreEqual(0f, gear4.CurrentRpm);

            // 検証: GearNetworkInfoのOperatingRateが0であること
            Assert.AreEqual(0f, gearNetwork.CurrentGearNetworkInfo.OperatingRate);

            // 検証: TotalRequiredGearPowerがTotalGenerateGearPowerより大きいこと
            Assert.IsTrue(gearNetwork.CurrentGearNetworkInfo.TotalRequiredGearPower > gearNetwork.CurrentGearNetworkInfo.TotalGenerateGearPower);
        }

        [Test]
        // エネルギー回復時に通常動作が再開されるテスト
        // Test that normal operation resumes when energy recovers
        public void EnergyRecoveryTest()
        {
            // セットアップ: エネルギー不足状態を作成
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            var generatorPosition = new Vector3Int(0, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var gearPosition1 = new Vector3Int(0, 0, 1);
            var gearPosition2 = new Vector3Int(1, 0, 1);
            var gearPosition3 = new Vector3Int(2, 0, 1);
            var gearPosition4 = new Vector3Int(3, 0, 1);

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear1Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition2, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear2Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition3, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear3Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition4, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear4Block);

            var gear1 = gear1Block.GetComponent<IGearEnergyTransformer>();
            var gear2 = gear2Block.GetComponent<IGearEnergyTransformer>();
            var gear3 = gear3Block.GetComponent<IGearEnergyTransformer>();
            var gear4 = gear4Block.GetComponent<IGearEnergyTransformer>();

            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDatastore.GearNetworks.First().Value;

            // 最初の更新でエネルギー不足状態になることを確認
            gearNetwork.ManualUpdate();
            AreEqual(0f, gear1.CurrentRpm);

            // 実行: 高負荷歯車を2つ削除してエネルギー充足状態にする
            worldBlockDatastore.RemoveBlock(gearPosition3);
            worldBlockDatastore.RemoveBlock(gearPosition4);

            // ネットワークが再構築されるので、最新のネットワークを取得
            gearNetwork = gearNetworkDatastore.GearNetworks.First().Value;
            gearNetwork.ManualUpdate();

            // 検証: GearTransformerのCurrentRpmが0より大きいこと
            Assert.IsTrue(gear1.CurrentRpm.AsPrimitive() > 0f);
            Assert.IsTrue(gear2.CurrentRpm.AsPrimitive() > 0f);

            // 検証: GearNetworkInfo.OperatingRateが0より大きいこと
            Assert.IsTrue(gearNetwork.CurrentGearNetworkInfo.OperatingRate > 0f);

            // 検証: 通常の動力分配処理が正しく実行されていること
            Assert.IsTrue(gearNetwork.CurrentGearNetworkInfo.TotalRequiredGearPower <= gearNetwork.CurrentGearNetworkInfo.TotalGenerateGearPower);
        }

        [Test]
        // ロック検知がエネルギー不足判定より優先されることをテスト
        // Test that lock detection takes priority over energy deficit judgment
        public void RockTakesPriorityOverEnergyDeficitTest()
        {
            // セットアップ: RPM矛盾を含み、かつエネルギー不足も発生するネットワークを構築
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            var generatorPos = new Vector3Int(1, 1, 0);
            var bigGearPos = new Vector3Int(0, 0, 1);
            var smallGear1Pos = new Vector3Int(3, 1, 1);
            var smallGear2Pos = new Vector3Int(1, 1, 2);

            // RPMが異なる歯車ネットワークを作成（ロックを引き起こす）
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BigGear, bigGearPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, smallGear1Pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var smallGear1);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, smallGear2Pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var smallGear2);

            // RPMが違う歯車同士を強制的に接続してロック状態を作成
            ForceConnectGear(smallGear1, smallGear2);

            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDatastore.GearNetworks.First().Value;

            // 実行: ネットワーク更新
            gearNetwork.ManualUpdate();

            // 検証: すべてのコンポーネントがロック状態であること
            Assert.AreEqual(GearNetworkStopReason.Rocked, gearNetwork.CurrentGearNetworkInfo.StopReason);

            // 検証: OperatingRateが0であること（ロック状態の結果）
            Assert.AreEqual(0f, gearNetwork.CurrentGearNetworkInfo.OperatingRate);
        }

        [Test]
        // ジェネレーター不在時の既存動作が維持されることをテスト
        // Test that existing behavior is maintained when there is no generator
        public void NoGeneratorBehaviorUnchangedTest()
        {
            // セットアップ: ジェネレーターなしでGearTransformerのみ配置
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            var gearPosition1 = new Vector3Int(0, 0, 0);
            var gearPosition2 = new Vector3Int(1, 0, 0);

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPosition1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear1Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPosition2, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear2Block);

            var gear1 = gear1Block.GetComponent<IGearEnergyTransformer>();
            var gear2 = gear2Block.GetComponent<IGearEnergyTransformer>();

            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDatastore.GearNetworks.First().Value;

            // 実行: ネットワーク更新
            gearNetwork.ManualUpdate();

            // 検証: すべてのGearTransformerのCurrentRpmが0であること（既存動作）
            AreEqual(0f, gear1.CurrentRpm);
            AreEqual(0f, gear2.CurrentRpm);

            // 検証: CurrentGearNetworkInfoがCreateEmpty()の結果と一致すること
            var emptyInfo = GearNetworkInfo.CreateEmpty();
            Assert.AreEqual(emptyInfo.TotalRequiredGearPower, gearNetwork.CurrentGearNetworkInfo.TotalRequiredGearPower);
            Assert.AreEqual(emptyInfo.TotalGenerateGearPower, gearNetwork.CurrentGearNetworkInfo.TotalGenerateGearPower);
            Assert.AreEqual(emptyInfo.OperatingRate, gearNetwork.CurrentGearNetworkInfo.OperatingRate);
        }

        [Test]
        // 必要GP = 生成GPの境界値テスト
        // Test the boundary case where required GP = generated GP
        public void ExactEnergyBalanceTest()
        {
            // セットアップ: 必要GPと生成GPが完全に等しいネットワークを構築
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            var generatorPosition = new Vector3Int(0, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // SimpleGearGeneratorのトルクは3、RPMは10なので、ギアパワーは30
            // Teeth10RequireTorqueTestGearのトルクは1、RPMは10なので、1つあたりのギアパワーは10
            // 3つの歯車を配置すれば、必要GP = 30 = 生成GPになる
            var gearPosition1 = new Vector3Int(0, 0, 1);
            var gearPosition2 = new Vector3Int(1, 0, 1);
            var gearPosition3 = new Vector3Int(2, 0, 1);

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear1Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition2, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear2Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition3, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gear3Block);

            var gear1 = gear1Block.GetComponent<IGearEnergyTransformer>();
            var gear2 = gear2Block.GetComponent<IGearEnergyTransformer>();
            var gear3 = gear3Block.GetComponent<IGearEnergyTransformer>();

            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDatastore.GearNetworks.First().Value;

            // 実行: ネットワーク更新
            gearNetwork.ManualUpdate();

            // 検証: GearTransformerのCurrentRpmが0より大きいこと（エネルギー充足として扱う）
            Assert.IsTrue(gear1.CurrentRpm.AsPrimitive() > 0f);
            Assert.IsTrue(gear2.CurrentRpm.AsPrimitive() > 0f);
            Assert.IsTrue(gear3.CurrentRpm.AsPrimitive() > 0f);

            // 検証: OperatingRateが1.0であること
            AreEqual(1.0f, gearNetwork.CurrentGearNetworkInfo.OperatingRate);

            // 検証: 必要GPと生成GPが等しいこと
            AreEqual(gearNetwork.CurrentGearNetworkInfo.TotalRequiredGearPower, gearNetwork.CurrentGearNetworkInfo.TotalGenerateGearPower);
        }
    }
}
