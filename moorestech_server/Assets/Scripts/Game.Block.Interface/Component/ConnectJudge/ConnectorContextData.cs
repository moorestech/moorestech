using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Interface.Component.ConnectJudge
{
    // 接続計算の入力はブロックごとに所有し、共有Contextへ毎回渡す
    // Own connection inputs per block and supply them to the shared context on each call
    public class ConnectorContextData
    {
        public readonly BlockPositionInfo Position;
        public readonly IReadOnlyList<IBlockConnector> Inputs;
        public readonly IReadOnlyList<IBlockConnector> Outputs;

        public ConnectorContextData(IReadOnlyList<IBlockConnector> inputs, IReadOnlyList<IBlockConnector> outputs, BlockPositionInfo position)
        {
            Inputs = inputs;
            Outputs = outputs;
            Position = position;
        }
    }

    // 配置イベント時はコンポーネント逆引きが未登録なので座標だけで読む
    // Read by coordinate because component reverse mappings are not registered during placement events
    public interface IConnectorWorldLookup
    {
        IBlock GetBlock(Vector3Int position);
    }
}
