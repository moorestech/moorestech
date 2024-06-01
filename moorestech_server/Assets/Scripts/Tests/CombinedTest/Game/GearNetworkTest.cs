using System.Collections.Generic;
using System.Linq;
using Game.Block.Blocks.Gear;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Context;
using Game.Gear.Common;
using Game.World.Interface.Util;
using Microsoft.Extensions.DependencyInjection;
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
            
            var generator = AddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(0, 0, 0), BlockDirection.North);
            var shaft = AddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(0, 0, 1), BlockDirection.North);
            var bigGear = AddBlock(ForUnitTestModBlockId.BigGear, new Vector3Int(-1, -1, 2), BlockDirection.North);
            var smallGear = AddBlock(ForUnitTestModBlockId.SmallGear, new Vector3Int(2, 0, 2), BlockDirection.North);
            
            //ネットワークをアップデート
            //Update the network
            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDatastore.GearNetworks.First().Value;
            gearNetwork.ManualUpdate();
            
            //ジェネレーターの供給が正しいか
            //Is the generator supply correct?
            var generatorComponent = generator.ComponentManager.GetComponent<IGearGenerator>();
            Assert.AreEqual(10.0f, generatorComponent.CurrentRpm);
            Assert.AreEqual(true, generatorComponent.GenerateIsClockwise);
            
            //シャフトの回転は正しいか
            //Is the rotation of the shaft correct?
            var shaftComponent = shaft.ComponentManager.GetComponent<GearEnergyTransformer>();
            Assert.AreEqual(10.0f, shaftComponent.CurrentRpm);
            Assert.AreEqual(true, shaftComponent.IsCurrentClockwise);
            
            //BigGearの回転は正しいか
            //Is the rotation of BigGear correct?
            var bigGearComponent = bigGear.ComponentManager.GetComponent<GearComponent>();
            Assert.AreEqual(10.0f, bigGearComponent.CurrentRpm);
            Assert.AreEqual(true, bigGearComponent.IsCurrentClockwise);
            
            //SmallGearの回転は正しいか
            //Is the rotation of SmallGear correct?
            var smallGearComponent = smallGear.ComponentManager.GetComponent<GearComponent>();
            Assert.AreEqual(20.0f, smallGearComponent.CurrentRpm); // ギア比2:1 Gear ratio 2:1
            Assert.AreEqual(false, smallGearComponent.IsCurrentClockwise); // 回転が反転する Rotation is reversed
        }
        
        [Test]
        // ループした歯車NWを作成し、RPM、回転方向があっているかをテスト
        // Create a looped gear NW and test if RPM and direction of rotation are correct.
        public void LoopGearNetworkTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
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
            
            var generatorBlock = AddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.North);
            var smallGearABlock = AddBlock(ForUnitTestModBlockId.SmallGear, gearPositionA, BlockDirection.North);
            var smallGearBBlock = AddBlock(ForUnitTestModBlockId.SmallGear, gearPositionB, BlockDirection.North);
            var smallGearCBlock = AddBlock(ForUnitTestModBlockId.SmallGear, gearPositionC, BlockDirection.North);
            var smallGearDBlock = AddBlock(ForUnitTestModBlockId.SmallGear, gearPositionD, BlockDirection.North);
            
            var generator = generatorBlock.ComponentManager.GetComponent<IGearGenerator>();
            var smallGearA = smallGearABlock.ComponentManager.GetComponent<GearComponent>();
            var smallGearB = smallGearBBlock.ComponentManager.GetComponent<GearComponent>();
            var smallGearC = smallGearCBlock.ComponentManager.GetComponent<GearComponent>();
            var smallGearD = smallGearDBlock.ComponentManager.GetComponent<GearComponent>();
            
            
            var gearNetworkDataStore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDataStore.GearNetworks.First().Value;
            gearNetwork.ManualUpdate();
            
            // Generatorの回転方向とRPMのテスト
            Assert.AreEqual(rpm, generator.CurrentRpm);
            Assert.AreEqual(true, generator.IsCurrentClockwise);
            
            // smallGearAの回転方向とRPMのテスト
            Assert.AreEqual(rpm, smallGearA.CurrentRpm);
            Assert.AreEqual(true, smallGearA.IsCurrentClockwise);
            
            // smallGearBの回転方向とRPMのテスト
            Assert.AreEqual(rpm, smallGearB.CurrentRpm);
            Assert.AreEqual(false, smallGearB.IsCurrentClockwise);
            
            // smallGearCの回転方向とRPMのテスト
            Assert.AreEqual(rpm, smallGearC.CurrentRpm);
            Assert.AreEqual(true, smallGearC.IsCurrentClockwise);
            
            // smallGearDの回転方向とRPMのテスト
            Assert.AreEqual(rpm, smallGearD.CurrentRpm);
            Assert.AreEqual(false, smallGearD.IsCurrentClockwise);
        }
        
        [Test]
        // BigGearを使ってRPMを変えたSmallGearと、RPMを変えていないSmallGearを無理やりつなぎ、ロックされることをテストする
        // Using BigGear, forcibly connect SmallGear with a different RPM and SmallGear with an unchanged RPM, and test that it locks.
        public void DifferentRpmGearNetworkToRockTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var generatorPos = new Vector3Int(1, 1, 0); // 大歯車を使ってRPMを変化させた側の歯車
            var bigGearPos = new Vector3Int(0, 0, 1); // Gears on the side that changed RPM with large gears
            var smallGear1Pos = new Vector3Int(3, 1, 1);
            
            var smallGear2Pos = new Vector3Int(1, 1, 2); // RPMを変化させていない側の歯車（回転方向を変えないために2つの小歯車をつかう）
            
            AddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPos, BlockDirection.North);
            AddBlock(ForUnitTestModBlockId.BigGear, bigGearPos, BlockDirection.North);
            
            AddBlock(ForUnitTestModBlockId.SmallGear, smallGear2Pos, BlockDirection.North);
            
            var smallGear1 = AddBlock(ForUnitTestModBlockId.SmallGear, smallGear1Pos, BlockDirection.North);
            var smallGear2 = AddBlock(ForUnitTestModBlockId.SmallGear, smallGear2Pos, BlockDirection.North);
            
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
            
            var generatorPosition = new Vector3Int(0, 0, 0);
            var gearPosition1 = new Vector3Int(0, 0, 1);
            var gearPosition2 = new Vector3Int(1, 0, 0);
            
            var gearPosition3 = new Vector3Int(0, 0, -1);
            
            AddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.North);
            AddBlock(ForUnitTestModBlockId.SmallGear, gearPosition1, BlockDirection.North);
            var gear2 = AddBlock(ForUnitTestModBlockId.SmallGear, gearPosition2, BlockDirection.North);
            var gear3 = AddBlock(ForUnitTestModBlockId.SmallGear, gearPosition3, BlockDirection.North);
            
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
            
            var fastGeneratorPosition = new Vector3Int(0, 0, 0);
            var fastGeneratorGearPosition = new Vector3Int(0, 0, 1);
            var smallGearAPosition = new Vector3Int(1, 0, 1);
            var generatorPosition = new Vector3Int(2, 0, 0);
            var generatorGearPosition = new Vector3Int(2, 0, 1);
            var smallGearBPosition = new Vector3Int(3, 0, 1);
            
            var fastGenerator = AddBlock(ForUnitTestModBlockId.SimpleFastGearGenerator, fastGeneratorPosition, BlockDirection.North).ComponentManager.GetComponent<IGearGenerator>();
            AddBlock(ForUnitTestModBlockId.SmallGear, fastGeneratorGearPosition, BlockDirection.North);
            
            // SmallGearA
            var smallGearA = AddBlock(ForUnitTestModBlockId.SmallGear, smallGearAPosition, BlockDirection.North).ComponentManager.GetComponent<GearComponent>();
            
            var generator = AddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.North).ComponentManager.GetComponent<IGearGenerator>();
            AddBlock(ForUnitTestModBlockId.SmallGear, generatorGearPosition, BlockDirection.North);
            
            var smallGearB = AddBlock(ForUnitTestModBlockId.SmallGear, smallGearBPosition, BlockDirection.North).ComponentManager.GetComponent<GearComponent>();
            
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
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var generator1Position = new Vector3Int(0, 0, 0);
            var generator2Position = new Vector3Int(1, 0, 0);
            
            var gearPosition1 = new Vector3Int(0, 0, 1);
            var gearPosition2 = new Vector3Int(1, 0, 1);
            
            var generator1 = AddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generator1Position, BlockDirection.North).ComponentManager.GetComponent<IGearEnergyTransformer>();
            var generator2 = AddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generator2Position, BlockDirection.North).ComponentManager.GetComponent<IGearEnergyTransformer>();
            var gear1 = AddBlock(ForUnitTestModBlockId.SmallGear, gearPosition1, BlockDirection.North).ComponentManager.GetComponent<IGearEnergyTransformer>();
            var gear2 = AddBlock(ForUnitTestModBlockId.SmallGear, gearPosition2, BlockDirection.North).ComponentManager.GetComponent<IGearEnergyTransformer>();
            
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
            
            var generatorPosition = new Vector3Int(0, 0, 0);
            AddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.North);
            
            var gearPosition1 = new Vector3Int(0, 0, 1);
            var gearPosition2 = new Vector3Int(1, 0, 1);
            var gearPosition3 = new Vector3Int(2, 0, 1);
            
            var gear1 = AddBlock(ForUnitTestModBlockId.SmallLossPowerGear, gearPosition1, BlockDirection.North).ComponentManager.GetComponent<IGearEnergyTransformer>();
            var gear2 = AddBlock(ForUnitTestModBlockId.SmallLossPowerGear, gearPosition2, BlockDirection.North).ComponentManager.GetComponent<IGearEnergyTransformer>();
            var gear3 = AddBlock(ForUnitTestModBlockId.SmallLossPowerGear, gearPosition3, BlockDirection.North).ComponentManager.GetComponent<IGearEnergyTransformer>();
            
            var gearNetworkDataStore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDataStore.GearNetworks.First().Value;
            
            gearNetwork.ManualUpdate();
            
            Assert.AreEqual(10, gear1.CurrentPower);
            Assert.AreEqual(10, gear2.CurrentPower);
            Assert.AreEqual(10, gear3.CurrentPower);
        }
        
        [Test]
        public void ServeTorqueOverTest()
        {
            //トルクが多いとその分供給トルクが減るテスト
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var generatorPosition = new Vector3Int(0, 0, 0);
            AddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.North);
            
            var gearPosition1 = new Vector3Int(0, 0, 1);
            var gearPosition2 = new Vector3Int(1, 0, 1);
            var gearPosition3 = new Vector3Int(2, 0, 1);
            var gearPosition4 = new Vector3Int(3, 0, 1);
            var gearPosition5 = new Vector3Int(4, 0, 1);
            var gearPosition6 = new Vector3Int(5, 0, 1);
            
            var gear1 = AddBlock(ForUnitTestModBlockId.SmallLossPowerGear, gearPosition1, BlockDirection.North).ComponentManager.GetComponent<IGearEnergyTransformer>();
            var gear2 = AddBlock(ForUnitTestModBlockId.SmallLossPowerGear, gearPosition2, BlockDirection.North).ComponentManager.GetComponent<IGearEnergyTransformer>();
            var gear3 = AddBlock(ForUnitTestModBlockId.SmallLossPowerGear, gearPosition3, BlockDirection.North).ComponentManager.GetComponent<IGearEnergyTransformer>();
            var gear4 = AddBlock(ForUnitTestModBlockId.SmallLossPowerGear, gearPosition4, BlockDirection.North).ComponentManager.GetComponent<IGearEnergyTransformer>();
            var gear5 = AddBlock(ForUnitTestModBlockId.SmallLossPowerGear, gearPosition5, BlockDirection.North).ComponentManager.GetComponent<IGearEnergyTransformer>();
            var gear6 = AddBlock(ForUnitTestModBlockId.SmallLossPowerGear, gearPosition6, BlockDirection.North).ComponentManager.GetComponent<IGearEnergyTransformer>();
            
            var gearNetworkDataStore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDataStore.GearNetworks.First().Value;
            gearNetwork.ManualUpdate();
            
            Assert.AreEqual(5, gear1.CurrentPower);
            Assert.AreEqual(5, gear2.CurrentPower);
            Assert.AreEqual(5, gear3.CurrentPower);
            Assert.AreEqual(5, gear4.CurrentPower);
            Assert.AreEqual(5, gear5.CurrentPower);
            Assert.AreEqual(5, gear6.CurrentPower);
        }
        
        [Test]
        public void GearNetworkMergeTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var gearNetworkDataStore = serviceProvider.GetService<GearNetworkDatastore>();
            
            var generatorPosition = new Vector3Int(0, 0, 0);
            var gearPosition1 = new Vector3Int(1, 0, 0);
            var gearPosition2 = new Vector3Int(2, 0, 0);
            var gearPosition3 = new Vector3Int(3, 0, 0);
            
            // 2つのネットワークを作成
            AddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.North).ComponentManager.GetComponent<IGearGenerator>();
            AddBlock(ForUnitTestModBlockId.SmallGear, gearPosition2, BlockDirection.North).ComponentManager.GetComponent<GearComponent>();
            AddBlock(ForUnitTestModBlockId.SmallGear, gearPosition3, BlockDirection.North).ComponentManager.GetComponent<GearComponent>();
            Assert.AreEqual(2, gearNetworkDataStore.GearNetworks.Count);
            
            // ネットワークをマージ
            AddBlock(ForUnitTestModBlockId.SmallGear, gearPosition1, BlockDirection.North).ComponentManager.GetComponent<GearComponent>();
            Assert.AreEqual(1, gearNetworkDataStore.GearNetworks.Count);
            
            // ネットワークの分離のテスト
            ServerContext.WorldBlockDatastore.RemoveBlock(gearPosition2);
            Assert.AreEqual(2, gearNetworkDataStore.GearNetworks.Count);
        }
        
        private static IBlock AddBlock(int blockId, Vector3Int pos, BlockDirection direction)
        {
            var config = ServerContext.BlockConfig.GetBlockConfig(blockId);
            
            var posInfo = new BlockPositionInfo(pos, direction, config.BlockSize);
            var block = ServerContext.BlockFactory.Create(blockId, CreateBlockEntityId.Create(), posInfo);
            ServerContext.WorldBlockDatastore.AddBlock(block);
            return block;
        }
        
        private static void ForceConnectGear(IBlock gear1, IBlock gear2)
        {
            var gear1Connector = gear1.ComponentManager.GetComponent<BlockConnectorComponent<IGearEnergyTransformer>>();
            var gear1Transform = gear1.ComponentManager.GetComponent<IGearEnergyTransformer>();
            
            var gear2Connector = gear2.ComponentManager.GetComponent<BlockConnectorComponent<IGearEnergyTransformer>>();
            var gear2Transform = gear2.ComponentManager.GetComponent<IGearEnergyTransformer>();
            
            
            ((Dictionary<IGearEnergyTransformer, (IConnectOption selfOption, IConnectOption targetOption)>)gear1Connector.ConnectTargets).Add(gear2Transform, (new GearConnectOption(true), new GearConnectOption(true)));
            ((Dictionary<IGearEnergyTransformer, (IConnectOption selfOption, IConnectOption targetOption)>)gear2Connector.ConnectTargets).Add(gear1Transform, (new GearConnectOption(true), new GearConnectOption(true)));
        }
    }
}