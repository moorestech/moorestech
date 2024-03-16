using Client.Game.Context;
using Client.Network.API;
using Game.World.Interface.DataStore;
using Constant;
using Game.Block.Interface.BlockConfig;
using Game.PlayerInventory.Interface;
using MainGame.Network.Send;
using MainGame.UnityView.Block;
using MainGame.UnityView.Chunk;
using MainGame.UnityView.Control;
using MainGame.UnityView.SoundEffect;
using MainGame.UnityView.UI.Inventory;
using MainGame.UnityView.UI.Inventory.Main;
using MainGame.UnityView.UI.UIState;
using ServerServiceProvider;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace MainGame.Presenter.Inventory.Send
{
    /// <summary>
    ///     マウスで地面をクリックしたときに発生するイベント
    /// </summary>
    public class DetectGroundClickToSendBlockPlacePacket : MonoBehaviour
    {
        private IBlockPlacePreview _blockPlacePreview;

        private BlockDirection _currentBlockDirection = BlockDirection.North;
        private HotBarView _hotBarView;
        private Camera _mainCamera;
        private UIStateControl _uiStateControl;
        private ILocalPlayerInventory _localPlayerInventory;

        private int _heightOffset = 1;

        private void Update()
        {
            UpdateHeightOffset();
            BlockDirectionControl();
            GroundClickControl();
        }

        [Inject]
        public void Construct(Camera mainCamera, HotBarView hotBarView, UIStateControl uiStateControl, IBlockPlacePreview blockPlacePreview, ILocalPlayerInventory localPlayerInventory)
        {
            _uiStateControl = uiStateControl;
            _hotBarView = hotBarView;
            _mainCamera = mainCamera;
            _blockPlacePreview = blockPlacePreview;
            _localPlayerInventory = localPlayerInventory;
        }

        private void UpdateHeightOffset()
        {
            if (Input.GetKeyDown(KeyCode.Q)) //TODO InputManagerに移す
            {
                _heightOffset--;
            }
            else if (Input.GetKeyDown(KeyCode.E))
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

                    _ => _currentBlockDirection
                };
            }

            //TODo シフトはインプットマネージャーに入れる
            if (Input.GetKey(KeyCode.LeftShift) && InputManager.Playable.BlockPlaceRotation.GetKeyDown)
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

                    _ => _currentBlockDirection
                };
            }
        }


        private void GroundClickControl()
        {
            var blockConfig = MoorestechContext.ServerServices.BlockConfig;

            //基本はプレビュー非表示
            _blockPlacePreview.SetActive(false);

            //UIの状態がゲームホットバー選択中か
            if (_uiStateControl.CurrentState != UIStateEnum.GameScreen) return;

            var selectIndex = (short) _hotBarView.SelectIndex;
            var itemId = _localPlayerInventory[PlayerInventoryConst.HotBarSlotToInventorySlot(selectIndex)].Id;
            //持っているアイテムがブロックじゃなかったら何もしない
            if (!blockConfig.IsBlock(itemId)) return;

            //プレビューの座標を取得
            if (!TryGetRayHitPosition(out var hitPoint)) return;

            hitPoint += new Vector3Int(0, _heightOffset, 0);

            //プレビュー表示
            _blockPlacePreview.SetActive(true);

            //クリックされてたらUIがゲームスクリーンの時にホットバーにあるブロックの設置
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !EventSystem.current.IsPointerOverGameObject())
            {
                MoorestechContext.VanillaApi.SendOnly.PlaceHotBarBlock(hitPoint, selectIndex, _currentBlockDirection);
                SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.PlaceBlock);
                return;
            }

            //クリックされてなかったらプレビューを表示する
            _blockPlacePreview.SetPreview(hitPoint, _currentBlockDirection, blockConfig.ItemIdToBlockConfig(itemId));
        }


        private bool TryGetRayHitPosition(out Vector3Int pos)
        {
            pos = Vector3Int.zero;
            var ray = _mainCamera.ScreenPointToRay(new Vector2(Screen.width / 2.0f, Screen.height / 2.0f));

            //画面からのrayが何かにヒットしているか
            if (!Physics.Raycast(ray, out var hit, 100, LayerConst.WithoutMapObjectAndPlayerLayerMask)) return false;
            //そのrayが地面のオブジェクトにヒットしてるか
            if (hit.transform.GetComponent<GroundPlane>() == null) return false;

            //基本的にブロックの原点は0,0なので、rayがヒットした座標を基準にブロックの原点を計算する
            var x = Mathf.FloorToInt(hit.point.x);
            var y = Mathf.FloorToInt(hit.point.y);
            var z = Mathf.FloorToInt(hit.point.z);

            pos = new Vector3Int(x, y, z);

            return true;
        }
    }

    public class OnPlaceProprietors
    {
        public BlockDirection PlaceBlockDirection;
        public Vector2Int PlaceBlockPosition;
        public int PlaceHotBarSlot;

        public OnPlaceProprietors(Vector2Int placeBlockPosition, int placeHotBarSlot, BlockDirection placeBlockDirection)
        {
            PlaceBlockPosition = placeBlockPosition;
            PlaceHotBarSlot = placeHotBarSlot;
            PlaceBlockDirection = placeBlockDirection;
        }
    }
}