using System;
using System.Linq;
using Mooresmaster.Model.FluidInventoryConnectsModule;
using Mooresmaster.Model.GearConnectOptionModule;
using Mooresmaster.Model.GearModule;
using Mooresmaster.Model.InventoryConnectsModule;
using UnityEngine;

namespace Game.Block.Interface.Component
{
    /// <summary>
    /// 各種コネクタ型をIBlockConnectorに変換するアダプタ
    /// Adapter to convert various connector types to IBlockConnector
    /// </summary>
    public class BlockConnectorAdapter : IBlockConnector
    {
        public Guid ConnectorGuid { get; }
        public Vector3Int Offset { get; }
        public Vector3Int[] Directions { get; }
        public object ConnectOption { get; }

        // InventoryConnects用
        // For InventoryConnects
        public BlockConnectorAdapter(InputConnectsElement item)
        {
            ConnectorGuid = item.ConnectorGuid;
            Offset = item.Offset;
            Directions = item.Directions;
            ConnectOption = item.Option;
        }

        public BlockConnectorAdapter(OutputConnectsElement item)
        {
            ConnectorGuid = item.ConnectorGuid;
            Offset = item.Offset;
            Directions = item.Directions;
            ConnectOption = item.Option;
        }

        // FluidInventoryConnects用
        // For FluidInventoryConnects
        public BlockConnectorAdapter(InflowConnectsElement item)
        {
            ConnectorGuid = item.ConnectorGuid;
            Offset = item.Offset;
            Directions = item.Directions;
            ConnectOption = item.Option;
        }

        public BlockConnectorAdapter(OutflowConnectsElement item)
        {
            ConnectorGuid = item.ConnectorGuid;
            Offset = item.Offset;
            Directions = item.Directions;
            ConnectOption = item.Option;
        }

        // GearConnects用
        // For GearConnects
        public BlockConnectorAdapter(GearConnectsElement item)
        {
            ConnectorGuid = item.ConnectorGuid;
            Offset = item.Offset;
            Directions = item.Directions;
            // GearConnectOptionをIGearConnectOptionにラップ
            // Wrap GearConnectOption to IGearConnectOption
            ConnectOption = WrapGearConnectOption(item.Option);
        }

        #region Extension Methods

        // 配列変換用の静的メソッド
        // Static methods for array conversion
        public static IBlockConnector[] FromInputConnects(InputConnectsElement[] items)
        {
            return items?.Select(i => (IBlockConnector)new BlockConnectorAdapter(i)).ToArray() ?? Array.Empty<IBlockConnector>();
        }

        public static IBlockConnector[] FromOutputConnects(OutputConnectsElement[] items)
        {
            return items?.Select(i => (IBlockConnector)new BlockConnectorAdapter(i)).ToArray() ?? Array.Empty<IBlockConnector>();
        }

        public static IBlockConnector[] FromInflowConnects(InflowConnectsElement[] items)
        {
            return items?.Select(i => (IBlockConnector)new BlockConnectorAdapter(i)).ToArray() ?? Array.Empty<IBlockConnector>();
        }

        public static IBlockConnector[] FromOutflowConnects(OutflowConnectsElement[] items)
        {
            return items?.Select(i => (IBlockConnector)new BlockConnectorAdapter(i)).ToArray() ?? Array.Empty<IBlockConnector>();
        }

        public static IBlockConnector[] FromGearConnects(GearConnectsElement[] items)
        {
            return items?.Select(i => (IBlockConnector)new BlockConnectorAdapter(i)).ToArray() ?? Array.Empty<IBlockConnector>();
        }

        /// <summary>
        /// テスト用のファクトリメソッド
        /// Factory method for testing
        /// </summary>
        public static IBlockConnector CreateForTest(Guid connectorGuid, Vector3Int offset, Vector3Int[] directions, object connectOption = null)
        {
            return new TestBlockConnector(connectorGuid, offset, directions, connectOption);
        }

        #endregion

        #region Internal

        private static IGearConnectOption WrapGearConnectOption(GearConnectOption option)
        {
            return new GearConnectOptionWrapper(option.IsReverse);
        }

        private class GearConnectOptionWrapper : IGearConnectOption
        {
            public bool IsReverse { get; }

            public GearConnectOptionWrapper(bool isReverse)
            {
                IsReverse = isReverse;
            }
        }

        private class TestBlockConnector : IBlockConnector
        {
            public Guid ConnectorGuid { get; }
            public Vector3Int Offset { get; }
            public Vector3Int[] Directions { get; }
            public object ConnectOption { get; }

            public TestBlockConnector(Guid connectorGuid, Vector3Int offset, Vector3Int[] directions, object connectOption)
            {
                ConnectorGuid = connectorGuid;
                Offset = offset;
                Directions = directions;
                ConnectOption = connectOption;
            }
        }

        #endregion
    }
}
