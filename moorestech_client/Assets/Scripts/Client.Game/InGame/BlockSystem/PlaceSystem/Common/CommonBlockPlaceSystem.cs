using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.SoundEffect;
using Client.Input;
using Common.Debug;
using Core.Master;
using Game.Block.Interface;
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
        private readonly Camera _mainCamera;
        private readonly CommonBlockPlacePointCalculator _blockPlacePointCalculator;
        
        private BlockDirection _currentBlockDirection = BlockDirection.North;
        private Vector3Int? _clickStartPosition;
        private int _clickStartHeightOffset;
        private bool? _isStartZDirection;
        private List<PlaceInfo> _currentPlaceInfos = new();
        
        private int _heightOffset;
        
        public CommonBlockPlaceSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _mainCamera = mainCamera;
            _previewBlockController = previewBlockController;
            _blockPlacePointCalculator = new CommonBlockPlacePointCalculator(blockGameObjectDataStore);
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
            if (!TryGetRayHitBlockPosition(_mainCamera, _heightOffset, _currentBlockDirection, holdingBlockMaster, out var placePoint, out var boundingBoxSurface)) return;
            
            // 設置可能な距離かどうか
            if (!IsBlockPlaceableDistance(PlaceableMaxDistance)) return;
            
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
            
            // Placeableの更新
            // update placeable
            for (var i = 0; i < blockGroundOverlapList.Count; i++)
            {
                // 地面と接触していたら設置不可
                // if collision with ground, cannot place
                if (blockGroundOverlapList[i])
                {
                    _currentPlaceInfos[i].Placeable = false;
                }
            }
            
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

                _heightOffset = _clickStartHeightOffset;
                _clickStartPosition = null;
                SendPlaceProtocol(_currentPlaceInfos, context);
            }
            
            #endregion
        }
    }
}