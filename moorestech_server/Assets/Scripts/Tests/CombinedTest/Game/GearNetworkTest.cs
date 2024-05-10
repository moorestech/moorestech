using System;
using System.Linq;
using Game.Block.Blocks.Gear;
using Game.Block.Config.LoadConfig.Param;
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
            var shaft = AddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(0,0,1), BlockDirection.North);
            var bigGear = AddBlock(ForUnitTestModBlockId.BigGear, new Vector3Int(0,0,2), BlockDirection.North);
            var smallGear = AddBlock(ForUnitTestModBlockId.SmallGear, new Vector3Int(2,0,2), BlockDirection.North);
            
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
        public void LoopGearNetworkTest()
        {
            //TODO ループした歯車NWを作成し、RPM、回転方向があっているかをテスト
        }

        [Test]
        public void DifferentRpmGearNetworkToRockTest()
        {
            //TODO RPMが違ってロックされるテスト
            //TODO Test locked with different RPM
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);

            Vector3Int Generator0Pos = new Vector3Int(0, 0, 0);
            Vector3Int Generator1Pos = new Vector3Int(3, 0, 0);
            Vector3Int BigGearPos = new Vector3Int(0, 0, 1);
            Vector3Int SmallGear1Pos = new Vector3Int(2, 0, 1);
            Vector3Int SmallGear0Pos = new Vector3Int(3, 0, 1);

            var generator0 = AddBlock(ForUnitTestModBlockId.SimpleGearGenerator, Generator0Pos, BlockDirection.North);
            var generator1 = AddBlock(ForUnitTestModBlockId.SimpleGearGenerator, Generator1Pos, BlockDirection.North);
            var bigGear = AddBlock(ForUnitTestModBlockId.BigGear, BigGearPos, BlockDirection.North);
            var smallGear0 = AddBlock(ForUnitTestModBlockId.SmallGear, SmallGear0Pos, BlockDirection.North);
            var smallGear1 = AddBlock(ForUnitTestModBlockId.SmallGear, SmallGear1Pos, BlockDirection.North);

            // 大ギアは小ギア0に接続され、小ギアは小ギア1に接続されている。
            // 発電機は2つあり、発電機0と発電機1である。
            // 発電機0は大ギヤに、発電機1は小ギヤ1に電力を供給する。
            // 大ギアは小ギア0に小ギア1よりも高い回転数を与えるはずである。
            // 小歯車0と小歯車1が互いに異なる回転数で接続されているため、 ネットワークはロックされるべきである。
            // 回転方向が異なるため、ネットワークはロックされるべきではない。
            // big gear is connected to small gear 0 which is connected to small gear 1.
            // there are two generators, generator 0 and generator 1.
            // generator 0 powers the big gear and generator 1 powers the small gear 1.
            // the big gear should give the small gear 0 a higher rpm than the small gear 1.
            // the network should be locked because the small gear 0 and small gear 1 are connected to each other with a difference RPM.
            // the network should not lock because of a different rotation direction.

            // find the network
            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            GearNetwork gearNetwork = null;
            foreach (var network in gearNetworkDatastore.GearNetworks)
                foreach (var generator in network.GearGenerators)
                    if (generator == generator0)
                    {
                        gearNetwork = network;
                        break;
                    }
            Assert.NotNull(gearNetwork);

            //ネットワークをアップデート
            //Update the network
            gearNetwork.ManualUpdate();

            // TODO: ネットワークがロックされているかどうかを確認する
            //Assert.IsTrue(gearNetwork.IsLocked);

            // Are the generators the same RPM?
            // ジェネレーターの回転数は同じですか？
            var generatorComponent0 = generator0.ComponentManager.GetComponent<GearGeneratorComponent>();
            var generatorComponent1 = generator1.ComponentManager.GetComponent<GearGeneratorComponent>();
            Assert.AreEqual(generatorComponent0.CurrentRpm, generatorComponent1.CurrentRpm);
            Assert.AreEqual(generatorComponent0.GenerateIsClockwise, generatorComponent1.GenerateIsClockwise);

            ServerContext.WorldBlockDatastore.RemoveBlock(Generator0Pos);
            ServerContext.WorldBlockDatastore.RemoveBlock(Generator1Pos);
            ServerContext.WorldBlockDatastore.RemoveBlock(BigGearPos);
            ServerContext.WorldBlockDatastore.RemoveBlock(SmallGear0Pos);
            ServerContext.WorldBlockDatastore.RemoveBlock(SmallGear1Pos);
        }

        [Test]
        public void DifferentDirectionGearNetworkToRockTest()
        {
            //TODO 回転方向が違ってロックされるテスト
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

        private static IBlock AddBlock(int blockId,Vector3Int pos,BlockDirection direction)
        {
            var config = ServerContext.BlockConfig.GetBlockConfig(blockId);
            
            var posInfo = new BlockPositionInfo(pos, direction, config.BlockSize);
            var block = ServerContext.BlockFactory.Create(blockId, CreateBlockEntityId.Create(), posInfo);
            ServerContext.WorldBlockDatastore.AddBlock(block);
            return block;
        }
    }
}