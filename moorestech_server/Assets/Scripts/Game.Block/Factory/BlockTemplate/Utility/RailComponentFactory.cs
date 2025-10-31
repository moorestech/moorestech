using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Train.RailGraph;
using System.Diagnostics;

namespace Game.Block.Factory.BlockTemplate.Utility
{
    public static class RailComponentFactory
    {
        /// <summary>
        /// 指定数のRailComponentを作成し、必要に応じて自動的に接続します。
        /// 今のところstation,cargoなど1つのブロックに2つのRailComponentを持つものだけを想定しています。
        /// 一応countが3以上でも動くが要調整
        /// </summary>
        public static RailComponent[] CreateRailComponents(int count, BlockPositionInfo positionInfo)
        {
            var positions = RailComponentUtility.CalculateRailComponentPositions(positionInfo);
            var components = new RailComponent[count];

            for (int i = 0; i < count; i++)
            {
                var componentId = new RailComponentID(positionInfo.OriginalPos, i);
                components[i] = new RailComponent(positions[i], positionInfo.BlockDirection , componentId);
            }

            // stationの前と後ろにそれぞれrailComponentがある、自動で接続する
            if (count == 2)
            {
                components[0].ConnectRailComponent(components[1], true, true);
            }

            return components;
        }

        /// <summary>
        /// RailSaverComponentを生成します。
        /// </summary>
        public static RailSaverComponent CreateRailSaverComponent(RailComponent[] railComponents)
        {
            return new RailSaverComponent(railComponents);
        }
    }
}
