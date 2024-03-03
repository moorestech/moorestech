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


        private BlockDirection _currentBlockDirection;
        private HotBarView _hotBarView;
        private Camera _mainCamera;
        private UIStateControl _uiStateControl;
        private IBlockConfig _blockConfig;
        private ILocalPlayerInventory _localPlayerInventory;

        private void Update()
        {
            BlockDirectionControl();
            GroundClickControl();
        }

        [Inject]
        public void Construct(Camera mainCamera, HotBarView hotBarView, UIStateControl uiStateControl, IBlockPlacePreview blockPlacePreview,MoorestechServerServiceProvider moorestechServerServiceProvider,ILocalPlayerInventory localPlayerInventory)
        {
            _uiStateControl = uiStateControl;
            _hotBarView = hotBarView;
            _mainCamera = mainCamera;
            _blockPlacePreview = blockPlacePreview;
            _blockConfig = moorestechServerServiceProvider.BlockConfig;
            _localPlayerInventory = localPlayerInventory;
        }

        private void BlockDirectionControl()
        {
            if (!InputManager.Playable.BlockPlaceRotation.GetKeyDown) return;

            _currentBlockDirection = _currentBlockDirection switch
            {
                BlockDirection.North => BlockDirection.East,
                BlockDirection.East => BlockDirection.South,
                BlockDirection.South => BlockDirection.West,
                BlockDirection.West => BlockDirection.North,
                _ => _currentBlockDirection
            };
        }


        private void GroundClickControl()
        {
            //基本はプレビュー非表示
            _blockPlacePreview.SetActive(false);

            //UIの状態がゲームホットバー選択中か
            if (_uiStateControl.CurrentState != UIStateEnum.GameScreen) return;

            var selectIndex = (short)_hotBarView.SelectIndex;
            var itemId = _localPlayerInventory[PlayerInventoryConst.HotBarSlotToInventorySlot(selectIndex)].Id;
            //持っているアイテムがブロックじゃなかったら何もしない
            if (!_blockConfig.IsBlock(itemId)) return; 

            //プレビューの座標を取得
            var (isHit, hitPoint) = GetPreviewPosition();
            if (!isHit) return;

            //プレビュー表示
            _blockPlacePreview.SetActive(true);

            //クリックされてたらUIがゲームスクリーンの時にホットバーにあるブロックの設置
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !EventSystem.current.IsPointerOverGameObject())
            {
                MoorestechContext.VanillaApi.SendOnly.PlaceHotBarBlock(hitPoint.x, hitPoint.y,selectIndex,  _currentBlockDirection);
                SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.PlaceBlock);
                return;
            }


            //クリックされてなかったらプレビューを表示する
            _blockPlacePreview.SetPreview(hitPoint, _currentBlockDirection,_blockConfig.ItemIdToBlockConfig(itemId));
        }


        private (bool, Vector2Int pos) GetPreviewPosition()
        {
            var ray = _mainCamera.ScreenPointToRay(new Vector2(Screen.width / 2.0f, Screen.height / 2.0f));

            //画面からのrayが何かにヒットしているか
            if (!Physics.Raycast(ray, out var hit, 100, LayerConst.WithoutMapObjectAndPlayerLayerMask)) return (false, new Vector2Int());
            //そのrayが地面のオブジェクトにヒットしてるか
            if (hit.transform.GetComponent<GroundPlane>() == null) return (false, new Vector2Int());

            //基本的にブロックの原点は0,0なので、rayがヒットした座標を基準にブロックの原点を計算する
            var x = Mathf.FloorToInt(hit.point.x);
            var y = Mathf.FloorToInt(hit.point.z); //サーバー上のY軸がUnityのZ軸に相当する

            return (true, new Vector2Int(x, y));
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