using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.TrainRail;
using Game.Block.Blocks.TrainRail.ContainerComponents;
using Game.Block.Factory.BlockTemplate.Utility;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Train.RailGraph;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaTrainFluidPlatformTemplate : IBlockTemplate
    {
        private readonly IRailGraphDatastore _railGraphDatastore;

        public VanillaTrainFluidPlatformTemplate(IRailGraphDatastore railGraphDatastore)
        {
            _railGraphDatastore = railGraphDatastore;
        }

        public IBlock New(BlockMasterElement masterElement,
            BlockInstanceId instanceId,
            BlockPositionInfo positionInfo, BlockCreateParam[] createParams)
        {
            var param = masterElement.BlockParam as TrainFluidPlatformBlockParam;

            var railComponents = RailComponentUtility.Create2RailComponents(positionInfo, param.EntryRailPosition, param.ExitRailPosition, _railGraphDatastore);
            RailComponentUtility.RegisterAndConnetStationBlocks(railComponents, _railGraphDatastore);

            var trainPlatformDockingComponent = new TrainPlatformDockingComponent(param.LoadingAnimeSpeed);
            var trainPlatformTransferComponent = new TrainPlatformTransferComponent(TrainPlatformTransferComponent.TransferMode.LoadToTrain);

            var fluidConnector = IFluidInventory.CreateFluidInventoryConnector(param.FluidInventoryConnectors, positionInfo);
            var trainPlatformFluidContainer = new TrainPlatformFluidContainerComponent(trainPlatformDockingComponent, trainPlatformTransferComponent, fluidConnector);

            var blockComponents = new List<IBlockComponent>();
            blockComponents.AddRange(railComponents);
            blockComponents.Add(trainPlatformDockingComponent);
            blockComponents.Add(trainPlatformTransferComponent);
            blockComponents.Add(trainPlatformFluidContainer);
            blockComponents.Add(fluidConnector);

            var createdBlock = new BlockSystem(instanceId, masterElement.BlockGuid, blockComponents, positionInfo);
            railComponents[0].FrontNode.StationRef.SetStationReference(createdBlock, StationNodeRole.Entry, StationNodeSide.Front);
            railComponents[1].FrontNode.StationRef.SetStationReference(createdBlock, StationNodeRole.Exit, StationNodeSide.Front);
            railComponents[1].BackNode.StationRef.SetStationReference(createdBlock, StationNodeRole.Entry, StationNodeSide.Back);
            railComponents[0].BackNode.StationRef.SetStationReference(createdBlock, StationNodeRole.Exit, StationNodeSide.Back);
            return createdBlock;
        }

        public IBlock Load(
            Dictionary<string, string> componentStates,
            BlockMasterElement masterElement,
            BlockInstanceId instanceId,
            BlockPositionInfo positionInfo)
        {
            var param = masterElement.BlockParam as TrainFluidPlatformBlockParam;

            var railComponents = RailComponentUtility.Restore2RailComponents(positionInfo, param.EntryRailPosition, param.ExitRailPosition, _railGraphDatastore);
            RailComponentUtility.RegisterStationBlocks(railComponents, _railGraphDatastore);

            var trainPlatformDockingComponent = new TrainPlatformDockingComponent(componentStates, param.LoadingAnimeSpeed);
            var trainPlatformTransferComponent = new TrainPlatformTransferComponent(componentStates);

            var fluidConnector = IFluidInventory.CreateFluidInventoryConnector(param.FluidInventoryConnectors, positionInfo);
            var trainPlatformFluidContainer = new TrainPlatformFluidContainerComponent(trainPlatformDockingComponent, trainPlatformTransferComponent, fluidConnector, componentStates);

            var blockComponents = new List<IBlockComponent>();
            blockComponents.AddRange(railComponents);
            blockComponents.Add(trainPlatformDockingComponent);
            blockComponents.Add(trainPlatformTransferComponent);
            blockComponents.Add(trainPlatformFluidContainer);
            blockComponents.Add(fluidConnector);

            var createdBlock = new BlockSystem(instanceId, masterElement.BlockGuid, blockComponents, positionInfo);
            railComponents[0].FrontNode.StationRef.SetStationReference(createdBlock, StationNodeRole.Entry, StationNodeSide.Front);
            railComponents[1].FrontNode.StationRef.SetStationReference(createdBlock, StationNodeRole.Exit, StationNodeSide.Front);
            railComponents[1].BackNode.StationRef.SetStationReference(createdBlock, StationNodeRole.Entry, StationNodeSide.Back);
            railComponents[0].BackNode.StationRef.SetStationReference(createdBlock, StationNodeRole.Exit, StationNodeSide.Back);
            return createdBlock;
        }
    }
}
