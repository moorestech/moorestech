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

        /// <summary>
        /// 新規にブロック（および対応するRailComponent等）を生成する
        /// Create a new block with RailComponents
        /// </summary>
        public IBlock New(BlockMasterElement masterElement,
            BlockInstanceId instanceId,
            BlockPositionInfo positionInfo, BlockCreateParam[] createParams)
        {
            var param = masterElement.BlockParam as TrainFluidPlatformBlockParam;

            // レールコンポーネントの生成と接続
            // Create and connect rail components
            var railComponents = RailComponentUtility.Create2RailComponents(positionInfo, param.EntryRailPosition, param.ExitRailPosition, _railGraphDatastore);
            RailComponentUtility.RegisterAndConnetStationBlocks(railComponents, _railGraphDatastore);

            // ドッキング・転送コンポーネントの生成
            // Create docking and transfer components
            var trainPlatformDockingComponent = new TrainPlatformDockingComponent(param.LoadingAnimeSpeed);
            var trainPlatformTransferComponent = new TrainPlatformTransferComponent(TrainPlatformTransferComponent.TransferMode.LoadToTrain);

            // 液体コネクタとコンテナの生成
            // Create fluid connector and container
            var fluidConnector = IFluidInventory.CreateFluidInventoryConnector(param.FluidInventoryConnectors, positionInfo);
            var trainPlatformFluidContainer = new TrainPlatformFluidContainerComponent(trainPlatformDockingComponent, trainPlatformTransferComponent, param.Capacity, fluidConnector);

            // 生成したコンポーネントをブロックに登録する
            // Register created components to the block
            var blockComponents = new List<IBlockComponent>();
            blockComponents.AddRange(railComponents);
            blockComponents.Add(trainPlatformDockingComponent);
            blockComponents.Add(trainPlatformTransferComponent);
            blockComponents.Add(trainPlatformFluidContainer);
            blockComponents.Add(fluidConnector);

            // 各RailNodeにStationReferenceを設定
            // Set StationReference on each RailNode
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

            // レールコンポーネントの復元と登録
            // Restore and register rail components
            var railComponents = RailComponentUtility.Restore2RailComponents(positionInfo, param.EntryRailPosition, param.ExitRailPosition, _railGraphDatastore);
            RailComponentUtility.RegisterStationBlocks(railComponents, _railGraphDatastore);

            // ドッキング・転送コンポーネントの復元
            // Restore docking and transfer components
            var trainPlatformDockingComponent = new TrainPlatformDockingComponent(componentStates, param.LoadingAnimeSpeed);
            var trainPlatformTransferComponent = new TrainPlatformTransferComponent(componentStates);

            // 液体コネクタとコンテナの復元
            // Restore fluid connector and container
            var fluidConnector = IFluidInventory.CreateFluidInventoryConnector(param.FluidInventoryConnectors, positionInfo);
            var trainPlatformFluidContainer = new TrainPlatformFluidContainerComponent(trainPlatformDockingComponent, trainPlatformTransferComponent, param.Capacity, fluidConnector, componentStates);

            // 復元したコンポーネントをブロックに登録する
            // Register restored components to the block
            var blockComponents = new List<IBlockComponent>();
            blockComponents.AddRange(railComponents);
            blockComponents.Add(trainPlatformDockingComponent);
            blockComponents.Add(trainPlatformTransferComponent);
            blockComponents.Add(trainPlatformFluidContainer);
            blockComponents.Add(fluidConnector);

            // 各RailNodeにStationReferenceを設定
            // Set StationReference on each RailNode
            var createdBlock = new BlockSystem(instanceId, masterElement.BlockGuid, blockComponents, positionInfo);
            railComponents[0].FrontNode.StationRef.SetStationReference(createdBlock, StationNodeRole.Entry, StationNodeSide.Front);
            railComponents[1].FrontNode.StationRef.SetStationReference(createdBlock, StationNodeRole.Exit, StationNodeSide.Front);
            railComponents[1].BackNode.StationRef.SetStationReference(createdBlock, StationNodeRole.Entry, StationNodeSide.Back);
            railComponents[0].BackNode.StationRef.SetStationReference(createdBlock, StationNodeRole.Exit, StationNodeSide.Back);
            return createdBlock;
        }
    }
}
