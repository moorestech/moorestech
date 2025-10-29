using System;
using System.Collections.Generic;
using ClassLibrary;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.SoundEffect;
using Client.Input;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;
using UnityEngine.EventSystems;

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
        
        public CommonBlockPlaceSystem(
            Camera mainCamera,
            IPlacementPreviewBlockGameObjectController previewBlockController,
            BlockGameObjectDataStore blockGameObjectDataStore
        )
        {
            _mainCamera = mainCamera;
            _previewBlockController = previewBlockController;
            _blockPlacePointCalculator = new CommonBlockPlacePointCalculator(blockGameObjectDataStore);
        }
        
        public void Enable()
        {
            _clickStartHeightOffset = -1;
            var playerObjectController = PlayerSystemContainer.Instance.PlayerObjectController;
            Mathf.RoundToInt(playerObjectController.Position.y);
        }
        public void Disable()
        {
            _previewBlockController.SetActive(false);
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
            
            if (!TryGetRayHitPosition(out var hitPoint, out var boundingBoxSurface)) return; // ブロック設置用のrayが当たっているか
            
            //設置座標計算 calculate place point
            var holdingBlockMaster = GetBlockMaster();
            var placePoint = CalcPlacePoint();
            
            if (!IsBlockPlaceableDistance(PlaceableMaxDistance)) return; // 設置可能な距離かどうか
            
            _previewBlockController.SetActive(true);
            
            //クリックされてたらUIがゲームスクリーンの時にホットバーにあるブロックの設置
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !EventSystem.current.IsPointerOverGameObject())
            {
                _clickStartPosition = placePoint;
                _clickStartHeightOffset = _heightOffset;
            }
            
            //プレビュー表示と地面との接触を取得する
            //display preview and get collision with ground
            var groundDetects = new List<bool>();
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
                groundDetects = _previewBlockController.SetPreviewAndGroundDetect(_currentPlaceInfos, holdingBlockMaster);
            }
            else
            {
                _isStartZDirection = null;
                _currentPlaceInfos = _blockPlacePointCalculator.CalculatePoint(placePoint, placePoint, true, _currentBlockDirection, holdingBlockMaster);
                groundDetects = _previewBlockController.SetPreviewAndGroundDetect(_currentPlaceInfos, holdingBlockMaster);
            }
            
            // Placeableの更新
            // update placeable
            for (var i = 0; i < groundDetects.Count; i++)
            {
                // 地面と接触していたら設置不可
                // if collision with ground, cannot place
                if (groundDetects[i])
                {
                    _currentPlaceInfos[i].Placeable = false;
                }
            }
            
            // 設置するブロックをサーバーに送信
            // send block place info to server
            if (InputManager.Playable.ScreenLeftClick.GetKeyUp)
            {
                _heightOffset = _clickStartHeightOffset;
                _clickStartPosition = null;
                ClientContext.VanillaApi.SendOnly.PlaceHotBarBlock(_currentPlaceInfos, context.CurrentSelectHotbarSlotIndex);
                SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.PlaceBlock);
            }
            
            #region Internal
            
            BlockMasterElement GetBlockMaster()
            {
                var blockId = MasterHolder.BlockMaster.GetBlockId(context.HoldingItemId);
                return MasterHolder.BlockMaster.GetBlockMaster(blockId);
            }
            
            bool IsBlockPlaceableDistance(float maxDistance)
            {
                var placePosition = (Vector3)placePoint;
                var playerPosition = PlayerSystemContainer.Instance.PlayerObjectController.Position;
                
                return Vector3.Distance(playerPosition, placePosition) <= maxDistance;
            }
            
            Vector3Int CalcPlacePoint()
            {
                var rotateAction = _currentBlockDirection.GetCoordinateConvertAction();
                var rotatedSize = rotateAction(holdingBlockMaster.BlockSize).Abs();
                
                if (boundingBoxSurface == null)
                {
                    var point = Vector3Int.zero;
                    point.x = Mathf.FloorToInt(hitPoint.x + (rotatedSize.x % 2 == 0 ? 0.5f : 0));
                    point.z = Mathf.FloorToInt(hitPoint.z + (rotatedSize.z % 2 == 0 ? 0.5f : 0));
                    point.y = Mathf.FloorToInt(hitPoint.y);
                    
                    point += new Vector3Int(0, _heightOffset, 0);
                    point -= new Vector3Int(rotatedSize.x, 0, rotatedSize.z) / 2;
                    
                    return point;
                }
                
                switch (boundingBoxSurface.PreviewSurfaceType)
                {
                    case PreviewSurfaceType.YX_Origin:
                        return new Vector3Int(
                            Mathf.FloorToInt(hitPoint.x) - Mathf.FloorToInt(rotatedSize.x / 2f),
                            Mathf.FloorToInt(hitPoint.y),
                            Mathf.FloorToInt(hitPoint.z) - Mathf.RoundToInt(rotatedSize.z / 2f)
                        );
                    case PreviewSurfaceType.YX_Z:
                        return new Vector3Int(
                            Mathf.FloorToInt(hitPoint.x) - Mathf.FloorToInt(rotatedSize.x / 2f),
                            Mathf.FloorToInt(hitPoint.y),
                            Mathf.FloorToInt(hitPoint.z)
                        );
                    case PreviewSurfaceType.YZ_Origin:
                        return new Vector3Int(
                            Mathf.FloorToInt(hitPoint.x) - Mathf.RoundToInt(rotatedSize.x / 2f),
                            Mathf.FloorToInt(hitPoint.y),
                            Mathf.FloorToInt(hitPoint.z) - Mathf.FloorToInt(rotatedSize.z / 2f)
                        );
                    case PreviewSurfaceType.YZ_X:
                        return new Vector3Int(
                            Mathf.FloorToInt(hitPoint.x),
                            Mathf.FloorToInt(hitPoint.y),
                            Mathf.FloorToInt(hitPoint.z) - Mathf.FloorToInt(rotatedSize.z / 2f)
                        );
                    
                    case PreviewSurfaceType.XZ_Origin:
                        return new Vector3Int(
                            Mathf.FloorToInt(hitPoint.x) - Mathf.FloorToInt(rotatedSize.x / 2f),
                            Mathf.FloorToInt(hitPoint.y) - rotatedSize.y,
                            Mathf.FloorToInt(hitPoint.z) - Mathf.FloorToInt(rotatedSize.z / 2f)
                        );
                    case PreviewSurfaceType.XZ_Y:
                        return new Vector3Int(
                            Mathf.FloorToInt(hitPoint.x) - Mathf.FloorToInt(rotatedSize.x / 2f),
                            Mathf.FloorToInt(hitPoint.y),
                            Mathf.FloorToInt(hitPoint.z) - Mathf.FloorToInt(rotatedSize.z / 2f)
                        );
                    
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            
            #endregion
        }
        
        
        private bool TryGetRayHitPosition(out Vector3 pos,out BlockPreviewBoundingBoxSurface surface)
        {
            surface = null;
            pos = Vector3Int.zero;
            var ray = _mainCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            
            //画面からのrayが何かにヒットしているか
            if (!Physics.Raycast(ray, out var hit, float.PositiveInfinity, LayerConst.Without_Player_MapObject_Block_LayerMask)) return false;
            //そのrayが地面のオブジェクトかブロックのバウンディングボックスにヒットしてるか
            if (
                !hit.transform.TryGetComponent<GroundGameObject>(out _) &&
                !hit.transform.TryGetComponent(out surface)
            )
            {
                return false;
            }
            
            //基本的にブロックの原点は0,0なので、rayがヒットした座標を基準にブロックの原点を計算する
            pos = hit.point;
            
            return true;
        }
    }
}