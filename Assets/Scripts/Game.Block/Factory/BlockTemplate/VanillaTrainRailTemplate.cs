using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.TrainRail;
using Game.Block.Factory.BlockTemplate.Utility;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Train.RailGraph;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Factory.BlockTemplate
{
    /// <summary>
    /// バニラ版のTrainRailブロックを生成・復元するテンプレート
    /// Template that creates and restores vanilla TrainRail blocks
    /// </summary>
    public class VanillaTrainRailTemplate : IBlockTemplate
    {
        private readonly IRailGraphDatastore _railGraphDatastore;

        public VanillaTrainRailTemplate(IRailGraphDatastore railGraphDatastore)
        {
            _railGraphDatastore = railGraphDatastore;
        }

        public IBlock New(
            BlockMasterElement blockMasterElement,
            BlockInstanceId blockInstanceId,
            BlockPositionInfo blockPositionInfo,
            BlockCreateParam[] createParams)
        {
            var trainRailParam = blockMasterElement.BlockParam as TrainRailBlockParam;
            var railComponents = new RailComponent[1];

            // 生成時に渡された向き情報を読み出す
            // Read rail direction provided at placement time
            var state = createParams.GetStateDetail<RailBridgePierComponentStateDetail>(RailBridgePierComponentStateDetail.StateDetailKey);
            if (state == null)
            {
                Debug.LogError($"[VanillaTrainRailTemplate] Missing create param: {RailBridgePierComponentStateDetail.StateDetailKey} for block {blockMasterElement.Name}");
                return null;
            }

            var railDirection = state.RailBlockDirection;
            var railComponentPosition = RailComponentUtility.CalculateRailComponentPosition(blockPositionInfo, trainRailParam.RailPosition);
            railComponents[0] = new RailComponent(_railGraphDatastore, railComponentPosition, railDirection, blockPositionInfo.OriginalPos, 0);

            var stateDetailComponent = new RailComponentStateDetailComponent(railComponents[0]);
            var components = new List<IBlockComponent>();
            components.AddRange(railComponents);
            components.Add(stateDetailComponent);
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }

        public IBlock Load(
            Dictionary<string, string> componentStates,
            BlockMasterElement masterElement,
            BlockInstanceId instanceId,
            BlockPositionInfo positionInfo)
        {
            var trainRailParam = masterElement.BlockParam as TrainRailBlockParam;

            // ブロック保存データから向きを復元する
            // Restore rail direction from block save data
            var railDirection = RailComponentStateDetailComponent.LoadRailDirection(componentStates);
            var railComponents = new RailComponent[1];
            var railComponentPosition = RailComponentUtility.CalculateRailComponentPosition(positionInfo, trainRailParam.RailPosition);
            railComponents[0] = new RailComponent(_railGraphDatastore, railComponentPosition, railDirection, positionInfo.OriginalPos, 0);

            var stateDetailComponent = new RailComponentStateDetailComponent(railComponents[0]);
            var components = new List<IBlockComponent>();
            components.AddRange(railComponents);
            components.Add(stateDetailComponent);
            return new BlockSystem(instanceId, masterElement.BlockGuid, components, positionInfo);
        }
    }
}