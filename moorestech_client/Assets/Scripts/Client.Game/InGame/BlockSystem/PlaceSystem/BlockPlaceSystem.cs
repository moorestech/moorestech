using System;
using System.Collections.Generic;
using ClassLibrary;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.SoundEffect;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using Core.Master;
using Game.Block.Interface;
using Game.PlayerInventory.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer.Unity;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    /// <summary>
    ///     マウスで地面をクリックしたときに発生するイベント
    /// </summary>
    public class BlockPlaceSystem : IPostTickable
    {
        public static BlockPlaceSystem Instance;
        
        private const float PlaceableMaxDistance = 100f;
        private readonly IBlockPlacePreview _blockPlacePreview;
        private readonly HotBarView _hotBarView;
        private readonly ILocalPlayerInventory _localPlayerInventory;
        private readonly Camera _mainCamera;
        private readonly BlockPlacePointCalculator _blockPlacePointCalculator;
        
        private BlockDirection _currentBlockDirection = BlockDirection.North;
        private Vector3Int? _clickStartPosition;
        private int _clickStartHeightOffset;
        private int _baseHeight;
        private bool? _isStartZDirection;
        private List<PlaceInfo> _currentPlaceInfos = new();
        
        private bool _enableBlockPlace;
        
        private int _heightOffset;
        
        public BlockPlaceSystem(
            Camera mainCamera,
            HotBarView hotBarView,
            IBlockPlacePreview blockPlacePreview,
            ILocalPlayerInventory localPlayerInventory,
            BlockGameObjectDataStore blockGameObjectDataStore
        )
        {
            Instance = this;
            _hotBarView = hotBarView;
            _mainCamera = mainCamera;
            _blockPlacePreview = blockPlacePreview;
            _localPlayerInventory = localPlayerInventory;
            _blockPlacePointCalculator = new BlockPlacePointCalculator(blockGameObjectDataStore);
        }
        
        public static void SetEnableBlockPlace(bool enable)
        {
            if (Instance == null) return;
            
            Instance._enableBlockPlace = enable;
            
            if (enable)
            {
                Instance._clickStartHeightOffset = -1;
                var playerObjectController = PlayerSystemContainer.Instance.PlayerObjectController;
                Instance._baseHeight = Mathf.RoundToInt(playerObjectController.Position.y);
            }
            else
            {
                Instance._blockPlacePreview.SetActive(false);
                // 連続設置状態をリセット
                Instance._clickStartPosition = null;
                Instance._isStartZDirection = null;
                Instance._currentPlaceInfos.Clear();
            }
        }
        
        public void PostTick()
        {
            if (!_enableBlockPlace) return;
            
            UpdateHeightOffset();
            BlockDirectionControl();
            GroundClickControl();
            
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
        
        private int _lastSelectedIndex = -1;
        
        private void GroundClickControl()
        {
            var selectIndex = _hotBarView.SelectIndex;
            if (selectIndex != _lastSelectedIndex)
            {
                _clickStartPosition = null;
                _lastSelectedIndex = selectIndex;
                _clickStartHeightOffset = _heightOffset;
            }
            
            var itemId = _localPlayerInventory[PlayerInventoryConst.HotBarSlotToInventorySlot(selectIndex)].Id;
            var hitPoint = Vector3.zero;
            
            //基本はプレビュー非表示
            _blockPlacePreview.SetActive(false);
            
            if (!MasterHolder.BlockMaster.IsBlock(itemId)) return; // 置けるブロックかどうか
            if (!TryGetRayHitPosition(out hitPoint, out var boundingBoxSurface)) return; // ブロック設置用のrayが当たっているか
            
            //設置座標計算 calculate place point
            var blockId = MasterHolder.BlockMaster.GetBlockId(itemId);
            var holdingBlockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var placePoint = CalcPlacePoint();
            
            if (!IsBlockPlaceableDistance(PlaceableMaxDistance)) return; // 設置可能な距離かどうか
            
            _blockPlacePreview.SetActive(true);
            
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
                groundDetects = _blockPlacePreview.SetPreviewAndGroundDetect(_currentPlaceInfos, holdingBlockMaster);
            }
            else
            {
                _isStartZDirection = null;
                _currentPlaceInfos = _blockPlacePointCalculator.CalculatePoint(placePoint, placePoint, true, _currentBlockDirection, holdingBlockMaster);
                groundDetects = _blockPlacePreview.SetPreviewAndGroundDetect(_currentPlaceInfos, holdingBlockMaster);
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
                ClientContext.VanillaApi.SendOnly.PlaceHotBarBlock(_currentPlaceInfos, selectIndex);
                SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.PlaceBlock);
            }
            
            #region Internal
            
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
                            Mathf.FloorToInt(hitPoint.x),
                            Mathf.FloorToInt(hitPoint.y),
                            Mathf.FloorToInt(hitPoint.z) - Mathf.RoundToInt(rotatedSize.z / 2f)
                        );
                    case PreviewSurfaceType.YX_Z:
                        return new Vector3Int(
                            Mathf.FloorToInt(hitPoint.x),
                            Mathf.FloorToInt(hitPoint.y),
                            Mathf.FloorToInt(hitPoint.z) + Mathf.RoundToInt(rotatedSize.z / 2f)
                        );
                    case PreviewSurfaceType.YZ_Origin:
                        return new Vector3Int(
                            Mathf.FloorToInt(hitPoint.x) - Mathf.RoundToInt(rotatedSize.x / 2f),
                            Mathf.FloorToInt(hitPoint.y),
                            Mathf.FloorToInt(hitPoint.z)
                        );
                    case PreviewSurfaceType.YZ_X:
                        return new Vector3Int(
                            Mathf.FloorToInt(hitPoint.x) + Mathf.RoundToInt(rotatedSize.x / 2f),
                            Mathf.FloorToInt(hitPoint.y),
                            Mathf.FloorToInt(hitPoint.z)
                        );
                    
                    case PreviewSurfaceType.XZ_Origin:
                        return new Vector3Int(
                            Mathf.FloorToInt(hitPoint.x),
                            Mathf.FloorToInt(hitPoint.y) - rotatedSize.y,
                            Mathf.FloorToInt(hitPoint.z)
                        );
                    case PreviewSurfaceType.XZ_Y:
                        return new Vector3Int(
                            Mathf.FloorToInt(hitPoint.x),
                            Mathf.FloorToInt(hitPoint.y),
                            Mathf.FloorToInt(hitPoint.z)
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