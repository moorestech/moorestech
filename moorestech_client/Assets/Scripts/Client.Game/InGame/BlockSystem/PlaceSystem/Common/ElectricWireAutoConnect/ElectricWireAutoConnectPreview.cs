using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
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

        public ElectricWireAutoConnectPreview(BlockGameObjectDataStore blockDataStore, IPlacementPreviewBlockGameObjectController previewBlockController, IGameUnlockStateData gameUnlockStateData)
        {
            _blockDataStore = blockDataStore;
            _previewBlockController = previewBlockController;
            _gameUnlockStateData = gameUnlockStateData;
            _renderer = new AutoConnectWirePreviewRenderer();
        }

        /// <summary>
        /// 電気系なら各セルの自動接続を評価してPlaceableを上書きし、表示を更新する。戻り値は設置クリック可否
        /// For electric blocks, evaluates auto-connect per cell, overrides Placeable and updates visuals. Returns click placeability
        /// </summary>
        public bool ApplyAutoConnect(List<PlaceInfo> placeInfos, BlockId blockId, BlockDirection direction, ILocalPlayerInventory inventory, Vector3Int cursorCell, PlacementFeedback feedback)
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
            var cursorIndex = -1;
            var cursorWirePlaceable = true;
            var cursorRawTargetCount = 0;
            for (var i = 0; i < placeInfos.Count; i++)
            {
                var placeInfo = placeInfos[i];
                var targets = GetOrCollectCellGeometry(placeInfo.Position);
                var wirePlaceable = ElectricWireAutoConnectToolSelector.TrySelect(targets, virtualInventory, _gameUnlockStateData, out var cellMaterials, out var cellCost);
                if (!wirePlaceable) placeInfo.Placeable = false;

                if (placeInfo.Placeable)
                {
                    virtualInventory.ConsumePlacedCell(cellMaterials);
                    totalCost += cellCost;
                    anyPlaceable = true;
                }
                // カーソルセルが確定するまでは毎セル上書きし、一致セルが無い末尾フォールバック時も通知が末尾セルの値になるようにする
                // Overwrite every cell until the cursor cell is fixed, so the last-cell fallback also carries that cell's values
                if (cursorIndex < 0 || placeInfo.Position == cursorCell)
                {
                    if (placeInfo.Position == cursorCell) cursorIndex = i;
                    cursorWirePlaceable = wirePlaceable;
                    // 地形干渉や建設コスト不足によるPlaceable=falseと無関係な、生の接続候補数
                    // Raw candidate count, independent of Placeable=false caused by ground/build-cost issues
                    cursorRawTargetCount = targets.Count;
                }
            }

            // ワイヤー線はカーソルセル分のみ描画し（全セル分は過剰）、コスト行は全セル合計を表示する
            // Draw wires only for the cursor cell (all cells would be excessive); the cost line shows the drag-wide total
            if (cursorIndex < 0) cursorIndex = placeInfos.Count - 1;
            var cursorInfo = placeInfos[cursorIndex];
            var originEndpoint = ResolveOriginEndpoint(cursorIndex, cursorInfo);
            var cursorTargets = cursorInfo.Placeable ? ResolveTargetEndpoints(cursorInfo.Position) : EmptyTargets;
            ShowCursorNotice();

            // 設置可能なセルが1つでも残っていればクリック許可（不可セルはサーバーが個別に拒否する既存方針に揃える）
            // Allow the click when any cell remains placeable (bad cells are rejected per-cell by the server, matching existing policy)
            return anyPlaceable;

            #region Internal

            // カーソルセルの状態に応じてワイヤー線を描き、理由・案内をツールチップ行として積む
            // Draws the wires for the cursor cell and pushes the reason / notice as tooltip lines
            void ShowCursorNotice()
            {
                // 電線不足は自動接続プレビューが唯一拒否する理由。不可色の線で「足りていればどこへ張られたか」を見せる
                // Insufficient wire is the only rejection reason here; the failure-colored wires show where they would have run
                if (!cursorWirePlaceable)
                {
                    _renderer.Show(originEndpoint, ResolveTargetEndpoints(cursorInfo.Position), true);
                    feedback.AddWireShortage();
                    return;
                }

                // 1件も配線されず、かつ範囲判定で落ちた近傍が実在するときだけ、設置許可のまま範囲外を案内する
                // Only when nothing gets wired and a neighbor actually failed the range check, keep placement allowed and report out-of-range
                if (cursorRawTargetCount == 0 && ClientElectricWireAutoConnectCollector.ExistsElectricNeighborOutOfConnectionRange(blockId, cursorInfo.Position, direction, _blockDataStore))
                {
                    _renderer.Show(originEndpoint, cursorTargets, false);
                    feedback.AddWireOutOfRangeNotice();
                    return;
                }

                _renderer.Show(originEndpoint, cursorTargets, false);
                feedback.AddWireCost(totalCost);
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
            Vector3 ResolveOriginEndpoint(int originIndex, PlaceInfo originInfo)
            {
                _previewBlockController.TryGetPreviewBlock(originIndex, out var ghost);
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
