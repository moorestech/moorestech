using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.ConnectToolsModule;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect
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
            var virtualInventory = new ElectricWireAutoConnectVirtualInventory(inventory, blockMaster.RequiredItems);
            var totalCost = 0;
            var anyPlaceable = false;
            PlaceInfo cursorInfo = null;
            foreach (var placeInfo in placeInfos)
            {
                var targets = GetOrCollectCellGeometry(placeInfo.Position);
                var wirePlaceable = TrySelectConnectTool(targets, out var cellMaterials, out var cellCost);
                if (!wirePlaceable) placeInfo.Placeable = false;

                if (placeInfo.Placeable)
                {
                    virtualInventory.ConsumePlacedCell(cellMaterials);
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

            // 全ターゲットを賄えるelectricWire connectToolをSortPriority順に仮想在庫から選ぶ（サーバーと同じ選定規則）
            // Pick the electricWire connectTool covering all targets in SortPriority order against the virtual inventory (same rule as the server)
            bool TrySelectConnectTool(List<(Vector3Int TargetPos, float Distance)> targets, out IReadOnlyList<ConnectToolMaterialCost> selectedMaterials, out int selectedCost)
            {
                selectedMaterials = null;
                selectedCost = 0;

                // 接続先なし・electricWire未設定マスタは自動接続なしで設置可
                // No targets or no configured electricWire connectTool allows placement without auto-connect
                if (targets.Count == 0) return true;
                var electricWireTools = new List<ConnectToolMasterElement>();
                foreach (var element in MasterHolder.ConnectToolMaster.All)
                    if (element.ToolType == ConnectToolMasterElement.ToolTypeConst.electricWire) electricWireTools.Add(element);
                if (electricWireTools.Count == 0) return true;
                electricWireTools.Sort((a, b) => a.SortPriority.CompareTo(b.SortPriority));

                foreach (var element in electricWireTools)
                {
                    if (!TrySumCost(element.ConnectToolGuid, out var materials, out var cost)) continue;
                    if (!virtualInventory.CanAfford(materials)) continue;

                    selectedMaterials = materials;
                    selectedCost = cost;
                    return true;
                }

                return false;

                bool TrySumCost(System.Guid connectToolGuid, out IReadOnlyList<ConnectToolMaterialCost> materials, out int cost)
                {
                    cost = 0;
                    var accumulator = new Dictionary<ItemId, int>();
                    foreach (var target in targets)
                    {
                        if (!ElectricWirePlacementEvaluator.TryCalculateWireCost(connectToolGuid, target.Distance, out var targetCost))
                        {
                            materials = null;
                            return false;
                        }
                        cost += targetCost.TotalCount;
                        foreach (var material in targetCost.Materials)
                        {
                            accumulator.TryGetValue(material.ItemId, out var current);
                            accumulator[material.ItemId] = current + material.Count;
                        }
                    }

                    var list = new List<ConnectToolMaterialCost>(accumulator.Count);
                    foreach (var (itemId, count) in accumulator) list.Add(new ConnectToolMaterialCost(itemId, count));
                    materials = list;
                    return true;
                }
            }

            #endregion
        }

        public void Hide()
        {
            _renderer.Hide();
            _cellGeometryCache.Clear();
            _hasCacheKey = false;
        }
    }
}
