using Client.Game.InGame.Block;
using Client.Game.InGame.BlockInfoPolling;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.Tooltip;
using Core.Master;
using Server.Protocol.PacketResponse;
using UniRx;

namespace Client.Game.InGame.CleanRoom
{
    /// <summary>
    ///     壁・ドア・ハッチの照準表示
    ///     Aim display for walls, doors and hatches
    /// </summary>
    public class CleanRoomBoundaryHoverService
    {
        private readonly CleanRoomTooltipPresenter _tooltipPresenter;
        private readonly CleanRoomLeakMarkerVisualizer _markers = new();
        private readonly BlockInfoPoller<GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack, CleanRoomInfoView> _poller = CleanRoomInfoPollSource.CreatePoller();
        private BlockGameObject _trackingBlock;

        public CleanRoomBoundaryHoverService(IMouseCursorTooltip tooltip)
        {
            _tooltipPresenter = new CleanRoomTooltipPresenter(tooltip);

            // 応答ごとに状態が決まったら、行と印を作り直す
            // Rebuild the lines and markers whenever a response settles the poll state
            _poller.OnBlockInfoSnapshotUpdated.Subscribe(_ => OnRoomInfoUpdated());

            #region Internal

            void OnRoomInfoUpdated()
            {
                // 読めた値が無い（消失・読めない応答・非対応）なら表示を消す。通信失敗の間は最後の値を出し続ける。警告はポーラーが出している
                // Without a value read (vanished, unreadable or unsupported), clear the display; a transport failure keeps the last value; the poller already logged the warning
                if (!_poller.TryGetSnapshot(out var view))
                {
                    ClearDisplay();
                    return;
                }

                // ハッチの操作案内など他の表示があっても、その下に併記する
                // List it under any other show such as a hatch's interact hint
                _tooltipPresenter.Present(CleanRoomTooltipLines.Build(view));

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
                // 部屋要約を出す境界ブロックはマスタの集合で決める
                // The master's set decides which boundary blocks show the room summary
                if (!BlockClickDetectUtil.TryGetCursorOnBlock(out block)) return false;
                return MasterHolder.CleanRoomMaster.IsRoomSummaryBoundaryBlock(block.BlockMasterElement.BlockGuid);
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
            _tooltipPresenter.Hide();
            _markers.HideAll();
        }
    }
}
