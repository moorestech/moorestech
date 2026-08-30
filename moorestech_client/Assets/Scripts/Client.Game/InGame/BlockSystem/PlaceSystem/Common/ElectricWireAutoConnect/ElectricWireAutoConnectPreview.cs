using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Game.Block.Interface;
using Game.Construction;
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
        private readonly ConstructionWalletQuery _constructionWalletQuery;
        private readonly AutoConnectWirePreviewRenderer _renderer;

        // セル単位の幾何キャッシュ。向きかブロックが変わったら全破棄する
        // Per-cell geometry cache, fully invalidated when direction or block changes
        private readonly Dictionary<Vector3Int, List<(Vector3Int TargetPos, float Distance)>> _cellGeometryCache = new();
        private BlockDirection _cachedDirection;
        private BlockId _cachedBlockId;
        private bool _hasCacheKey;

        public ElectricWireAutoConnectPreview(BlockGameObjectDataStore blockDataStore, IPlacementPreviewBlockGameObjectController previewBlockController, IGameUnlockStateData gameUnlockStateData, ConstructionWalletQuery constructionWalletQuery)
        {
            _blockDataStore = blockDataStore;
            _previewBlockController = previewBlockController;
            _gameUnlockStateData = gameUnlockStateData;
            _constructionWalletQuery = constructionWalletQuery;
            _renderer = new AutoConnectWirePreviewRenderer();
        }

        /// <summary>
        /// 電気系なら各セルの自動接続を評価してPlaceableを上書きし、表示を更新する。戻り値は設置クリック可否
        /// For electric blocks, evaluates auto-connect per cell, overrides Placeable and updates visuals. Returns click placeability
        /// </summary>
        // cursorIndexは呼び出し側がPlacementCursorCellResolverで解決済み（このメソッド内では再解決しない）
        // cursorIndex is already resolved by the caller via PlacementCursorCellResolver (not re-resolved here)
        public bool ApplyAutoConnect(List<PlaceInfo> placeInfos, BlockId blockId, BlockDirection direction, ILocalPlayerInventory inventory, int cursorIndex, PlacementFeedback feedback)
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
            // 予約する建設コストは財布へ問い合わせる。サーバーがPlaceBlockProtocolで plan.ItemsToConsume を渡すのと同じ形
            // The construction reservation comes from the wallet, the same shape the server passes as plan.ItemsToConsume
            // 注意: 先頭セル基準の近似。ドラッグ途中で財布が尽きて再び支払う切り替わりは再現しない（安全側に倒れる）
            // Note: this approximates from the first cell; a mid-drag switch back to paying is not replayed (it errs on the safe side)
            var virtualInventory = new ElectricWireAutoConnectVirtualInventory(inventory, _constructionWalletQuery.GetItemsToConsume(blockId));
            var totalCost = 0;
            var anyPlaceable = false;
            var cursorWirePlaceable = true;
            var cursorRawTargetCount = 0;
            IReadOnlyList<ConstructionMaterialShortage> cursorWireShortages = Array.Empty<ConstructionMaterialShortage>();
            for (var i = 0; i < placeInfos.Count; i++)
            {
                var placeInfo = placeInfos[i];
                var targets = GetOrCollectCellGeometry(placeInfo.Position);
                var wirePlaceable = ElectricWireAutoConnectToolSelector.TrySelect(targets, virtualInventory, _gameUnlockStateData, out var cellMaterials, out var cellCost, out var cellShortages);
                if (!wirePlaceable) placeInfo.Placeable = false;

                if (placeInfo.Placeable)
                {
                    virtualInventory.ConsumePlacedCell(cellMaterials);
                    totalCost += cellCost;
                    anyPlaceable = true;
                }
                // カーソルセル添字のときだけ記録
                // Records the notice only for the cursor cell index
                if (i == cursorIndex)
                {
                    cursorWirePlaceable = wirePlaceable;
                    // 地形干渉や建設コスト不足によるPlaceable=falseと無関係な、生の接続候補数
                    // Raw candidate count, independent of Placeable=false caused by ground/build-cost issues
                    cursorRawTargetCount = targets.Count;

                    // 不足行は選定が仮想在庫（建設コスト予約込み）で算出したものをそのまま使い、判定と同じ基準で「所持/必要」を出す
                    // The shortage lines come straight from the selection's own virtual-inventory (reservation included) calculation so held/required matches the judgement
                    cursorWireShortages = cellShortages;
                }
            }

            // ワイヤー線はカーソルセル分のみ描画し（全セル分は過剰）、コスト行は全セル合計を表示する
            // Draw wires only for the cursor cell (all cells would be excessive); the cost line shows the drag-wide total
            var cursorInfo = placeInfos[cursorIndex];
            var originEndpoint = ResolveOriginEndpoint(cursorIndex, cursorInfo);
            var cursorTargets = cursorInfo.Placeable ? ResolveTargetEndpoints(cursorInfo.Position) : EmptyTargets;

            // 近傍走査は全ブロック走査で重いため、案内に必要なときだけ実行する
            // The neighbor scan walks every block, so run it only when the notice actually needs it
            var hasOutOfRangeNeighbor = AutoConnectNoticeLines.NeedsOutOfRangeProbe(cursorWirePlaceable, cursorRawTargetCount) &&
                                        ClientElectricWireAutoConnectCollector.ExistsElectricNeighborOutOfConnectionRange(blockId, cursorInfo.Position, direction, _blockDataStore);

            // どの案内行を積むかの判断は純関数へ委ね、線描画だけここに残す
            // The notice-line judgement lives in the pure helper; only the wire drawing stays here
            var isWireShortage = AutoConnectNoticeLines.Report(cursorWirePlaceable, cursorRawTargetCount, hasOutOfRangeNeighbor, totalCost, cursorWireShortages, feedback);

            // 電線不足時のみ「足りていればどこへ張られたか」を不可色の線で見せる
            // Only on wire shortage, failure-colored wires show where they would have run
            if (isWireShortage) _renderer.Show(originEndpoint, ResolveTargetEndpoints(cursorInfo.Position), true);
            else _renderer.Show(originEndpoint, cursorTargets, false);

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
