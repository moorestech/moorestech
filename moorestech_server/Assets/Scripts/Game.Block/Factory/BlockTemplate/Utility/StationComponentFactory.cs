using Game.Block.Interface;
using Game.Train.RailGraph;
using Game.Train.Utility;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface.Component;
using System;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate.Utility
{
    public static class StationComponentFactory
    {
        /// <summary>
        /// StationまたはCargoPlatformコンポーネントを作成し、RailComponentを接続します。
        /// </summary>
        public static T CreateAndConnectStationComponent<T>(
            BlockMasterElement masterElement,
            BlockPositionInfo positionInfo,
            RailComponent[] railComponents)
            where T : IBlockComponent
        {
            T component = CreateStationComponent<T>(masterElement.BlockParam);

            ConnectStationComponents(positionInfo, railComponents[1], railComponents[0]);

            return component;
        }

        /// <summary>
        /// T型のStation関連コンポーネントを生成します。
        /// </summary>
        private static T CreateStationComponent<T>(IBlockParam blockParam) where T : IBlockComponent
        {
            switch (blockParam)
            {
                case TrainCargoPlatformBlockParam cargoParam when typeof(T) == typeof(CargoplatformComponent):
                    return (T)(IBlockComponent)new CargoplatformComponent(cargoParam.PlatformDistance, cargoParam.InputSlotCount, cargoParam.OutputSlotCount);

                case TrainStationBlockParam stationParam when typeof(T) == typeof(StationComponent):
                    return (T)(IBlockComponent)new StationComponent(stationParam.StationDistance, "test", 1);

                default:
                    throw new ArgumentException($"Unsupported blockParam type: {blockParam.GetType()} for component type {typeof(T)}");
            }
        }

        /// <summary>
        /// StationのRailComponent接続処理（既存の処理を共通化したもの）
        /// stationをつなげて設置した場合に自動でrailComponentを接続するための処理
        /// </summary>
        private static void ConnectStationComponents(BlockPositionInfo positionInfo, RailComponent frontComponent, RailComponent backComponent)
        {
            var (frontPos, hasFrontConnection) = StationConnectionChecker.IsStationConnectedToFront(positionInfo);
            if (hasFrontConnection)
            {
                RailComponentUtility.EstablishConnection(frontComponent, new ConnectionDestination(new RailComponentID(frontPos, 0), true), true);
            }

            var (backPos, hasBackConnection) = StationConnectionChecker.IsStationConnectedToBack(positionInfo);
            if (hasBackConnection)
            {
                RailComponentUtility.EstablishConnection(backComponent, new ConnectionDestination(new RailComponentID(backPos, 1), false), false);
            }
        }
    }
}
