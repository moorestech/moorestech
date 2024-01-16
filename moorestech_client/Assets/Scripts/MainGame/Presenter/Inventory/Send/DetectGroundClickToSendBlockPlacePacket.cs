using Game.World.Interface.DataStore;
using Constant;
using MainGame.Network.Send;
using MainGame.UnityView.Block;
using MainGame.UnityView.Chunk;
using MainGame.UnityView.Control;
using MainGame.UnityView.SoundEffect;
using MainGame.UnityView.UI.Inventory.HotBar;
using MainGame.UnityView.UI.UIState;
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
        private GroundPlane _groundPlane;
        private SelectHotBarControl _hotBarControl;
        private Camera _mainCamera;
        private SendPlaceHotBarBlockProtocol _sendPlaceHotBarBlockProtocol;
        private UIStateControl _uiStateControl;

        private void Update()
        {
            BlockDirectionControl();
            GroundClickControl();
        }

        [Inject]
        public void Construct(Camera mainCamera, GroundPlane groundPlane,
            SelectHotBarControl selectHotBarControl, SendPlaceHotBarBlockProtocol sendPlaceHotBarBlockProtocol, UIStateControl uiStateControl, IBlockPlacePreview blockPlacePreview)
        {
            _uiStateControl = uiStateControl;
            _sendPlaceHotBarBlockProtocol = sendPlaceHotBarBlockProtocol;
            _hotBarControl = selectHotBarControl;
            _mainCamera = mainCamera;
            _groundPlane = groundPlane;
            _blockPlacePreview = blockPlacePreview;
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


            var (isHit, hitPoint) = GetPreviewPosition();
            if (!isHit) return;

            //プレビュー表示
            _blockPlacePreview.SetActive(true);

            //クリックされてたらUIがゲームスクリーンの時にホットバーにあるブロックの設置
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !EventSystem.current.IsPointerOverGameObject())
            {
                _sendPlaceHotBarBlockProtocol.Send(hitPoint.x, hitPoint.y, (short)_hotBarControl.SelectIndex, _currentBlockDirection);
                SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.PlaceBlock);
                return;
            }


            //クリックされてなかったらプレビューを表示する
            _blockPlacePreview.SetPreview(hitPoint, _currentBlockDirection);
        }


        private (bool, Vector2Int pos) GetPreviewPosition()
        {
            var mousePosition = InputManager.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = _mainCamera.ScreenPointToRay(mousePosition);

            //画面からのrayが何かにヒットしているか
            if (!Physics.Raycast(ray, out var hit, 100, LayerConst.WithoutOnlyMapObjectLayerMask)) return (false, new Vector2Int());
            //そのrayが地面のオブジェクトにヒットしてるか
            if (hit.transform.GetComponent<GroundPlane>() == null) return (false, new Vector2Int());
            //UIの状態がゲーム中か
            if (_uiStateControl.CurrentState != UIStateEnum.SelectHotBar) return (false, new Vector2Int());

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