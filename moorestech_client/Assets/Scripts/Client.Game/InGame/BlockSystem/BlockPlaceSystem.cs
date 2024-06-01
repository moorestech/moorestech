using ClassLibrary;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Chunk;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.SoundEffect;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer.Unity;

namespace Client.Game.InGame.BlockSystem
{
    /// <summary>
    ///     マウスで地面をクリックしたときに発生するイベント
    /// </summary>
    public class BlockPlaceSystem : IPostTickable
    {
        private const float PlaceableMaxDistance = 100f;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly IBlockPlacePreview _blockPlacePreview;
        private readonly HotBarView _hotBarView;
        private readonly ILocalPlayerInventory _localPlayerInventory;
        private readonly Camera _mainCamera;
        private readonly PlayerObjectController _playerObjectController;
        private readonly UIStateControl _uiState;
        
        private BlockDirection _currentBlockDirection = BlockDirection.North;
        
        private int _heightOffset;
        
        public BlockPlaceSystem(
            Camera mainCamera,
            HotBarView hotBarView,
            IBlockPlacePreview blockPlacePreview,
            ILocalPlayerInventory localPlayerInventory,
            UIStateControl uiStateControl,
            BlockGameObjectDataStore blockGameObjectDataStore,
            PlayerObjectController playerObjectController
        )
        {
            _hotBarView = hotBarView;
            _mainCamera = mainCamera;
            _blockPlacePreview = blockPlacePreview;
            _localPlayerInventory = localPlayerInventory;
            _uiState = uiStateControl;
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _playerObjectController = playerObjectController;
        }
        
        public void PostTick()
        {
            UpdateHeightOffset();
            BlockDirectionControl();
            GroundClickControl();
        }
        
        private void UpdateHeightOffset()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Q)) //TODO InputManagerに移す
                _heightOffset--;
            else if (UnityEngine.Input.GetKeyDown(KeyCode.E)) _heightOffset++;
        }
        
        private void BlockDirectionControl()
        {
            if (InputManager.Playable.BlockPlaceRotation.GetKeyDown)
                // 東西南北の向きを変更する
                _currentBlockDirection = _currentBlockDirection switch
                {
                    BlockDirection.UpNorth => BlockDirection.UpEast,
                    BlockDirection.UpEast => BlockDirection.UpSouth,
                    BlockDirection.UpSouth => BlockDirection.UpWest,
                    BlockDirection.UpWest => BlockDirection.UpNorth,
                    
                    BlockDirection.North => BlockDirection.East,
                    BlockDirection.East => BlockDirection.South,
                    BlockDirection.South => BlockDirection.West,
                    BlockDirection.West => BlockDirection.North,
                    
                    BlockDirection.DownNorth => BlockDirection.DownEast,
                    BlockDirection.DownEast => BlockDirection.DownSouth,
                    BlockDirection.DownSouth => BlockDirection.DownWest,
                    BlockDirection.DownWest => BlockDirection.DownNorth,
                    
                    _ => _currentBlockDirection
                };
            
            //TODo シフトはインプットマネージャーに入れる
            if (UnityEngine.Input.GetKey(KeyCode.LeftShift) && InputManager.Playable.BlockPlaceRotation.GetKeyDown)
                _currentBlockDirection = _currentBlockDirection switch
                {
                    BlockDirection.UpNorth => BlockDirection.DownNorth,
                    BlockDirection.UpEast => BlockDirection.DownEast,
                    BlockDirection.UpSouth => BlockDirection.DownSouth,
                    BlockDirection.UpWest => BlockDirection.DownWest,
                    
                    BlockDirection.North => BlockDirection.UpNorth,
                    BlockDirection.East => BlockDirection.UpEast,
                    BlockDirection.South => BlockDirection.UpSouth,
                    BlockDirection.West => BlockDirection.UpWest,
                    
                    BlockDirection.DownNorth => BlockDirection.North,
                    BlockDirection.DownEast => BlockDirection.East,
                    BlockDirection.DownSouth => BlockDirection.South,
                    BlockDirection.DownWest => BlockDirection.West,
                    
                    _ => _currentBlockDirection
                };
        }
        
        private void GroundClickControl()
        {
            var selectIndex = _hotBarView.SelectIndex;
            var itemId = _localPlayerInventory[PlayerInventoryConst.HotBarSlotToInventorySlot(selectIndex)].Id;
            var hitPoint = Vector3.zero;
            
            //基本はプレビュー非表示
            _blockPlacePreview.SetActive(false);
            
            if (_uiState.CurrentState != UIStateEnum.PlaceBlock) return; // ブロックを設置するステートかどうか
            if (!ServerContext.BlockConfig.IsBlock(itemId)) return; // 置けるブロックかどうか
            if (!TryGetRayHitPosition(out hitPoint)) return; // ブロック設置用のrayが当たっているか
            
            //設置座標計算 calculate place point
            var holdingBlockConfig = ServerContext.BlockConfig.ItemIdToBlockConfig(itemId);
            var placePoint = CalcPlacePoint();
            
            _blockPlacePreview.SetActive(true);
            var placeable =
                !IsAlreadyExistingBlock(placePoint, holdingBlockConfig.BlockSize) &&
                IsBlockPlaceableDistance(PlaceableMaxDistance) &&
                !IsTerrainOverlapBlock();
            
            //プレビュー表示 display preview
            _blockPlacePreview.SetPreview(placeable, placePoint, _currentBlockDirection, holdingBlockConfig);
            
            //クリックされてたらUIがゲームスクリーンの時にホットバーにあるブロックの設置
            if (placeable && InputManager.Playable.ScreenLeftClick.GetKeyDown && !EventSystem.current.IsPointerOverGameObject())
            {
                ClientContext.VanillaApi.SendOnly.PlaceHotBarBlock(placePoint, selectIndex, _currentBlockDirection);
                SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.PlaceBlock);
            }
            
            #region Internal
            
            bool IsAlreadyExistingBlock(Vector3Int originPosition, Vector3Int size)
            {
                // ブロックが既に存在しているかどうか
                var previewPositionInfo = new BlockPositionInfo(originPosition, _currentBlockDirection, size);
                
                return _blockGameObjectDataStore.IsOverlapPositionInfo(previewPositionInfo);
            }
            
            bool IsTerrainOverlapBlock()
            {
                // ブロックとterrainが重なっていること
                //TODO ちゃんとできないから一旦放置 return _blockPlacePreview.IsCollisionGround;
                return false;
            }
            
            bool IsBlockPlaceableDistance(float maxDistance)
            {
                var placePosition = (Vector3)placePoint;
                var playerPosition = _playerObjectController.transform.position;
                
                return Vector3.Distance(playerPosition, placePosition) <= maxDistance;
            }
            
            Vector3Int CalcPlacePoint()
            {
                var rotateAction = _currentBlockDirection.GetCoordinateConvertAction();
                var rotatedSize = rotateAction(holdingBlockConfig.BlockSize).Abs();
                
                var point = Vector3Int.zero;
                point.x = Mathf.FloorToInt(hitPoint.x + (rotatedSize.x % 2 == 0 ? 0.5f : 0));
                point.z = Mathf.FloorToInt(hitPoint.z + (rotatedSize.z % 2 == 0 ? 0.5f : 0));
                point.y = Mathf.FloorToInt(hitPoint.y);
                
                point += new Vector3Int(0, _heightOffset, 0);
                point -= new Vector3Int(rotatedSize.x, 0, rotatedSize.z) / 2;
                
                return point;
            }
            
            #endregion
        }
        
        
        private bool TryGetRayHitPosition(out Vector3 pos)
        {
            pos = Vector3Int.zero;
            var ray = _mainCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            
            //画面からのrayが何かにヒットしているか
            if (!Physics.Raycast(ray, out var hit, float.PositiveInfinity, LayerConst.WithoutMapObjectAndPlayerLayerMask)) return false;
            //そのrayが地面のオブジェクトかブロックにヒットしてるか
            if (!hit.transform.TryGetComponent<GroundGameObject>(out _) && !hit.transform.TryGetComponent<BlockGameObjectChild>(out _)) return false;
            
            //基本的にブロックの原点は0,0なので、rayがヒットした座標を基準にブロックの原点を計算する
            pos = hit.point;
            
            return true;
        }
    }
}