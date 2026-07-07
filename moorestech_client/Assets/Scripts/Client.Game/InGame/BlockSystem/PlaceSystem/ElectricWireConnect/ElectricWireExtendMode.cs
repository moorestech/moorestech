using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Control;
using Client.Input;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.PlaceSystemModule;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect
{
    /// <summary>
    /// 起点選択済み時の接続・電柱延長設置を処理する挙動
    /// Behavior when an origin is selected: connect, or place a pole and extend
    /// </summary>
    public class ElectricWireExtendMode
    {
        private readonly ElectricWireToolContext _context;
        private readonly CommonBlockPlacePointCalculator _pointCalculator;

        public ElectricWireExtendMode(ElectricWireToolContext context)
        {
            _context = context;
            _pointCalculator = new CommonBlockPlacePointCalculator(context.BlockDataStore);
        }

        /// <summary>
        /// 起点選択済みの1フレーム更新。延長リクエストを送信したらtrueを返し、上位が起点をクリアする
        /// One-frame update with an origin; returns true when an extend request was sent so the owner clears the origin
        /// </summary>
        public bool Update(PlaceSystemUpdateContext ctx, BlockGameObject source, int toolEpoch)
        {
            // 起点の接続上限と最大長を解決
            // Resolve the origin's connection limit and max wire length (do nothing when it is not electric)
            if (!ElectricWireExtendPreviewCalculator.TryResolveWireParam(source, out var sourceMaxCount, out var sourceMaxLength)) return false;

            // 敷設に使う電線アイテムをマスタ定義順で自動選択する（手持ち非依存）
            // Auto-select the wire item in master definition order, independent of the held item
            var wireItemId = ElectricWireItemAutoSelector.FindOwnedWireItemId(_context.Inventory);
            var fromPos = source.BlockPosInfo.OriginalPos;

            // 接続先ブロックがカーソル下にあり、起点と異なる電気系なら接続モード
            // Connect mode when a different electric block is under the cursor
            if (BlockClickDetectUtil.TryGetCursorOnBlock(out var target) &&
                target.BlockInstanceId != source.BlockInstanceId &&
                ElectricWireExtendPreviewCalculator.TryResolveWireParam(target, out var targetMaxCount, out var targetMaxLength))
            {
                ConnectToTarget(target, targetMaxCount, targetMaxLength);
                return false;
            }

            // それ以外は空きスペースへの電柱設置＋延長モード
            // Otherwise, pole-placement-into-empty-space extension mode
            return ExtendToEmptySpace();

            #region Internal

            void ConnectToTarget(BlockGameObject targetBlock, int targetMaxConnectionCount, float targetMaxWireLength)
            {
                _context.PreviewBlockController.SetActive(false);

                // 既接続・接続上限の判定はCalculator内部に委ねる
                // Already-connected and connection-full judgements are delegated to the calculator
                var toPos = targetBlock.BlockPosInfo.OriginalPos;
                var distance = Vector3Int.Distance(fromPos, toPos);
                var judgement = ElectricWireExtendPreviewCalculator.Evaluate(source, targetBlock, sourceMaxCount, targetMaxConnectionCount, sourceMaxLength, targetMaxWireLength, distance, wireItemId, _context.Inventory);

                _context.WirePreview.Show(fromPos, toPos, judgement.IsPlaceable, ResolveCostCount(judgement, distance));

                // 可否OK かつクリックで接続する。起点は維持し連続接続できる
                // Connect on click when placeable; keep the origin for continuous connection
                if (InputManager.Playable.ScreenLeftClick.GetKeyDown && judgement.IsPlaceable)
                {
                    ElectricWireExtendRequestSender.Connect(fromPos, toPos, wireItemId);
                }
            }

            bool ExtendToEmptySpace()
            {
                // 延長用電柱ブロックを接続ツールマスタから解決する（選択駆動）
                // Resolve the extension pole block from the connect tool master (selection-driven)
                if (!ConnectToolMasterUtil.TryGetPlaceBlock(PlaceSystemMasterElement.PlaceModeConst.ElectricWireConnect, out var poleBlockId, out var poleMaster))
                {
                    HidePreview();
                    return false;
                }

                // 電柱の建設コストを賄えるかを所持素材から判定する
                // Judge from owned materials whether the pole's construction cost is affordable
                var canAffordPole = ConstructionCostPreviewCalculator.CalculateAffordableCellCount(poleMaster.RequiredItems, _context.Inventory) >= 1;

                // 電柱の設置座標を地面レイキャストから求める
                // Compute the pole placement position from a ground raycast
                if (!PlaceSystemUtil.TryGetRayHitBlockPosition(_context.MainCamera, 0, BlockDirection.North, poleMaster, out var placePoint, out _))
                {
                    HidePreview();
                    return false;
                }

                if (!ElectricWireExtendPreviewCalculator.TryResolveWireParam(poleMaster, out _, out var poleMaxLength))
                {
                    HidePreview();
                    return false;
                }

                // 通常設置と同じ計算でPlaceInfo生成
                // Build the pole PlaceInfo using the same calculation as normal placement
                var placeInfos = _pointCalculator.CalculatePoint(placePoint, placePoint, true, BlockDirection.North, poleMaster);
                var placeInfo = placeInfos[0];

                // 設置可否を確定しワイヤー可否と合算
                // Finalize block placeability via ground contact, then combine with wire placeability
                var groundOverlaps = _context.PreviewBlockController.SetPreviewAndGroundDetect(placeInfos, poleMaster);
                if (groundOverlaps[0]) placeInfo.Placeable = false;

                // 新設電柱側の判定はCalculator内部に委ねる
                // Judgement for the newly placed pole is delegated to the calculator
                // 電柱は建設コスト充足を別途判定するためワイヤー判定へはポールアイテム所持前提を渡さない
                // Pole affordability is judged separately, so the wire judgement receives no pole-item assumption
                var distance = Vector3Int.Distance(fromPos, placeInfo.Position);
                var judgement = ElectricWireExtendPreviewCalculator.EvaluateNewPole(source, sourceMaxCount, sourceMaxLength, poleMaxLength, distance, wireItemId, ItemMaster.EmptyItemId, _context.Inventory);
                var placeable = placeInfo.Placeable && judgement.IsPlaceable && canAffordPole;

                // ゴーストとワイヤー線を可否色で表示する
                // Show the ghost and wire line colored by placeability
                placeInfo.Placeable = placeable;
                _context.PreviewBlockController.UpdatePlaceableColors(placeInfos);
                _context.WirePreview.Show(fromPos, placeInfo.Position, placeable, ResolveCostCount(judgement, distance));

                // 可否OK かつクリックで延長設置する。trueを返して上位が起点をクリアし、二重発火を防ぐ
                // Extend on click when placeable; return true so the owner clears the origin, preventing double-fire
                if (InputManager.Playable.ScreenLeftClick.GetKeyDown && placeable)
                {
                    _context.WirePreview.SetActive(false);
                    _context.PreviewBlockController.SetActive(false);
                    ElectricWireExtendRequestSender.Extend(fromPos, poleBlockId, placeInfo, wireItemId, _context.BlockDataStore, toolEpoch);
                    return true;
                }

                return false;
            }

            int ResolveCostCount(ElectricWirePlacementJudgement judgement, float distance)
            {
                // 成功時は判定結果のコストを、失敗時も距離から算出したコストを表示する
                // Show the judgement cost on success, or the distance-derived cost even on failure
                if (judgement.IsPlaceable) return judgement.WireCost.Count;
                return ElectricWirePlacementEvaluator.TryCalculateWireCost(wireItemId, distance, out var cost) ? cost.Count : 0;
            }

            void HidePreview()
            {
                _context.PreviewBlockController.SetActive(false);
                _context.WirePreview.SetActive(false);
            }

            #endregion
        }
    }
}
