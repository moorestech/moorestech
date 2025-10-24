using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.BlocksModule;
using Game.Train.RailGraph;
using Newtonsoft.Json;
using Game.Context;
using UnityEngine;
using Game.Block.Factory.BlockTemplate.Utility;

namespace Game.Block.Factory.BlockTemplate
{
    /// <summary>
    /// バニラ版のTrainRailブロックを生成・復元するテンプレート
    /// </summary>
    public class VanillaTrainRailTemplate : IBlockTemplate
    {
        /// <summary>
        /// 新規にブロック（と対応するRailComponent等）を生成
        /// </summary>
        public IBlock New(
            BlockMasterElement blockMasterElement,
            BlockInstanceId blockInstanceId,
            BlockPositionInfo blockPositionInfo)
        {
            // railブロックは常にRailComponentが1つだけ
            var railComponents = new RailComponent[1];

            // RailComponentを生成
            var railComponentId = new RailComponentID(blockPositionInfo.OriginalPos, 0);
            var railComponentPositions = RailComponentUtility.CalculateRailComponentPositions(blockPositionInfo);
            railComponents[0] = new RailComponent(railComponentPositions[0], blockPositionInfo.BlockDirection, railComponentId);
            var railSaverComponent = RailComponentFactory.CreateRailSaverComponent(railComponents);
            // コンポーネントをまとめてブロックに登録
            var components = new List<IBlockComponent>();
            components.Add(railSaverComponent);
            components.AddRange(railComponents);
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
            var railComponents = RailComponentUtility.RestoreRailComponents(componentStates, positionInfo);
            var railSaverComponent = RailComponentFactory.CreateRailSaverComponent(railComponents);

            var components = new List<IBlockComponent> { railSaverComponent };
            components.AddRange(railComponents);

            return new BlockSystem(instanceId, masterElement.BlockGuid, components, positionInfo);
        }
    }
}
