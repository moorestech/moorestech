using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;
using UnityEngine.EventSystems;
using static Client.Common.LayerConst;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// GearChainPoleの接続システム。レール同様にポール自動設置つきの連続延長に対応する
    /// GearChainPole connection system supporting rail-style continuous extension with auto pole placement
    /// </summary>
    public class GearChainPoleConnectSystem : IPlaceSystem
    {
        // 通常ブロック設置と同等の設置可能距離
        // Placeable distance equivalent to common block placement
        private const float PlaceableMaxDistance = 100f;

        private readonly Camera _mainCamera;
        private readonly ILocalPlayerInventory _playerInventory;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly GearChainPoleExtendPreviewObject _previewObject;
        private readonly GearChainPoleExtendRequestSender _requestSender;

        // 接続元のGearChainPole
        // Source GearChainPole for connection
        private IGearChainPoleConnectAreaCollider _connectFromPole;

        public GearChainPoleConnectSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, LocalPlayerInventoryController localPlayerInventory, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _mainCamera = mainCamera;
            _playerInventory = localPlayerInventory.LocalPlayerInventory;
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _previewObject = new GearChainPoleExtendPreviewObject(previewBlockController);
            _requestSender = new GearChainPoleExtendRequestSender(blockGameObjectDataStore);
        }

        public void Enable()
        {
            // 接続元の選択状態と進行中の応答をリセットする
            // Reset source selection state and pending responses
            _connectFromPole = null;
            _requestSender.Invalidate();
        }

        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            // ポール有無で既存操作と空き延長を分岐
            // Branch between existing pole operations and empty-space extension by cursor hit
            var hitPole = GetGearChainPoleCollider();
            if (hitPole != null) UpdatePoleTarget();
            else UpdateEmptySpace();

            #region Internal

            void UpdatePoleTarget()
            {
                _previewObject.HideGhost();

                // 状態①: 起点未選択ならクリックで起点を選択する
                // State 1: select the source pole by click when none is selected
                if (_connectFromPole == null)
                {
                    _previewObject.HideLine();
                    if (IsScreenClicked()) SelectSourcePole();
                    return;
                }

                var fromPos = _connectFromPole.GetBlockPosition();
                var toPos = hitPole.GetBlockPosition();
                if (fromPos == toPos)
                {
                    _previewObject.HideLine();
                    return;
                }

                // 状態③: 起点↔対象ポールの接続プレビューを表示する
                // State 3: preview the connection between the source and target poles
                var previewData = GearChainPoleExtendPreviewCalculator.CalculatePoleToPole(fromPos, toPos, _blockGameObjectDataStore, _playerInventory, context.HoldingItemId);
                if (!previewData.IsValid)
                {
                    // 起点情報が解決できない場合はクリックで起点を選び直せるようにする（消失ポール対策）
                    // Allow re-selecting the source by click when it cannot be resolved (handles removed poles)
                    _previewObject.HideLine();
                    if (IsScreenClicked()) SelectSourcePole();
                    return;
                }
                _previewObject.ShowLine(previewData.StartPoint, previewData.EndPoint, previewData.IsPlaceable);

                if (!previewData.IsPlaceable || !IsScreenClicked()) return;

                // 接続プロトコルを送信して起点をリセットする
                // Send the connect protocol and reset the source
                _previewObject.HideLine();
                ClientContext.VanillaApi.SendOnly.ConnectGearChain(fromPos, toPos, context.HoldingItemId);
                _connectFromPole = null;
                _requestSender.Invalidate();
            }

            void UpdateEmptySpace()
            {
                // インベントリからポールアイテムを自動選択する
                // Auto-select a pole item from inventory
                if (!GearChainPoleExtendPreviewCalculator.TryFindPoleItemSlot(_playerInventory, out var poleSlot, out var poleItemId, out var poleBlockMaster))
                {
                    _previewObject.Hide();
                    return;
                }

                // ゴースト位置を算出し距離内か確認
                // Calculate ghost position and ensure it is within placeable distance
                if (!PlaceSystemUtil.TryGetRayHitBlockPosition(_mainCamera, 0, BlockDirection.North, poleBlockMaster, out var placePos, out _) ||
                    PlaceableMaxDistance < Vector3.Distance(_mainCamera.transform.position, placePos))
                {
                    _previewObject.Hide();
                    return;
                }

                var placeInfo = new PlaceInfo { Position = placePos, Direction = BlockDirection.North, VerticalDirection = BlockVerticalDirection.Horizontal, Placeable = true };

                if (_connectFromPole == null)
                {
                    // 状態②: 孤立ポールをその場に設置する
                    // State 2: place an isolated pole at the position
                    _previewObject.HideLine();
                    var placeable = _previewObject.ShowGhost(placeInfo, poleBlockMaster, true);
                    if (CanSendPlace(placeable)) SendExtendProtocol(null, poleSlot, placeInfo);
                }
                else
                {
                    // 状態④: 新規ポールを設置し起点接続
                    // State 4: place a new pole and connect it with the source pole
                    var fromPos = _connectFromPole.GetBlockPosition();
                    var previewData = GearChainPoleExtendPreviewCalculator.CalculateExtend(fromPos, placePos, (GearChainPoleBlockParam)poleBlockMaster.BlockParam, poleItemId, _blockGameObjectDataStore, _playerInventory, context.HoldingItemId);
                    var placeable = _previewObject.ShowGhost(placeInfo, poleBlockMaster, previewData.IsValid && previewData.IsPlaceable);
                    _previewObject.ShowLine(GearChainPoleExtendPreviewCalculator.GetPoleCenter(fromPos), GearChainPoleExtendPreviewCalculator.GetPoleCenter(placePos), placeable);
                    if (CanSendPlace(placeable)) SendExtendProtocol(fromPos, poleSlot, placeInfo);
                }
            }

            void SendExtendProtocol(Vector3Int? fromPos, int poleSlot, PlaceInfo placeInfo)
            {
                _previewObject.Hide();
                _connectFromPole = null;

                // 応答受信後、新規ポールを次の起点として引き継ぐ（連続延長）
                // After the response, hand off the new pole as the next source (continuous extension)
                _requestSender.Send(fromPos, poleSlot, placeInfo, context.HoldingItemId, placedPole => _connectFromPole = placedPole);
            }

            void SelectSourcePole()
            {
                _connectFromPole = hitPole;
                _requestSender.Invalidate();
            }

            bool CanSendPlace(bool placeable)
            {
                // 応答待ち中は誤送信（孤立設置化）を防ぐため送信しない
                // Do not send while awaiting a response to avoid unintended isolated placement
                return placeable && !_requestSender.IsAwaitingResponse && IsScreenClicked();
            }

            bool IsScreenClicked()
            {
                // UI上のクリックはブロック設置操作として扱わない
                // Ignore clicks over UI as placement input
                return InputManager.Playable.ScreenLeftClick.GetKeyDown && !EventSystem.current.IsPointerOverGameObject();
            }

            IGearChainPoleConnectAreaCollider GetGearChainPoleCollider()
            {
                PlaceSystemUtil.TryGetRaySpecifiedComponentHit<IGearChainPoleConnectAreaCollider>(
                    _mainCamera,
                    out var collider,
                    Without_Player_MapObject_BlockBoundingBox_LayerMask);
                return collider;
            }

            #endregion
        }

        public void Disable()
        {
            // 無効化時に状態・プレビュー・応答をクリア
            // Clear selection state, preview and pending responses on disable
            _connectFromPole = null;
            _previewObject.Hide();
            _requestSender.Invalidate();
        }
    }
}
