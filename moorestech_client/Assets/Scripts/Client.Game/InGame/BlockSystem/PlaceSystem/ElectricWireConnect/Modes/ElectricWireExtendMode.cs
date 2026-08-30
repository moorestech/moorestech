using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using Client.Game.InGame.Control;
using Client.Input;
using Game.Block.Interface;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Modes
{
    /// <summary>
    /// 起点選択済み時の接続・電柱延長設置を処理する挙動
    /// Behavior when an origin is selected: connect, or place a pole and extend
    /// </summary>
    public class ElectricWireExtendMode
    {
        private readonly ElectricWireToolContext _context;

        public ElectricWireExtendMode(ElectricWireToolContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 起点選択済みの1フレーム更新。送信はsender経由で行い、起点の引き継ぎは応答確認後にシステム側が行う
        /// One-frame update with an origin; sending goes through the sender, and origin hand-off happens after the response
        /// </summary>
        public void Update(PlaceSystemUpdateContext ctx, BlockGameObject source)
        {
            var feedback = ctx.Feedback;

            // 起点の接続上限を解決（非電気系なら何もしない）
            // Resolve the origin's connection limit (do nothing when it is not electric)
            if (!ElectricWireExtendPreviewCalculator.TryResolveWireParam(source, out var sourceMaxCount, out _, out _)) return;

            // 選択中の電線connectToolのGuidを使う（未選択時はEmpty）
            // Use the selected wire connectTool's Guid (Empty when nothing is selected)
            var connectToolGuid = ctx.Target is ConnectToolPlacementTarget connectTool ? connectTool.ConnectToolGuid : System.Guid.Empty;
            var fromPos = source.BlockPosInfo.OriginalPos;

            // 接続先ブロックがカーソル下にあり、起点と異なる電気系なら接続モード
            // Connect mode when a different electric block is under the cursor
            if (BlockClickDetectUtil.TryGetCursorOnBlock(out var target) &&
                target.BlockInstanceId != source.BlockInstanceId &&
                ElectricWireExtendPreviewCalculator.TryResolveWireParam(target, out var targetMaxCount, out _, out _))
            {
                ConnectToTarget(target, targetMaxCount);
                return;
            }

            // それ以外は空きスペースへの電柱設置＋延長モード
            // Otherwise, pole-placement-into-empty-space extension mode
            ExtendToEmptySpace();

            #region Internal

            void ConnectToTarget(BlockGameObject targetBlock, int targetMaxConnectionCount)
            {
                _context.PreviewBlockController.SetActive(false);

                // 既接続・接続上限の判定はCalculator内部に委ねる
                // Already-connected and connection-full judgements are delegated to the calculator
                var toPos = targetBlock.BlockPosInfo.OriginalPos;
                var distance = Vector3Int.Distance(fromPos, toPos);
                var preview = ElectricWireExtendPreviewCalculator.Evaluate(source, targetBlock, sourceMaxCount, targetMaxConnectionCount, distance, connectToolGuid, _context.Inventory);

                _context.WirePreview.Show(ElectricWireEndpointResolver.Resolve(source), ElectricWireEndpointResolver.Resolve(targetBlock), preview.IsPlaceable);

                // 不可理由と電線消費数を積む
                // Pushes the failure reason and wire cost
                ElectricWirePlacementFailureTooltipKey.Report(preview, feedback);

                // 可否OK かつクリックで接続する。起点は応答確認後に接続先へ移る
                // The origin moves to the target after the response confirms
                if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi() && preview.IsPlaceable && !_context.RequestSender.IsAwaitingResponse) _context.RequestSender.SendConnect(fromPos, toPos, connectToolGuid);
            }

            void ExtendToEmptySpace()
            {
                if (!_context.PoleGhostPart.TryEvaluateGhost(_context.PoleSelection, feedback, out var evaluation))
                {
                    HidePreview();
                    return;
                }

                // 新設電柱側の判定はCalculator内部に委ねる
                // 電柱の建設コストは同一フレームで先に消費されるため、予約としてワイヤー判定と不足算出へ渡す（サーバーのHasEnoughWireMaterialsと同じ合算）
                // The pole's construction cost is consumed first in the same frame, so it is reserved for the wire judgement and shortage calculation (the same sum the server's HasEnoughWireMaterials makes)
                // 新設電柱の仮AABBを構築して範囲相互判定込みで評価する
                // Build the new pole's ghost AABB and evaluate including the mutual range check
                var poleGhostInfo = new BlockPositionInfo(evaluation.PlaceInfo.Position, _context.PoleSelection.CurrentDirection, evaluation.PoleMaster.BlockSize);
                var distance = Vector3Int.Distance(fromPos, evaluation.PlaceInfo.Position);
                var preview = ElectricWireExtendPreviewCalculator.EvaluateNewPole(source, sourceMaxCount, evaluation.PoleParam, poleGhostInfo, distance, connectToolGuid, _context.Inventory, evaluation.PoleConstructionItemCounts);
                var placeable = evaluation.IsGhostPlaceable && preview.IsPlaceable;

                // ゴーストとワイヤー線を可否色で表示する
                // Show the ghost and wire line colored by placeability
                evaluation.PlaceInfo.Placeable = placeable;
                _context.PreviewBlockController.UpdatePlaceableColors(evaluation.PlaceInfos);

                // 新設電柱ゴースト内のマーカー端点を実描画と同じ計算式で解決する。ゴースト未生成時のフォールバックはResolver側が担う
                // Resolve the new pole ghost's marker endpoint using the same calculation as the actual rendering; the ghost-unavailable fallback lives in the resolver
                _ = _context.PreviewBlockController.TryGetPreviewBlock(0, out var poleGhost);
                var endEndpoint = ElectricWireEndpointResolver.ResolveFromGhost(poleGhost, evaluation.PlaceInfo, evaluation.PoleMaster);

                // ゴーストの不可理由はTryEvaluateGhostが積み済みなので、続けてワイヤー判定の理由と消費電線数を積む
                // TryEvaluateGhost already pushed the ghost reasons, so push the wire judgement reason and cost next
                ElectricWirePlacementFailureTooltipKey.Report(preview, feedback);
                _context.WirePreview.Show(ElectricWireEndpointResolver.Resolve(source), endEndpoint, placeable);

                // 可否OK かつクリックで延長設置する。応答待ち中は多重送信を防ぐため送信しない
                // Extend on click when placeable; skip sending while a response is pending to avoid duplicate sends
                if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi() && placeable && !_context.RequestSender.IsAwaitingResponse)
                {
                    _context.WirePreview.SetActive(false);
                    _context.PreviewBlockController.SetActive(false);
                    _context.RequestSender.SendExtend(fromPos, evaluation.PoleBlockId, evaluation.PlaceInfo, connectToolGuid);
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
