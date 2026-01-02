using System;
using UnityEngine;

namespace Game.Block.Interface.Component
{
    /// <summary>
    /// ブロックコネクタの共通インターフェース
    /// Common interface for block connectors
    /// </summary>
    /// <remarks>
    /// 各コネクタタイプ（Inventory、Gear、Fluid）から変換して使用する
    /// Used by converting from each connector type (Inventory, Gear, Fluid)
    /// </remarks>
    public interface IBlockConnector
    {
        Guid ConnectorGuid { get; }
        Vector3Int Offset { get; }
        Vector3Int[] Directions { get; }

        /// <summary>
        /// コネクタ固有のオプション情報
        /// Connector-specific option information
        /// </summary>
        object ConnectOption { get; }
    }
}
