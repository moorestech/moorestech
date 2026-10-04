using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
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

            // 接続先と引いた種類を受け取る
            // Receive partner IDs and their connect tool kinds
            var partners = ConnectionLinePartner.FromMessagePacks(state.Partners);

            // ライン表示を更新
            // Update line display
            chainLineView.UpdateConnectionLines(partners);
        }
    }
}
