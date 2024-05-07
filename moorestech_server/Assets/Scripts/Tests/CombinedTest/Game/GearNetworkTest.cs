using System;
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

            AddBlock(ForUnitTestModBlockId.SimpleGearGenerator, Vector3Int.zero, BlockDirection.North);
            AddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(0,0,1), BlockDirection.North);
            AddBlock(ForUnitTestModBlockId.BigGear, new Vector3Int(0,0,2), BlockDirection.North);
            AddBlock(ForUnitTestModBlockId.SmallGear, new Vector3Int(2,0,2), BlockDirection.North);

            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDatastore.GearNetworks[0];

            gearNetwork.ManualUpdate();
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

        private static void AddBlock(int blockId,Vector3Int pos,BlockDirection direction)
        {
            var config = ServerContext.BlockConfig.GetBlockConfig(blockId);
            
            var posInfo = new BlockPositionInfo(pos, direction, config.BlockSize);
            var block = ServerContext.BlockFactory.Create(blockId, CreateBlockEntityId.Create(), posInfo);
            ServerContext.WorldBlockDatastore.AddBlock(block);
        }
    }
}