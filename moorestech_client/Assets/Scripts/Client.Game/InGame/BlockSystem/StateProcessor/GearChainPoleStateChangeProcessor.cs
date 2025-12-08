using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor.GearPole;
using Game.Block.Blocks.GearChainPole;
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

            // 接続先座標を配列に変換
            // Convert partner positions to array
            var partnerPositions = state.PartnerBlockPositions?
                .Select(p => p.Vector3Int)
                .ToArray() ?? System.Array.Empty<Vector3Int>();

            // ライン表示を更新
            // Update line display
            chainLineView.UpdateChainLines(partnerPositions);
        }
    }
}
