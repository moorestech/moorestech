using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.Control;
using Client.Input;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Modes
{
    /// <summary>
    /// 起点未選択時の挙動（起点選択・孤立設置）
    /// Behavior with no origin: source selection and isolated placement
    /// </summary>
    public class ElectricWireEditMode
    {
        private readonly ElectricWireToolContext _context;

        public ElectricWireEditMode(ElectricWireToolContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 起点未選択の更新。選択した起点を返す
        /// Update with no origin; returns the selected origin block
        /// </summary>
        public BlockGameObject Update(PlacementFeedback feedback)
        {
            // 起点が無い状態では接続線プレビューは表示しない
            // No connection preview while there is no origin
            _context.WirePreview.SetActive(false);

            var isClicked = InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi();

            // 電気系ブロックにホバー中はゴーストを消し、クリックで起点として選択する（ExtendMode.ConnectToTargetと同じ規則）
            // While hovering an electric block, hide the ghost and select it as origin on click, matching ExtendMode.ConnectToTarget
            if (BlockClickDetectUtil.TryGetCursorOnBlock(out var block) &&
                ElectricWireExtendPreviewCalculator.TryResolveWireParam(block, out _, out _, out _))
            {
                HideGhost();
                return isClicked ? block : null;
            }

            // 何もない空間なら電柱の孤立設置ゴーストを表示し、クリックで設置する
            // Over empty space, show the isolated pole ghost and place it on click
            if (!_context.PoleGhostPart.TryEvaluateGhost(_context.PoleSelection, feedback, out var evaluation))
            {
                _context.PreviewBlockController.SetActive(false);
                return null;
            }

            var placeable = evaluation.IsGhostPlaceable;
            evaluation.PlaceInfo.Placeable = placeable;
            _context.PreviewBlockController.UpdatePlaceableColors(evaluation.PlaceInfos);

            // 送信直前にプレビューを消してから孤立設置を送る（ExtendModeの延長送信と同じ手順）
            // Hide the preview right before sending the isolated placement, mirroring ExtendMode's extend send
            if (isClicked && placeable && !_context.RequestSender.IsAwaitingResponse)
            {
                HideGhost();
                _context.RequestSender.SendIsolatedPlace(evaluation.PoleBlockId, evaluation.PlaceInfo);
            }

            return null;

            #region Internal

            void HideGhost()
            {
                _context.PreviewBlockController.SetActive(false);
            }

            #endregion
        }
    }
}
