using System.Reflection;
using Core.Block.BeltConveyor.Generally;
using Core.Block.BlockFactory;
using Core.Block.Machine;
using Game.World.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using World;

namespace Test.UnitTest.Game
{
    public class BlockPlaceToConnectionBlockTest
    {
        
        const int MachineId = 1;
        const int BeltConveyorId = 3;
        /// <summary>
        /// 機械にベルトコンベアが自動でつながるかをテストする
        /// </summary>
        [Test]
        public void BeltConveyorConnectMachineTest()
        {

            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var world =  serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<BlockFactory>();
            
            
            //北向きにベルトコンベアを設置した時、機械とつながるかをテスト
            var (blockIntId,connectorIntId) = BlockPlaceToGetMachineIdAndConnectorId(
                10, 0, 
                9, 0, BlockDirection.North, blockFactory, world);
            Assert.AreEqual(blockIntId,connectorIntId);
            
            //東向きにベルトコンベアを設置した時、機械とつながるかをテスト
            (blockIntId,connectorIntId) = BlockPlaceToGetMachineIdAndConnectorId(
                0, 10, 
                0, 9, BlockDirection.East, blockFactory, world);
            Assert.AreEqual(blockIntId,connectorIntId);
            
            //南向きにベルトコンベアを設置した時、機械とつながるかをテスト
            (blockIntId,connectorIntId) = BlockPlaceToGetMachineIdAndConnectorId(
                -10, 0, 
                -9, 0, BlockDirection.South, blockFactory, world);
            Assert.AreEqual(blockIntId,connectorIntId);
            
            //西向きにベルトコンベアを設置した時、機械とつながるかをテスト
            (blockIntId,connectorIntId) = BlockPlaceToGetMachineIdAndConnectorId(
                0, -10, 
                0, -9, BlockDirection.West, blockFactory, world);
            Assert.AreEqual(blockIntId,connectorIntId); 
        }

        private (int, int) BlockPlaceToGetMachineIdAndConnectorId(int machineX,int machineY,int conveyorX,int conveyorY,BlockDirection direction,BlockFactory blockFactory,IWorldBlockDatastore world)
        {
            var normalMachine = blockFactory.Create(MachineId, IntId.NewIntId());
            world.AddBlock(normalMachine, 10, 0, BlockDirection.North);
            var beltConveyor = (NormalBeltConveyor)blockFactory.Create(BeltConveyorId, IntId.NewIntId());
            world.AddBlock(beltConveyor, 9, 0, BlockDirection.North);
            //繋がっているコネクターを取得
            var _connector = (NormalMachine)typeof(NormalBeltConveyor).GetField("_connector",BindingFlags.NonPublic | BindingFlags.Instance).GetValue(beltConveyor);
            
            return (normalMachine.GetIntId(), _connector.GetIntId());
        }
    }
}