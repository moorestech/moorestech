using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockInfoPolling;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.Tooltip;
using Server.Protocol.PacketResponse;
using UniRx;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.Game.InGame.CleanRoom
{
    /// <summary>
    ///     壁・ドア・ハッチへの照準を追い、部屋の要約ツールチップと漏れマーカーを出す
    ///     Follows the aim on walls, doors and hatches, showing the room summary tooltip and leak markers
    /// </summary>
    public class CleanRoomBoundaryHoverService
    {
        private readonly CleanRoomTooltipPresenter _tooltipPresenter;
        private readonly CleanRoomLeakMarkerVisualizer _markers = new();
        private readonly BlockInfoPoller<GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack, CleanRoomInfoView> _poller = CleanRoomInfoPollSource.CreatePoller();
        private BlockGameObject _trackingBlock;
        private TooltipLine[] _currentLines = Array.Empty<TooltipLine>();

        public CleanRoomBoundaryHoverService(IMouseCursorTooltip tooltip)
        {
            _tooltipPresenter = new CleanRoomTooltipPresenter(tooltip);

            // 取得済み・消失に変わったときだけ、行と印を作り直す
            // Rebuild the lines and markers only when the poll becomes fetched or vanished
            _poller.OnBlockInfoSnapshotUpdated.Subscribe(_ => OnRoomInfoUpdated());
            // 他の表示が消えたときだけ再提示する
            // Present again only after another presentation disappears
            tooltip.OnPresentationChanged.Subscribe(presentation =>
            {
                if (!presentation.Visible && 0 < _currentLines.Length) _tooltipPresenter.Present(_currentLines);
            });

            #region Internal

            void OnRoomInfoUpdated()
            {
                // 消失なら表示を消す。消失の警告はポーラーが出している
                // On vanishing, clear the display; the poller already logged the warning
                if (!_poller.TryGetSnapshot(out var view))
                {
                    ClearDisplay();
                    return;
                }

                _currentLines = CleanRoomTooltipLines.Build(view);
                _tooltipPresenter.Present(_currentLines);

                // 隙間があるときだけ、そのセルに印を出す
                // Mark the gap cells only when openings exist
                if (view is CleanRoomNotEnclosedView { Leak: CleanRoomOpeningsLeakView openings }) _markers.Show(openings.OpeningCells);
                else _markers.HideAll();
            }

            #endregion
        }

        public void ManualUpdate()
        {
            // 照準中の境界ブロックが変わったときだけ照会を張り直す
            // Restart the query only when the aimed boundary block changes
            var hovered = TryGetHoveredBoundary(out var block) ? block : null;
            if (!ReferenceEquals(hovered, _trackingBlock))
            {
                StopTracking();
                if (hovered == null) return;
                _trackingBlock = hovered;
                _poller.Start(hovered.BlockInstanceId);
            }

            #region Internal

            static bool TryGetHoveredBoundary(out BlockGameObject block)
            {
                if (!BlockClickDetectUtil.TryGetCursorOnBlock(out block)) return false;
                var blockType = block.BlockMasterElement.BlockType;
                return blockType == BlockTypeConst.CleanRoomWall || blockType == BlockTypeConst.CleanRoomDoor ||
                       blockType == BlockTypeConst.CleanRoomItemHatch || blockType == BlockTypeConst.CleanRoomPipeHatch;
            }

            #endregion
        }

        public void Disable()
        {
            StopTracking();
        }

        private void StopTracking()
        {
            _poller.Stop();
            _trackingBlock = null;
            ClearDisplay();
        }

        private void ClearDisplay()
        {
            _currentLines = Array.Empty<TooltipLine>();
            _tooltipPresenter.Hide();
            _markers.HideAll();
        }


    }
}
