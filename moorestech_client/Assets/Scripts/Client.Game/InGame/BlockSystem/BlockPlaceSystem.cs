using ClassLibrary;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Chunk;
using Client.Game.InGame.Context;
using Client.Game.InGame.SoundEffect;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Main;
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
        private readonly IBlockPlacePreview _blockPlacePreview;
        private readonly HotBarView _hotBarView;
        private readonly ILocalPlayerInventory _localPlayerInventory;
        private readonly Camera _mainCamera;
        
        private BlockDirection _currentBlockDirection = BlockDirection.North;
        
        private int _heightOffset;
        public bool DisplayPreview = true;
        public Vector2 MousePosition;
        
        public BlockPlaceSystem(Camera mainCamera, HotBarView hotBarView, IBlockPlacePreview blockPlacePreview, ILocalPlayerInventory localPlayerInventory)
        {
            _hotBarView = hotBarView;
            _mainCamera = mainCamera;
            _blockPlacePreview = blockPlacePreview;
            _localPlayerInventory = localPlayerInventory;
            MousePosition = new Vector2(Screen.width / 2.0f, Screen.height / 2.0f);
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
            {
                _heightOffset--;
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.E))
            {
                _heightOffset++;
            }
        }
        
        private void BlockDirectionControl()
        {
            if (InputManager.Playable.BlockPlaceRotation.GetKeyDown)
            {
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
                    
                    _ => _currentBlockDirection,
                };
            }
            
            //TODo シフトはインプットマネージャーに入れる
            if (UnityEngine.Input.GetKey(KeyCode.LeftShift) && InputManager.Playable.BlockPlaceRotation.GetKeyDown)
            {
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
                    
                    _ => _currentBlockDirection,
                };
            }
        }
        
        private void GroundClickControl()
        {
            var blockConfig = ServerContext.BlockConfig;
            var selectIndex = _hotBarView.SelectIndex;
            var itemId = _localPlayerInventory[PlayerInventoryConst.HotBarSlotToInventorySlot(selectIndex)].Id;
            var hitPoint = Vector3.zero;
            
            //基本はプレビュー非表示
            _blockPlacePreview.SetActive(false);
            
            //プレビュー表示判定
            if (!IsDisplayPreviewBlock()) return;
            
            //設置座標計算 calculate place point
            var holdingBlockConfig = blockConfig.ItemIdToBlockConfig(itemId);
            var placePoint = CalcPlacePoint();
            
            //プレビュー表示 display preview
            _blockPlacePreview.SetActive(true);
            _blockPlacePreview.SetPreview(placePoint, _currentBlockDirection, holdingBlockConfig);
            
            //クリックされてたらUIがゲームスクリーンの時にホットバーにあるブロックの設置
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !EventSystem.current.IsPointerOverGameObject())
            {
                MoorestechContext.VanillaApi.SendOnly.PlaceHotBarBlock(placePoint, selectIndex, _currentBlockDirection);
                SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.PlaceBlock);
            }
            
            #region Internal
            
            bool IsDisplayPreviewBlock()
            {
                //UIの状態がゲームホットバー選択中か
                if (!DisplayPreview) return false;
                
                //持っているアイテムがブロックじゃなかったら何もしない
                if (!blockConfig.IsBlock(itemId)) return false;
                
                //プレビューの座標を取得
                return TryGetRayHitPosition(out hitPoint);
            }
            
            Vector3Int CalcPlacePoint()
            {
                var convertAction = _currentBlockDirection.GetCoordinateConvertAction();
                var convertedSize = convertAction(holdingBlockConfig.BlockSize).Abs();
                
                var point = Vector3Int.zero;
                point.x = Mathf.FloorToInt(hitPoint.x + (convertedSize.x % 2 == 0 ? 0.5f : 0));
                point.z = Mathf.FloorToInt(hitPoint.z + (convertedSize.z % 2 == 0 ? 0.5f : 0));
                point.y = Mathf.FloorToInt(hitPoint.y);
                
                point += new Vector3Int(0, _heightOffset, 0);
                point -= new Vector3Int(convertedSize.x, 0, convertedSize.z) / 2;
                
                return point;
            }
            
            #endregion
        }
        
        
        private bool TryGetRayHitPosition(out Vector3 pos)
        {
            pos = Vector3Int.zero;
            var ray = _mainCamera.ScreenPointToRay(MousePosition);
            
            //画面からのrayが何かにヒットしているか
            if (!Physics.Raycast(ray, out var hit, 100, LayerConst.WithoutMapObjectAndPlayerLayerMask)) return false;
            //そのrayが地面のオブジェクトかブロックにヒットしてるか
            if (!hit.transform.TryGetComponent<GroundGameObject>(out _) && !hit.transform.TryGetComponent<BlockGameObjectChild>(out _)) return false;
            
            //基本的にブロックの原点は0,0なので、rayがヒットした座標を基準にブロックの原点を計算する
            pos = hit.point;
            
            return true;
        }
    }
}