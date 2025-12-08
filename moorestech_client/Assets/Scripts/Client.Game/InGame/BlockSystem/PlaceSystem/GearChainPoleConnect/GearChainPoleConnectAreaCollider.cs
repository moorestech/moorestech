using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Game.Block.Blocks.GearChainPole;
using Mooresmaster.Model.BlocksModule;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// GearChainPoleブロックのレイキャスト検出とステート管理を行うコンポーネント
    /// Component for raycast detection and state management of GearChainPole blocks
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class GearChainPoleConnectAreaCollider : MonoBehaviour, IGearChainPoleConnectAreaCollider, IBlockStateChangeProcessor
    {
        private BlockGameObject _blockGameObject;
        private GearChainPoleBlockParam _param;
        private readonly List<Vector3Int> _connectedPolePositions = new();

        // IGearChainPoleConnectAreaCollider実装
        // IGearChainPoleConnectAreaCollider implementation
        public Vector3Int Position { get; private set; }
        public float MaxConnectionDistance => _param?.MaxConnectionDistance ?? 10f;
        public bool IsConnectionFull => _connectedPolePositions.Count >= (_param?.MaxConnectionCount ?? 4);
        public IReadOnlyList<Vector3Int> ConnectedPolePositions => _connectedPolePositions;

        public void Initialize(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
            Position = blockGameObject.BlockPosInfo.OriginalPos;

            // マスターデータからパラメータを取得する
            // Get parameters from master data
            _param = blockGameObject.BlockMasterElement.BlockParam as GearChainPoleBlockParam;
        }

        public bool ContainsConnection(Vector3Int partnerPosition)
        {
            return _connectedPolePositions.Contains(partnerPosition);
        }

        // IBlockStateChangeProcessor実装
        // IBlockStateChangeProcessor implementation
        public void OnChangeState(BlockStateMessagePack blockState)
        {
            // サーバーからの状態データをデシリアライズする
            // Deserialize state data from server
            var stateDetail = blockState.GetStateDetail<GearChainPoleStateDetail>(GearChainPoleStateDetail.BlockStateDetailKey);
            if (stateDetail == null) return;

            UpdateConnectedPositions(stateDetail);

            #region Internal

            void UpdateConnectedPositions(GearChainPoleStateDetail detail)
            {
                _connectedPolePositions.Clear();

                // サーバーから受信した位置データを使用する
                // Use position data received from server
                if (detail.PartnerPositions == null) return;

                foreach (var partnerPos in detail.PartnerPositions)
                {
                    _connectedPolePositions.Add(partnerPos);
                }
            }

            #endregion
        }
    }
}
