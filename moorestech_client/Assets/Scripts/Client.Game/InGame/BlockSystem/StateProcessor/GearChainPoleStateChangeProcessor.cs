using System;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor.GearPole;
using Game.Block.Blocks.GearChainPole;
using Game.Block.Interface;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    /// <summary>
    /// GearChainPoleの状態変更を処理するプロセッサ
    /// Processor for handling GearChainPole state changes
    /// </summary>
    public class GearChainPoleStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        [SerializeField] private GearChainPoleChainLineView chainLineView;

        public void Initialize(BlockGameObject blockGameObject)
        {
            chainLineView.Initialize(blockGameObject);
        }

        public void OnChangeState(BlockStateMessagePack blockState)
        {
            // GearChainPoleのステートを取得
            // Get GearChainPole state
            var state = blockState.GetStateDetail<GearChainPoleStateDetail>(GearChainPoleStateDetail.BlockStateDetailKey);
            if (state == null) return;

            // 接続先InstanceIdを配列に変換
            // Convert partner instance IDs to array
            var partnerInstanceIds = state.PartnerBlockInstanceIds?
                .Select(id => new BlockInstanceId(id))
                .ToArray() ?? Array.Empty<BlockInstanceId>();

            // ライン表示を更新
            // Update line display
            chainLineView.UpdateChainLines(partnerInstanceIds);
        }
    }
}
