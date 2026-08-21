using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using Client.Game.InGame.Control;
using Client.Input;
using Game.Block.Interface;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

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
                var judgement = ElectricWireExtendPreviewCalculator.Evaluate(source, targetBlock, sourceMaxCount, targetMaxConnectionCount, distance, connectToolGuid, _context.Inventory);

                _context.WirePreview.Show(ElectricWireEndpointResolver.Resolve(source), ElectricWireEndpointResolver.Resolve(targetBlock), judgement.IsPlaceable);

                // 不可理由と消費電線数をツールチップ行へ積む
                // Push the failure reason and the wire cost as tooltip lines
                if (!judgement.IsPlaceable) feedback.Add(new TooltipLine(ElectricWirePlacementFailureTooltipKey.ToKey(judgement.FailureReason)));
                feedback.AddWireCost(ResolveCostCount(judgement, distance));

                // 可否OK かつクリックで接続する。起点は応答確認後に接続先へ移る
                // The origin moves to the target after the response confirms
                if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi() && judgement.IsPlaceable && !_context.RequestSender.IsAwaitingResponse) _context.RequestSender.SendConnect(fromPos, toPos, connectToolGuid);
            }

            void ExtendToEmptySpace()
            {
                if (!_context.PoleGhostPart.TryEvaluateGhost(_context.PoleSelection, feedback, out var evaluation))
                {
                    HidePreview();
                    return;
                }

                // 新設電柱側の判定はCalculator内部に委ねる
                // 電柱は建設コスト充足を別途判定するためワイヤー判定へはポールアイテム所持前提を渡さない
                // Judgement for the newly placed pole is delegated to the calculator; pole affordability is judged separately
                // 新設電柱の仮AABBを構築して範囲相互判定込みで評価する
                // Build the new pole's ghost AABB and evaluate including the mutual range check
                var poleGhostInfo = new BlockPositionInfo(evaluation.PlaceInfo.Position, _context.PoleSelection.CurrentDirection, evaluation.PoleMaster.BlockSize);
                var distance = Vector3Int.Distance(fromPos, evaluation.PlaceInfo.Position);
                var judgement = ElectricWireExtendPreviewCalculator.EvaluateNewPole(source, sourceMaxCount, evaluation.PoleParam, poleGhostInfo, distance, connectToolGuid, _context.Inventory);
                var placeable = evaluation.IsGroundClear && evaluation.IsPositionFree && judgement.IsPlaceable && evaluation.CanAffordPole;

                // ゴーストとワイヤー線を可否色で表示する
                // Show the ghost and wire line colored by placeability
                evaluation.PlaceInfo.Placeable = placeable;
                _context.PreviewBlockController.UpdatePlaceableColors(evaluation.PlaceInfos);

                // 新設電柱ゴースト内のマーカー端点を実描画と同じ計算式で解決する。ゴースト未生成時のフォールバックはResolver側が担う
                // Resolve the new pole ghost's marker endpoint using the same calculation as the actual rendering; the ghost-unavailable fallback lives in the resolver
                _ = _context.PreviewBlockController.TryGetPreviewBlock(0, out var poleGhost);
                var endEndpoint = ElectricWireEndpointResolver.ResolveFromGhost(poleGhost, evaluation.PlaceInfo, evaluation.PoleMaster);

                // ゴーストの不可理由（地形・重複・素材）→ ワイヤー判定の理由 → 消費電線数 の順で積む
                // Push ghost block reasons (terrain/overlap/materials), then the wire judgement reason, then the wire cost
                evaluation.PushBlockReasons(feedback);
                if (!judgement.IsPlaceable) feedback.Add(new TooltipLine(ElectricWirePlacementFailureTooltipKey.ToKey(judgement.FailureReason)));
                feedback.AddWireCost(ResolveCostCount(judgement, distance));
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

            int ResolveCostCount(ElectricWirePlacementJudgement judgement, float distance)
            {
                // 成功時は判定結果のコストを、失敗時も距離から算出したコストを表示する
                // Show the judgement cost on success, or the distance-derived cost even on failure
                if (judgement.IsPlaceable) return judgement.WireCost.TotalCount;
                return ElectricWirePlacementEvaluator.TryCalculateWireCost(connectToolGuid, distance, out var cost) ? cost.TotalCount : 0;
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
