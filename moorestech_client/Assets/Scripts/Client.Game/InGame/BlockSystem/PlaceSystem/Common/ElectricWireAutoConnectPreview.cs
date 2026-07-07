using System.Collections.Generic;
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
    /// 評価は受信済みクライアント状態のみで行い、ドラッグ中はセル順の仮想在庫でサーバーの逐次消費を再現する
    /// Evaluation uses received client state only; during drags a virtual inventory replays the server's sequential consumption
    /// </summary>
    public class ElectricWireAutoConnectPreview
    {
        private static readonly IReadOnlyList<Vector3Int> EmptyTargets = new List<Vector3Int>();

        private readonly BlockGameObjectDataStore _blockDataStore;
        private readonly AutoConnectWirePreviewRenderer _renderer;

        // セル単位の幾何キャッシュ。向きかブロックが変わったら全破棄する
        // Per-cell geometry cache, fully invalidated when direction or block changes
        private readonly Dictionary<Vector3Int, List<(Vector3Int TargetPos, float Distance)>> _cellGeometryCache = new();
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

            // セル順に仮想在庫を減算しながら評価し、サーバーの逐次設置と同じ消費結果を予測する
            // Evaluate cells in order while decrementing a virtual inventory, predicting the server's sequential consumption
            // 注意: ドラッグ中の未設置電柱同士の接続は評価に現れない近似（サーバーが設置順に個別再検証するため安全側）
            // Note: connections between not-yet-placed poles in a drag are approximated away (the server re-validates each in placement order, so this stays safe)
            var virtualCounts = BuildVirtualCounts();
            var placingItemId = MasterHolder.BlockMaster.GetItemId(blockId);
            var totalCost = 0;
            var anyPlaceable = false;
            PlaceInfo cursorInfo = null;
            foreach (var placeInfo in placeInfos)
            {
                var targets = GetOrCollectCellGeometry(placeInfo.Position);
                var wirePlaceable = TrySelectWire(targets, virtualCounts, placingItemId, out var wireItemId, out var cellCost);
                if (!wirePlaceable) placeInfo.Placeable = false;

                if (placeInfo.Placeable)
                {
                    ConsumeVirtual(virtualCounts, placingItemId, wireItemId, cellCost);
                    totalCost += cellCost;
                    anyPlaceable = true;
                }
                if (placeInfo.Position == cursorCell) cursorInfo = placeInfo;
            }

            // ワイヤー線はカーソルセル分のみ描画し（全セル分は過剰）、ラベルは全セル合計を表示する
            // Draw wires only for the cursor cell (all cells would be excessive); the label shows the drag-wide total
            cursorInfo ??= placeInfos[^1];
            var cursorTargets = cursorInfo.Placeable ? ResolveTargetPositions(cursorInfo.Position) : EmptyTargets;
            _renderer.Show(cursorInfo.Position, cursorTargets, totalCost);

            // 設置可能なセルが1つでも残っていればクリック許可（不可セルはサーバーが個別に拒否する既存方針に揃える）
            // Allow the click when any cell remains placeable (bad cells are rejected per-cell by the server, matching existing policy)
            return anyPlaceable;

            #region Internal

            void InvalidateCacheOnKeyChange()
            {
                if (_hasCacheKey && _cachedDirection == direction && _cachedBlockId == blockId) return;
                _cellGeometryCache.Clear();
                _cachedDirection = direction;
                _cachedBlockId = blockId;
                _hasCacheKey = true;
            }

            Dictionary<ItemId, int> BuildVirtualCounts()
            {
                // 所持アイテムをID別に合算する
                // Sum held items per item id
                var counts = new Dictionary<ItemId, int>();
                foreach (var itemStack in inventory)
                {
                    if (itemStack.Count <= 0) continue;
                    counts[itemStack.Id] = counts.GetValueOrDefault(itemStack.Id) + itemStack.Count;
                }

                return counts;
            }

            List<(Vector3Int TargetPos, float Distance)> GetOrCollectCellGeometry(Vector3Int position)
            {
                if (_cellGeometryCache.TryGetValue(position, out var cached)) return cached;

                var targets = ClientElectricWireAutoConnectCollector.Collect(blockId, position, direction, _blockDataStore);
                _cellGeometryCache[position] = targets;
                return targets;
            }

            IReadOnlyList<Vector3Int> ResolveTargetPositions(Vector3Int position)
            {
                var targets = GetOrCollectCellGeometry(position);
                var positions = new List<Vector3Int>(targets.Count);
                foreach (var target in targets) positions.Add(target.TargetPos);
                return positions;
            }

            #endregion
        }

        public void Hide()
        {
            _renderer.Hide();
            _cellGeometryCache.Clear();
            _hasCacheKey = false;
        }

        // 全ターゲットを賄える電線アイテムをマスタ設定順に仮想在庫から選ぶ（サーバーと同じ選定規則）
        // Pick the wire item covering all targets in master order against the virtual inventory (same rule as the server)
        private static bool TrySelectWire(List<(Vector3Int TargetPos, float Distance)> targets, Dictionary<ItemId, int> virtualCounts, ItemId placingItemId, out ItemId wireItemId, out int totalCost)
        {
            wireItemId = ItemMaster.EmptyItemId;
            totalCost = 0;

            // 設置ブロック自身の1個が仮想在庫に無ければ設置不可
            // The cell is unplaceable when the virtual inventory lacks the block item itself
            if (virtualCounts.GetValueOrDefault(placingItemId) < 1) return false;

            // 接続先なし・電線未設定マスタは自動接続なしで設置可
            // No targets or no configured wire items allows placement without auto-connect
            if (targets.Count == 0) return true;
            var wireItems = MasterHolder.BlockMaster.Blocks.ElectricWireItems;
            if (wireItems.Length == 0) return true;

            foreach (var wireItem in wireItems)
            {
                var candidateItemId = MasterHolder.ItemMaster.GetItemId(wireItem.ItemGuid);
                if (!TrySumCost(candidateItemId, out var cost)) continue;

                // 設置ブロックと電線が同一アイテムなら設置分の1個を上乗せして判定する
                // Require one extra when the placed block shares the wire item
                var required = cost + (candidateItemId == placingItemId ? 1 : 0);
                if (virtualCounts.GetValueOrDefault(candidateItemId) < required) continue;

                wireItemId = candidateItemId;
                totalCost = cost;
                return true;
            }

            return false;

            #region Internal

            bool TrySumCost(ItemId candidateItemId, out int cost)
            {
                cost = 0;
                foreach (var target in targets)
                {
                    if (!ElectricWirePlacementEvaluator.TryCalculateWireCost(candidateItemId, target.Distance, out var targetCost)) return false;
                    cost += targetCost.Count;
                }

                return true;
            }

            #endregion
        }

        // 設置ブロック1個と選択電線の消費を仮想在庫へ反映する
        // Apply the placed block and selected wire consumption to the virtual inventory
        private static void ConsumeVirtual(Dictionary<ItemId, int> virtualCounts, ItemId placingItemId, ItemId wireItemId, int wireCost)
        {
            virtualCounts[placingItemId] = virtualCounts.GetValueOrDefault(placingItemId) - 1;
            if (wireItemId == ItemMaster.EmptyItemId || wireCost <= 0) return;
            virtualCounts[wireItemId] = virtualCounts.GetValueOrDefault(wireItemId) - wireCost;
        }
    }
}
