using MainGame.Basic;
using MainGame.Network.Send;
using MainGame.UnityView.Block;
using MainGame.UnityView.Chunk;
using MainGame.UnityView.UI.Inventory.View.HotBar;
using MainGame.UnityView.UI.UIState;
using UnityEngine;
using VContainer;

namespace MainGame.Presenter.Inventory.Send
{
    /// <summary>
    /// マウスで地面をクリックしたときに発生するイベント
    /// </summary>
    public class DetectGroundClickToSendBlockPlacePacket : MonoBehaviour
    {
        private Camera _mainCamera;
        private GroundPlane _groundPlane;
        private MoorestechInputSettings _input;
        private SelectHotBarControl _hotBarControl;
        private SendPlaceHotBarBlockProtocol _sendPlaceHotBarBlockProtocol;
        private UIStateControl _uiStateControl;
        private IBlockPlacePreview _blockPlacePreview;
        
        
        private BlockDirection _currentBlockDirection;
        
        [Inject]
        public void Construct(Camera mainCamera,GroundPlane groundPlane,
            SelectHotBarControl selectHotBarControl,SendPlaceHotBarBlockProtocol sendPlaceHotBarBlockProtocol,UIStateControl uiStateControl,IBlockPlacePreview blockPlacePreview)
        {
            _uiStateControl = uiStateControl;
            _sendPlaceHotBarBlockProtocol = sendPlaceHotBarBlockProtocol;
            _hotBarControl = selectHotBarControl;
            _mainCamera = mainCamera;
            _groundPlane = groundPlane;
            _blockPlacePreview = blockPlacePreview;
            
            _input = new MoorestechInputSettings();
            _input.Enable();
        }

        private void FixedUpdate()
        {
            BlockDirectionControl();
            GroundClickControl();
        }

        private void BlockDirectionControl()
        {
            if (!_input.Playable.BlockPlaceRotation.triggered) return;
            
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
            if (!isHit)
            { 
                return;   
            }

            //プレビュー表示
            _blockPlacePreview.SetActive(true);
            
            //クリックされてたらUIがゲームスクリーンの時にホットバーにあるブロックの設置
            if (_input.Playable.ScreenClick.triggered)
            {
                _sendPlaceHotBarBlockProtocol.Send(hitPoint.x,hitPoint.y,(short)_hotBarControl.SelectIndex,_currentBlockDirection);
                return;
            }
            
            
            //クリックされてなかったらプレビューを表示する
            _blockPlacePreview.SetPreview(hitPoint,_currentBlockDirection);
        }
        
        
        private (bool,Vector2Int pos) GetPreviewPosition()
        {
            var mousePosition = _input.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = _mainCamera.ScreenPointToRay(mousePosition);
            
            //画面からのrayが何かにヒットしているか
            if (!Physics.Raycast(ray, out var hit)) return (false,new Vector2Int());
            //そのrayが地面のオブジェクトにヒットしてるか
            if (hit.transform.GetComponent<GroundPlane>() == null) return (false,new Vector2Int());
            //UIの状態がゲーム中か
            if (_uiStateControl.CurrentState != UIStateEnum.BlockPlace) return (false,new Vector2Int());
            
            
            var x = Mathf.RoundToInt(hit.point.x);
            var y = Mathf.RoundToInt(hit.point.z);

            return (true ,new Vector2Int(x,y));
        }
    }
}