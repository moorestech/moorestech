using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.SoundEffect;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using Common.Debug;
using Core.Master;
using Game.Block.Interface;
using Game.PlayerInventory.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;
using UnityEngine.EventSystems;
using static Client.Game.InGame.BlockSystem.PlaceSystem.Util.PlaceSystemUtil;
using static Client.Game.DebugConst;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    ///     マウスで地面をクリックしたときに発生するイベント
    /// </summary>
    public class CommonBlockPlaceSystem : IPlaceSystem
    {
        private const float PlaceableMaxDistance = 100f;
        private readonly IPlacementPreviewBlockGameObjectController _previewBlockController;
        private readonly ILocalPlayerInventory _localPlayerInventory;
        private readonly Camera _mainCamera;
        private readonly CommonBlockPlacePointCalculator _blockPlacePointCalculator;
        private readonly ElectricWireAutoConnectPreview _autoConnectPreview;

        private BlockDirection _currentBlockDirection = BlockDirection.North;
        private Vector3Int? _clickStartPosition;
        private int _clickStartHeightOffset;
        private bool? _isStartZDirection;
        private List<PlaceInfo> _currentPlaceInfos = new();

        private int _heightOffset;

        public CommonBlockPlaceSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, BlockGameObjectDataStore blockGameObjectDataStore, ILocalPlayerInventory localPlayerInventory)
        {
            _mainCamera = mainCamera;
            _previewBlockController = previewBlockController;
            _localPlayerInventory = localPlayerInventory;
            _blockPlacePointCalculator = new CommonBlockPlacePointCalculator(blockGameObjectDataStore);
            _autoConnectPreview = new ElectricWireAutoConnectPreview(mainCamera, blockGameObjectDataStore);
        }
        
        public void Enable()
        {
            _clickStartHeightOffset = -1;
        }
        public void Disable()
        {
            // デバッグモード時はプレビューを維持
            // Keep preview in debug mode
            if (!DebugParameters.GetValueOrDefaultBool(PlacePreviewKeepKey))
            {
                _previewBlockController.SetActive(false);
                _autoConnectPreview.Hide();
            }

            // 連続設置状態をリセット
            _clickStartPosition = null;
            _isStartZDirection = null;
            _currentPlaceInfos.Clear();
        }
        
        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            UpdateHeightOffset();
            BlockDirectionControl();
            GroundClickControl(context);
            
            #region Internal
            
            void UpdateHeightOffset()
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.Q)) //TODO InputManagerに移す
                    _heightOffset--;
                else if (UnityEngine.Input.GetKeyDown(KeyCode.E)) _heightOffset++;
            }
            
            void BlockDirectionControl()
            {
                if (InputManager.Playable.BlockPlaceRotation.GetKeyDown)
                    // 東西南北の向きを変更する
                    _currentBlockDirection = _currentBlockDirection.HorizonRotation();
                
                //TODo シフトはインプットマネージャーに入れる
                if (UnityEngine.Input.GetKey(KeyCode.LeftShift) && InputManager.Playable.BlockPlaceRotation.GetKeyDown)
                    _currentBlockDirection = _currentBlockDirection.VerticalRotation();
            }
            
            #endregion
        }
        
        
        private void GroundClickControl(PlaceSystemUpdateContext context)
        {
            if (context.IsSelectSlotChanged)
            {
                _clickStartPosition = null;
                _clickStartHeightOffset = _heightOffset;
            }
            
            //基本はプレビュー非表示
            _previewBlockController.SetActive(false);

            // ブロック設置用のrayが当たっているか、当たっていたら設置位置を取得する
            var holdingBlockMaster = MasterHolder.BlockMaster.GetBlockMaster(context.HoldingItemId);
            if (!TryGetRayHitBlockPosition(_mainCamera, _heightOffset, _currentBlockDirection, holdingBlockMaster, out var placePoint, out var boundingBoxSurface)) { _autoConnectPreview.Hide(); return; }

            // 設置可能な距離かどうか
            if (!IsBlockPlaceableDistance(PlaceableMaxDistance)) { _autoConnectPreview.Hide(); return; }
            
            _previewBlockController.SetActive(true);
            
            //クリックされてたらUIがゲームスクリーンの時にホットバーにあるブロックの設置
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !EventSystem.current.IsPointerOverGameObject())
            {
                _clickStartPosition = placePoint;
                _clickStartHeightOffset = _heightOffset;
            }
            
            //プレビュー表示と地面との接触を取得する
            //display preview and get collision with ground
            SetCurrentPlaceInfo();

            var blockGroundOverlapList = _previewBlockController.SetPreviewAndGroundDetect(_currentPlaceInfos, holdingBlockMaster);

            // 地面との接触でPlaceableを更新
            // Update placeable based on ground collision
            for (var i = 0; i < blockGroundOverlapList.Count; i++)
            {
                // 地面と接触していたら設置不可
                // if collision with ground, cannot place
                if (blockGroundOverlapList[i])
                {
                    _currentPlaceInfos[i].Placeable = false;
                }
            }

            // 地面フィルタ後にアイテム数チェック（地面に埋まったブロックがアイテム枠を消費しないようにする）
            // Check item count after ground filtering (so ground-blocked cells don't consume item quota)
            MarkInsufficientItemPreviewsAsNotPlaceable();

            // 各セルの自動接続を評価し表示更新
            // Evaluate auto-connect per cell and update the preview
            var wirePlaceable = _autoConnectPreview.ApplyAutoConnect(_currentPlaceInfos, MasterHolder.BlockMaster.GetBlockId(context.HoldingItemId), _currentBlockDirection, _localPlayerInventory, placePoint);

            // 最終的なPlaceable状態でプレビュー色を更新
            // Update preview colors based on the final Placeable state
            _previewBlockController.UpdatePlaceableColors(_currentPlaceInfos);

            // 設置するブロックをサーバーに送信
            // send block place info to server
            PlaceBlock();
            
            #region Internal
            
            bool IsBlockPlaceableDistance(float maxDistance)
            {
                var placePosition = (Vector3)placePoint;
                var playerPosition = PlayerSystemContainer.Instance.PlayerObjectController.Position;
                
                return Vector3.Distance(playerPosition, placePosition) <= maxDistance;
            }
            
            void SetCurrentPlaceInfo()
            {
                if (_clickStartPosition.HasValue)
                {
                    if (_clickStartPosition.Value == placePoint)
                    {
                        _isStartZDirection = null;
                    }
                    else if (!_isStartZDirection.HasValue)
                    {
                        _isStartZDirection = Mathf.Abs(placePoint.z - _clickStartPosition.Value.z) > Mathf.Abs(placePoint.x - _clickStartPosition.Value.x);
                    }
                    
                    _currentPlaceInfos = _blockPlacePointCalculator.CalculatePoint(_clickStartPosition.Value, placePoint, _isStartZDirection ?? true, _currentBlockDirection, holdingBlockMaster);
                }
                else
                {
                    _isStartZDirection = null;
                    _currentPlaceInfos = _blockPlacePointCalculator.CalculatePoint(placePoint, placePoint, true, _currentBlockDirection, holdingBlockMaster);
                }
            }
            
            void PlaceBlock()
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyUp) return;

                // デバッグモード時は送信しない
                // Skip sending in debug mode
                if (DebugParameters.GetValueOrDefaultBool(PlacePreviewKeepKey)) return;

                // マウスを離したので連続設置状態は解除する（設置有無に関わらず）
                // Clear the continuous-placement state on mouse release (regardless of whether we place)
                _heightOffset = _clickStartHeightOffset;
                _clickStartPosition = null;

                // 電線不足で全セル設置不可なら設置クリックを無効化する（サーバーも拒否するが先回りで抑止）
                // Disable the placement click when no cell is placeable due to wire shortage (server also rejects, but block early)
                if (!wirePlaceable) return;

                SendPlaceProtocol(_currentPlaceInfos, context);

                // 設置でワールドとインベントリが変わるため、自動接続の評価キャッシュを破棄する
                // Placement changes the world and inventory, so drop the auto-connect evaluation cache
                _autoConnectPreview.Hide();
            }

            void MarkInsufficientItemPreviewsAsNotPlaceable()
            {
                // 設置は選択中ホットバースロット1枠からのみ消費されるため、その枠の所持数で判定する
                // Placement consumes only from the selected hotbar slot, so judge by that slot's count
                var holdingSlotIndex = _localPlayerInventory.GetHotBarInventorySlot(context.CurrentSelectHotbarSlotIndex);
                var availableCount = _localPlayerInventory[holdingSlotIndex].Count;

                // 設置可能なブロック数をカウントし、所持数を超えたら設置不可にする
                // Count placeable blocks and mark as not placeable when exceeding available count
                var placeableCount = 0;
                for (var i = 0; i < _currentPlaceInfos.Count; i++)
                {
                    if (!_currentPlaceInfos[i].Placeable) continue;

                    placeableCount++;
                    if (placeableCount > availableCount)
                    {
                        _currentPlaceInfos[i].Placeable = false;
                    }
                }
            }

            #endregion
        }
    }
}