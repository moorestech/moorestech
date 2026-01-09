using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;
using Game.Train.RailGraph;
using UnityEngine;
using Game.Block.Factory.BlockTemplate.Utility;

namespace Game.Block.Factory.BlockTemplate
{
    /// <summary>
    /// バニラ版のTrainRailブロックを生成・復元するテンプレート
    /// </summary>
    public class VanillaTrainRailTemplate : IBlockTemplate
    {
        private readonly IRailGraphDatastore _railGraphDatastore;

        public VanillaTrainRailTemplate(IRailGraphDatastore railGraphDatastore)
        {
            _railGraphDatastore = railGraphDatastore;
        }
        /// <summary>
        /// 新規にブロック（と対応するRailComponent等）を生成
        /// </summary>
        public IBlock New(BlockMasterElement blockMasterElement,
            BlockInstanceId blockInstanceId,
            BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            var trainRailParam = blockMasterElement.BlockParam as TrainRailBlockParam;
            // railブロックは常にRailComponentが1つだけ
            var railComponents = new RailComponent[1];
            
            // ブロック生成パラメータからRailBridgePierComponentStateDetailを取得して方向ベクトルを取得
            var state = createParams.GetStateDetail<RailBridgePierComponentStateDetail>(RailBridgePierComponentStateDetail.StateDetailKey);
            if (state == null)
            {
                // 状態情報が存在しないため詳細ログを出力
                Debug.LogError($"[VanillaTrainRailTemplate] Missing create param: {RailBridgePierComponentStateDetail.StateDetailKey} for block {blockMasterElement.Name}");
            }
            var railBlockDirection = state?.RailBlockDirection;

            // RailComponentを生成
            var railComponentId = new RailComponentID(blockPositionInfo.OriginalPos, 0);
            var railComponentPosition = RailComponentUtility.CalculateRailComponentPosition(blockPositionInfo,trainRailParam.RailPosition);
            railComponents[0] = new RailComponent(_railGraphDatastore, railComponentPosition, railBlockDirection, railComponentId);
            var railSaverComponent = new RailSaverComponent(railComponents);
            // StateDetailコンポーネントを生成
            var stateDetailComponent = new RailComponentStateDetailComponent(railComponents[0]);
            // コンポーネントをまとめてブロックに登録
            var components = new List<IBlockComponent>();
            components.Add(railSaverComponent);
            components.AddRange(railComponents);
            components.Add(stateDetailComponent);
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }

        /// <summary>
        /// セーブデータ（componentStates）からRailComponent等を復元してブロックを生成
        /// </summary>
        public IBlock Load(
                Dictionary<string, string> componentStates,
                BlockMasterElement masterElement,
                BlockInstanceId instanceId,
                BlockPositionInfo positionInfo)
        {
            var trainRailParam = masterElement.BlockParam as TrainRailBlockParam;
            var railComponents = RailComponentUtility.Restore1RailComponents(componentStates, positionInfo, trainRailParam.RailPosition, _railGraphDatastore);
            var railSaverComponent = new RailSaverComponent(railComponents);
            // StateDetailコンポーネントを生成
            var stateDetailComponent = new RailComponentStateDetailComponent(railComponents[0]);

            var components = new List<IBlockComponent> { railSaverComponent };
            components.AddRange(railComponents);
            components.Add(stateDetailComponent);

            return new BlockSystem(instanceId, masterElement.BlockGuid, components, positionInfo);
        }
    }
}


