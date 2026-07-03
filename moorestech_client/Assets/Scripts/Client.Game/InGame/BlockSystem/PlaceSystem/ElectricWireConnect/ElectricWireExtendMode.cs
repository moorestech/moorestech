using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Control;
using Client.Input;
using Core.Master;
using Game.Block.Interface;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using UnityEngine;

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
        /// 起点選択済みの1フレーム更新。setSourceは延長送信時に起点をクリアし、成功応答で新電柱へ差し替える
        /// One-frame update with an origin; setSource clears the origin on extend send and swaps in the new pole on success
        /// </summary>
        public void Update(PlaceSystemUpdateContext ctx, BlockGameObject source, Action<BlockGameObject> setSource)
        {
            // 起点の接続上限と最大長を解決
            // Resolve the origin's connection limit and max wire length (do nothing when it is not electric)
            if (!ElectricWireExtendPreviewCalculator.TryResolveWireParam(source, out var sourceMaxCount, out var sourceMaxLength)) return;

            var wireItemId = ctx.HoldingItemId;
            var fromPos = source.BlockPosInfo.OriginalPos;
            var sourceFull = ElectricWireExtendPreviewCalculator.IsConnectionFull(source, sourceMaxCount);

            // 接続先ブロックがカーソル下にあり、起点と異なる電気系なら接続モード
            // Connect mode when a different electric block is under the cursor
            if (BlockClickDetectUtil.TryGetCursorOnBlock(out var target) &&
                target.BlockInstanceId != source.BlockInstanceId &&
                ElectricWireExtendPreviewCalculator.TryResolveWireParam(target, out var targetMaxCount, out var targetMaxLength))
            {
                ConnectToTarget(target, targetMaxCount, targetMaxLength);
                return;
            }

            // それ以外は空きスペースへの電柱設置＋延長モード
            // Otherwise, pole-placement-into-empty-space extension mode
            ExtendToEmptySpace();

            #region Internal

            void ConnectToTarget(BlockGameObject targetBlock, int targetMaxConnectionCount, float targetMaxWireLength)
            {
                _context.PreviewBlockController.SetActive(false);

                // 受信済みワイヤー状態から既接続・接続上限をクライアント側で判定する
                // Judge already-connected and connection-full states from received wire state on the client
                var toPos = targetBlock.BlockPosInfo.OriginalPos;
                var distance = Vector3Int.Distance(fromPos, toPos);
                var alreadyConnected = ElectricWireExtendPreviewCalculator.IsAlreadyConnected(source, targetBlock);
                var anyConnectionFull = sourceFull || ElectricWireExtendPreviewCalculator.IsConnectionFull(targetBlock, targetMaxConnectionCount);
                var judgement = ElectricWireExtendPreviewCalculator.Evaluate(sourceMaxLength, targetMaxWireLength, distance, alreadyConnected, anyConnectionFull, wireItemId, ItemMaster.EmptyItemId, _context.Inventory);

                _context.WirePreview.Show(fromPos, toPos, judgement.IsPlaceable, ResolveCostCount(judgement, distance));

                // 可否OK かつクリックで接続する。起点は維持し連続接続できる
                // Connect on click when placeable; keep the origin for continuous connection
                if (InputManager.Playable.ScreenLeftClick.GetKeyDown && judgement.IsPlaceable)
                {
                    ElectricWireExtendRequestSender.Connect(fromPos, toPos, wireItemId);
                }
            }

            void ExtendToEmptySpace()
            {
                // 延長用電柱アイテムをインベントリから自動選択する
                // Auto-select the pole item for extension from the inventory
                if (!ElectricWireExtendRequestSender.TryFindPoleSlot(_context.Inventory, out var poleSlot, out var poleMaster, out var poleItemId))
                {
                    HidePreview();
                    return;
                }

                // 電柱の設置座標を地面レイキャストから求める
                // Compute the pole placement position from a ground raycast
                if (!PlaceSystemUtil.TryGetRayHitBlockPosition(_context.MainCamera, 0, BlockDirection.North, poleMaster, out var placePoint, out _))
                {
                    HidePreview();
                    return;
                }

                if (!ElectricWireExtendPreviewCalculator.TryResolveWireParam(poleMaster, out _, out var poleMaxLength))
                {
                    HidePreview();
                    return;
                }

                // 設置予定電柱のPlaceInfoを通常設置と同じ計算で生成する
                // Build the pole PlaceInfo using the same calculation as normal placement
                var placeInfos = _pointCalculator.CalculatePoint(placePoint, placePoint, true, BlockDirection.North, poleMaster);
                var placeInfo = placeInfos[0];

                // 地面接触でブロック設置可否を確定し、ワイヤー可否と合算する
                // Finalize block placeability via ground contact, then combine with wire placeability
                var groundOverlaps = _context.PreviewBlockController.SetPreviewAndGroundDetect(placeInfos, poleMaster);
                if (groundOverlaps[0]) placeInfo.Placeable = false;

                // 新設電柱は未接続のため既接続はfalse、上限は起点側のみ判定する
                // A new pole has no connections, so alreadyConnected is false and only the origin's limit applies
                var distance = Vector3Int.Distance(fromPos, placeInfo.Position);
                var judgement = ElectricWireExtendPreviewCalculator.Evaluate(sourceMaxLength, poleMaxLength, distance, false, sourceFull, wireItemId, poleItemId, _context.Inventory);
                var placeable = placeInfo.Placeable && judgement.IsPlaceable;

                // ゴーストとワイヤー線を可否色で表示する
                // Show the ghost and wire line colored by placeability
                placeInfo.Placeable = placeable;
                _context.PreviewBlockController.UpdatePlaceableColors(placeInfos);
                _context.WirePreview.Show(fromPos, placeInfo.Position, placeable, ResolveCostCount(judgement, distance));

                // 可否OK かつクリックで延長設置する。応答待ち中の二重発火を防ぐため送信前に起点を同期的にクリアする
                // Extend on click when placeable; clear the origin synchronously before sending to prevent double-fire while awaiting
                if (InputManager.Playable.ScreenLeftClick.GetKeyDown && placeable)
                {
                    _context.WirePreview.SetActive(false);
                    _context.PreviewBlockController.SetActive(false);
                    setSource(null);
                    ElectricWireExtendRequestSender.Extend(fromPos, poleSlot, placeInfo, wireItemId, _context.BlockDataStore, setSource);
                }
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
