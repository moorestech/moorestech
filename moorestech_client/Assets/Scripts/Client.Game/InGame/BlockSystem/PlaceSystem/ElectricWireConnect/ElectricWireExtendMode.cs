using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Control;
using Client.Input;
using Core.Master;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect
{
    /// <summary>
    /// 起点選択済み時の挙動。別ブロックへの接続と、空きスペースへの電柱設置＋延長を処理する
    /// Behavior while an origin is selected: connect to another block, or place a pole into empty space and extend
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
        /// 起点選択済みの1フレーム更新。setSourceは延長成功時に新電柱を次の起点へ差し替える
        /// One-frame update while an origin is selected; setSource swaps the new pole in as the next origin on extend success
        /// </summary>
        public void Update(PlaceSystemUpdateContext ctx, BlockGameObject source, Action<BlockGameObject> setSource)
        {
            // 起点の最大ワイヤー長を解決する（電気系でなければ何もしない）
            // Resolve the origin's max wire length (do nothing when it is not electric)
            if (!ElectricWireExtendPreviewCalculator.TryResolveMaxWireLength(source, out var sourceMax)) return;

            var wireItemId = ctx.HoldingItemId;
            var fromPos = source.BlockPosInfo.OriginalPos;

            // 接続先ブロックがカーソル下にあり、起点と異なる電気系なら接続モード
            // Connect mode when a different electric block is under the cursor
            if (BlockClickDetectUtil.TryGetCursorOnBlock(out var target) &&
                target.BlockInstanceId != source.BlockInstanceId &&
                ElectricWireExtendPreviewCalculator.TryResolveMaxWireLength(target, out var targetMax))
            {
                ConnectToTarget(target, targetMax);
                return;
            }

            // それ以外は空きスペースへの電柱設置＋延長モード
            // Otherwise, pole-placement-into-empty-space extension mode
            ExtendToEmptySpace();

            #region Internal

            void ConnectToTarget(BlockGameObject targetBlock, float targetMaxWireLength)
            {
                _context.PreviewBlockController.SetActive(false);

                var toPos = targetBlock.BlockPosInfo.OriginalPos;
                var distance = Vector3Int.Distance(fromPos, toPos);
                var judgement = ElectricWireExtendPreviewCalculator.Evaluate(sourceMax, targetMaxWireLength, distance, wireItemId, ItemMaster.EmptyItemId, _context.Inventory);

                _context.WirePreview.Show(fromPos, toPos, judgement.IsPlaceable);

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

                if (!ElectricWireExtendPreviewCalculator.TryResolveMaxWireLength(poleMaster, out var poleMax))
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

                var distance = Vector3Int.Distance(fromPos, placeInfo.Position);
                var judgement = ElectricWireExtendPreviewCalculator.Evaluate(sourceMax, poleMax, distance, wireItemId, poleItemId, _context.Inventory);
                var placeable = placeInfo.Placeable && judgement.IsPlaceable;

                // ゴーストとワイヤー線を可否色で表示する
                // Show the ghost and wire line colored by placeability
                placeInfo.Placeable = placeable;
                _context.PreviewBlockController.UpdatePlaceableColors(placeInfos);
                _context.WirePreview.Show(fromPos, placeInfo.Position, placeable);

                // 可否OK かつクリックで延長設置し、成功後は新電柱を次の起点にする
                // Extend on click when placeable; after success the new pole becomes the next origin
                if (InputManager.Playable.ScreenLeftClick.GetKeyDown && placeable)
                {
                    _context.WirePreview.SetActive(false);
                    _context.PreviewBlockController.SetActive(false);
                    ElectricWireExtendRequestSender.Extend(fromPos, poleSlot, placeInfo, wireItemId, _context.BlockDataStore, setSource);
                }
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
