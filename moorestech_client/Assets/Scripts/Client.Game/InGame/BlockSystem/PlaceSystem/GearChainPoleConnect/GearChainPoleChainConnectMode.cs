using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// チェーンアイテム手持ち時のモード。既存の設置済みポール同士の接続のみを行い、ポールの新規設置はしない。
    /// Mode while holding a chain item: only connects existing placed poles and never places new poles.
    /// </summary>
    public class GearChainPoleChainConnectMode
    {
        private readonly GearChainPoleConnectModeContext _modeContext;

        public GearChainPoleChainConnectMode(GearChainPoleConnectModeContext modeContext)
        {
            _modeContext = modeContext;
        }

        public void ManualUpdate(PlaceSystemUpdateContext updateContext, IGearChainPoleConnectAreaCollider hitPole)
        {
            _modeContext.PreviewObject.HideGhost();

            // チェーン手持ちでは設置不可のため、空きでは起点からの赤い線のみ表示する
            // Holding a chain cannot place poles, so show only a red line from the source over empty space
            if (hitPole == null)
            {
                ShowUnplaceableLineToCursor();
                return;
            }

            // 起点未選択ならクリックで起点を選択する
            // Select the source pole by click when none is selected
            if (_modeContext.ConnectFromPole == null)
            {
                _modeContext.PreviewObject.HideLine();
                if (_modeContext.IsScreenClicked()) _modeContext.SelectSourcePole(hitPole);
                return;
            }

            var fromPos = _modeContext.ConnectFromPole.GetBlockPosition();
            var toPos = hitPole.GetBlockPosition();
            if (fromPos == toPos)
            {
                _modeContext.PreviewObject.HideLine();
                return;
            }

            // 起点↔対象ポールの接続プレビューを表示する
            // Preview the connection between the source and target poles
            var previewData = GearChainPoleExtendPreviewCalculator.CalculatePoleToPole(fromPos, toPos, _modeContext.BlockGameObjectDataStore, _modeContext.PlayerInventory, updateContext.HoldingItemId);
            if (!previewData.IsValid)
            {
                // 起点情報が解決できない場合はクリックで起点を選び直せるようにする（消失ポール対策）
                // Allow re-selecting the source by click when it cannot be resolved (handles removed poles)
                _modeContext.PreviewObject.HideLine();
                if (_modeContext.IsScreenClicked()) _modeContext.SelectSourcePole(hitPole);
                return;
            }
            _modeContext.PreviewObject.ShowLine(previewData.StartPoint, previewData.EndPoint, previewData.IsPlaceable);

            if (!previewData.IsPlaceable || !_modeContext.IsScreenClicked()) return;

            // 接続プロトコルを送信して起点をリセットする
            // Send the connect protocol and reset the source
            _modeContext.PreviewObject.HideLine();
            ClientContext.VanillaApi.SendOnly.ConnectGearChain(fromPos, toPos, updateContext.HoldingItemId);
            _modeContext.SetConnectFromPole(null);
            _modeContext.RequestSender.Invalidate();

            #region Internal

            void ShowUnplaceableLineToCursor()
            {
                // 起点未選択またはレイ非命中時は線も表示しない
                // Hide the line when no source is selected or the ray hits nothing
                if (_modeContext.ConnectFromPole == null || !PlaceSystemUtil.TryGetRayHitPosition(_modeContext.MainCamera, out var hitPosition, out _))
                {
                    _modeContext.PreviewObject.HideLine();
                    return;
                }

                _modeContext.PreviewObject.ShowLine(GearChainPoleExtendPreviewCalculator.GetPoleCenter(_modeContext.ConnectFromPole.GetBlockPosition()), hitPosition, false);
            }

            #endregion
        }
    }
}
