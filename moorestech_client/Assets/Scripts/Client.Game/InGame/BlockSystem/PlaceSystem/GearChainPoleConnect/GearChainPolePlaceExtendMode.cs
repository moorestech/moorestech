using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Core.Master;
using Game.Block.Interface;
using Game.PlayerInventory.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// ポールアイテム手持ち時のモード。手持ちポールの孤立設置と、起点からの連続延長設置を行う。
    /// 設置するポールは常に手持ちアイテムなので、複数種類のポールを所持していても曖昧にならない。
    /// Mode while holding a pole item: isolated placement and continuous extension of the holding pole.
    /// The placed pole is always the holding item, so owning multiple pole types is never ambiguous.
    /// </summary>
    public class GearChainPolePlaceExtendMode
    {
        // 通常ブロック設置と同等の設置可能距離
        // Placeable distance equivalent to common block placement
        private const float PlaceableMaxDistance = 100f;

        private readonly GearChainPoleConnectModeContext _modeContext;

        public GearChainPolePlaceExtendMode(GearChainPoleConnectModeContext modeContext)
        {
            _modeContext = modeContext;
        }

        public void ManualUpdate(PlaceSystemUpdateContext updateContext, IGearChainPoleConnectAreaCollider hitPole, BlockMasterElement poleBlockMaster)
        {
            // 既存ポールをクリックしたら延長の起点として選択する
            // Select the clicked existing pole as the extension source
            if (hitPole != null)
            {
                _modeContext.PreviewObject.Hide();
                if (_modeContext.IsScreenClicked()) _modeContext.SelectSourcePole(hitPole);
                return;
            }

            // ゴースト位置を算出し距離内か確認
            // Calculate ghost position and ensure it is within placeable distance
            if (!PlaceSystemUtil.TryGetRayHitBlockPosition(_modeContext.MainCamera, 0, BlockDirection.North, poleBlockMaster, out var placePos, out _) ||
                PlaceableMaxDistance < Vector3.Distance(_modeContext.MainCamera.transform.position, placePos))
            {
                _modeContext.PreviewObject.Hide();
                return;
            }

            var poleParam = (GearChainPoleBlockParam)poleBlockMaster.BlockParam;
            var poleSlot = PlayerInventoryConst.HotBarSlotToInventorySlot(updateContext.CurrentSelectHotbarSlotIndex);
            var placeInfo = new PlaceInfo { Position = placePos, Direction = BlockDirection.North, VerticalDirection = BlockVerticalDirection.Horizontal, Placeable = true };

            if (_modeContext.ConnectFromPole == null)
            {
                // 起点なし: 手持ちポールをその場に孤立設置する
                // No source: place the holding pole in isolation
                _modeContext.PreviewObject.HideLine();
                var placeable = _modeContext.PreviewObject.ShowGhost(placeInfo, poleBlockMaster, true);
                if (CanSendPlace(placeable)) SendExtendProtocol(null, ItemMaster.EmptyItemId);
            }
            else
            {
                // 起点あり: 手持ちポールを設置し自動選択したチェーンで起点と接続する
                // With a source: place the holding pole and connect with an auto-selected chain
                var chainItemId = GearChainPoleExtendPreviewCalculator.FindOwnedChainItemId(_modeContext.PlayerInventory);
                var fromPos = _modeContext.ConnectFromPole.GetBlockPosition();
                var previewData = GearChainPoleExtendPreviewCalculator.CalculateExtend(fromPos, placePos, poleParam, updateContext.HoldingItemId, _modeContext.BlockGameObjectDataStore, _modeContext.PlayerInventory, chainItemId);
                var placeable = _modeContext.PreviewObject.ShowGhost(placeInfo, poleBlockMaster, previewData.IsValid && previewData.IsPlaceable);
                _modeContext.PreviewObject.ShowLine(GearChainPoleExtendPreviewCalculator.GetPoleCenter(fromPos), GearChainPoleExtendPreviewCalculator.GetPoleCenter(placePos), placeable);
                if (CanSendPlace(placeable)) SendExtendProtocol(fromPos, chainItemId);
            }

            #region Internal

            void SendExtendProtocol(Vector3Int? fromPos, ItemId chainItemId)
            {
                _modeContext.PreviewObject.Hide();
                _modeContext.SetConnectFromPole(null);

                // 設置直後に接続上限へ達するポールは次の起点に引き継がず連続延長を終了する
                // End continuous extension when the placed pole reaches its connection limit immediately
                var usedConnectionCount = fromPos.HasValue ? 1 : 0;
                var canContinueExtension = usedConnectionCount < poleParam.MaxConnectionCount;
                _modeContext.RequestSender.Send(fromPos, poleSlot, placeInfo, chainItemId, placedPole => { if (canContinueExtension) _modeContext.SetConnectFromPole(placedPole); });
            }

            bool CanSendPlace(bool placeable)
            {
                // 応答待ち中は誤送信（孤立設置化）を防ぐため送信しない
                // Do not send while awaiting a response to avoid unintended isolated placement
                return placeable && !_modeContext.RequestSender.IsAwaitingResponse && _modeContext.IsScreenClicked();
            }

            #endregion
        }
    }
}
