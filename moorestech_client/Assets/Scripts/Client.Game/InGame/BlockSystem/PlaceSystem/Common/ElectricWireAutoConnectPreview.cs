using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Game.Block.Interface;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    /// 通常設置プレビュー中、電気系ブロックの自動接続先ワイヤーと合計消費電線数を表示する
    /// Shows auto-connect wires and total wire cost for electric blocks during normal placement preview
    /// </summary>
    public class ElectricWireAutoConnectPreview
    {
        private readonly BlockGameObjectDataStore _blockDataStore;
        private readonly AutoConnectWirePreviewRenderer _renderer;

        // グリッド座標・向き・ブロックが変化したときのみ再評価するためのキャッシュ
        // Cache to re-evaluate only when grid position, direction or block changes
        private bool _hasCache;
        private Vector3Int _cachedPosition;
        private BlockDirection _cachedDirection;
        private BlockId _cachedBlockId;
        private bool _cachedPlaceable;

        public ElectricWireAutoConnectPreview(Camera mainCamera, BlockGameObjectDataStore blockDataStore)
        {
            _blockDataStore = blockDataStore;
            _renderer = new AutoConnectWirePreviewRenderer(mainCamera);
        }

        /// <summary>
        /// 電気系ブロックなら自動接続を評価してワイヤー表示を更新し、設置可否を返す。非電気系は常にtrue
        /// Evaluates auto-connect for electric blocks, updates wires, and returns placeability. Non-electric is always true
        /// </summary>
        public bool UpdatePreview(BlockId blockId, Vector3Int position, BlockDirection direction, ILocalPlayerInventory inventory)
        {
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);

            // 電気系でなければワイヤー表示は行わず、設置はそのまま許可する
            // Non-electric blocks show no wires and placement stays allowed
            if (!ElectricWireBlockParamResolver.TryGetWireParam(blockMaster.BlockParam, out _, out _))
            {
                Hide();
                return true;
            }

            // 位置・向き・ブロックが前回と同じなら再評価せずキャッシュ結果を返す
            // Skip re-evaluation when position, direction and block are unchanged
            if (_hasCache && _cachedPosition == position && _cachedDirection == direction && _cachedBlockId == blockId)
                return _cachedPlaceable;

            // サーバーと同じ自動接続計画を評価する（シングルプレイなので同プロセスのワールド状態を直接参照）
            // Evaluate the same server-side auto-connect plan (single-player shares world state in-process)
            var plan = ElectricWireAutoConnectService.EvaluateAutoConnect(blockId, position, direction, inventory.ToList());
            UpdateVisual(position, plan);

            _hasCache = true;
            _cachedPosition = position;
            _cachedDirection = direction;
            _cachedBlockId = blockId;
            _cachedPlaceable = plan.IsPlaceable;
            return plan.IsPlaceable;

            #region Internal

            void UpdateVisual(Vector3Int originPos, ElectricWireAutoConnectPlan evaluatedPlan)
            {
                // 電線不足（設置不可）または接続先なしならワイヤーは非表示にする
                // Hide wires when insufficient wires (not placeable) or there is no target
                if (!evaluatedPlan.IsPlaceable || evaluatedPlan.Targets.Count == 0)
                {
                    _renderer.Hide();
                    return;
                }

                // 接続先ブロックの座標を解決し、合計消費電線数を集計する
                // Resolve target block positions and sum the total wire cost
                var targetPositions = new List<Vector3Int>();
                var totalCost = 0;
                foreach (var target in evaluatedPlan.Targets)
                {
                    if (!_blockDataStore.TryGetBlockGameObject(target.TargetId, out var targetBlock)) continue;
                    targetPositions.Add(targetBlock.BlockPosInfo.OriginalPos);
                    totalCost += target.Cost.Count;
                }

                _renderer.Show(originPos, targetPositions, totalCost);
            }

            #endregion
        }

        public void Hide()
        {
            _renderer.Hide();
            _hasCache = false;
        }
    }
}
