using System.Collections.Generic;
using Game.Block.Blocks.Gear;
using Game.Block.Component;
using Game.Block.Interface;
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
            
            var generator = AddBlock(ForUnitTestModBlockId.SimpleGearGenerator, Vector3Int.zero, BlockDirection.North);
            var shaft = AddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(0, 0, 1), BlockDirection.North);
            var bigGear = AddBlock(ForUnitTestModBlockId.BigGear, new Vector3Int(0, 0, 2), BlockDirection.North);
            var smallGear = AddBlock(ForUnitTestModBlockId.SmallGear, new Vector3Int(2, 0, 2), BlockDirection.North);
            
            //ネットワークをアップデート
            //Update the network
            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDatastore.GearNetworks[0];
            gearNetwork.ManualUpdate();
            
            //ジェネレーターの供給が正しいか
            //Is the generator supply correct?
            var generatorComponent = generator.ComponentManager.GetComponent<GearGeneratorComponent>();
            Assert.AreEqual(10.0f, generatorComponent.CurrentRpm);
            Assert.AreEqual(true, generatorComponent.GenerateIsClockwise);
            
            //シャフトの回転は正しいか
            //Is the rotation of the shaft correct?
            var shaftComponent = shaft.ComponentManager.GetComponent<GearComponent>();
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
            var generatorPosition = gearPositionA - new Vector3Int(0, 1, 0);
            
            var generatorBlock = AddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.UpNorth);
            var smallGearABlock = AddBlock(ForUnitTestModBlockId.SmallGear, gearPositionA, BlockDirection.UpNorth);
            var smallGearBBlock = AddBlock(ForUnitTestModBlockId.SmallGear, gearPositionB, BlockDirection.UpNorth);
            var smallGearCBlock = AddBlock(ForUnitTestModBlockId.SmallGear, gearPositionC, BlockDirection.UpNorth);
            var smallGearDBlock = AddBlock(ForUnitTestModBlockId.SmallGear, gearPositionD, BlockDirection.UpNorth);
            
            var generator = generatorBlock.ComponentManager.GetComponent<GearGeneratorComponent>();
            var smallGearA = smallGearABlock.ComponentManager.GetComponent<GearGeneratorComponent>();
            var smallGearB = smallGearBBlock.ComponentManager.GetComponent<GearGeneratorComponent>();
            var smallGearC = smallGearCBlock.ComponentManager.GetComponent<GearGeneratorComponent>();
            var smallGearD = smallGearDBlock.ComponentManager.GetComponent<GearGeneratorComponent>();
            
            var gearNetworkDataStore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDataStore.GearNetworks[0];
            gearNetwork.ManualUpdate();
            
            // Generatorの回転方向とRPMのテスト
            Assert.AreEqual(rpm, generator.CurrentRpm);
            Assert.AreEqual(true, generator.GenerateIsClockwise);
            
            // smallGearAの回転方向とRPMのテスト
            Assert.AreEqual(rpm, smallGearA.CurrentRpm);
            Assert.AreEqual(true, smallGearA.GenerateIsClockwise);
            
            // smallGearBの回転方向とRPMのテスト
            Assert.AreEqual(rpm, smallGearB.CurrentRpm);
            Assert.AreEqual(false, smallGearB.GenerateIsClockwise);
            
            // smallGearCの回転方向とRPMのテスト
            Assert.AreEqual(rpm, smallGearC.CurrentRpm);
            Assert.AreEqual(false, smallGearC.GenerateIsClockwise);
            
            // smallGearDの回転方向とRPMのテスト
            Assert.AreEqual(rpm, smallGearD.CurrentRpm);
            Assert.AreEqual(true, smallGearD.GenerateIsClockwise);
        }
        
        [Test]
        // BigGearを使ってRPMを変えたSmallGearと、RPMを変えていないSmallGearを無理やりつなぎ、ロックされることをテストする
        // Using BigGear, forcibly connect SmallGear with a different RPM and SmallGear with an unchanged RPM, and test that it locks.
        public void DifferentRpmGearNetworkToRockTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var generatorPos = new Vector3Int(0, 0, 0); // 大歯車を使ってRPMを変化させた側の歯車
            var bigGearPos = new Vector3Int(0, 0, 1); // Gears on the side that changed RPM with large gears
            var smallGear1Pos = new Vector3Int(2, 0, 1);
            
            var smallGear2Pos = new Vector3Int(0, 0, -1); // RPMを変化させていない側の歯車（回転方向を変えないために2つの小歯車をつかう）
            var smallGear3Pos = new Vector3Int(1, 0, -1); // Gears on the side not changing RPM (two small gears are used to keep the direction of rotation the same)
            
            AddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPos, BlockDirection.North);
            AddBlock(ForUnitTestModBlockId.BigGear, bigGearPos, BlockDirection.North);
            
            AddBlock(ForUnitTestModBlockId.SmallGear, smallGear2Pos, BlockDirection.North);
            
            var smallGear1 = AddBlock(ForUnitTestModBlockId.SmallGear, smallGear1Pos, BlockDirection.North);
            BlockConnectorComponent<IGearEnergyTransformer> smallGear1Connector = smallGear1.ComponentManager.GetComponent<BlockConnectorComponent<IGearEnergyTransformer>>();
            var smallGear1Transform = smallGear1.ComponentManager.GetComponent<IGearEnergyTransformer>();
            
            var smallGear3 = AddBlock(ForUnitTestModBlockId.SmallGear, smallGear3Pos, BlockDirection.North);
            BlockConnectorComponent<IGearEnergyTransformer> smallGear3Connector = smallGear3.ComponentManager.GetComponent<BlockConnectorComponent<IGearEnergyTransformer>>();
            var smallGear3Transform = smallGear3.ComponentManager.GetComponent<IGearEnergyTransformer>();
            
            //RPMが違う歯車同士を強制的に接続
            //Force connection between gears with different RPM
            ((List<IGearEnergyTransformer>)smallGear1Connector.ConnectTargets).Add(smallGear1Transform);
            ((List<IGearEnergyTransformer>)smallGear3Connector.ConnectTargets).Add(smallGear3Transform);
            
            // find the network
            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDatastore.GearNetworks[0];
            Assert.NotNull(gearNetwork);
            
            //ネットワークをアップデート
            //Update the network
            gearNetwork.ManualUpdate();
            
            // TODO: ネットワークがロックされているかどうかを確認する
            //Assert.IsTrue(gearNetwork.IsLocked);
            Assert.IsTrue(false);
        }
        
        [Test]
        public void DifferentDirectionGearNetworkToRockTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var generatorPositionA = new Vector3Int(0, 0, 0);
            var generatorPositionB = new Vector3Int(1, 0, 0);
            var gearPositionA = new Vector3Int(0, 1, 0);
            var gearPositionB = new Vector3Int(1, 1, 0);
            
            AddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPositionA, BlockDirection.UpNorth);
            AddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPositionB, BlockDirection.UpNorth);
            AddBlock(ForUnitTestModBlockId.SmallGear, gearPositionA, BlockDirection.UpNorth);
            AddBlock(ForUnitTestModBlockId.SmallGear, gearPositionB, BlockDirection.UpNorth);
            
            var gearNetworkDataStore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDataStore.GearNetworks[0];
            gearNetwork.ManualUpdate();
            
            // TODO: ネットワークがロックされているかどうかを確認する
            //Assert.IsTrue(gearNetwork.IsLocked);
            Assert.IsTrue(false);
        }
        
        
        [Test]
        public void MultiGeneratorOverrideRpmTest()
        {
            //TODO 複数のジェネレーターのRPMがオーバーライドされるテスト
        }
        
        
        [Test]
        public void MultiGeneratorDifferentDirectionToRockTest()
        {
            //TODO 複数のジェネレーターの回転方向が違うことでロックされるテスト
        }
        
        [Test]
        public void ServeTorqueTest()
        {
            //TODO 機械によってトルクが消費されるテスト（正しいトルクが供給されるかのテスト
            //TODO 供給トルクが足りないときに稼働時間が長くなるテスト
        }
        
        [Test]
        public void ServeTorqueOverTest()
        {
            //TODO トルクが多いとその分供給トルクが減るテスト
        }
        
        [Test]
        public void GearNetworkMergeTest()
        {
            //TODO 設置したら歯車NWが増える、歯車NWのマージのテスト、削除したら歯車NWが分割されるテスト
        }
        
        private static IBlock AddBlock(int blockId, Vector3Int pos, BlockDirection direction)
        {
            var config = ServerContext.BlockConfig.GetBlockConfig(blockId);
            
            var posInfo = new BlockPositionInfo(pos, direction, config.BlockSize);
            var block = ServerContext.BlockFactory.Create(blockId, CreateBlockEntityId.Create(), posInfo);
            ServerContext.WorldBlockDatastore.AddBlock(block);
            return block;
        }
    }
}