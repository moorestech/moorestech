using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Game.Block.Interface;
using Game.UnlockState;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect;

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
        private static readonly IReadOnlyList<Vector3> EmptyTargets = new List<Vector3>();

        private readonly BlockGameObjectDataStore _blockDataStore;
        private readonly IPlacementPreviewBlockGameObjectController _previewBlockController;
        private readonly IGameUnlockStateData _gameUnlockStateData;
        private readonly AutoConnectWirePreviewRenderer _renderer;

        // セル単位の幾何キャッシュ。向きかブロックが変わったら全破棄する
        // Per-cell geometry cache, fully invalidated when direction or block changes
        private readonly Dictionary<Vector3Int, List<(Vector3Int TargetPos, float Distance)>> _cellGeometryCache = new();
        private BlockDirection _cachedDirection;
        private BlockId _cachedBlockId;
        private bool _hasCacheKey;

        public ElectricWireAutoConnectPreview(Camera mainCamera, BlockGameObjectDataStore blockDataStore, IPlacementPreviewBlockGameObjectController previewBlockController, IGameUnlockStateData gameUnlockStateData)
        {
            _blockDataStore = blockDataStore;
            _previewBlockController = previewBlockController;
            _gameUnlockStateData = gameUnlockStateData;
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
            if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(blockMaster.BlockParam, out _, out _, out _))
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
            var cursorWirePlaceable = true;
            foreach (var placeInfo in placeInfos)
            {
                var targets = GetOrCollectCellGeometry(placeInfo.Position);
                var wirePlaceable = ElectricWireAutoConnectToolSelector.TrySelect(targets, virtualInventory, _gameUnlockStateData, out var cellMaterials, out var cellCost);
                if (!wirePlaceable) placeInfo.Placeable = false;

                if (placeInfo.Placeable)
                {
                    virtualInventory.ConsumePlacedCell(cellMaterials);
                    totalCost += cellCost;
                    anyPlaceable = true;
                }
                if (placeInfo.Position == cursorCell)
                {
                    cursorInfo = placeInfo;
                    cursorWirePlaceable = wirePlaceable;
                }
            }

            // ワイヤー線はカーソルセル分のみ描画し（全セル分は過剰）、ラベルは全セル合計を表示する
            // Draw wires only for the cursor cell (all cells would be excessive); the label shows the drag-wide total
            cursorInfo ??= placeInfos[^1];
            var originEndpoint = ResolveOriginEndpoint(cursorInfo);
            var cursorTargets = cursorInfo.Placeable ? ResolveTargetEndpoints(cursorInfo.Position) : EmptyTargets;
            ShowCursorNotice();

            // 設置可能なセルが1つでも残っていればクリック許可（不可セルはサーバーが個別に拒否する既存方針に揃える）
            // Allow the click when any cell remains placeable (bad cells are rejected per-cell by the server, matching existing policy)
            return anyPlaceable;

            #region Internal

            // カーソルセルの状態に応じてコスト表示・拒否理由・範囲外案内のいずれかを描画する
            // Renders the cost, the rejection reason, or an out-of-range notice depending on the cursor cell's state
            void ShowCursorNotice()
            {
                // 電線不足は自動接続プレビューが唯一拒否する理由であり、不可色で表示する
                // Insufficient wire is the only rejection reason for the auto-connect preview, shown in the failure color
                if (!cursorWirePlaceable)
                {
                    _renderer.Show(originEndpoint, cursorTargets, totalCost, ElectricWirePlacementFailureText.ToText(ElectricWirePlacementFailureReason.NoWireItem), true);
                    return;
                }

                // 範囲外に電気ブロックはあるが1件も配線されないときは、設置許可のまま情報表示する
                // When electric blocks exist out of range but none are connectable, keep placement allowed and show an info notice
                if (cursorTargets.Count == 0 && ClientElectricWireAutoConnectCollector.ExistsOutOfRangeElectricNeighbor(cursorInfo.Position, _blockDataStore, cursorTargets.Count))
                {
                    _renderer.Show(originEndpoint, cursorTargets, 0, "接続範囲外のため配線されません", false);
                    return;
                }

                _renderer.Show(originEndpoint, cursorTargets, totalCost, string.Empty, false);
            }

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

            // 接続先ブロックの端点を実描画と同じ計算式で解決する
            // Resolve each target block's endpoint using the same calculation as the actual rendering
            List<Vector3> ResolveTargetEndpoints(Vector3Int position)
            {
                var targets = GetOrCollectCellGeometry(position);
                var endpoints = new List<Vector3>(targets.Count);
                foreach (var target in targets)
                {
                    if (_blockDataStore.TryGetBlockGameObject(target.TargetPos, out var targetBlock))
                        endpoints.Add(ElectricWireEndpointResolver.Resolve(targetBlock));
                }
                return endpoints;
            }

            // 起点（設置予定ブロック自身）のゴースト端点を解決する。ゴースト未取得時のフォールバックはResolver内部に一本化されている
            // Resolve the origin (the block about to be placed) ghost endpoint; the ghost-unavailable fallback is centralized inside the resolver
            Vector3 ResolveOriginEndpoint(PlaceInfo originInfo)
            {
                var index = placeInfos.IndexOf(originInfo);
                _previewBlockController.TryGetPreviewBlock(index, out var ghost);
                return ElectricWireEndpointResolver.ResolveFromGhost(ghost, originInfo, blockMaster);
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
