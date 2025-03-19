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

namespace Tests.CombinedTest.Game
{
    public class GearNetworkTest
    {
        [Test]
        //シンプルジェネレーターを設置し、簡易的な歯車NWを作り、RPM、回転方向があっているかをテスト
        //Install a simple generator, make a simple gear NW, and test if RPM and direction of rotation are correct.
        public void SimpleGeneratorAndGearRpmTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(0, 0, 0), BlockDirection.North, out var generator);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(0, 0, 1), BlockDirection.North, out var shaft);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BigGear, new Vector3Int(-1, -1, 2), BlockDirection.North, out var bigGear);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, new Vector3Int(2, 0, 2), BlockDirection.North, out var smallGear);
            
            //ネットワークをアップデート
            //Update the network
            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDatastore.GearNetworks.First().Value;
            gearNetwork.ManualUpdate();
            
            //ジェネレーターの供給が正しいか
            //Is the generator supply correct?
            var generatorComponent = generator.GetComponent<IGearGenerator>();
            Assert.AreEqual(10.0f, generatorComponent.CurrentRpm.AsPrimitive());
            Assert.AreEqual(true, generatorComponent.GenerateIsClockwise);
            
            //シャフトの回転は正しいか
            //Is the rotation of the shaft correct?
            var shaftComponent = shaft.GetComponent<GearEnergyTransformer>();
            Assert.AreEqual(10.0f, shaftComponent.CurrentRpm.AsPrimitive());
            Assert.AreEqual(true, shaftComponent.IsCurrentClockwise);
            
            //BigGearの回転は正しいか
            //Is the rotation of BigGear correct?
            var bigGearComponent = bigGear.GetComponent<GearComponent>();
            Assert.AreEqual(10.0f, bigGearComponent.CurrentRpm.AsPrimitive());
            Assert.AreEqual(true, bigGearComponent.IsCurrentClockwise);
            
            //SmallGearの回転は正しいか
            //Is the rotation of SmallGear correct?
            var smallGearComponent = smallGear.GetComponent<GearComponent>();
            Assert.AreEqual(20.0f, smallGearComponent.CurrentRpm.AsPrimitive()); // ギア比2:1 Gear ratio 2:1
            Assert.AreEqual(false, smallGearComponent.IsCurrentClockwise); // 回転が反転する Rotation is reversed
        }
        
        [Test]
        // ループした歯車NWを作成し、RPM、回転方向があっているかをテスト
        // Create a looped gear NW and test if RPM and direction of rotation are correct.
        public void LoopGearNetworkTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
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
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, generatorPosition, BlockDirection.North, out var generatorBlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPositionA, BlockDirection.North, out var smallGearABlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPositionB, BlockDirection.North, out var smallGearBBlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPositionC, BlockDirection.North, out var smallGearCBlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPositionD, BlockDirection.North, out var smallGearDBlock);
            
            var generator = generatorBlock.GetComponent<IGearGenerator>();
            var smallGearA = smallGearABlock.GetComponent<GearComponent>();
            var smallGearB = smallGearBBlock.GetComponent<GearComponent>();
            var smallGearC = smallGearCBlock.GetComponent<GearComponent>();
            var smallGearD = smallGearDBlock.GetComponent<GearComponent>();
            
            
            var gearNetworkDataStore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDataStore.GearNetworks.First().Value;
            gearNetwork.ManualUpdate();
            
            // Generatorの回転方向とRPMのテスト
            Assert.AreEqual(rpm, generator.CurrentRpm.AsPrimitive());
            Assert.AreEqual(true, generator.IsCurrentClockwise);
            
            // smallGearAの回転方向とRPMのテスト
            Assert.AreEqual(rpm, smallGearA.CurrentRpm.AsPrimitive());
            Assert.AreEqual(true, smallGearA.IsCurrentClockwise);
            
            // smallGearBの回転方向とRPMのテスト
            Assert.AreEqual(rpm, smallGearB.CurrentRpm.AsPrimitive());
            Assert.AreEqual(false, smallGearB.IsCurrentClockwise);
            
            // smallGearCの回転方向とRPMのテスト
            Assert.AreEqual(rpm, smallGearC.CurrentRpm.AsPrimitive());
            Assert.AreEqual(true, smallGearC.IsCurrentClockwise);
            
            // smallGearDの回転方向とRPMのテスト
            Assert.AreEqual(rpm, smallGearD.CurrentRpm.AsPrimitive());
            Assert.AreEqual(false, smallGearD.IsCurrentClockwise);
        }
        
        [Test]
        // BigGearを使ってRPMを変えたSmallGearと、RPMを変えていないSmallGearを無理やりつなぎ、ロックされることをテストする
        // Using BigGear, forcibly connect SmallGear with a different RPM and SmallGear with an unchanged RPM, and test that it locks.
        public void DifferentRpmGearNetworkToRockTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            var generatorPos = new Vector3Int(1, 1, 0); // 大歯車を使ってRPMを変化させた側の歯車
            var bigGearPos = new Vector3Int(0, 0, 1); // Gears on the side that changed RPM with large gears
            var smallGear1Pos = new Vector3Int(3, 1, 1);
            
            var smallGear2Pos = new Vector3Int(1, 1, 2); // RPMを変化させていない側の歯車（回転方向を変えないために2つの小歯車をつかう）
            
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPos, BlockDirection.North, out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BigGear, bigGearPos, BlockDirection.North, out _);
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, smallGear2Pos, BlockDirection.North, out _);
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, smallGear1Pos, BlockDirection.North, out var smallGear1);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, smallGear2Pos, BlockDirection.North, out var smallGear2);
            
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
            Assert.IsTrue(gearNetwork.GearTransformers.All(g => g.IsRocked));
            Assert.IsTrue(gearNetwork.GearGenerators.All(g => g.IsRocked));
        }
        
        [Test]
        public void DifferentDirectionGearNetworkToRockTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            var generatorPosition = new Vector3Int(0, 0, 0);
            var gearPosition1 = new Vector3Int(0, 0, 1);
            var gearPosition2 = new Vector3Int(1, 0, 0);
            
            var gearPosition3 = new Vector3Int(0, 0, -1);
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.North, out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPosition1, BlockDirection.North, out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPosition2, BlockDirection.North, out var gear2);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPosition3, BlockDirection.North, out var gear3);
            
            //回転方向が違う歯車同士を強制的に接続
            //Forced connection of gears with different directions of rotation
            ForceConnectGear(gear2, gear3);
            
            var gearNetworkDataStore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDataStore.GearNetworks.First().Value;
            gearNetwork.ManualUpdate();
            
            // ネットワークがロックされているかどうかを確認する
            Assert.IsTrue(gearNetwork.GearTransformers.All(g => g.IsRocked));
            Assert.IsTrue(gearNetwork.GearGenerators.All(g => g.IsRocked));
        }
        
        [Test]
        public void MultiGeneratorOverrideRpmTest()
        {
            // 複数のジェネレーターのRPMがオーバーライドされるテスト
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            var fastGeneratorPosition = new Vector3Int(0, 0, 0);
            var fastGeneratorGearPosition = new Vector3Int(0, 0, 1);
            var smallGearAPosition = new Vector3Int(1, 0, 1);
            var generatorPosition = new Vector3Int(2, 0, 0);
            var generatorGearPosition = new Vector3Int(2, 0, 1);
            var smallGearBPosition = new Vector3Int(3, 0, 1);
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleFastGearGenerator, fastGeneratorPosition, BlockDirection.North, out var fastGeneratorBlock);
            var fastGenerator = fastGeneratorBlock.GetComponent<IGearGenerator>();
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, fastGeneratorGearPosition, BlockDirection.North, out _);
            
            // SmallGearA
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, smallGearAPosition, BlockDirection.North, out var smallGearABlock);
            var smallGearA = smallGearABlock.GetComponent<GearComponent>();
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.North, out var generatorBlock);
            var generator = generatorBlock.GetComponent<IGearGenerator>();
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, generatorGearPosition, BlockDirection.North, out _);
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, smallGearBPosition, BlockDirection.North, out var smallGearBBlock);
            var smallGearB = smallGearBBlock.GetComponent<GearComponent>();
            
            var gearNetworkDataStore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDataStore.GearNetworks.First().Value;
            
            gearNetwork.ManualUpdate();
            
            Assert.AreEqual(fastGenerator.CurrentRpm.AsPrimitive(), 20f);
            Assert.AreEqual(smallGearA.CurrentRpm.AsPrimitive(), 20f);
            Assert.AreEqual(generator.CurrentRpm.AsPrimitive(), 20f);
            Assert.AreEqual(smallGearB.CurrentRpm.AsPrimitive(), 20f);
        }
        
        [Test]
        public void MultiGeneratorDifferentDirectionToRockTest()
        {
            // 複数のジェネレーターの回転方向が違うことでロックされるテスト
            // Gen1 - Gear1 このような構成になっている
            //        Gear2
            // Gen2 - Gear3
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            var generator1Position = new Vector3Int(0, 0, 0);
            var generator2Position = new Vector3Int(1, 0, 0);
            
            var gearPosition1 = new Vector3Int(0, 0, 1);
            var gearPosition2 = new Vector3Int(1, 0, 1);
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generator1Position, BlockDirection.North, out var generator1Block);
            var generator1 = generator1Block.GetComponent<IGearEnergyTransformer>();
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generator2Position, BlockDirection.North, out var generator2Block);
            var generator2 = generator2Block.GetComponent<IGearEnergyTransformer>();
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPosition1, BlockDirection.North, out var gear1Block);
            var gear1 = gear1Block.GetComponent<IGearEnergyTransformer>();
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPosition2, BlockDirection.North, out var gear2Block);
            var gear2 = gear2Block.GetComponent<IGearEnergyTransformer>();
            
            var gearNetworkDataStore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDataStore.GearNetworks.First().Value;
            
            gearNetwork.ManualUpdate();
            
            Assert.IsTrue(generator1.IsRocked);
            Assert.IsTrue(generator2.IsRocked);
            Assert.IsTrue(gear1.IsRocked);
            Assert.IsTrue(gear2.IsRocked);
        }
        
        [Test]
        public void ServeTorqueTest()
        {
            // 機械によってトルクが消費されるテスト（正しいトルクが供給されるかのテスト
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            var generatorPosition = new Vector3Int(0, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.North, out _);
            
            var gearPosition1 = new Vector3Int(0, 0, 1);
            var gearPosition2 = new Vector3Int(1, 0, 1);
            var gearPosition3 = new Vector3Int(2, 0, 1);
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition1, BlockDirection.North, out var gear1Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition2, BlockDirection.North, out var gear2Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition3, BlockDirection.North, out var gear3Block);
            
            var gear1 = gear1Block.GetComponent<IGearEnergyTransformer>();
            var gear2 = gear2Block.GetComponent<IGearEnergyTransformer>();
            var gear3 = gear3Block.GetComponent<IGearEnergyTransformer>();
            
            var gearNetworkDataStore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDataStore.GearNetworks.First().Value;
            
            gearNetwork.ManualUpdate();
            
            Assert.AreEqual(10, gear1.CurrentPower.AsPrimitive());
            Assert.AreEqual(10, gear2.CurrentPower.AsPrimitive());
            Assert.AreEqual(10, gear3.CurrentPower.AsPrimitive());
        }
        
        [Test]
        public void ServeTorqueOverTest()
        {
            //トルクが多いとその分供給トルクが減るテスト
            // ジェネレーターは3のトルクを生成するが、6つの歯車がつながっているため、要求するトルクは6になる
            // 結果、供給されるトルクは3/6=0.5になる
            // Test that the supply torque decreases as the torque increases
            // The generator generates 3 torque, but since it is connected to 6 gears, the required torque becomes 6
            // As a result, the supplied torque becomes 3/6=0.5
            
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            var generatorPosition = new Vector3Int(0, 0, 0);
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.North, out var generatorBlock);
            var generator = generatorBlock.GetComponent<SimpleGearGeneratorComponent>();
            
            var gearPosition1 = new Vector3Int(0, 0, 1);
            var gearPosition2 = new Vector3Int(0, 0, 2);
            var gearPosition3 = new Vector3Int(1, 0, 2);
            var gearPosition4 = new Vector3Int(2, 0, 2);
            var gearPosition5 = new Vector3Int(2, 0, 3);
            var gearPosition6 = new Vector3Int(3, 0, 3);
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition1, BlockDirection.North, out var gear1Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth20RequireTorqueTestGear, gearPosition2, BlockDirection.North, out var gear2Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition3, BlockDirection.North, out var gear3Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition4, BlockDirection.North, out var gear4Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth20RequireTorqueTestGear, gearPosition5, BlockDirection.North, out var gear5Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition6, BlockDirection.North, out var gear6Block);
            
            var gear1 = gear1Block.GetComponent<IGearEnergyTransformer>();
            var gear2 = gear2Block.GetComponent<IGearEnergyTransformer>();
            var gear3 = gear3Block.GetComponent<IGearEnergyTransformer>();
            var gear4 = gear4Block.GetComponent<IGearEnergyTransformer>();
            var gear5 = gear5Block.GetComponent<IGearEnergyTransformer>();
            var gear6 = gear6Block.GetComponent<IGearEnergyTransformer>();
            
            var gearNetworkDataStore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDataStore.GearNetworks.First().Value;
            gearNetwork.ManualUpdate();
            
            Assert.AreEqual(5, gear1.CurrentRpm.AsPrimitive());
            Assert.AreEqual(5, gear2.CurrentRpm.AsPrimitive());
            Assert.AreEqual(0.5f, gear1.CurrentTorque.AsPrimitive());
            Assert.AreEqual(0.5f, gear2.CurrentTorque.AsPrimitive());
            
            Assert.AreEqual(10, gear3.CurrentRpm.AsPrimitive());
            Assert.AreEqual(10, gear4.CurrentRpm.AsPrimitive());
            Assert.AreEqual(10, gear5.CurrentRpm.AsPrimitive());
            Assert.AreEqual(0.25f, gear3.CurrentTorque.AsPrimitive());
            Assert.AreEqual(0.25f, gear4.CurrentTorque.AsPrimitive());
            Assert.AreEqual(0.25f, gear5.CurrentTorque.AsPrimitive());
            
            Assert.AreEqual(20, gear6.CurrentRpm.AsPrimitive());
            Assert.AreEqual(0.125f, gear6.CurrentTorque.AsPrimitive());
        }
        
        
        [Test]
        // RPMが1/2になると供給されるトルクが倍になるテスト
        public void TorqueHalfTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            var generatorPosition = new Vector3Int(0, 0, 0);
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.North, out var generatorBlock);
            var generator = generatorBlock.GetComponent<SimpleGearGeneratorComponent>();
            
            
            var gearPosition1 = new Vector3Int(0, 0, 1);
            var gearPosition2 = new Vector3Int(1, 0, 1);
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth20RequireTorqueTestGear, gearPosition1, BlockDirection.North, out var gear1Block);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, gearPosition2, BlockDirection.North, out var gear2Block);
            
            var gear1 = gear1Block.GetComponent<IGearEnergyTransformer>();
            // テストしたいGear2で不要なトルク消費が行われないように必要トルクを0に設定
            // Set the required torque to 0 so that unnecessary torque consumption is not performed in Gear2 to be tested
            typeof(GearEnergyTransformer).GetField("_requiredTorque", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gear1, new Torque(0));
            
            var gear2 = gear2Block.GetComponent<IGearEnergyTransformer>();
            
            var gearNetworkDataStore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDataStore.GearNetworks.First().Value;
            gearNetwork.ManualUpdate();
            
            Assert.AreEqual(10, gear1.CurrentRpm.AsPrimitive());
            Assert.AreEqual(20, gear2.CurrentRpm.AsPrimitive());
            Assert.AreEqual(0, gear1.CurrentTorque.AsPrimitive());
            Assert.AreEqual(1.5f, gear2.CurrentTorque.AsPrimitive());
        }
        
        [Test]
        public void GearNetworkMergeTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var gearNetworkDataStore = serviceProvider.GetService<GearNetworkDatastore>();
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            var generatorPosition = new Vector3Int(0, 0, 0);
            var gearPosition1 = new Vector3Int(1, 0, 0);
            var gearPosition2 = new Vector3Int(2, 0, 0);
            var gearPosition3 = new Vector3Int(3, 0, 0);
            
            // 2つのネットワークを作成
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.North, out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPosition2, BlockDirection.North, out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPosition3, BlockDirection.North, out _);
            Assert.AreEqual(2, gearNetworkDataStore.GearNetworks.Count);
            
            // ネットワークをマージ
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPosition1, BlockDirection.North, out _);
            Assert.AreEqual(1, gearNetworkDataStore.GearNetworks.Count);
            
            // ネットワークの分離のテスト
            ServerContext.WorldBlockDatastore.RemoveBlock(gearPosition2);
            Assert.AreEqual(2, gearNetworkDataStore.GearNetworks.Count);
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
    }
}