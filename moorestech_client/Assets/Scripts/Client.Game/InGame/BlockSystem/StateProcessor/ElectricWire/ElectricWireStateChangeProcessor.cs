using System;
using System.Collections.Generic;
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
    [RequireComponent(typeof(ElectricWireLineView))]
    public class ElectricWireStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        // 現在の接続先ID集合。電線ツールのプレビュー既接続判定が参照する
        // Current partner ID set, referenced by the wire tool preview's already-connected judgement
        public IReadOnlyCollection<BlockInstanceId> CurrentPartnerIds => _currentPartnerIds;
        private readonly HashSet<BlockInstanceId> _currentPartnerIds = new();

        private ElectricWireLineView _wireLineView;

        public void Initialize(BlockGameObject blockGameObject)
        {
            // RequireComponentで保証されるライン表示コンポーネントを取得する
            // Get the line view component guaranteed by RequireComponent
            _wireLineView = GetComponent<ElectricWireLineView>();
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

            // 接続先ID集合を最新状態へ置き換える
            // Replace the partner ID set with the latest state
            _currentPartnerIds.Clear();
            foreach (var id in partnerInstanceIds) _currentPartnerIds.Add(id);

            // ワイヤー表示を更新する
            // Update the wire display
            _wireLineView.UpdateWireLines(partnerInstanceIds);
        }
    }
}
