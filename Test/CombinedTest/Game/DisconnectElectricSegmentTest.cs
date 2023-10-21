#if NET6_0
using Core.EnergySystem;
using Core.EnergySystem.Electric;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Test.Module.TestMod;

namespace Test.CombinedTest.Game
{
    
    public class DisconnectElectricSegmentTest
    {
        private const int ElectricPoleId = UnitTestModBlockId.ElectricPoleId;
        private const int MachineId = UnitTestModBlockId.MachineId;
        private const int GenerateId = UnitTestModBlockId.GeneratorId;

        [Test]
        public void RemoveElectricPoleToDisconnectSegment()
        {
            var (_, saveServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            /*
             * M □  □ G □  □ M
             * P □  □ P □  □ P
             * G
             */

            var worldBlockDatastore = saveServiceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = saveServiceProvider.GetService<IBlockFactory>();

            
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 0), 0, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 1), 3, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 2), 6, 0, BlockDirection.North);

            
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 3), 0, 1, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 4), 0, -1, BlockDirection.North);

            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 5), 3, 1, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 6), 6, 1, BlockDirection.North);

            var worldElectricSegment = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();
            
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());

            
            worldBlockDatastore.RemoveBlock(6, 0);
            
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());
            
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 2), 6, 0, BlockDirection.North);
            
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());


            
            worldBlockDatastore.RemoveBlock(3, 0);
            
            Assert.AreEqual(2, worldElectricSegment.GetEnergySegmentListCount());

            //2
            Assert.AreEqual(false, worldElectricSegment.GetEnergySegment(0).Generators.ContainsKey(5));
            Assert.AreEqual(false, worldElectricSegment.GetEnergySegment(1).Generators.ContainsKey(5));

            
            var segment1 = worldElectricSegment.GetEnergySegment(worldBlockDatastore.GetBlock(0, 0) as IElectricPole);
            var segment2 = worldElectricSegment.GetEnergySegment(worldBlockDatastore.GetBlock(6, 0) as IElectricPole);

            Assert.AreNotEqual(segment1.GetHashCode(), segment2.GetHashCode());

            
            worldBlockDatastore.RemoveBlock(6, 0);
            
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());
        }

        //1
        [Test]
        public void LoopedElectricSegmentRemoveElectricPoleTest()
        {
            /*
             * P □ □ P □ □ P
             * G □ □ □ □ □ □
             * M □ □ □ □ □ M
             * P □ □ P □ □ P
             * G □ □ G
             */
            var (_, saveServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            var worldBlockDatastore = saveServiceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = saveServiceProvider.GetService<IBlockFactory>();
            var worldElectricSegment = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();

            
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 0), 0, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 1), 3, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 2), 6, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 3), 0, 3, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 4), 3, 3, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 5), 6, 3, BlockDirection.North);

            
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 103), 0, 1, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 104), 0, -1, BlockDirection.North);

            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 105), 3, -1, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 106), 6, 1, BlockDirection.North);


            
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());

            
            worldBlockDatastore.RemoveBlock(3, 0);
            
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());

            
            Assert.AreEqual(false, worldElectricSegment.GetEnergySegment(0).Generators.ContainsKey(105));
        }
    }
}
#endif