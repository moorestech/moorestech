#if NET6_0
using Core.EnergySystem;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Test.Module.TestMod;

namespace Test.CombinedTest.Game
{
    public class ConnectElectricSegmentTest
    {
        //ID
        private const int ElectricPoleId = UnitTestModBlockId.ElectricPoleId;
        private const int MachineId = UnitTestModBlockId.MachineId;
        private const int GenerateId = UnitTestModBlockId.GeneratorId;

        
        [Test]
        public void PlaceElectricPoleToPlaceElectricPoleTest()
        {
            var (_, saveServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = saveServiceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = saveServiceProvider.GetService<IBlockFactory>();

            
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 0), 0, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 1), 2, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 2), 3, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 3), -3, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 4), 0, 3, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 5), 0, -3, BlockDirection.North);

            
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 10), 7, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 11), -7, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 12), 0, 7, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 13), 0, -7, BlockDirection.North);

            var worldElectricSegment = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();
            
            Assert.AreEqual(5, worldElectricSegment.GetEnergySegmentListCount());

            var segment = worldElectricSegment.GetEnergySegment(0);
            
            var electricPoles = segment.EnergyTransformers;

            
            Assert.AreEqual(6, electricPoles.Count);
            //ID
            for (var i = 0; i < 6; i++) Assert.AreEqual(i, electricPoles[i].EntityId);

            //ID
            for (var i = 10; i < 13; i++) Assert.AreEqual(false, electricPoles.ContainsKey(i));

            
            
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 15), 5, 0, BlockDirection.North);
            
            Assert.AreEqual(4, worldElectricSegment.GetEnergySegmentListCount());
            
            segment = worldElectricSegment.GetEnergySegment(3);
            electricPoles = segment.EnergyTransformers;
            
            Assert.AreEqual(8, electricPoles.Count);
            //ID
            Assert.AreEqual(10, electricPoles[10].EntityId);
            Assert.AreEqual(15, electricPoles[15].EntityId);
        }

        
        [Test]
        public void PlaceElectricPoleToPlaceMachineTest()
        {
            var (_, saveServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = saveServiceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = saveServiceProvider.GetService<IBlockFactory>();

            
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 0), 0, 0, BlockDirection.North);

            
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 1), 2, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 2), -2, 0, BlockDirection.North);
            
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 3), 0, 2, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 4), 0, -2, BlockDirection.North);

            
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 10), 3, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 11), -3, 0, BlockDirection.North);
            
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 12), 0, 3, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 13), 0, -3, BlockDirection.North);

            var segmentDatastore = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();
            
            var segment = segmentDatastore.GetEnergySegment(0);
            
            var electricBlocks = segment.Consumers;
            var powerGeneratorBlocks = segment.Generators;


            
            Assert.AreEqual(2, electricBlocks.Count);
            Assert.AreEqual(2, powerGeneratorBlocks.Count);
            //ID
            Assert.AreEqual(1, electricBlocks[1].EntityId);
            Assert.AreEqual(2, electricBlocks[2].EntityId);
            Assert.AreEqual(3, powerGeneratorBlocks[3].EntityId);
            Assert.AreEqual(4, powerGeneratorBlocks[4].EntityId);

            
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 20), 3, 1, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 21), 1, 3, BlockDirection.North);

            segment = segmentDatastore.GetEnergySegment(0);
            electricBlocks = segment.Consumers;
            powerGeneratorBlocks = segment.Generators;
            
            Assert.AreEqual(1, segmentDatastore.GetEnergySegmentListCount());
            Assert.AreEqual(3, electricBlocks.Count);
            Assert.AreEqual(3, powerGeneratorBlocks.Count);
            //ID
            Assert.AreEqual(10, electricBlocks[10].EntityId);
            Assert.AreEqual(12, powerGeneratorBlocks[12].EntityId);
        }

        
        [Test]
        public void PlaceMachineToPlaceElectricPoleTest()
        {
            var (_, saveServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = saveServiceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = saveServiceProvider.GetService<IBlockFactory>();


            
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 1), 2, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 2), -2, 0, BlockDirection.North);
            
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 3), 0, 2, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 4), 0, -2, BlockDirection.North);

            
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 10), 3, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 11), -3, 0, BlockDirection.North);
            
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 12), 0, 3, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 13), 0, -3, BlockDirection.North);


            
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 0), 0, 0, BlockDirection.North);


            
            var segment = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>().GetEnergySegment(0);
            
            var electricBlocks = segment.Consumers;
            var powerGeneratorBlocks = segment.Generators;


            
            Assert.AreEqual(2, electricBlocks.Count);
            Assert.AreEqual(2, powerGeneratorBlocks.Count);
            //ID
            Assert.AreEqual(1, electricBlocks[1].EntityId);
            Assert.AreEqual(2, electricBlocks[2].EntityId);
            Assert.AreEqual(3, powerGeneratorBlocks[3].EntityId);
            Assert.AreEqual(4, powerGeneratorBlocks[4].EntityId);
        }

        //々
        [Test]
        public void SegmentConnectionTest()
        {
            var (_, saveServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = saveServiceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = saveServiceProvider.GetService<IBlockFactory>();

            
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 0), 0, 0, BlockDirection.North);
            
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 1), 2, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 2), -2, 0, BlockDirection.North);

            
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 10), 6, 0, BlockDirection.North);
            
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 3), 7, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 4), 7, 1, BlockDirection.North);

            var segmentDatastore = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();
            
            Assert.AreEqual(2, segmentDatastore.GetEnergySegmentListCount());

            
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 20), 3, 0, BlockDirection.North);
            
            Assert.AreEqual(1, segmentDatastore.GetEnergySegmentListCount());
            
            var segment = segmentDatastore.GetEnergySegment(0);
            
            Assert.AreEqual(2, segment.Consumers.Count);
            Assert.AreEqual(2, segment.Generators.Count);
        }
    }
}
#endif