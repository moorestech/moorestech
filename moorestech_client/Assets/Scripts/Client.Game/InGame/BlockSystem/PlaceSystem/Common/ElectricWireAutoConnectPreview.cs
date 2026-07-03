using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
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
        private static readonly IReadOnlyList<Vector3Int> EmptyTargets = new List<Vector3Int>();

        private readonly BlockGameObjectDataStore _blockDataStore;
        private readonly AutoConnectWirePreviewRenderer _renderer;

        // セル単位の評価キャッシュ。向きかブロックが変わったら全破棄する
        // Per-cell evaluation cache, fully invalidated when direction or block changes
        private readonly Dictionary<Vector3Int, CellPlan> _cellPlanCache = new();
        private BlockDirection _cachedDirection;
        private BlockId _cachedBlockId;
        private bool _hasCacheKey;

        public ElectricWireAutoConnectPreview(Camera mainCamera, BlockGameObjectDataStore blockDataStore)
        {
            _blockDataStore = blockDataStore;
            _renderer = new AutoConnectWirePreviewRenderer(mainCamera);
        }

        /// <summary>
        /// 電気系なら各セルの自動接続を評価してPlaceableを上書きし、表示を更新する。戻り値は設置クリック可否
        /// For electric blocks, evaluates auto-connect per cell, overrides Placeable and updates visuals. Returns click placeability
        /// </summary>
        public bool ApplyAutoConnect(List<PlaceInfo> placeInfos, BlockId blockId, BlockDirection direction, ILocalPlayerInventory inventory, Vector3Int cursorCell)
        {
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);

            // 電気系でなければワイヤー表示は行わず、設置はそのまま許可する
            // Non-electric blocks show no wires and placement stays allowed
            if (!ElectricWireBlockParamResolver.TryGetWireParam(blockMaster.BlockParam, out _, out _))
            {
                Hide();
                return true;
            }

            // セルが無ければ評価も表示も不要
            // Nothing to evaluate or show without cells
            if (placeInfos.Count == 0) { Hide(); return true; }

            InvalidateCacheOnKeyChange();

            // 各セルを評価し可否と消費数を集計
            // Evaluate each cell for placeability and sum the total cost
            // 注意: ドラッグ中の未設置電柱同士の接続は評価に現れない近似（サーバーが設置順に個別再検証するため安全側）
            // Note: connections between not-yet-placed poles in a drag are approximated away (the server re-validates each in placement order, so this stays safe)
            var totalCost = 0;
            var anyPlaceable = false;
            PlaceInfo cursorInfo = null;
            foreach (var placeInfo in placeInfos)
            {
                var plan = GetOrEvaluateCell(placeInfo.Position);
                if (!plan.IsPlaceable) placeInfo.Placeable = false;
                if (placeInfo.Placeable)
                {
                    totalCost += plan.TotalCost;
                    anyPlaceable = true;
                }
                if (placeInfo.Position == cursorCell) cursorInfo = placeInfo;
            }

            // ワイヤー線はカーソルセル分のみ描画し（全セル分は過剰）、ラベルは全セル合計を表示する
            // Draw wires only for the cursor cell (all cells would be excessive); the label shows the drag-wide total
            cursorInfo ??= placeInfos[^1];
            var cursorTargets = cursorInfo.Placeable ? _cellPlanCache[cursorInfo.Position].TargetPositions : EmptyTargets;
            _renderer.Show(cursorInfo.Position, cursorTargets, totalCost);

            // 設置可能なセルが1つでも残っていればクリック許可（不可セルはサーバーが個別に拒否する既存方針に揃える）
            // Allow the click when any cell remains placeable (bad cells are rejected per-cell by the server, matching existing policy)
            return anyPlaceable;

            #region Internal

            void InvalidateCacheOnKeyChange()
            {
                if (_hasCacheKey && _cachedDirection == direction && _cachedBlockId == blockId) return;
                _cellPlanCache.Clear();
                _cachedDirection = direction;
                _cachedBlockId = blockId;
                _hasCacheKey = true;
            }

            CellPlan GetOrEvaluateCell(Vector3Int position)
            {
                if (_cellPlanCache.TryGetValue(position, out var cached)) return cached;

                // サーバーと同じ自動接続計画を評価する（シングルプレイなので同プロセスのワールド状態を直接参照）
                // Evaluate the same server-side auto-connect plan (single-player shares world state in-process)
                var plan = ElectricWireAutoConnectService.EvaluateAutoConnect(blockId, position, direction, inventory.ToList());

                // 座標解決と消費数集計しセル計画化
                // Resolve target positions, sum the wire cost and store as the cell plan
                var targetPositions = new List<Vector3Int>();
                var cost = 0;
                foreach (var target in plan.Targets)
                {
                    if (!_blockDataStore.TryGetBlockGameObject(target.TargetId, out var targetBlock)) continue;
                    targetPositions.Add(targetBlock.BlockPosInfo.OriginalPos);
                    cost += target.Cost.Count;
                }

                var cellPlan = new CellPlan(plan.IsPlaceable, cost, targetPositions);
                _cellPlanCache[position] = cellPlan;
                return cellPlan;
            }

            #endregion
        }

        public void Hide()
        {
            _renderer.Hide();
            _cellPlanCache.Clear();
            _hasCacheKey = false;
        }

        // 1セル分の評価結果（可否・消費電線数・接続先座標）
        // Evaluation result for a single cell (placeability, wire cost, target positions)
        private class CellPlan
        {
            public readonly bool IsPlaceable;
            public readonly int TotalCost;
            public readonly IReadOnlyList<Vector3Int> TargetPositions;

            public CellPlan(bool isPlaceable, int totalCost, IReadOnlyList<Vector3Int> targetPositions)
            {
                IsPlaceable = isPlaceable;
                TotalCost = totalCost;
                TargetPositions = targetPositions;
            }
        }
    }
}
