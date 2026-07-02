using System;
using System.Linq;
using Client.Game.InGame.Block;
using Game.Block.Blocks.ElectricWire;
using Game.Block.Interface;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire
{
    /// <summary>
    /// 電力ワイヤーの状態変更を処理するプロセッサ
    /// Processor for handling electric wire state changes
    /// </summary>
    public class ElectricWireStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        private ElectricWireLineView _wireLineView;

        public void Initialize(BlockGameObject blockGameObject)
        {
            // 動的付与のため、ライン表示コンポーネントも自身で生成する
            // Since this is added dynamically, create the line view component here too
            _wireLineView = gameObject.AddComponent<ElectricWireLineView>();
            _wireLineView.Initialize(blockGameObject);
        }

        public void OnChangeState(BlockStateMessagePack blockState)
        {
            // 電力ワイヤーのステートを取得する
            // Get the electric wire state
            var state = blockState.GetStateDetail<ElectricWireStateDetail>(ElectricWireStateDetail.BlockStateDetailKey);
            if (state == null) return;

            // 接続先InstanceIdを配列に変換する
            // Convert partner instance IDs to an array
            var partnerInstanceIds = state.PartnerBlockInstanceIds?
                .Select(id => new BlockInstanceId(id))
                .ToArray() ?? Array.Empty<BlockInstanceId>();

            // ワイヤー表示を更新する
            // Update the wire display
            _wireLineView.UpdateWireLines(partnerInstanceIds);
        }
    }
}
